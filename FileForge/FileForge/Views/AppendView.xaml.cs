using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FileForge.Core;

namespace FileForge.Views
{
    public partial class AppendView : UserControl
    {
        private long _sourceFileSize = -1; // -1 = no file loaded

        public AppendView() { InitializeComponent(); }

        // ── File browse ───────────────────────────────────────────────────────

        private void BtnBrowseInput_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) SetInputFile(dlg.FileName);
        }

        private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) txtOutput.Text = dlg.FileName;
        }

        private void SetInputFile(string path)
        {
            txtInput.Text = path;
            if (!File.Exists(path)) return;
            var info = new FileInfo(path);
            _sourceFileSize = info.Length;
            txtInputInfo.Text = "Size: " + FileEngine.FormatSize(_sourceFileSize)
                                + "  \u2022  Modified: " + info.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
            if (string.IsNullOrWhiteSpace(txtOutput.Text))
            {
                string ext = Path.GetExtension(path);
                txtOutput.Text = Path.Combine(info.DirectoryName,
                    Path.GetFileNameWithoutExtension(path) + "_out" + ext);
            }
            RecalcSizes();
        }

        // ── UI change handlers ───────────────────────────────────────────────

        private void Position_Changed(object sender, RoutedEventArgs e)
        {
            if (panelSingle == null) return;
            bool both = rdoBoth.IsChecked == true;
            panelSingle.Visibility = both ? Visibility.Collapsed : Visibility.Visible;
            panelBoth.Visibility   = both ? Visibility.Visible   : Visibility.Collapsed;
            RecalcSizes();
        }

        private void SizeMode_Changed(object sender, RoutedEventArgs e)
        {
            if (panelExactFill == null) return;
            bool total = rdoTotalSingle.IsChecked == true;
            panelExactFill.Visibility  = total ? Visibility.Collapsed : Visibility.Visible;
            panelTotalFill.Visibility  = total ? Visibility.Visible   : Visibility.Collapsed;
            RecalcSizes();
        }

        private void SizeInput_Changed(object sender, RoutedEventArgs e) => RecalcSizes();
        private void BothSizeInput_Changed(object sender, RoutedEventArgs e) => RecalcSizes();

        private void CboFillMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (panelSpecByte == null) return;
            int idx = cboFillMode.SelectedIndex;
            panelSpecByte.Visibility = idx == 1 ? Visibility.Visible : Visibility.Collapsed;
            panelHexPat.Visibility   = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Auto-calculate hints ──────────────────────────────────────────────

        private void RecalcSizes()
        {
            if (rdoBoth == null) return;

            if (rdoBoth.IsChecked == true)
            {
                RecalcBoth();
            }
            else
            {
                // Single side
                if (rdoTotalSingle.IsChecked == true && _sourceFileSize > 0)
                {
                    long total;
                    if (TryParseSize(txtTotalSize.Text, GetUnit(cboTotalUnit), out total))
                    {
                        long fill = total - _sourceFileSize;
                        txtCalcFill.Text = fill > 0
                            ? "\u2192 Fill: " + FileEngine.FormatSize(fill)
                            : fill == 0
                                ? "\u2192 Fill: 0 bytes (no padding needed)"
                                : "\u26a0 Total smaller than source";
                        txtCalcFill.Foreground = fill >= 0
                            ? new SolidColorBrush(Color.FromRgb(0, 120, 212))
                            : new SolidColorBrush(Color.FromRgb(192, 57, 43));
                    }
                    else
                    {
                        txtCalcFill.Text = "";
                    }
                }
                else
                {
                    if (txtCalcFill != null) txtCalcFill.Text = "";
                }
            }
        }

        private void RecalcBoth()
        {
            if (_sourceFileSize <= 0) { txtBothCalcSummary.Text = ""; return; }

            long front = ParseSizeOrNeg(txtFrontSize.Text, GetUnit(cboFrontUnit));
            long back  = ParseSizeOrNeg(txtBackSize.Text,  GetUnit(cboBackUnit));
            long total = ParseSizeOrNeg(txtBothTotalSize.Text, GetUnit(cboBothTotalUnit));

            int known = (front >= 0 ? 1 : 0) + (back >= 0 ? 1 : 0) + (total >= 0 ? 1 : 0);

            txtFrontCalc.Text = "";
            txtBackCalc.Text  = "";

            if (known == 0)
            {
                txtBothCalcSummary.Text = "Enter at least two values to auto-calculate the third.";
                return;
            }

            if (total >= 0 && front >= 0 && back < 0)
            {
                long calcBack = total - _sourceFileSize - front;
                txtBackCalc.Text = calcBack >= 0 ? "\u2192 " + FileEngine.FormatSize(calcBack) : "\u26a0 negative";
                txtBothCalcSummary.Text = calcBack >= 0
                    ? string.Format("Total: {0}  =  Source {1}  +  Front {2}  +  Back {3}",
                        FileEngine.FormatSize(total), FileEngine.FormatSize(_sourceFileSize),
                        FileEngine.FormatSize(front), FileEngine.FormatSize(calcBack))
                    : "Target total is too small.";
            }
            else if (total >= 0 && back >= 0 && front < 0)
            {
                long calcFront = total - _sourceFileSize - back;
                txtFrontCalc.Text = calcFront >= 0 ? "\u2192 " + FileEngine.FormatSize(calcFront) : "\u26a0 negative";
                txtBothCalcSummary.Text = calcFront >= 0
                    ? string.Format("Total: {0}  =  Source {1}  +  Front {2}  +  Back {3}",
                        FileEngine.FormatSize(total), FileEngine.FormatSize(_sourceFileSize),
                        FileEngine.FormatSize(calcFront), FileEngine.FormatSize(back))
                    : "Target total is too small.";
            }
            else if (total >= 0 && front < 0 && back < 0)
            {
                long remaining = total - _sourceFileSize;
                if (remaining >= 0)
                {
                    long half1 = remaining / 2, half2 = remaining - half1;
                    txtFrontCalc.Text = "\u2192 " + FileEngine.FormatSize(half1);
                    txtBackCalc.Text  = "\u2192 " + FileEngine.FormatSize(half2);
                    txtBothCalcSummary.Text = string.Format(
                        "Total: {0}  =  Source {1}  +  Front {2}  +  Back {3}",
                        FileEngine.FormatSize(total), FileEngine.FormatSize(_sourceFileSize),
                        FileEngine.FormatSize(half1), FileEngine.FormatSize(half2));
                }
                else
                {
                    txtBothCalcSummary.Text = "Target total is smaller than source file.";
                }
            }
            else if (front >= 0 && back >= 0)
            {
                long calcTotal = _sourceFileSize + front + back;
                txtBothCalcSummary.Text = string.Format(
                    "Result total: {0}  =  Source {1}  +  Front {2}  +  Back {3}",
                    FileEngine.FormatSize(calcTotal), FileEngine.FormatSize(_sourceFileSize),
                    FileEngine.FormatSize(front), FileEngine.FormatSize(back));
            }
            else
            {
                txtBothCalcSummary.Text = "Enter at least two values to auto-calculate the third.";
            }
        }

        // ── Execute ───────────────────────────────────────────────────────────

        private async void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            try { await ExecuteAsync(); }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private async Task ExecuteAsync()
        {
            string input  = txtInput.Text.Trim();
            string output = txtOutput.Text.Trim();

            if (!File.Exists(input))  throw new Exception("Input file not found.");
            if (string.IsNullOrWhiteSpace(output)) throw new Exception("Please specify an output file.");
            if (string.Equals(input, output, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Input and output paths must be different.");

            long src = new FileInfo(input).Length;

            long prependSize = 0, appendSize = 0;

            if (rdoBoth.IsChecked == true)
            {
                // Resolve Both sizes
                long front = ParseSizeOrNeg(txtFrontSize.Text, GetUnit(cboFrontUnit));
                long back  = ParseSizeOrNeg(txtBackSize.Text,  GetUnit(cboBackUnit));
                long total = ParseSizeOrNeg(txtBothTotalSize.Text, GetUnit(cboBothTotalUnit));

                if (total >= 0 && front >= 0 && back < 0)
                    back = total - src - front;
                else if (total >= 0 && back >= 0 && front < 0)
                    front = total - src - back;
                else if (total >= 0 && front < 0 && back < 0)
                {
                    long rem = total - src;
                    front = rem / 2; back = rem - front;
                }

                if (front < 0) throw new Exception("Could not determine front fill size. Provide front size, back size, or target total.");
                if (back  < 0) throw new Exception("Could not determine back fill size. Provide front size, back size, or target total.");
                prependSize = front;
                appendSize  = back;
            }
            else if (rdoPrepend.IsChecked == true)
            {
                prependSize = ResolveSingleFill(src);
            }
            else // append
            {
                appendSize = ResolveSingleFill(src);
            }

            FillMode mode;
            byte specByte = 0;
            byte[] hexPat  = null;

            switch (cboFillMode.SelectedIndex)
            {
                case 1:
                    mode = FillMode.SpecificByte;
                    string hexVal = txtSpecByte.Text.Trim().TrimStart('0', 'x', 'X');
                    if (!byte.TryParse(hexVal, System.Globalization.NumberStyles.HexNumber, null, out specByte))
                        throw new Exception("Invalid byte value — enter a hex value e.g. FF.");
                    break;
                case 2:
                    mode   = FillMode.HexPattern;
                    hexPat = FileEngine.ParseHexBytes(txtHexPat.Text);
                    if (hexPat.Length == 0) throw new Exception("Hex pattern is empty.");
                    break;
                case 3:
                    mode = FillMode.Random;
                    break;
                default:
                    mode = FillMode.Zeros;
                    break;
            }

            string  i_input   = input;  string  i_output  = output;
            long    i_prepend  = prependSize; long    i_append  = appendSize;
            FillMode i_mode    = mode; byte    i_spec    = specByte; byte[] i_pat = hexPat;

            btnExecute.IsEnabled = false;
            ShowInfo("Working…");
            try
            {
                await Task.Run(() =>
                    FileEngine.AppendPrepend(i_input, i_output, i_prepend, i_append, i_mode, i_spec, i_pat));
                long newSize = new FileInfo(i_output).Length;
                ShowSuccess("Done!  " + Path.GetFileName(i_output) + "  \u2192  " + FileEngine.FormatSize(newSize));
            }
            finally
            {
                btnExecute.IsEnabled = true;
            }
        }

        private long ResolveSingleFill(long sourceSize)
        {
            if (rdoTotalSingle.IsChecked == true)
            {
                long total;
                if (!TryParseSize(txtTotalSize.Text, GetUnit(cboTotalUnit), out total))
                    throw new Exception("Invalid target total size.");
                long fill = total - sourceSize;
                if (fill < 0) throw new Exception("Target total size is smaller than source file.");
                return fill;
            }
            else
            {
                long fill;
                if (!TryParseSize(txtFillSize.Text, GetUnit(cboFillUnit), out fill))
                    throw new Exception("Invalid fill size.");
                if (fill <= 0) throw new Exception("Fill size must be > 0.");
                return fill;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string GetUnit(ComboBox cb)
            => (cb?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Bytes";

        private static bool TryParseSize(string text, string unit, out long result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            try { result = FileEngine.ParseSize(text, unit); return true; }
            catch { return false; }
        }

        private static long ParseSizeOrNeg(string text, string unit)
        {
            if (string.IsNullOrWhiteSpace(text)) return -1;
            try { long v = FileEngine.ParseSize(text, unit); return v >= 0 ? v : -1; }
            catch { return -1; }
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

        private void ShowError  (string msg) => statusBanner.ShowError  (msg);
        private void ShowInfo   (string msg) => statusBanner.ShowInfo   (msg);
        private void ShowSuccess(string msg) => statusBanner.ShowSuccess(msg);
    }
}
