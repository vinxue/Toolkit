using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FileForge.Core;

namespace FileForge.Views
{
    public partial class AlignView : UserControl
    {
        private static readonly long[] AlignValues =
            { 512, 1024, 4096, 65536, 524288, 1048576, 4194304, 0 }; // 0 = custom

        public AlignView()
        {
            InitializeComponent();
            cboAlign.SelectedIndex = 4; // 1 MB default
        }

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
            txtInputInfo.Text = $"Size: {FileEngine.FormatSize(info.Length)}  •  0x{info.Length:X}";
            if (string.IsNullOrWhiteSpace(txtOutput.Text))
            {
                string ext = info.Extension;
                txtOutput.Text = Path.Combine(info.DirectoryName,
                    Path.GetFileNameWithoutExtension(info.Name) + "_aligned" + ext);
            }
            UpdatePreview();
        }

        private void CboAlign_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (panelCustomAlign == null) return;
            bool custom = cboAlign.SelectedIndex == AlignValues.Length - 1 || cboAlign.SelectedIndex == 6;
            panelCustomAlign.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
            UpdatePreview();
        }

        private void CboFillByte_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (panelCustomByte == null) return;
            panelCustomByte.Visibility = cboFillByte.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AlignInput_Changed(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (txtPreviewOriginal == null) return;
            if (!File.Exists(txtInput.Text.Trim()))
            {
                txtPreviewOriginal.Text = txtPreviewPad.Text = txtPreviewNew.Text = "—";
                return;
            }

            try
            {
                long fileSize  = new FileInfo(txtInput.Text.Trim()).Length;
                long alignment = GetAlignment();
                if (alignment <= 0) { txtPreviewPad.Text = "—"; return; }

                long rem     = fileSize % alignment;
                long padSize = rem == 0 ? 0 : alignment - rem;

                txtPreviewOriginal.Text = FileEngine.FormatSize(fileSize);
                txtPreviewPad.Text      = padSize == 0 ? "Already aligned (0)" : FileEngine.FormatSize(padSize);
                txtPreviewNew.Text      = FileEngine.FormatSize(fileSize + padSize);
            }
            catch
            {
                txtPreviewPad.Text = "—";
            }
        }

        private long GetAlignment()
        {
            int idx = cboAlign.SelectedIndex;
            if (idx < 0 || idx >= AlignValues.Length) return 0;
            if (AlignValues[idx] != 0) return AlignValues[idx];
            // Custom
            if (FileEngine.TryParseOffset(txtCustomAlign.Text, out long v)) return v;
            return 0;
        }

        private byte GetFillByte()
        {
            switch (cboFillByte.SelectedIndex)
            {
                case 1: return 0xFF;
                case 2:
                    string hv = txtCustomByte.Text.Trim().TrimStart('0', 'x', 'X');
                    if (byte.TryParse(hv, System.Globalization.NumberStyles.HexNumber, null, out byte b))
                        return b;
                    throw new Exception("Invalid custom fill byte — use two hex digits, e.g. AA.");
                default: return 0x00;
            }
        }

        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string input  = txtInput.Text.Trim();
                string output = txtOutput.Text.Trim();

                if (!File.Exists(input))  throw new Exception("Input file not found.");
                if (string.IsNullOrWhiteSpace(output)) throw new Exception("Specify an output file.");
                if (string.Equals(input, output, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Input and output paths must be different.");

                long alignment = GetAlignment();
                if (alignment <= 0) throw new Exception("Invalid alignment value.");
                byte fillByte = GetFillByte();

                long padded = FileEngine.AlignFile(input, output, alignment, fillByte);
                long newSize = new FileInfo(output).Length;

                ShowSuccess(padded == 0
                    ? $"File was already aligned — copied as-is ({FileEngine.FormatSize(newSize)})"
                    : $"Added {FileEngine.FormatSize(padded)} of padding → {FileEngine.FormatSize(newSize)}");
            }
            catch (Exception ex) { ShowError(ex.Message); }
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

        private void ShowError  (string msg) => ViewHelper.ShowError  (txtStatus, msg);
        private void ShowSuccess(string msg) => ViewHelper.ShowSuccess(txtStatus, msg);
        private void ShowInfo   (string msg) => ViewHelper.ShowInfo   (txtStatus, msg);
    }
}
