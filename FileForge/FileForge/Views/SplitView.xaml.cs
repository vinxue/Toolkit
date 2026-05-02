using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FileForge.Core;

namespace FileForge.Views
{
    public partial class SplitView : UserControl
    {
        public SplitView()
        {
            InitializeComponent();
        }

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
            txtInputInfo.Text = $"Size: {FileEngine.FormatSize(info.Length)}  •  {info.LastWriteTime:yyyy-MM-dd HH:mm}";
            if (string.IsNullOrWhiteSpace(txtOutputDir.Text))
                txtOutputDir.Text = info.DirectoryName;
        }

        private void BtnBrowseDir_Click(object sender, RoutedEventArgs e)
        {
            string selected = ViewHelper.BrowseForFolder(
                Window.GetWindow(this), "Select Output Directory",
                string.IsNullOrWhiteSpace(txtOutputDir.Text) ? null : txtOutputDir.Text);
            if (selected != null) txtOutputDir.Text = selected;
        }

        private void SplitMode_Changed(object sender, RoutedEventArgs e)
        {
            if (panelBySize == null) return;
            bool bySize = rdoBySize.IsChecked == true;
            panelBySize.Visibility   = bySize ? Visibility.Visible : Visibility.Collapsed;
            panelByOffset.Visibility = bySize ? Visibility.Collapsed : Visibility.Visible;
        }

        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            try { _ = ExecuteAsync(sender as System.Windows.Controls.Button); }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private async Task ExecuteAsync(System.Windows.Controls.Button btn)
        {
            string input   = txtInput.Text.Trim();
            string outDir  = txtOutputDir.Text.Trim();
            string pattern = txtNamePattern.Text.Trim();

            if (!File.Exists(input))                throw new Exception("Input file not found.");
            if (string.IsNullOrWhiteSpace(outDir))  throw new Exception("Please specify an output directory.");
            if (string.IsNullOrWhiteSpace(pattern)) throw new Exception("Please specify a naming pattern.");

            bool bySize      = rdoBySize.IsChecked == true;
            long chunkSize   = 0;
            long[] offsets   = null;

            if (bySize)
            {
                chunkSize = FileEngine.ParseSize(txtChunkSize.Text,
                    (cboChunkUnit.SelectedItem as ComboBoxItem)?.Content?.ToString());
            }
            else
            {
                string[] lines = txtOffsets.Text.Split(new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries);
                var offs = new List<long>();
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (!FileEngine.TryParseOffset(line.Trim(), out long off))
                        throw new Exception($"Invalid offset: '{line.Trim()}'");
                    offs.Add(off);
                }
                if (offs.Count == 0) throw new Exception("No valid offsets specified.");
                offsets = offs.ToArray();
            }

            if (btn != null) btn.IsEnabled = false;
            ShowInfo("Working…");
            try
            {
                List<string> results = await Task.Run(() =>
                    bySize
                        ? FileEngine.SplitBySize(input, outDir, pattern, chunkSize)
                        : FileEngine.SplitByOffsets(input, outDir, pattern, offsets));

                lstResults.Visibility = Visibility.Visible;
                lstResults.Items.Clear();
                foreach (string f in results)
                {
                    long sz = new FileInfo(f).Length;
                    lstResults.Items.Add($"{Path.GetFileName(f)}  ({FileEngine.FormatSize(sz)})");
                }
                ShowSuccess($"Split into {results.Count} file(s).");
            }
            finally { if (btn != null) btn.IsEnabled = true; }
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
