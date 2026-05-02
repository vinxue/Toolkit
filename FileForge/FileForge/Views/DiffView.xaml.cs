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
    public partial class DiffView : UserControl
    {
        private List<DiffEntry> _diffs = new List<DiffEntry>();

        public DiffView()
        {
            InitializeComponent();
        }

        private void BtnBrowseA_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) SetFileA(dlg.FileName);
        }

        private void BtnBrowseB_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) SetFileB(dlg.FileName);
        }

        private void SetFileA(string path)
        {
            txtFileA.Text = path;
            if (!File.Exists(path)) return;
            var info = new FileInfo(path);
            txtInfoA.Text = $"Size: {FileEngine.FormatSize(info.Length)}  •  0x{info.Length:X}";
        }

        private void SetFileB(string path)
        {
            txtFileB.Text = path;
            if (!File.Exists(path)) return;
            var info = new FileInfo(path);
            txtInfoB.Text = $"Size: {FileEngine.FormatSize(info.Length)}  •  0x{info.Length:X}";
        }

        private void BtnCompare_Click(object sender, RoutedEventArgs e)
        {
            try { Compare(); }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private void Compare()
        {
            string pathA = txtFileA.Text.Trim();
            string pathB = txtFileB.Text.Trim();
            if (!File.Exists(pathA)) throw new Exception("File A not found.");
            if (!File.Exists(pathB)) throw new Exception("File B not found.");

            ShowInfo("Comparing…");
            _diffs = FileEngine.DiffFiles(pathA, pathB, 50000);

            var rows = new List<DiffRow>();
            foreach (var d in _diffs)
            {
                rows.Add(new DiffRow
                {
                    OffsetHex = d.OffsetHex,
                    ValueAHex = d.ValueAHex,
                    ValueBHex = d.ValueBHex,
                    CharA     = PrintableChar(d.ValueA),
                    CharB     = PrintableChar(d.ValueB)
                });
            }
            gridDiffs.ItemsSource = rows;

            long sizeA = new FileInfo(pathA).Length;
            long sizeB = new FileInfo(pathB).Length;
            long sizeDiff = sizeB - sizeA;

            bool capped = _diffs.Count >= 50000;
            string msg = _diffs.Count == 0
                ? "Files are identical."
                : $"{_diffs.Count} difference{(_diffs.Count == 1 ? "" : "s")}{(capped ? " (first 50 000 shown)" : "")}  •  Size difference: {(sizeDiff >= 0 ? "+" : "")}{FileEngine.FormatSize(Math.Abs(sizeDiff))}";

            if (_diffs.Count == 0) ShowSuccess(msg);
            else ShowInfo(msg);

            btnExport.Visibility = _diffs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter   = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    FileName = "diff_report.txt"
                };
                if (dlg.ShowDialog() != true) return;

                var sb = new StringBuilder();
                sb.AppendLine($"# FileForge Diff Report");
                sb.AppendLine($"# File A: {txtFileA.Text}");
                sb.AppendLine($"# File B: {txtFileB.Text}");
                sb.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"# Differences: {_diffs.Count}");
                sb.AppendLine();
                sb.AppendLine("Offset      File A  File B");
                sb.AppendLine(new string('-', 30));
                foreach (var d in _diffs)
                    sb.AppendLine($"{d.OffsetHex}  {d.ValueAHex}      {d.ValueBHex}");

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                ShowSuccess($"Report saved: {Path.GetFileName(dlg.FileName)}");
            }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private void View_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length >= 2) { SetFileA(files[0]); SetFileB(files[1]); }
                else if (files.Length == 1)
                {
                    if (string.IsNullOrWhiteSpace(txtFileA.Text)) SetFileA(files[0]);
                    else SetFileB(files[0]);
                }
            }
        }

        private void View_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private static string PrintableChar(byte b)
        {
            if (b == 0) return "";
            char c = (char)b;
            return c >= 0x20 && c < 0x7F ? c.ToString() : ".";
        }

        private void ShowError  (string msg) => ViewHelper.ShowError  (txtStatus, msg);
        private void ShowSuccess(string msg) => ViewHelper.ShowSuccess(txtStatus, msg);
        private void ShowInfo   (string msg) => ViewHelper.ShowInfo   (txtStatus, msg);
    }
}
