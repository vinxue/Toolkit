using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FileForge.Core;

namespace FileForge.Views
{
    public partial class RegionView : UserControl
    {
        private bool _isBusy;

        public RegionView()
        {
            InitializeComponent();
        }

        // ── Shared ────────────────────────────────────────────────────────

        private void BtnBrowseInput_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) SetInputFile(dlg.FileName);
        }

        private void SetInputFile(string path)
        {
            txtInput.Text = path;
            if (!File.Exists(path)) return;
            var info = new FileInfo(path);
            txtInputInfo.Text = $"Size: {FileEngine.FormatSize(info.Length)}  •  0x{info.Length:X}  •  {info.LastWriteTime:yyyy-MM-dd HH:mm}";
            AutoFillOutputs(info);
        }

        private void AutoFillOutputs(FileInfo info)
        {
            string nameNoExt = Path.GetFileNameWithoutExtension(info.Name);
            string ext = info.Extension;
            string dir = info.DirectoryName;

            if (string.IsNullOrWhiteSpace(txtExtractOut.Text))
                txtExtractOut.Text = Path.Combine(dir, nameNoExt + "_extract" + ext);
            if (string.IsNullOrWhiteSpace(txtInsertOut.Text))
                txtInsertOut.Text = Path.Combine(dir, nameNoExt + "_insert" + ext);
            if (string.IsNullOrWhiteSpace(txtDeleteOut.Text))
                txtDeleteOut.Text = Path.Combine(dir, nameNoExt + "_delete" + ext);
            if (string.IsNullOrWhiteSpace(txtOvrOut.Text))
                txtOvrOut.Text = Path.Combine(dir, nameNoExt + "_overwrite" + ext);
            if (string.IsNullOrWhiteSpace(txtTruncOut.Text))
                txtTruncOut.Text = Path.Combine(dir, nameNoExt + "_truncated" + ext);
        }

        private void View_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0) SetInputFile(files[0]);
            }
        }

        private void View_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private string RequireInput()
        {
            string path = txtInput.Text.Trim();
            if (!File.Exists(path)) throw new Exception("Source file not found — browse or drag one in.");
            return path;
        }

        private long ParseOffset(TextBox tb)
        {
            if (!FileEngine.TryParseOffset(tb.Text.Trim(), out long val))
                throw new Exception($"Invalid offset: '{tb.Text.Trim()}' — use hex (0x…) or decimal.");
            return val;
        }

        // ── Extract tab ───────────────────────────────────────────────────

        private void BtnExtractOut_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) txtExtractOut.Text = dlg.FileName;
        }

        private void BtnExtract_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string input  = RequireInput();
                long   offset = ParseOffset(txtExtractOffset);

                // size == 0 → to end of file; ParseSize rejects 0, so special-case it here
                string rawSizeStr = txtExtractSize.Text.Trim();
                string sizeUnit   = (cboExtractUnit.SelectedItem as ComboBoxItem)?.Content?.ToString();
                long   size       = long.TryParse(rawSizeStr, out long rawN) && rawN == 0
                                        ? 0
                                        : FileEngine.ParseSize(rawSizeStr, sizeUnit);

                string output = txtExtractOut.Text.Trim();
                if (string.IsNullOrWhiteSpace(output)) throw new Exception("Specify an output file.");

                FileEngine.ExtractRegion(input, output, offset, size == 0 ? long.MaxValue : size);
                long outSize = new FileInfo(output).Length;
                SetTabStatus(borderExtractStatus, txtExtractStatus, $"Extracted {FileEngine.FormatSize(outSize)} → {Path.GetFileName(output)}", true);
            }
            catch (Exception ex) { SetTabStatus(borderExtractStatus, txtExtractStatus, ex.Message, false); }
        }

        // ── Insert tab ────────────────────────────────────────────────────

        private void BtnInsertOut_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) txtInsertOut.Text = dlg.FileName;
        }

        private async void BtnInsert_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            var btn = (Button)sender; _isBusy = true; btn.IsEnabled = false;
            ViewHelper.ShowInfo(borderInsertStatus, txtInsertStatus, "Working…");
            try
            {
                string input  = RequireInput();
                long   offset = ParseOffset(txtInsertOffset);
                byte[] data   = FileEngine.ParseHexBytes(txtInsertData.Text);
                if (data.Length == 0) throw new Exception("Data to insert is empty.");
                string output = txtInsertOut.Text.Trim();
                if (string.IsNullOrWhiteSpace(output)) throw new Exception("Specify an output file.");
                int    dataLen = data.Length; long ins = offset;
                long outSize = await Task.Run(() =>
                {
                    FileEngine.InsertData(input, output, offset, data);
                    return new FileInfo(output).Length;
                });
                ViewHelper.ShowSuccess(borderInsertStatus, txtInsertStatus,
                    $"Inserted {dataLen} bytes at 0x{ins:X8} \u2192 {FileEngine.FormatSize(outSize)} total");
            }
            catch (Exception ex) { ViewHelper.ShowError(borderInsertStatus, txtInsertStatus, ex.Message); }
            finally { btn.IsEnabled = true; _isBusy = false; }
        }

        // ── Delete tab ────────────────────────────────────────────────────

        private void BtnDeleteOut_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) txtDeleteOut.Text = dlg.FileName;
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            var btn = (Button)sender; _isBusy = true; btn.IsEnabled = false;
            ViewHelper.ShowInfo(borderDeleteStatus, txtDeleteStatus, "Working…");
            try
            {
                string input  = RequireInput();
                long   offset = ParseOffset(txtDeleteOffset);
                long   size   = FileEngine.ParseSize(txtDeleteSize.Text,
                                    (cboDeleteUnit.SelectedItem as ComboBoxItem)?.Content?.ToString());
                string output = txtDeleteOut.Text.Trim();
                if (string.IsNullOrWhiteSpace(output)) throw new Exception("Specify an output file.");
                long delSize = size;
                long outSize = await Task.Run(() =>
                {
                    FileEngine.DeleteRegion(input, output, offset, size);
                    return new FileInfo(output).Length;
                });
                ViewHelper.ShowSuccess(borderDeleteStatus, txtDeleteStatus,
                    $"Deleted {FileEngine.FormatSize(delSize)} → {FileEngine.FormatSize(outSize)} remaining");
            }
            catch (Exception ex) { ViewHelper.ShowError(borderDeleteStatus, txtDeleteStatus, ex.Message); }
            finally { btn.IsEnabled = true; _isBusy = false; }
        }

        // ── Overwrite tab ─────────────────────────────────────────────────

        private void CboOvrFill_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (panelOvrByte == null) return;
            int idx = cboOvrFill.SelectedIndex;
            panelOvrByte.Visibility = idx == 1 ? Visibility.Visible : Visibility.Collapsed;
            panelOvrPat.Visibility  = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnOvrOut_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) txtOvrOut.Text = dlg.FileName;
        }

        private async void BtnOverwrite_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            var btn = (Button)sender; _isBusy = true; btn.IsEnabled = false;
            ViewHelper.ShowInfo(borderOvrStatus, txtOvrStatus, "Working…");
            try
            {
                string input  = RequireInput();
                long   offset = ParseOffset(txtOvrOffset);
                long   size   = FileEngine.ParseSize(txtOvrSize.Text,
                                    (cboOvrUnit.SelectedItem as ComboBoxItem)?.Content?.ToString());
                string output = txtOvrOut.Text.Trim();
                if (string.IsNullOrWhiteSpace(output)) throw new Exception("Specify an output file.");

                FillMode mode;
                byte specByte = 0;
                byte[] hexPat = null;
                switch (cboOvrFill.SelectedIndex)
                {
                    case 1:
                        mode = FillMode.SpecificByte;
                        string hv = txtOvrByte.Text.Trim().TrimStart('0', 'x', 'X');
                        if (!byte.TryParse(hv, System.Globalization.NumberStyles.HexNumber, null, out specByte))
                            throw new Exception("Invalid byte value.");
                        break;
                    case 2:
                        mode   = FillMode.HexPattern;
                        hexPat = FileEngine.ParseHexBytes(txtOvrPat.Text);
                        if (hexPat.Length == 0) throw new Exception("Hex pattern is empty.");
                        break;
                    case 3: mode = FillMode.Random; break;
                    default: mode = FillMode.Zeros; break;
                }

                FillMode  modeC = mode; byte specC = specByte; byte[] patC = hexPat;
                long      ovrSize = size; long ovrOff = offset;
                await Task.Run(() => FileEngine.OverwriteRegion(input, output, ovrOff, ovrSize, modeC, specC, patC));
                ViewHelper.ShowSuccess(borderOvrStatus, txtOvrStatus,
                    $"Overwritten {FileEngine.FormatSize(size)} at 0x{offset:X8}");
            }
            catch (Exception ex) { ViewHelper.ShowError(borderOvrStatus, txtOvrStatus, ex.Message); }
            finally { btn.IsEnabled = true; _isBusy = false; }
        }

        // ── Truncate tab ──────────────────────────────────────────────────

        private void BtnTruncOut_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) txtTruncOut.Text = dlg.FileName;
        }

        private async void BtnTruncate_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            var btn = (Button)sender; _isBusy = true; btn.IsEnabled = false;
            ViewHelper.ShowInfo(borderTruncStatus, txtTruncStatus, "Working…");
            try
            {
                string input  = RequireInput();
                long   size   = FileEngine.ParseSize(txtTruncSize.Text,
                                    (cboTruncUnit.SelectedItem as ComboBoxItem)?.Content?.ToString());
                string output = txtTruncOut.Text.Trim();
                if (string.IsNullOrWhiteSpace(output)) throw new Exception("Specify an output file.");
                long truncSize = size;
                await Task.Run(() => FileEngine.TruncateFile(input, output, truncSize));
                ViewHelper.ShowSuccess(borderTruncStatus, txtTruncStatus,
                    $"Truncated to {FileEngine.FormatSize(size)} → {Path.GetFileName(output)}");
            }
            catch (Exception ex) { ViewHelper.ShowError(borderTruncStatus, txtTruncStatus, ex.Message); }
            finally { btn.IsEnabled = true; _isBusy = false; }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static void SetTabStatus(Border border, TextBlock tb, string msg, bool ok)
            => ViewHelper.ShowTabStatus(border, tb, msg, ok);

        private void BtnDismissStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Parent is Grid g && g.Parent is Border b)
                b.Visibility = Visibility.Collapsed;
        }
    }
}
