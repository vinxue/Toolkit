using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FileForge.Core;

namespace FileForge.Views
{
    public partial class GenerateView : UserControl
    {
        public GenerateView() { InitializeComponent(); }

        // ── Output browse ─────────────────────────────────────────────────────

        private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) txtOutput.Text = dlg.FileName;
        }

        // ── UI change handlers ────────────────────────────────────────────────

        private void SizeInput_Changed(object sender, RoutedEventArgs e)
        {
            if (txtSizeHint == null) return;
            string unit = (cboUnit?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Bytes";
            if (TryParseSize(txtSize?.Text, unit, out long bytes))
                txtSizeHint.Text = "= " + FileEngine.FormatSize(bytes);
            else
                txtSizeHint.Text = "";
        }

        private void CboFillMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (panelSpecByte == null) return;
            int idx = cboFillMode.SelectedIndex;
            panelSpecByte.Visibility = idx == 1 ? Visibility.Visible : Visibility.Collapsed;
            panelHexPat.Visibility   = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Execute ───────────────────────────────────────────────────────────

        private async void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            try { await ExecuteAsync(); }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private async Task ExecuteAsync()
        {
            string output = txtOutput.Text.Trim();
            if (string.IsNullOrWhiteSpace(output))
                throw new Exception("Please specify an output file.");

            string unit = (cboUnit?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Bytes";
            if (!TryParseSize(txtSize.Text, unit, out long size))
                throw new Exception("Invalid file size.");
            if (size <= 0)
                throw new Exception("File size must be greater than zero.");

            FillMode mode;
            byte specByte = 0;
            byte[] hexPat = null;

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

            string  i_output  = output;
            long    i_size    = size;
            FillMode i_mode   = mode;
            byte    i_spec    = specByte;
            byte[]  i_pat     = hexPat;

            btnExecute.IsEnabled = false;
            ShowInfo("Generating…");
            try
            {
                await Task.Run(() => FileEngine.GenerateFile(i_output, i_size, i_mode, i_spec, i_pat));
                ShowSuccess("Done!  " + Path.GetFileName(i_output) + "  →  " + FileEngine.FormatSize(size));
            }
            finally
            {
                btnExecute.IsEnabled = true;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool TryParseSize(string text, string unit, out long result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            try { result = FileEngine.ParseSize(text, unit); return true; }
            catch { return false; }
        }

        private void ShowError  (string msg) => statusBanner.ShowError  (msg);
        private void ShowInfo   (string msg) => statusBanner.ShowInfo   (msg);
        private void ShowSuccess(string msg) => statusBanner.ShowSuccess(msg);
    }
}
