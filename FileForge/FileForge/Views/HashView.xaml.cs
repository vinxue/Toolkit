using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FileForge.Core;

namespace FileForge.Views
{
    public partial class HashView : UserControl
    {
        public HashView()
        {
            InitializeComponent();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) SetFile(dlg.FileName);
        }

        private void SetFile(string path)
        {
            txtInput.Text = path;
            panelResults.Visibility = Visibility.Collapsed;
            if (!File.Exists(path)) return;
            var info = new FileInfo(path);
            txtInputInfo.Text = $"Size: {FileEngine.FormatSize(info.Length)}  •  0x{info.Length:X}  •  {info.LastWriteTime:yyyy-MM-dd HH:mm}";
        }

        private void ChkRegion_Changed(object sender, RoutedEventArgs e)
        {
            if (panelRegion == null) return;
            panelRegion.IsEnabled = chkRegion.IsChecked == true;
        }

        private void BtnCompute_Click(object sender, RoutedEventArgs e)
        {
            _ = ComputeAsync(sender as System.Windows.Controls.Button);
        }

        private async Task ComputeAsync(System.Windows.Controls.Button btn)
        {
            try
            {
                string path = txtInput.Text.Trim();
                if (!File.Exists(path)) throw new Exception("File not found.");

                long offset = 0, size = -1;
                bool useRegion = chkRegion.IsChecked == true;
                if (useRegion)
                {
                    if (!FileEngine.TryParseOffset(txtOffset.Text.Trim(), out offset))
                        throw new Exception("Invalid offset.");
                    if (!long.TryParse(txtSize.Text.Trim(), out size) || size < 0)
                        throw new Exception("Invalid size — enter 0 for full file.");
                    if (size == 0) size = -1;
                }

                // Fixed: was '!chkMD5.IsChecked == true' (ambiguous); now unambiguous
                bool doMd5    = chkMD5.IsChecked    == true;
                bool doSha1   = chkSHA1.IsChecked   == true;
                bool doSha256 = chkSHA256.IsChecked == true;
                bool doSha512 = chkSHA512.IsChecked == true;
                bool doCrc32  = chkCRC32.IsChecked  == true;

                if (!doMd5 && !doSha1 && !doSha256 && !doSha512 && !doCrc32)
                    throw new Exception("Select at least one algorithm.");

                if (btn != null) btn.IsEnabled = false;
                ShowInfo("Computing…");

                var results = await Task.Run(() =>
                {
                    var list = new List<HashResult>();
                    if (doMd5)    list.Add(new HashResult { Label = "MD5",     Value = HashEngine.ComputeMD5   (path, offset, size) });
                    if (doSha1)   list.Add(new HashResult { Label = "SHA-1",   Value = HashEngine.ComputeSHA1  (path, offset, size) });
                    if (doSha256) list.Add(new HashResult { Label = "SHA-256", Value = HashEngine.ComputeSHA256(path, offset, size) });
                    if (doSha512) list.Add(new HashResult { Label = "SHA-512", Value = HashEngine.ComputeSHA512(path, offset, size) });
                    if (doCrc32)  list.Add(new HashResult { Label = "CRC-32",  Value = $"0x{HashEngine.ComputeCRC32(path, offset, size):X8}" });
                    return list;
                });

                lstResults.ItemsSource = results;
                panelResults.Visibility = Visibility.Visible;
                ShowSuccess($"Computed {results.Count} hash(es).");
            }
            catch (Exception ex) { ShowError(ex.Message); }
            finally { if (btn != null) btn.IsEnabled = true; }
        }

        private void BtnCopyHash_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string val && !string.IsNullOrEmpty(val))
                try { Clipboard.SetDataObject(val, true); } catch { }
        }

        private void BtnCopyAll_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            if (lstResults.ItemsSource is List<HashResult> items)
                foreach (var r in items)
                    sb.AppendLine($"{r.Label}: {r.Value}");
            string text = sb.ToString().TrimEnd();
            if (string.IsNullOrEmpty(text)) return;
            try { Clipboard.SetDataObject(text, true); } catch { }
        }

        private void View_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0) SetFile(files[0]);
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
