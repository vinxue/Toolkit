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
    public partial class SearchView : UserControl
    {
        private List<long> _lastMatches = new List<long>();

        public SearchView()
        {
            InitializeComponent();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) SetFile(dlg.FileName);
        }

        private void BtnBrowseOut_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) txtOutput.Text = dlg.FileName;
        }

        private void SetFile(string path)
        {
            txtInput.Text = path;
            _lastMatches.Clear();
            lstMatches.Items.Clear();
            txtMatchCount.Text = "Matches";
            if (!File.Exists(path)) return;
            var info = new FileInfo(path);
            txtInputInfo.Text = $"Size: {FileEngine.FormatSize(info.Length)}  •  0x{info.Length:X}";
        }

        private void ChkReplace_Changed(object sender, RoutedEventArgs e)
        {
            if (panelReplace == null) return;
            bool replacing = chkReplace.IsChecked == true;
            panelReplace.Visibility = replacing ? Visibility.Visible : Visibility.Collapsed;
            btnReplace.Visibility   = replacing ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            try { Search(); }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private void BtnReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            try { ReplaceAll(); }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private void Search()
        {
            string path = txtInput.Text.Trim();
            if (!File.Exists(path)) throw new Exception("File not found.");

            byte?[] pattern = FileEngine.ParseHexPattern(txtSearch.Text);

            ShowInfo("Searching…");
            _lastMatches = FileEngine.SearchPattern(path, pattern);

            lstMatches.Items.Clear();
            foreach (long m in _lastMatches)
                lstMatches.Items.Add($"0x{m:X8}  ({m})");

            int count = _lastMatches.Count;
            bool capped = count >= 100000;
            txtMatchCount.Text = capped
                ? $"{count} matches (limit reached — first 100 000 shown)"
                : $"{count} match{(count == 1 ? "" : "es")}";

            if (count == 0) ShowInfo("No matches found.");
            else ShowSuccess($"Found {count} match{(count == 1 ? "" : "es")}{(capped ? " (capped)" : "")}.");
        }

        private void ReplaceAll()
        {
            string path   = txtInput.Text.Trim();
            string output = txtOutput.Text.Trim();
            if (!File.Exists(path))   throw new Exception("Source file not found.");
            if (string.IsNullOrWhiteSpace(output)) throw new Exception("Specify an output file.");
            if (string.Equals(path, output, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Input and output paths must be different.");

            byte?[] searchPat  = FileEngine.ParseHexPattern(txtSearch.Text);
            byte[]  replacePat = FileEngine.ParseHexBytes(txtReplace.Text);

            ShowInfo("Replacing…");
            long count = FileEngine.ReplaceAll(path, output, searchPat, replacePat);
            long newSize = new FileInfo(output).Length;
            ShowSuccess($"Replaced {count} occurrence(s) → {Path.GetFileName(output)}  ({FileEngine.FormatSize(newSize)})");
        }

        private void BtnCopyList_Click(object sender, RoutedEventArgs e)
        {
            if (_lastMatches.Count == 0) return;
            var sb = new StringBuilder();
            foreach (long m in _lastMatches)
                sb.AppendLine($"0x{m:X8}");
            try { Clipboard.SetDataObject(sb.ToString().TrimEnd(), true); } catch { }
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
