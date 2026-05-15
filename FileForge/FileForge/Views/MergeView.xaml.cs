using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FileForge.Core;

namespace FileForge.Views
{
    public partial class MergeView : UserControl
    {
        private readonly ObservableCollection<MergeSegment> _segments =
            new ObservableCollection<MergeSegment>();

        private MergeSegment _selectedSegment;
        private bool _updating;

        public MergeView()
        {
            InitializeComponent();
            lstSegments.ItemsSource = _segments;
            UpdatePropertiesPanel();
        }

        // ── Toolbar ───────────────────────────────────────────────────────────

        private void BtnAddFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "All Files (*.*)|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() != true) return;

            MergeSegment firstAdded = null;
            foreach (string f in dlg.FileNames)
            {
                var seg = new MergeSegment { Kind = SegmentKind.File, FilePath = f };
                _segments.Add(seg);
                if (firstAdded == null) firstAdded = seg;
            }
            if (firstAdded != null)
            {
                lstSegments.SelectedItem = firstAdded;
                lstSegments.ScrollIntoView(firstAdded);
            }
            SuggestOutputIfEmpty();
            UpdateTotalSize();
        }

        private void BtnAddBuffer_Click(object sender, RoutedEventArgs e)
        {
            var seg = new MergeSegment
            {
                Kind          = SegmentKind.Buffer,
                FillSizeText  = "512",
                FillSizeUnit  = "Bytes",
                FillModeIndex = 0,
                SpecificByte  = "FF",
                HexPattern    = "DE AD BE EF"
            };
            _segments.Add(seg);
            lstSegments.SelectedItem = seg;
            lstSegments.ScrollIntoView(seg);
            UpdateTotalSize();
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            int idx = lstSegments.SelectedIndex;
            if (idx < 0) return;
            _segments.RemoveAt(idx);
            if (_segments.Count > 0)
                lstSegments.SelectedIndex = Math.Min(idx, _segments.Count - 1);
            UpdateTotalSize();
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int idx = lstSegments.SelectedIndex;
            if (idx <= 0) return;
            var tmp = _segments[idx - 1];
            _segments[idx - 1] = _segments[idx];
            _segments[idx]     = tmp;
            lstSegments.SelectedIndex = idx - 1;
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int idx = lstSegments.SelectedIndex;
            if (idx < 0 || idx >= _segments.Count - 1) return;
            var tmp = _segments[idx + 1];
            _segments[idx + 1] = _segments[idx];
            _segments[idx]     = tmp;
            lstSegments.SelectedIndex = idx + 1;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            _segments.Clear();
            UpdateTotalSize();
        }

        // ── Drag-drop (files → FILE segments) ────────────────────────────────

        private void LstSegments_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void LstSegments_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            MergeSegment firstAdded = null;
            foreach (string f in files)
            {
                if (!File.Exists(f)) continue;
                var seg = new MergeSegment { Kind = SegmentKind.File, FilePath = f };
                _segments.Add(seg);
                if (firstAdded == null) firstAdded = seg;
            }
            if (firstAdded != null)
            {
                lstSegments.SelectedItem = firstAdded;
                lstSegments.ScrollIntoView(firstAdded);
            }
            SuggestOutputIfEmpty();
            UpdateTotalSize();
        }

        // ── Segment selection → properties panel ──────────────────────────────

        private void LstSegments_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedSegment = lstSegments.SelectedItem as MergeSegment;
            UpdatePropertiesPanel();
        }

        private void UpdatePropertiesPanel()
        {
            if (txtNoSelection == null) return;

            if (_selectedSegment == null)
            {
                txtNoSelection.Visibility  = Visibility.Visible;
                panelFileProp.Visibility   = Visibility.Collapsed;
                panelBufferProp.Visibility = Visibility.Collapsed;
                return;
            }

            txtNoSelection.Visibility = Visibility.Collapsed;
            _updating = true;
            try
            {
                if (_selectedSegment.Kind == SegmentKind.File)
                {
                    panelFileProp.Visibility   = Visibility.Visible;
                    panelBufferProp.Visibility = Visibility.Collapsed;
                    txtFilePath.Text           = _selectedSegment.FilePath;
                    UpdateFileInfo();
                }
                else
                {
                    panelFileProp.Visibility   = Visibility.Collapsed;
                    panelBufferProp.Visibility = Visibility.Visible;
                    txtBufSize.Text            = _selectedSegment.FillSizeText;
                    cboBufUnit.SelectedIndex   = UnitToIndex(_selectedSegment.FillSizeUnit);
                    cboBufFill.SelectedIndex   = _selectedSegment.FillModeIndex;
                    txtBufByte.Text            = _selectedSegment.SpecificByte;
                    txtBufHex.Text             = _selectedSegment.HexPattern;
                    UpdateBufferFillPanels();
                    UpdateBufSizeHint();
                }
            }
            finally
            {
                _updating = false;
            }
        }

        // ── File property handlers ─────────────────────────────────────────────

        private void TxtFilePath_Changed(object sender, TextChangedEventArgs e)
        {
            if (_updating || _selectedSegment == null) return;
            _selectedSegment.FilePath = txtFilePath.Text;
            UpdateFileInfo();
            UpdateTotalSize();
        }

        private void BtnBrowseFileProp_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() != true) return;
            if (_selectedSegment == null) return;
            _updating = true;
            txtFilePath.Text = dlg.FileName;
            _updating = false;
            _selectedSegment.FilePath = dlg.FileName;
            UpdateFileInfo();
            UpdateTotalSize();
        }

        private void UpdateFileInfo()
        {
            if (txtFileInfo == null || _selectedSegment == null) return;
            string path = _selectedSegment.FilePath;
            if (string.IsNullOrWhiteSpace(path)) { txtFileInfo.Text = ""; return; }
            try
            {
                txtFileInfo.Text = File.Exists(path)
                    ? FileEngine.FormatSize(new FileInfo(path).Length)
                    : "File not found";
            }
            catch { txtFileInfo.Text = ""; }
        }

        // ── Buffer property handlers ───────────────────────────────────────────

        private void BufText_Changed(object sender, TextChangedEventArgs e)
        {
            if (_updating || _selectedSegment == null || _selectedSegment.Kind != SegmentKind.Buffer) return;
            SaveBufferProps();
        }

        private void BufCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_updating || _selectedSegment == null || _selectedSegment.Kind != SegmentKind.Buffer) return;
            SaveBufferProps();
        }

        private void SaveBufferProps()
        {
            _selectedSegment.FillSizeText  = txtBufSize.Text;
            _selectedSegment.FillSizeUnit  = IndexToUnit(cboBufUnit.SelectedIndex);
            _selectedSegment.FillModeIndex = cboBufFill.SelectedIndex;
            _selectedSegment.SpecificByte  = txtBufByte.Text;
            _selectedSegment.HexPattern    = txtBufHex.Text;
            UpdateBufferFillPanels();
            UpdateBufSizeHint();
            UpdateTotalSize();
        }

        private void UpdateBufferFillPanels()
        {
            if (panelBufByte == null) return;
            int mode = cboBufFill.SelectedIndex;
            panelBufByte.Visibility = mode == 1 ? Visibility.Visible : Visibility.Collapsed;
            panelBufHex.Visibility  = mode == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateBufSizeHint()
        {
            if (txtBufSizeHint == null || _selectedSegment == null) return;
            long bytes = _selectedSegment.FillSizeBytes;
            txtBufSizeHint.Text = bytes > 0 ? "= " + FileEngine.FormatSize(bytes) : "";
        }

        // ── Output file browse ────────────────────────────────────────────────

        private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) txtOutput.Text = dlg.FileName;
        }

        // ── Execute ───────────────────────────────────────────────────────────

        private async void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            try { await ExecuteAsync(); }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private async Task ExecuteAsync()
        {
            if (_segments.Count == 0)
                throw new Exception("Add at least one segment to the composition.");

            string output = txtOutput.Text.Trim();
            if (string.IsNullOrWhiteSpace(output))
                throw new Exception("Please specify an output file.");

            var dataList = new List<MergeSegmentData>();

            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];

                if (seg.Kind == SegmentKind.File)
                {
                    if (string.IsNullOrWhiteSpace(seg.FilePath))
                        throw new Exception($"Segment {i + 1}: file path is empty.");
                    if (!File.Exists(seg.FilePath))
                        throw new Exception($"Segment {i + 1}: file not found — {seg.FilePath}");
                    dataList.Add(new MergeSegmentData { IsFile = true, FilePath = seg.FilePath });
                }
                else
                {
                    long fillSize = seg.FillSizeBytes;
                    if (fillSize <= 0)
                        throw new Exception($"Segment {i + 1}: invalid buffer size.");

                    FillMode mode;
                    byte     specByte = 0;
                    byte[]   hexPat   = null;

                    switch (seg.FillModeIndex)
                    {
                        case 1:
                            mode = FillMode.SpecificByte;
                            string hexVal = seg.SpecificByte.Trim();
                            if (hexVal.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                                hexVal = hexVal.Substring(2);
                            if (!byte.TryParse(hexVal,
                                    System.Globalization.NumberStyles.HexNumber, null, out specByte))
                                throw new Exception($"Segment {i + 1}: invalid byte value.");
                            break;
                        case 2:
                            mode   = FillMode.HexPattern;
                            hexPat = FileEngine.ParseHexBytes(seg.HexPattern);
                            if (hexPat.Length == 0)
                                throw new Exception($"Segment {i + 1}: hex pattern is empty.");
                            break;
                        case 3:
                            mode = FillMode.Random;
                            break;
                        default:
                            mode = FillMode.Zeros;
                            break;
                    }

                    dataList.Add(new MergeSegmentData
                    {
                        IsFile       = false,
                        FillSize     = fillSize,
                        FillMode     = mode,
                        SpecificByte = specByte,
                        HexPattern   = hexPat
                    });
                }
            }

            int fileCount = dataList.Count(d => d.IsFile);
            int bufCount  = dataList.Count(d => !d.IsFile);
            string capturedOutput = output;
            var capturedData      = dataList;

            ShowInfo("Merging…");
            await Task.Run(() => FileEngine.MergeSegments(capturedData, capturedOutput));

            long outSize = new FileInfo(capturedOutput).Length;
            ShowSuccess(
                $"Done!  {Path.GetFileName(capturedOutput)}  ({FileEngine.FormatSize(outSize)})" +
                $"  ·  {fileCount} file{(fileCount == 1 ? "" : "s")}, " +
                $"{bufCount} buffer{(bufCount == 1 ? "" : "s")}");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void UpdateTotalSize()
        {
            if (txtTotalSize == null) return;
            if (_segments.Count == 0) { txtTotalSize.Text = ""; return; }

            long total      = 0;
            bool hasUnknown = false;

            foreach (var seg in _segments)
            {
                if (seg.Kind == SegmentKind.File)
                {
                    if (string.IsNullOrWhiteSpace(seg.FilePath) || !File.Exists(seg.FilePath))
                    { hasUnknown = true; continue; }
                    try { total += new FileInfo(seg.FilePath).Length; }
                    catch { hasUnknown = true; }
                }
                else
                {
                    long b = seg.FillSizeBytes;
                    if (b > 0) total += b; else hasUnknown = true;
                }
            }

            int count = _segments.Count;
            txtTotalSize.Text =
                $"Estimated: {FileEngine.FormatSize(total)}{(hasUnknown ? " +" : "")}  " +
                $"·  {count} segment{(count == 1 ? "" : "s")}";
        }

        private void SuggestOutputIfEmpty()
        {
            if (!string.IsNullOrWhiteSpace(txtOutput?.Text)) return;
            foreach (var seg in _segments)
            {
                if (seg.Kind == SegmentKind.File && !string.IsNullOrWhiteSpace(seg.FilePath))
                {
                    try
                    {
                        var fi = new FileInfo(seg.FilePath);
                        txtOutput.Text = Path.Combine(fi.DirectoryName,
                            Path.GetFileNameWithoutExtension(fi.Name) + "_merged" + fi.Extension);
                    }
                    catch { }
                    return;
                }
            }
        }

        private static int UnitToIndex(string unit)
        {
            switch (unit)
            {
                case "KB": return 1;
                case "MB": return 2;
                case "GB": return 3;
                default:   return 0;
            }
        }

        private static string IndexToUnit(int idx)
        {
            switch (idx)
            {
                case 1:  return "KB";
                case 2:  return "MB";
                case 3:  return "GB";
                default: return "Bytes";
            }
        }

        private void ShowError  (string msg) => statusBanner.ShowError  (msg);
        private void ShowInfo   (string msg) => statusBanner.ShowInfo   (msg);
        private void ShowSuccess(string msg) => statusBanner.ShowSuccess(msg);
    }
}
