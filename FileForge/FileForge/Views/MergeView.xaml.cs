using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FileForge.Core;

namespace FileForge.Views
{
    public partial class MergeView : UserControl
    {
        private readonly ObservableCollection<string> _files = new ObservableCollection<string>();

        public MergeView()
        {
            InitializeComponent();
            lstFiles.ItemsSource = _files;
        }

        private void BtnAddFiles_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "All Files (*.*)|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
            {
                foreach (string f in dlg.FileNames) _files.Add(f);
                if (string.IsNullOrWhiteSpace(txtOutput.Text) && _files.Count > 0)
                    SuggestOutput();
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (lstFiles.SelectedIndex >= 0)
                _files.RemoveAt(lstFiles.SelectedIndex);
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int idx = lstFiles.SelectedIndex;
            if (idx > 0)
            {
                var tmp = _files[idx - 1];
                _files[idx - 1] = _files[idx];
                _files[idx]     = tmp;
                lstFiles.SelectedIndex = idx - 1;
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int idx = lstFiles.SelectedIndex;
            if (idx >= 0 && idx < _files.Count - 1)
            {
                var tmp = _files[idx + 1];
                _files[idx + 1] = _files[idx];
                _files[idx]     = tmp;
                lstFiles.SelectedIndex = idx + 1;
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            _files.Clear();
        }

        private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) txtOutput.Text = dlg.FileName;
        }

        private void CboSepMode_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (panelSepHex == null) return;
            int idx = cboSepMode.SelectedIndex;
            panelSepHex.Visibility  = idx == 1 ? Visibility.Visible : Visibility.Collapsed;
            panelSepZero.Visibility = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LstFiles_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string f in files)
                    if (File.Exists(f)) _files.Add(f);
                if (string.IsNullOrWhiteSpace(txtOutput.Text) && _files.Count > 0)
                    SuggestOutput();
            }
        }

        private void LstFiles_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            try { Execute(); }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private void Execute()
        {
            if (_files.Count < 2) throw new Exception("Add at least two files to merge.");
            foreach (string f in _files)
                if (!File.Exists(f)) throw new Exception($"File not found: {f}");

            string output = txtOutput.Text.Trim();
            if (string.IsNullOrWhiteSpace(output)) throw new Exception("Please specify an output file.");

            byte[] separator = BuildSeparator();
            FileEngine.MergeFiles(_files.ToList(), output, separator);

            long outSize = new FileInfo(output).Length;
            ShowSuccess($"Merged {_files.Count} files → {Path.GetFileName(output)}  ({FileEngine.FormatSize(outSize)})");
        }

        private byte[] BuildSeparator()
        {
            switch (cboSepMode.SelectedIndex)
            {
                case 1:
                    return FileEngine.ParseHexBytes(txtSepHex.Text);
                case 2:
                    if (!int.TryParse(txtSepZeroCount.Text.Trim(), out int n) || n < 0)
                        throw new Exception("Invalid zero-byte count.");
                    return new byte[n];
                default:
                    return null;
            }
        }

        private void SuggestOutput()
        {
            if (_files.Count == 0) return;
            var first = new FileInfo(_files[0]);
            txtOutput.Text = Path.Combine(first.DirectoryName,
                Path.GetFileNameWithoutExtension(first.Name) + "_merged" + first.Extension);
        }

        private void ShowError  (string msg) => statusBanner.ShowError  (msg);
        private void ShowSuccess(string msg) => statusBanner.ShowSuccess(msg);
        private void ShowInfo   (string msg) => statusBanner.ShowInfo   (msg);
    }
}
