using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FileForge.Core;

namespace FileForge.Views
{
    public partial class PatchView : UserControl
    {
        private readonly ObservableCollection<PatchEntry> _patches =
            new ObservableCollection<PatchEntry>();

        public PatchView()
        {
            InitializeComponent();
            gridPatches.ItemsSource = _patches;
        }

        // ── Source file ───────────────────────────────────────────────────

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
            txtInputInfo.Text = $"Size: {FileEngine.FormatSize(info.Length)}  •  0x{info.Length:X}";
            if (string.IsNullOrWhiteSpace(txtOutput.Text))
            {
                string ext = info.Extension;
                txtOutput.Text = Path.Combine(info.DirectoryName,
                    Path.GetFileNameWithoutExtension(info.Name) + "_patched" + ext);
            }
        }

        private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) txtOutput.Text = dlg.FileName;
        }

        // ── Patch entries ─────────────────────────────────────────────────

        private void BtnAddEntry_Click(object sender, RoutedEventArgs e)
        {
            ShowEntryDialog(null);
        }

        private void BtnEditEntry_Click(object sender, RoutedEventArgs e)
        {
            if (gridPatches.SelectedItem is PatchEntry sel)
                ShowEntryDialog(sel);
        }

        private void BtnRemoveEntry_Click(object sender, RoutedEventArgs e)
        {
            if (gridPatches.SelectedItem is PatchEntry sel)
                _patches.Remove(sel);
        }

        private void ShowEntryDialog(PatchEntry existing)
        {
            var dlg = new PatchEntryDialog(existing) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true)
            {
                if (existing != null) _patches.Remove(existing);
                _patches.Add(dlg.Result);
            }
        }

        // ── Import / Export ───────────────────────────────────────────────

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Patch Scripts (*.fpatch;*.txt)|*.fpatch;*.txt|All Files (*.*)|*.*"
                };
                if (dlg.ShowDialog() != true) return;

                int count = 0;
                foreach (string line in File.ReadAllLines(dlg.FileName, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")) continue;
                    string[] parts = line.Split('\t');
                    if (parts.Length < 2) continue;
                    if (!FileEngine.TryParseOffset(parts[0].Trim(), out long offset)) continue;
                    byte[] bytes = FileEngine.ParseHexBytes(parts[1].Trim());
                    if (bytes.Length == 0) continue;
                    string desc = parts.Length > 2 ? parts[2].Trim() : "";
                    _patches.Add(new PatchEntry { Offset = offset, NewBytes = bytes, Description = desc });
                    count++;
                }
                ShowSuccess($"Imported {count} patch entr{(count == 1 ? "y" : "ies")}.");
            }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_patches.Count == 0) throw new Exception("No patch entries to export.");
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter   = "Patch Scripts (*.fpatch)|*.fpatch|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    FileName = "patch.fpatch"
                };
                if (dlg.ShowDialog() != true) return;

                var sb = new StringBuilder();
                sb.AppendLine("# FileForge Patch Script");
                sb.AppendLine("# Format: Offset[hex]<TAB>NewBytes[hex]<TAB>Description");
                foreach (var p in _patches)
                    sb.AppendLine($"0x{p.Offset:X8}\t{p.BytesDisplay}\t{p.Description ?? ""}");

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                ShowSuccess($"Exported {_patches.Count} entries to {Path.GetFileName(dlg.FileName)}.");
            }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        // ── Apply ─────────────────────────────────────────────────────────

        private async void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            await ApplyAsync(sender as System.Windows.Controls.Button);
        }

        private async Task ApplyAsync(System.Windows.Controls.Button btn)
        {
            try
            {
                string input  = txtInput.Text.Trim();
                string output = txtOutput.Text.Trim();
                if (!File.Exists(input))  throw new Exception("Source file not found.");
                if (string.IsNullOrWhiteSpace(output)) throw new Exception("Specify an output file.");
                if (string.Equals(input, output, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Input and output paths must be different.");
                if (_patches.Count == 0) throw new Exception("No patch entries defined.");

                var patchCopy = _patches.ToArray();
                if (btn != null) btn.IsEnabled = false;
                ShowInfo("Applying patches…");
                await Task.Run(() => FileEngine.ApplyPatch(input, output, patchCopy));
                ShowSuccess($"Applied {patchCopy.Length} patch{(patchCopy.Length == 1 ? "" : "es")} \u2192 {Path.GetFileName(output)}");
            }
            catch (Exception ex) { ShowError(ex.Message); }
            finally { if (btn != null) btn.IsEnabled = true; }
        }

        // ── Drag-drop ─────────────────────────────────────────────────────

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

        // ── Status ────────────────────────────────────────────────────────

        private void ShowError  (string msg) => statusBanner.ShowError  (msg);
        private void ShowSuccess(string msg) => statusBanner.ShowSuccess(msg);
        private void ShowInfo   (string msg) => statusBanner.ShowInfo   (msg);
    }
}
