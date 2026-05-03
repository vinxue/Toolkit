using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace FileForge.Views
{
    public partial class TimestampView : UserControl
    {
        private string   _filePath;
        private DateTime _origCreated;
        private DateTime _origModified;
        private DateTime _origAccessed;

        public TimestampView()
        {
            InitializeComponent();
        }

        // ── File open ─────────────────────────────────────────────────────────

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) LoadFile(dlg.FileName);
        }

        private void LoadFile(string path)
        {
            if (!File.Exists(path))
            { ViewHelper.ShowError(txtStatus, "File not found."); return; }

            try
            {
                _filePath     = path;
                txtInput.Text = path;

                _origCreated  = File.GetCreationTime(path);
                _origModified = File.GetLastWriteTime(path);
                _origAccessed = File.GetLastAccessTime(path);

                PopulateFields(_origCreated, _origModified, _origAccessed);

                var fi = new FileInfo(path);
                txtFileInfo.Text = string.Format(
                    "{0}   \u2022   {1:N0} bytes",
                    fi.Name, fi.Length);

                panelTimestamps.Visibility = Visibility.Visible;
                btnApply.IsEnabled = true;
                btnReset.IsEnabled = true;
                ViewHelper.ShowInfo(txtStatus, "Timestamps loaded.");
            }
            catch (UnauthorizedAccessException)
            {
                ViewHelper.ShowError(txtStatus, "Access denied — try running as administrator.");
            }
            catch (Exception ex)
            {
                ViewHelper.ShowError(txtStatus, "Error reading timestamps: " + ex.Message);
            }
        }

        private void PopulateFields(DateTime created, DateTime modified, DateTime accessed)
        {
            SetRow(dpCreated,  txtCreatedTime,  created);
            SetRow(dpModified, txtModifiedTime, modified);
            SetRow(dpAccessed, txtAccessedTime, accessed);
        }

        // ── Timestamp helpers ─────────────────────────────────────────────────

        private static void SetRow(DatePicker dp, TextBox tb, DateTime dt)
        {
            dp.SelectedDate = dt.Date;
            tb.Text = dt.ToString("HH:mm:ss");
        }

        private static bool TryGetDateTime(DatePicker dp, TextBox tb, out DateTime result)
        {
            result = DateTime.MinValue;
            if (!dp.SelectedDate.HasValue) return false;

            string timeStr = (tb.Text ?? "").Trim();
            // Accept HH:mm:ss, H:mm:ss, HH:mm, H:mm
            string[] formats = { @"hh\:mm\:ss", @"h\:mm\:ss", @"hh\:mm", @"h\:mm" };
            if (!TimeSpan.TryParseExact(timeStr, formats,
                    CultureInfo.InvariantCulture, out TimeSpan ts))
                return false;

            result = dp.SelectedDate.Value.Date + ts;
            return true;
        }

        // ── "Now" buttons ─────────────────────────────────────────────────────

        private void BtnNowCreated_Click(object sender, RoutedEventArgs e)
            => SetRow(dpCreated, txtCreatedTime, DateTime.Now);

        private void BtnNowModified_Click(object sender, RoutedEventArgs e)
            => SetRow(dpModified, txtModifiedTime, DateTime.Now);

        private void BtnNowAccessed_Click(object sender, RoutedEventArgs e)
            => SetRow(dpAccessed, txtAccessedTime, DateTime.Now);

        // ── Apply / Reset ─────────────────────────────────────────────────────

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (_filePath == null)
            { ViewHelper.ShowError(txtStatus, "No file selected."); return; }

            if (!TryGetDateTime(dpCreated, txtCreatedTime, out DateTime created))
            { ViewHelper.ShowError(txtStatus, "Invalid Created time — use HH:mm:ss."); return; }
            if (!TryGetDateTime(dpModified, txtModifiedTime, out DateTime modified))
            { ViewHelper.ShowError(txtStatus, "Invalid Modified time — use HH:mm:ss."); return; }
            if (!TryGetDateTime(dpAccessed, txtAccessedTime, out DateTime accessed))
            { ViewHelper.ShowError(txtStatus, "Invalid Accessed time — use HH:mm:ss."); return; }

            try
            {
                File.SetCreationTime(_filePath,   created);
                File.SetLastWriteTime(_filePath,  modified);
                File.SetLastAccessTime(_filePath, accessed);

                // Update cached originals so Reset reflects the just-applied values
                _origCreated  = created;
                _origModified = modified;
                _origAccessed = accessed;

                ViewHelper.ShowSuccess(txtStatus, "Timestamps updated successfully.");
            }
            catch (UnauthorizedAccessException)
            {
                ViewHelper.ShowError(txtStatus, "Access denied — try running as administrator.");
            }
            catch (Exception ex)
            {
                ViewHelper.ShowError(txtStatus, "Failed to update timestamps: " + ex.Message);
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if (_filePath == null) return;
            PopulateFields(_origCreated, _origModified, _origAccessed);
            ViewHelper.ShowInfo(txtStatus, "Reset to last saved values.");
        }

        // ── Drag-drop ─────────────────────────────────────────────────────────

        private void View_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0) LoadFile(files[0]);
            }
        }

        private void View_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                        ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }
    }
}
