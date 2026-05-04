using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FileForge.Core;

namespace FileForge.Views
{
    // ══════════════════════════════════════════════════════════════════════════
    //  HexPanel  — custom FrameworkElement for unlimited-size hex file viewing
    // ══════════════════════════════════════════════════════════════════════════
    sealed class HexPanel : FrameworkElement, IDisposable
    {
        // ── Layout constants ──────────────────────────────────────────────────
        private const double FontPt    = 13.0;
        private const double LeftPad   = 10.0;
        private const double RowPadTop = 3.0;

        private static readonly Typeface   MonoFace  = new Typeface("Consolas");
        private static readonly CultureInfo InvCI    = CultureInfo.InvariantCulture;

        // ── Brushes & pens ───────────────────────────────────────────────────
        private static readonly Brush FgBrush       = new SolidColorBrush(Color.FromRgb(44,  62,  80));
        private static readonly Brush OffsetBrush   = new SolidColorBrush(Color.FromRgb(127, 140, 141));
        private static readonly Brush AscBrush      = new SolidColorBrush(Color.FromRgb(127, 140, 141));
        private static readonly Brush DelimBrush    = new SolidColorBrush(Color.FromRgb(189, 195, 199));
        private static readonly Brush LineBgOdd     = new SolidColorBrush(Color.FromRgb(250, 251, 252));
        private static readonly Brush OffsetBgBrush = new SolidColorBrush(Color.FromRgb(248, 249, 250));
        private static readonly Brush SelBgBrush    = new SolidColorBrush(Color.FromArgb(200, 0, 120, 212));
        private static readonly Brush SelFgBrush    = Brushes.White;
        private static readonly Brush MatchBgBrush  = new SolidColorBrush(Color.FromArgb(200, 255, 215, 0));
        private static readonly Brush CurMatchBrush = new SolidColorBrush(Color.FromArgb(230, 255, 130, 0));
        private static readonly Pen   SepPen        = new Pen(new SolidColorBrush(Color.FromRgb(232, 236, 239)), 1.0);
        private static readonly Brush HeaderBgBrush = new SolidColorBrush(Color.FromRgb(236, 240, 243));
        private static readonly Pen   HeaderPen     = new Pen(new SolidColorBrush(Color.FromRgb(207, 216, 220)), 1.0);
        private static readonly string HeaderLine   = BuildHeaderLine();

        private static string BuildHeaderLine()
        {
            var sb = new StringBuilder(80);
            sb.Append("  Offset  ");          // 10 chars
            for (int i = 0; i < 16; i++)
            {
                if (i == 8) sb.Append(' ');   // extra gap
                sb.AppendFormat("{0:X2} ", i); // "XX "
            }
            sb.Append(" |");
            for (int i = 0; i < 16; i++) sb.Append(i.ToString("X1"));
            sb.Append('|');
            return sb.ToString();
        }

        static HexPanel()
        {
            FgBrush.Freeze(); OffsetBrush.Freeze(); AscBrush.Freeze(); DelimBrush.Freeze();
            LineBgOdd.Freeze(); OffsetBgBrush.Freeze(); SelBgBrush.Freeze(); SelFgBrush.Freeze();
            MatchBgBrush.Freeze(); CurMatchBrush.Freeze(); SepPen.Freeze();
            HeaderBgBrush.Freeze(); HeaderPen.Freeze();
        }

        // ── Computed layout ───────────────────────────────────────────────────
        private double _cw;        // char width
        private double _lh;        // line height
        // String offsets (char indices in each row string):
        //   0..9   : "XXXXXXXX  "
        //   10..    : hex bytes (each "XX "; extra ' ' after byte 7)
        //   byte b(0..7):  char 10 + b*3
        //   byte b(8..15): char 10 + b*3 + 1
        //   " |"  at char 59,60
        //   ASCII  at char 61..76
        //   "|"   at char 77
        // X pixel positions:
        private double _xRow;       // row text x (= LeftPad)
        private double _xHexStart;  // x of first hex byte digit = _xRow + 10*cw
        private double _xPipe1;     // x of space before "|" = _xRow + 59*cw
        private double _xAscii;     // x of ascii[0] = _xRow + 61*cw
        private double _xPipe2;     // x of closing "|" = _xRow + 77*cw
        private double _xEnd;       // end = _xRow + 78*cw

        // ── File state ────────────────────────────────────────────────────────
        private FileStream               _fileStream;
        private MemoryMappedFile         _mmf;
        private MemoryMappedViewAccessor _acc;
        private long   _fileSize;
        private long   _totalRows;   // ceil(fileSize/16)

        // ── Viewport ──────────────────────────────────────────────────────────
        private long   _topRow;
        private int    _visRows;
        private double _headerH;  // height of the fixed column-header row

        // ── Selection (byte indices into file) ────────────────────────────────
        private long _selAnchor = -1;
        private long _selEnd    = -1;
        private bool _dragging;

        // ── Search results ────────────────────────────────────────────────────
        private List<long> _matchOffsets = new List<long>();
        private int        _currentMatch = -1;

        // ── Linked scrollbar ──────────────────────────────────────────────────
        private readonly ScrollBar _sb;
        // Fixed internal scroll scale — decouples thumb size from actual row count.
        // Value always maps 0..SB_MAX → row 0..maxTop, so the thumb always reaches
        // the true bottom. MinThumbFrac enforces a minimum visible thumb height.
        private const double SB_MAX       = 10000.0;
        private const double MinThumbFrac = 0.04;  // thumb ≥ 4% of track

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<string> StatusChanged;

        public HexPanel(ScrollBar sb)
        {
            _sb = sb;
            Focusable    = true;
            ClipToBounds = true;

            sb.ValueChanged += (s, e) =>
            {
                if (_totalRows > 0)
                {
                    long maxTop = ComputeMaxTop();
                    _topRow = (long)Math.Round(e.NewValue / SB_MAX * maxTop);
                    _topRow = Math.Max(0, Math.Min(_topRow, maxTop));
                }
                InvalidateVisual();
            };
            SizeChanged += (s, e) => { UpdateVis(); UpdateSB(); InvalidateVisual(); };
            Loaded += (s, e) => { MeasureLayout(); UpdateVis(); UpdateSB(); };
        }

        // For hit-testing without a Background property
        protected override System.Windows.Media.HitTestResult HitTestCore(
            System.Windows.Media.PointHitTestParameters p)
            => new System.Windows.Media.PointHitTestResult(this, p.HitPoint);

        // ── Layout ────────────────────────────────────────────────────────────

        private void MeasureLayout()
        {
            double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var ft = MakeFT("X", FgBrush, ppd);
            _cw = ft.Width;
            _lh = ft.Height + RowPadTop * 2;

            _xRow      = LeftPad;
            _xHexStart = _xRow + 10 * _cw;
            _xPipe1    = _xRow + 59 * _cw;
            _xAscii    = _xRow + 61 * _cw;
            _xPipe2    = _xRow + 77 * _cw;
            _xEnd      = _xRow + 78 * _cw;
            _headerH   = _lh;
        }

        private void UpdateVis()
        {
            double contentH = _lh > 0 ? Math.Max(0, ActualHeight - _headerH) : 0;
            _visRows = contentH > 0 && _lh > 0 ? (int)(contentH / _lh) + 2 : 40;
        }

        // Returns the highest valid topRow such that the last content row is
        // guaranteed to be fully visible. Uses the actual panel pixel height so
        // the result is exact regardless of whether height divides evenly by _lh.
        private long ComputeMaxTop()
        {
            double contentH = _lh > 0 ? Math.Max(0, ActualHeight - _headerH) : 0;
            long vis = (_lh > 0 && contentH > 0)
                       ? Math.Max(1L, (long)(contentH / _lh))
                       : Math.Max(1L, _visRows - 1);
            return Math.Max(0, _totalRows - vis);
        }

        private void UpdateSB()
        {
            if (_totalRows == 0) { _sb.IsEnabled = false; _sb.Value = 0; return; }
            long maxTop = ComputeMaxTop();
            _sb.IsEnabled = maxTop > 0;
            _sb.Minimum   = 0;
            _sb.Maximum   = SB_MAX;
            // Natural thumb fraction; floor at MinThumbFrac so thumb is always clickable.
            double naturalFrac = _totalRows > 0 ? (double)_visRows / _totalRows : 1.0;
            double frac        = Math.Max(MinThumbFrac, Math.Min(0.95, naturalFrac));
            _sb.ViewportSize = SB_MAX * frac / (1.0 - frac);
            _sb.SmallChange  = maxTop > 0 ? SB_MAX * 3.0 / maxTop : 1;
            _sb.LargeChange  = maxTop > 0 ? SB_MAX * Math.Max(1, _visRows - 1) / maxTop : SB_MAX;
        }

        // ── File open/close ───────────────────────────────────────────────────

        public void OpenFile(string path)
        {
            Close();
            _fileSize  = new FileInfo(path).Length;
            _totalRows = (_fileSize + 15) / 16;
            _topRow    = 0;
            _selAnchor = _selEnd = -1;
            ClearMatchState();

            _fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _mmf = MemoryMappedFile.CreateFromFile(_fileStream, null, 0,
                                                   MemoryMappedFileAccess.Read,
                                                   HandleInheritability.None, leaveOpen: true);
            _acc = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            if (_cw == 0) MeasureLayout();
            UpdateVis();
            UpdateSB();
            InvalidateVisual();
        }

        public void Close()
        {
            _acc?.Dispose();        _acc        = null;
            _mmf?.Dispose();        _mmf        = null;
            _fileStream?.Dispose(); _fileStream = null;
            _fileSize  = 0;
            _totalRows = 0;
            _topRow    = 0;
            ClearMatchState();
            UpdateSB();
            InvalidateVisual();
        }

        public void Dispose() => Close();

        // ── Navigation ────────────────────────────────────────────────────────

        public void GoToOffset(long offset)
        {
            if (_totalRows == 0) return;
            offset  = Math.Max(0, Math.Min(offset, _fileSize - 1));
            long row = offset / 16;
            SetTopRow(row);
        }

        private void SetTopRow(long row)
        {
            long max  = ComputeMaxTop();
            _topRow   = Math.Max(0, Math.Min(row, max));
            _sb.Value = max > 0 ? _topRow * SB_MAX / max : 0;
            InvalidateVisual();
        }

        // ── Rendering ────────────────────────────────────────────────────────

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));
            if (_acc == null || _cw == 0) return;

            double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Offset gutter background + separator (full height)
            double gutterW = _xHexStart - _cw;
            dc.DrawRectangle(OffsetBgBrush, null, new Rect(0, 0, gutterW, h));
            dc.DrawLine(SepPen, new Point(gutterW, _headerH), new Point(gutterW, h));

            // Compute selection range
            long selMin = -1, selMax = -1;
            if (_selAnchor >= 0 && _selEnd >= 0)
            {
                selMin = Math.Min(_selAnchor, _selEnd);
                selMax = Math.Max(_selAnchor, _selEnd);
            }

            // ── Column header ─────────────────────────────────────────────────────
            dc.DrawRectangle(HeaderBgBrush, null, new Rect(0, 0, w, _headerH));
            dc.DrawLine(SepPen, new Point(gutterW, 0), new Point(gutterW, _headerH));
            dc.DrawLine(HeaderPen, new Point(0, _headerH - 0.5), new Point(w, _headerH - 0.5));
            {
                var hft = MakeFT(HeaderLine, OffsetBrush, ppd);
                hft.SetForegroundBrush(DelimBrush, 59, 2);
                hft.SetForegroundBrush(DelimBrush, 77, 1);
                dc.DrawText(hft, new Point(_xRow, RowPadTop));
            }

            byte[] buf = new byte[16];

            for (int r = 0; r < _visRows; r++)
            {
                long rowIdx = _topRow + r;
                if (rowIdx >= _totalRows) break;

                long byteBase = rowIdx * 16;
                int  rowLen   = (int)Math.Min(16, _fileSize - byteBase);
                ReadBytes(byteBase, buf, rowLen);

                double y = _headerH + r * _lh;

                // Alternating row background
                if (rowIdx % 2 == 1)
                    dc.DrawRectangle(LineBgOdd, null, new Rect(gutterW, y, w - gutterW, _lh));

                // Selection backgrounds
                if (selMin >= 0)
                {
                    for (int b = 0; b < rowLen; b++)
                    {
                        long bi = byteBase + b;
                        if (bi < selMin || bi > selMax) continue;

                        double hx = HexByteX(b);
                        dc.DrawRectangle(SelBgBrush, null, new Rect(hx, y, 2 * _cw, _lh));

                        double ax = _xAscii + b * _cw;
                        dc.DrawRectangle(SelBgBrush, null, new Rect(ax, y, _cw, _lh));
                    }
                }

                // Match backgrounds — O(log n + k) binary search instead of O(n) full scan
                if (_matchOffsets.Count > 0)
                {
                    long rowStart = byteBase;
                    long rowEnd   = byteBase + 16;
                    int  lo       = BinarySearchLower(_matchOffsets, rowStart);
                    while (lo < _matchOffsets.Count && _matchOffsets[lo] < rowEnd)
                    {
                        long mo = _matchOffsets[lo];
                        int  b  = (int)(mo % 16);
                        Brush mb = (_currentMatch >= 0 && lo == _currentMatch)
                                   ? CurMatchBrush : MatchBgBrush;
                        double hx = HexByteX(b);
                        dc.DrawRectangle(mb, null, new Rect(hx, y, 2 * _cw, _lh));
                        dc.DrawRectangle(mb, null, new Rect(_xAscii + b * _cw, y, _cw, _lh));
                        lo++;
                    }
                }

                // Build row string
                string line = BuildLine(rowIdx, buf, rowLen);

                // Draw with selection coloring
                var ft = MakeFT(line, FgBrush, ppd);

                // Offset: gray
                ft.SetForegroundBrush(OffsetBrush, 0, 8);

                // "  " separator after offset: already FgBrush (ok)
                // Hex bytes
                for (int b = 0; b < 16; b++)
                {
                    int ci = (b < 8) ? (10 + b * 3) : (10 + b * 3 + 1);
                    if (b >= rowLen)
                    {
                        ft.SetForegroundBrush(Brushes.Transparent, ci, 2);
                    }
                    else if (selMin >= 0 && byteBase + b >= selMin && byteBase + b <= selMax)
                    {
                        ft.SetForegroundBrush(SelFgBrush, ci, 2);
                    }
                }

                // " |" delimiters
                ft.SetForegroundBrush(DelimBrush, 59, 2);
                ft.SetForegroundBrush(DelimBrush, 77, 1);

                // ASCII
                for (int b = 0; b < rowLen; b++)
                {
                    int ci = 61 + b;
                    Brush fg = (selMin >= 0 && byteBase + b >= selMin && byteBase + b <= selMax)
                               ? SelFgBrush : AscBrush;
                    ft.SetForegroundBrush(fg, ci, 1);
                }
                for (int b = rowLen; b < 16; b++)
                    ft.SetForegroundBrush(Brushes.Transparent, 61 + b, 1);

                dc.DrawText(ft, new Point(_xRow, y + RowPadTop));
            }
        }

        private double HexByteX(int b)
            => b < 8 ? (_xHexStart + b * 3 * _cw) : (_xHexStart + b * 3 * _cw + _cw);

        private static string BuildLine(long rowIdx, byte[] buf, int len)
        {
            long offset = rowIdx * 16;
            var sb = new StringBuilder(80);
            sb.Append(offset.ToString("X8"));  // chars 0-7
            sb.Append("  ");                    // chars 8-9
            for (int i = 0; i < 16; i++)
            {
                if (i == 8) sb.Append(' ');    // extra gap: char 34
                sb.Append(i < len ? buf[i].ToString("X2") : "  "); // 2 chars
                sb.Append(' ');                 // separator
            }
            // After loop: 59 chars. Add " |" → chars 59-60
            sb.Append(" |");
            for (int i = 0; i < 16; i++)       // chars 61-76
            {
                if (i < len) { char c = (char)buf[i]; sb.Append(c >= 0x20 && c < 0x7F ? c : '.'); }
                else         sb.Append(' ');
            }
            sb.Append('|');                     // char 77
            return sb.ToString();
        }

        private void ReadBytes(long offset, byte[] buf, int count)
        {
            Array.Clear(buf, 0, 16);
            if (_acc == null || count <= 0) return;
            _acc.ReadArray(offset, buf, 0, count);
        }

        private FormattedText MakeFT(string text, Brush fg, double ppd)
            => new FormattedText(text, InvCI, FlowDirection.LeftToRight,
                                 MonoFace, FontPt, fg, ppd);

        // Lower-bound binary search: returns index of first element >= value.
        private static int BinarySearchLower(List<long> list, long value)
        {
            int lo = 0, hi = list.Count;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (list[mid] < value) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }

        // ── Mouse handling ────────────────────────────────────────────────────

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (_acc == null) return;
            Focus(); CaptureMouse();
            _dragging = true;
            long b = HitByte(e.GetPosition(this));
            if (b >= 0) { _selAnchor = b; _selEnd = b; InvalidateVisual(); NotifySel(); }
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_dragging || _acc == null) return;
            long b = HitByte(e.GetPosition(this));
            if (b >= 0 && b != _selEnd) { _selEnd = b; InvalidateVisual(); NotifySel(); }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            _dragging = false; ReleaseMouseCapture();
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            int delta = e.Delta > 0 ? -5 : 5;
            SetTopRow(_topRow + delta);
            e.Handled = true;
        }

        private long HitByte(Point pt)
        {
            if (_acc == null || _cw == 0) return -1;
            double hy = pt.Y - _headerH;
            if (hy < 0) return -1;
            int r = (int)(hy / _lh);
            if (r < 0 || r >= _visRows) return -1;
            long rowIdx = _topRow + r;
            if (rowIdx >= _totalRows) return -1;
            long byteBase = rowIdx * 16;
            int  rowLen   = (int)Math.Min(16, _fileSize - byteBase);

            double x = pt.X;

            // Hex area
            if (x >= _xHexStart && x < _xPipe1)
            {
                int rel = (int)((x - _xHexStart) / _cw);
                int b;
                if (rel < 24)       b = rel / 3;          // bytes 0-7  (rel 0..23)
                else if (rel == 24) b = 7;                // gap → snap to byte 7
                else                b = (rel - 1) / 3;    // bytes 8-15 (rel 25..48)
                return byteBase + Math.Max(0, Math.Min(b, rowLen - 1));
            }

            // ASCII area
            if (x >= _xAscii && x < _xPipe2)
            {
                int b = (int)((x - _xAscii) / _cw);
                return byteBase + Math.Max(0, Math.Min(b, rowLen - 1));
            }

            return -1;
        }

        private void NotifySel()
        {
            if (_selAnchor < 0) return;
            long sMin = Math.Min(_selAnchor, _selEnd);
            long sMax = Math.Max(_selAnchor, _selEnd);
            long count = sMax - sMin + 1;
            StatusChanged?.Invoke(
                string.Format("Selected  0x{0:X8} - 0x{1:X8}  ({2} byte{3})",
                              sMin, sMax, count, count == 1 ? "" : "s"));
        }

        // ── Search ────────────────────────────────────────────────────────────

        // Uses the already-open MMF accessor – avoids FileShare conflicts with
        // MemoryMappedFile (which opens with FileShare.None in .NET Framework).
        public void RunSearchAsync(long fileSize, byte?[] pattern,
                                   Action<string> progressCb, Action<int, List<long>> doneCb)
        {
            ClearMatchState();
            InvalidateVisual();
            if (pattern == null || pattern.Length == 0) { doneCb?.Invoke(0, _matchOffsets); return; }
            if (_acc == null) { doneCb?.Invoke(0, _matchOffsets); return; }

            int patLen = pattern.Length;
            var pat    = pattern;
            var acc    = _acc;  // capture so the reference stays valid on the worker thread

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                var matches = new List<long>();
                const int ChunkSize = 1 << 20; // 1 MB

                // buf holds: [prefix (patLen-1)] + [new chunk (ChunkSize)]
                // The prefix carries the last patLen-1 bytes from the previous chunk so
                // patterns that cross a 1 MB boundary are never missed.
                byte[] buf       = new byte[ChunkSize + patLen - 1];
                long   filePos   = 0;   // file offset of the first NEW byte in this iteration
                int    prefixLen = 0;   // bytes already in buf[0..prefixLen-1]
                int    iter      = 0;

                try
                {
                    while (filePos < fileSize)
                    {
                        int readCount = (int)Math.Min(ChunkSize, fileSize - filePos);
                        acc.ReadArray(filePos, buf, prefixLen, readCount);
                        int  n         = prefixLen + readCount;
                        long chunkBase = filePos - prefixLen; // file offset of buf[0]

                        for (int i = 0; i <= n - patLen; i++)
                        {
                            bool ok = true;
                            for (int j = 0; j < patLen; j++)
                                if (pat[j].HasValue && buf[i + j] != pat[j].Value) { ok = false; break; }
                            if (ok) { matches.Add(chunkBase + i); i += patLen - 1; }
                        }

                        filePos += readCount;

                        if (patLen > 1 && filePos < fileSize)
                        {
                            // Copy last patLen-1 bytes to the front of buf as prefix for next chunk.
                            int newPrefix = Math.Min(patLen - 1, n);
                            Buffer.BlockCopy(buf, n - newPrefix, buf, 0, newPrefix);
                            prefixLen = newPrefix;
                        }
                        else
                        {
                            prefixLen = 0;
                        }

                        iter++;
                        if (iter % 16 == 0)
                        {
                            int pct = (int)(100L * filePos / fileSize);
                            progressCb?.Invoke("Searching… " + pct + "%");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => progressCb?.Invoke("Search error: " + ex.Message));
                }

                Dispatcher.Invoke(() =>
                {
                    _matchOffsets = matches;
                    _currentMatch = -1;
                    InvalidateVisual();
                    doneCb?.Invoke(matches.Count, matches);
                });
            });
        }

        public int NavigateMatch(bool forward)
        {
            if (_matchOffsets.Count == 0) return -1;
            if (forward)
                _currentMatch = (_currentMatch + 1) % _matchOffsets.Count;
            else
                _currentMatch = (_currentMatch - 1 + _matchOffsets.Count) % _matchOffsets.Count;

            GoToOffset(_matchOffsets[_currentMatch]);
            return _currentMatch;
        }

        public void ClearMatchState()
        {
            _matchOffsets.Clear();
            _currentMatch = -1;
            InvalidateVisual();
        }

        public int MatchCount => _matchOffsets.Count;

        // ── Selection access ──────────────────────────────────────────────────

        /// <summary>Returns the inclusive start byte offset of the current selection, or -1.</summary>
        public long SelectionStart  => (_selAnchor >= 0 && _selEnd >= 0) ? Math.Min(_selAnchor, _selEnd) : -1;
        /// <summary>Returns the byte count of the current selection, or 0 when nothing is selected.</summary>
        public long SelectionLength => (_selAnchor >= 0 && _selEnd >= 0) ? (Math.Abs(_selEnd - _selAnchor) + 1) : 0;

        // ── Raw read helper ───────────────────────────────────────────────────

        /// <summary>Reads <paramref name="count"/> bytes starting at <paramref name="fileOffset"/>
        /// into <paramref name="buffer"/> using the open MMF accessor.</summary>
        public void ReadBytesRange(long fileOffset, byte[] buffer, int count)
        {
            if (_acc != null && count > 0)
                _acc.ReadArray(fileOffset, buffer, 0, count);
        }

        protected override Size MeasureOverride(Size sz) => sz;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  HexView  — the UserControl that hosts HexPanel
    // ══════════════════════════════════════════════════════════════════════════
    public partial class HexView : UserControl
    {
        private HexPanel _panel;
        private string   _openFilePath;
        private long     _openFileSize;
        private string   _lastQuery;
        private int      _lastSearchMode = -1;

        public HexView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _panel = new HexPanel(hexScrollBar);
            _panel.StatusChanged += msg => txtStatus.Text = msg;
            panelHost.Child = _panel;
        }

        // ── File open ─────────────────────────────────────────────────────────

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) OpenFile(dlg.FileName);
        }

        private void OpenFile(string path)
        {
            if (!File.Exists(path)) { txtStatus.Text = "File not found."; return; }
            txtInput.Text  = path;
            _openFilePath  = path;
            _openFileSize  = new FileInfo(path).Length;
            _lastQuery     = null;
            _lastSearchMode = -1;
            txtSearchStatus.Text = "";

            _panel.OpenFile(path);
            txtStatus.Text = string.Format("{0}  \u2022  {1}  \u2022  {2:N0} rows",
                Path.GetFileName(path),
                FileEngine.FormatSize(_openFileSize),
                (_openFileSize + 15) / 16);
        }

        // ── Keyboard shortcuts ────────────────────────────────────────────────

        private void TxtSearchQuery_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Search(forward: (Keyboard.Modifiers & ModifierKeys.Shift) == 0);
                e.Handled = true;
            }
        }

        private void TxtJumpOffset_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { BtnJump_Click(null, null); e.Handled = true; }
        }

        // ── Jump to offset ────────────────────────────────────────────────────

        private void BtnJump_Click(object sender, RoutedEventArgs e)
        {
            if (_panel == null || _openFilePath == null) return;
            if (!FileEngine.TryParseOffset(txtJumpOffset.Text.Trim(), out long offset))
            { txtStatus.Text = "Invalid offset — use hex (0x…) or decimal."; return; }
            _panel.GoToOffset(offset);
        }

        // ── Search ────────────────────────────────────────────────────────────

        private void BtnFindNext_Click(object sender, RoutedEventArgs e) => Search(forward: true);
        private void BtnFindPrev_Click(object sender, RoutedEventArgs e) => Search(forward: false);

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            _panel?.ClearMatchState();
            _lastQuery = null;
            _lastSearchMode = -1;
            SetSearchStatus("", SearchStatusKind.Normal);
        }

        private void Search(bool forward)
        {
            if (_panel == null || _openFilePath == null) return;
            string query = txtSearchQuery.Text.Trim();
            if (string.IsNullOrEmpty(query)) return;
            int mode = cboSearchMode.SelectedIndex;

            if (query != _lastQuery || mode != _lastSearchMode)
            {
                _lastQuery = query; _lastSearchMode = mode;

                byte?[] pattern;
                try
                {
                    if (mode == 0)
                        pattern = FileEngine.ParseHexPattern(query);
                    else
                    {
                        var b = Encoding.UTF8.GetBytes(query);
                        pattern = new byte?[b.Length];
                        for (int i = 0; i < b.Length; i++) pattern[i] = b[i];
                    }
                }
                catch (Exception ex)
                {
                    SetSearchStatus("Pattern error: " + ex.Message, SearchStatusKind.Error);
                    return;
                }

                SetSearchStatus("Searching\u2026", SearchStatusKind.Normal);
                IsEnabled = false;
                _panel.RunSearchAsync(_openFileSize, pattern,
                    prog => Dispatcher.Invoke(() => SetSearchStatus(prog, SearchStatusKind.Normal)),
                    (count, _) =>
                    {
                        IsEnabled = true;
                        if (count == 0) { SetSearchStatus("No matches", SearchStatusKind.Warning); return; }
                        NavigateToMatch(forward);
                    });
            }
            else
            {
                NavigateToMatch(forward);
            }
        }

        private void NavigateToMatch(bool forward)
        {
            if (_panel == null) return;
            int idx = _panel.NavigateMatch(forward);
            if (idx < 0) return;
            SetSearchStatus(
                string.Format("Match {0} / {1}", idx + 1, _panel.MatchCount),
                SearchStatusKind.Success);
        }

        private enum SearchStatusKind { Normal, Success, Warning, Error }

        private static readonly System.Windows.Media.Brush _statusNormal  = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(127, 140, 141));
        private static readonly System.Windows.Media.Brush _statusSuccess = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 132, 73));
        private static readonly System.Windows.Media.Brush _statusWarning = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 100, 20));
        private static readonly System.Windows.Media.Brush _statusError   = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(192, 57, 43));

        private void SetSearchStatus(string text, SearchStatusKind kind)
        {
            txtSearchStatus.Text = text;
            System.Windows.Media.Brush fg;
            if      (kind == SearchStatusKind.Success) fg = _statusSuccess;
            else if (kind == SearchStatusKind.Warning) fg = _statusWarning;
            else if (kind == SearchStatusKind.Error)   fg = _statusError;
            else                                       fg = _statusNormal;
            txtSearchStatus.Foreground = fg;
        }

        // ── Drag-drop ─────────────────────────────────────────────────────────

        private void View_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0) OpenFile(files[0]);
            }
        }

        private void View_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        // ── Export as C Header ────────────────────────────────────────────────

        private void BtnExportHeader_Click(object sender, RoutedEventArgs e)
        {
            if (_panel == null || _openFilePath == null)
            { txtStatus.Text = "Open a file first."; return; }

            // Determine byte range — use selection if active, otherwise whole file
            long exportStart  = 0;
            long exportCount  = _openFileSize;
            bool hasSelection = _panel.SelectionLength > 0;
            if (hasSelection)
            {
                exportStart = _panel.SelectionStart;
                exportCount = _panel.SelectionLength;
            }

            // Warn for very large output
            const long WarnThreshold = 2L * 1024 * 1024; // 2 MB
            if (exportCount > WarnThreshold)
            {
                var res = MessageBox.Show(
                    string.Format(
                        "The export range is {0} ({1:N0} bytes). The generated header file will be very large.\n\nContinue?",
                        FileEngine.FormatSize(exportCount), exportCount),
                    "Large Export", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res != MessageBoxResult.Yes) return;
            }

            // Derive a C-safe identifier from the source file name
            string baseName = Path.GetFileNameWithoutExtension(_openFilePath);
            if (hasSelection) baseName += "_sel";
            string varName = SanitizeCIdentifier(baseName);

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter      = "C Header Files (*.h)|*.h|All Files (*.*)|*.*",
                FileName    = varName + ".h",
                DefaultExt  = ".h",
                Title       = "Export as C Header"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                string outBase  = Path.GetFileNameWithoutExtension(dlg.FileName);
                string guardName = SanitizeCIdentifier(outBase).ToUpperInvariant() + "_H";

                using (var fs = new FileStream(dlg.FileName, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    sw.WriteLine("/* Auto-generated by FileForge — do not edit manually. */");
                    sw.WriteLine("/* Source: {0} */", Path.GetFileName(_openFilePath).Replace("*/", "* /"));
                    if (hasSelection)
                        sw.WriteLine("/* Byte range: 0x{0:X8} – 0x{1:X8}  ({2:N0} bytes) */",
                                     exportStart, exportStart + exportCount - 1, exportCount);
                    sw.WriteLine();
                    sw.WriteLine("#ifndef {0}", guardName);
                    sw.WriteLine("#define {0}", guardName);
                    sw.WriteLine();
                    sw.Write("static const unsigned char {0}[] = {{", varName);

                    const int RowSize   = 16;
                    const int ChunkSize = 64 * 1024;
                    byte[]    buf       = new byte[ChunkSize];
                    long      remaining = exportCount;
                    long      filePos   = exportStart;
                    long      byteIndex = 0;

                    while (remaining > 0)
                    {
                        int toRead = (int)Math.Min(ChunkSize, remaining);
                        _panel.ReadBytesRange(filePos, buf, toRead);

                        for (int i = 0; i < toRead; i++, byteIndex++)
                        {
                            // New row
                            if (byteIndex % RowSize == 0)
                                sw.Write("\n    ");

                            sw.Write("0x{0:X2}", buf[i]);

                            // Trailing comma except after the last byte
                            if (byteIndex < exportCount - 1)
                                sw.Write(", ");
                        }

                        filePos   += toRead;
                        remaining -= toRead;
                    }

                    sw.WriteLine("\n};");
                    sw.WriteLine();
                    sw.WriteLine("static const unsigned long long {0}_len = {1}ULL;", varName, exportCount);
                    sw.WriteLine();
                    sw.WriteLine("#endif /* {0} */", guardName);
                }

                txtStatus.Text = string.Format(
                    "\u2713 Exported {0:N0} bytes  \u2192  \"{1}\"",
                    exportCount, Path.GetFileName(dlg.FileName));
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Export failed: " + ex.Message;
            }
        }

        /// <summary>Converts an arbitrary string into a valid C identifier.</summary>
        private static string SanitizeCIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return "data";
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            // C identifiers must not start with a digit
            if (sb.Length == 0 || char.IsDigit(sb[0]))
                sb.Insert(0, '_');
            return sb.ToString();
        }
    }
}
