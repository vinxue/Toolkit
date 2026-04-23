using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using PdfKit.Models;
using PdfKit.Services;

namespace PdfKit
{
    public partial class MainWindow : Window
    {
        private readonly PdfService _pdf = new PdfService();
        private readonly ObservableCollection<PdfFileItem> _mergeFiles    = new ObservableCollection<PdfFileItem>();
        private readonly ObservableCollection<PageItem>    _organizePages = new ObservableCollection<PageItem>();

        // Track page counts for extract / rotate / watermark / split panels
        private int _extractPageCount;
        private int _rotatePageCount;
        private int _watermarkPageCount;
        private int _splitPageCount;

        public MainWindow()
        {
            InitializeComponent();
            var ver = System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            VersionLabel.Text = $"v{ver.FileMajorPart}.{ver.FileMinorPart}.{ver.FileBuildPart}.{ver.FilePrivatePart}";
            MergeFileList.ItemsSource = _mergeFiles;
            _mergeFiles.CollectionChanged += (s, e) => RefreshMergeEmptyState();
            RefreshMergeEmptyState();
            OrganizePageList.ItemsSource = _organizePages;
            _organizePages.CollectionChanged += (s, e) => RefreshOrganizeEmptyState();
            RefreshOrganizeEmptyState();
            ShowPanel(ExtractPanel);
            SizeChanged += MainWindow_SizeChanged;
            ApplyResponsiveLayout();
#if ENABLE_ACRYLIC
            Loaded += MainWindow_Loaded;
#endif
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveLayout();
        }

        private void ApplyResponsiveLayout()
        {
            Thickness hostMargin;
            Thickness cardPadding;

            if (ActualWidth < 980)
            {
                hostMargin = new Thickness(18, 20, 18, 24);
                cardPadding = new Thickness(18);
            }
            else if (ActualWidth < 1240)
            {
                hostMargin = new Thickness(28, 24, 28, 28);
                cardPadding = new Thickness(22);
            }
            else
            {
                hostMargin = new Thickness(40, 30, 40, 36);
                cardPadding = new Thickness(26);
            }

            ApplyResponsiveValues(ExtractHost,  ExtractCard,  hostMargin, cardPadding);
            ApplyResponsiveValues(MergeHost,    MergeCard,    hostMargin, cardPadding);
            ApplyResponsiveValues(RotateHost,   RotateCard,   hostMargin, cardPadding);
            ApplyResponsiveValues(OrganizeHost, OrganizeCard, hostMargin, cardPadding);
            ApplyResponsiveValues(WatermarkHost,  WatermarkCard,  hostMargin, cardPadding);
            ApplyResponsiveValues(SplitHost,      SplitCard,      hostMargin, cardPadding);
            ApplyResponsiveValues(MetadataHost,   MetadataCard,   hostMargin, cardPadding);
            ApplyResponsiveValues(SecurityHost,   SecurityCard,   hostMargin, cardPadding);
        }

        private static void ApplyResponsiveValues(FrameworkElement host, Border card, Thickness margin, Thickness padding)
        {
            if (host != null)
                host.Margin = margin;

            if (card != null)
                card.Padding = padding;
        }

        // ── Navigation ──────────────────────────────────────────────────

        private void NavExtract_Click(object sender, RoutedEventArgs e)  => ShowPanel(ExtractPanel);
        private void NavSplit_Click(object sender, RoutedEventArgs e)    => ShowPanel(SplitPanel);
        private void NavMerge_Click(object sender, RoutedEventArgs e)    => ShowPanel(MergePanel);
        private void NavRotate_Click(object sender, RoutedEventArgs e)   => ShowPanel(RotatePanel);
        private void NavOrganize_Click(object sender, RoutedEventArgs e) => ShowPanel(OrganizePanel);
        private void NavWatermark_Click(object sender, RoutedEventArgs e)=> ShowPanel(WatermarkPanel);
        private void NavMetadata_Click(object sender, RoutedEventArgs e) => ShowPanel(MetadataPanel);
        private void NavSecurity_Click(object sender, RoutedEventArgs e) => ShowPanel(SecurityPanel);

        // ── Language switcher ────────────────────────────────────────────────
        private void LangEN_Click(object sender, RoutedEventArgs e)
        {
            App.ApplyLanguage("en");
            Title = App.S("App_Title");
        }
        private void LangZHCN_Click(object sender, RoutedEventArgs e)
        {
            App.ApplyLanguage("zh-CN");
            Title = App.S("App_Title");
        }
        private void LangZHTW_Click(object sender, RoutedEventArgs e)
        {
            App.ApplyLanguage("zh-TW");
            Title = App.S("App_Title");
        }

        private void ShowPanel(UIElement target)
        {
            ExtractPanel.Visibility   = Visibility.Collapsed;
            SplitPanel.Visibility     = Visibility.Collapsed;
            MergePanel.Visibility     = Visibility.Collapsed;
            RotatePanel.Visibility    = Visibility.Collapsed;
            OrganizePanel.Visibility  = Visibility.Collapsed;
            WatermarkPanel.Visibility = Visibility.Collapsed;
            MetadataPanel.Visibility  = Visibility.Collapsed;
            SecurityPanel.Visibility  = Visibility.Collapsed;
            target.Visibility         = Visibility.Visible;
        }

        // ── Extract ─────────────────────────────────────────────────────

        private void ExtractBrowseSource_Click(object sender, RoutedEventArgs e)
        {
            string path = BrowsePdf();
            if (path == null) return;
            ExtractSourcePath.Text = path;
            try
            {
                _extractPageCount = _pdf.GetPageCount(path);
                ExtractPageInfo.Text = string.Format(App.S("Rt_PagesDetected"), _extractPageCount);
            }
            catch
            {
                ExtractPageInfo.Text = App.S("Rt_UnableToRead");
                _extractPageCount = 0;
            }
        }

        private void ExtractBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            string path = SavePdf(App.S("Dlg_SavePdf"));
            if (path != null) ExtractOutputPath.Text = path;
        }

        private async void ExtractExecute_Click(object sender, RoutedEventArgs e)
        {
            string src = ExtractSourcePath.Text.Trim();
            string dst = ExtractOutputPath.Text.Trim();
            string rangeStr = ExtractPages.Text.Trim();

            if (!ValidateSource(src)) return;
            if (string.IsNullOrEmpty(rangeStr)) { ShowError(ExtractStatus, ExtractStatusText, App.S("Err_NoPageRange")); return; }
            if (!ValidateOutput(dst)) return;
            if (_extractPageCount == 0) { ShowError(ExtractStatus, ExtractStatusText, App.S("Err_NoSourcePdf")); return; }

            System.Collections.Generic.List<int> pages;
            try { pages = PdfService.ParsePageRange(rangeStr, _extractPageCount); }
            catch (Exception ex) { ShowError(ExtractStatus, ExtractStatusText, ex.Message); return; }

            SetBusy(ExtractBtn, true);
            try
            {
                await Task.Run(() => _pdf.ExtractPages(src, pages, dst));
                ShowSuccess(ExtractStatus, ExtractStatusText,
                    string.Format(App.S("Ok_Extracted"), pages.Count, Path.GetFileName(dst)));
            }
            catch (Exception ex) { ShowError(ExtractStatus, ExtractStatusText, $"Extraction failed: {ex.Message}"); }
            finally { SetBusy(ExtractBtn, false); }
        }

        // ── Merge ────────────────────────────────────────────────────────

        private void MergeAddFiles_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = App.S("Dlg_SelectPdfs"),
                Filter = "PDF Files|*.pdf",
                Multiselect = true
            };
            if (dlg.ShowDialog() != true) return;

            foreach (string path in dlg.FileNames)
            {
                if (_mergeFiles.Any(f => f.FilePath == path)) continue;
                var item = new PdfFileItem { FilePath = path };
                try { item.PageCount = _pdf.GetPageCount(path); }
                catch { item.PageCount = 0; }
                _mergeFiles.Add(item);
            }
            RefreshMergeIndices();
        }

        private void MergeClearAll_Click(object sender, RoutedEventArgs e)
        {
            _mergeFiles.Clear();
        }

        private void MergeFileList_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string path in files)
            {
                if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) continue;
                if (_mergeFiles.Any(f => f.FilePath == path)) continue;
                var item = new PdfFileItem { FilePath = path };
                try { item.PageCount = _pdf.GetPageCount(path); }
                catch { item.PageCount = 0; }
                _mergeFiles.Add(item);
            }
            RefreshMergeIndices();
        }

        private void MergeFileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int idx = MergeFileList.SelectedIndex;
            bool selected = idx >= 0;
            MergeMoveUp.IsEnabled   = selected && idx > 0;
            MergeMoveDown.IsEnabled = selected && idx < _mergeFiles.Count - 1;
            MergeRemoveBtn.IsEnabled = selected;
        }

        private void MergeMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int idx = MergeFileList.SelectedIndex;
            if (idx <= 0) return;
            _mergeFiles.Move(idx, idx - 1);
            MergeFileList.SelectedIndex = idx - 1;
            RefreshMergeIndices();
        }

        private void MergeMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int idx = MergeFileList.SelectedIndex;
            if (idx < 0 || idx >= _mergeFiles.Count - 1) return;
            _mergeFiles.Move(idx, idx + 1);
            MergeFileList.SelectedIndex = idx + 1;
            RefreshMergeIndices();
        }

        private void MergeRemove_Click(object sender, RoutedEventArgs e)
        {
            int idx = MergeFileList.SelectedIndex;
            if (idx < 0) return;
            _mergeFiles.RemoveAt(idx);
            RefreshMergeIndices();
        }

        private void MergeBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            string path = SavePdf("Merge Documents");
            if (path != null) MergeOutputPath.Text = path;
        }

        private async void MergeExecute_Click(object sender, RoutedEventArgs e)
        {
            if (_mergeFiles.Count < 2)
            {
                ShowError(MergeStatus, MergeStatusText, "Add at least 2 PDF files.");
                return;
            }
            string dst = MergeOutputPath.Text.Trim();
            if (!ValidateOutput(dst)) return;

            SetBusy(MergeBtn, true);
            var paths = _mergeFiles.Select(f => f.FilePath).ToList();
            int totalPages = _mergeFiles.Sum(f => f.PageCount);
            try
            {
                await Task.Run(() => _pdf.MergePdfs(paths, dst));
                ShowSuccess(MergeStatus, MergeStatusText,
                    string.Format(App.S("Ok_Merged"), paths.Count, totalPages, Path.GetFileName(dst)));
            }
            catch (Exception ex) { ShowError(MergeStatus, MergeStatusText, $"Merge failed: {ex.Message}"); }
            finally { SetBusy(MergeBtn, false); }
        }

        private void RefreshMergeIndices()
        {
            for (int i = 0; i < _mergeFiles.Count; i++)
                _mergeFiles[i].DisplayIndex = i + 1;
            // Force ListBox to refresh display
            MergeFileList.Items.Refresh();
        }

        private void RefreshMergeEmptyState()
        {
            MergeEmptyState.Visibility = _mergeFiles.Count == 0
                ? Visibility.Visible : Visibility.Hidden;
        }

        // ── Rotate ───────────────────────────────────────────────────────

        private void RotateBrowseSource_Click(object sender, RoutedEventArgs e)
        {
            string path = BrowsePdf();
            if (path == null) return;
            RotateSourcePath.Text = path;
            try
            {
                _rotatePageCount = _pdf.GetPageCount(path);
                RotatePageInfo.Text = string.Format(App.S("Rt_PagesDetected"), _rotatePageCount);
            }
            catch
            {
                RotatePageInfo.Text = App.S("Rt_UnableToRead");
                _rotatePageCount = 0;
            }
        }

        private void RotateAllPages_Checked(object sender, RoutedEventArgs e)
        {
            if (RotatePages != null) RotatePages.IsEnabled = false;
        }

        private void RotateAllPages_Unchecked(object sender, RoutedEventArgs e)
        {
            if (RotatePages != null) RotatePages.IsEnabled = true;
        }

        private void RotateBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            string path = SavePdf("Rotate Pages");
            if (path != null) RotateOutputPath.Text = path;
        }

        private async void RotateExecute_Click(object sender, RoutedEventArgs e)
        {
            string src = RotateSourcePath.Text.Trim();
            string dst = RotateOutputPath.Text.Trim();

            if (!ValidateSource(src)) return;
            if (_rotatePageCount == 0) { ShowError(RotateStatus, RotateStatusText, App.S("Err_NoSourcePdf")); return; }
            if (!ValidateOutput(dst)) return;

            int degrees = Rotate90.IsChecked == true ? 90
                        : Rotate180.IsChecked == true ? 180 : 270;

            System.Collections.Generic.List<int> pages = null;
            bool allPages = RotateAllPagesChk.IsChecked == true;
            if (!allPages)
            {
                string rangeStr = RotatePages.Text.Trim();
                if (string.IsNullOrEmpty(rangeStr)) { ShowError(RotateStatus, RotateStatusText, App.S("Err_EnterPagesOrAll")); return; }
                try { pages = PdfService.ParsePageRange(rangeStr, _rotatePageCount); }
                catch (Exception ex) { ShowError(RotateStatus, RotateStatusText, ex.Message); return; }
            }

            SetBusy(RotateBtn, true);
            int pageCount = pages?.Count ?? _rotatePageCount;
            try
            {
                await Task.Run(() => _pdf.RotatePages(src, pages, degrees, dst));
                ShowSuccess(RotateStatus, RotateStatusText,
                    string.Format(App.S("Ok_Rotated"), pageCount, degrees, Path.GetFileName(dst)));
            }
            catch (Exception ex) { ShowError(RotateStatus, RotateStatusText, $"Rotation failed: {ex.Message}"); }
            finally { SetBusy(RotateBtn, false); }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static string BrowsePdf()
        {
            var dlg = new OpenFileDialog { Title = App.S("Dlg_SelectPdf"), Filter = "PDF Files|*.pdf" };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        private static string SavePdf(string title)
        {
            var dlg = new SaveFileDialog { Title = title, Filter = "PDF Files|*.pdf", DefaultExt = ".pdf" };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        private bool ValidateSource(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) return true;
            // Show error on whichever panel is visible
            var (status, text) = ActiveStatusControls();
            ShowError(status, text, App.S("Err_NoSourcePdf"));
            return false;
        }

        private bool ValidateOutput(string path)
        {
            if (!string.IsNullOrEmpty(path)) return true;
            var (status, text) = ActiveStatusControls();
            ShowError(status, text, App.S("Err_NoOutput"));
            return false;
        }

        private (Border, TextBlock) ActiveStatusControls()
        {
            if (SplitPanel.Visibility     == Visibility.Visible) return (SplitStatus,     SplitStatusText);
            if (MergePanel.Visibility     == Visibility.Visible) return (MergeStatus,     MergeStatusText);
            if (RotatePanel.Visibility    == Visibility.Visible) return (RotateStatus,    RotateStatusText);
            if (OrganizePanel.Visibility  == Visibility.Visible) return (OrganizeStatus,  OrganizeStatusText);
            if (WatermarkPanel.Visibility == Visibility.Visible) return (WatermarkStatus, WatermarkStatusText);
            if (MetadataPanel.Visibility  == Visibility.Visible) return (MetadataStatus,  MetadataStatusText);
            if (SecurityPanel.Visibility  == Visibility.Visible) return (SecurityStatus,  SecurityStatusText);
            return (ExtractStatus, ExtractStatusText);
        }

        private static void ShowSuccess(Border border, TextBlock label, string message)
        {
            border.Background   = new SolidColorBrush(Color.FromRgb(0xEA, 0xFA, 0xF1));
            border.BorderBrush  = new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71));
            border.BorderThickness = new Thickness(1);
            label.Foreground    = new SolidColorBrush(Color.FromRgb(0x1E, 0x84, 0x49));
            label.Text          = "✓  " + message;
            border.Visibility   = Visibility.Visible;
        }

        private static void ShowError(Border border, TextBlock label, string message)
        {
            border.Background   = new SolidColorBrush(Color.FromRgb(0xFD, 0xED, 0xEC));
            border.BorderBrush  = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
            border.BorderThickness = new Thickness(1);
            label.Foreground    = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B));
            label.Text          = "✗  " + message;
            border.Visibility   = Visibility.Visible;
        }

        private static void SetBusy(Button btn, bool busy)
        {
            btn.IsEnabled = !busy;
            if (busy)
            {
                var original = btn.Tag as string ?? btn.Content as string;
                if (btn.Tag == null) btn.Tag = original;
                btn.Content = "Processing...";
            }
            else
            {
                if (btn.Tag is string original)
                    btn.Content = original;
                btn.Tag = null;
            }
        }

        // ── Organize ─────────────────────────────────────────────────────

        private void OrganizeBrowseSource_Click(object sender, RoutedEventArgs e)
        {
            string path = BrowsePdf();
            if (path == null) return;
            OrganizeSourcePath.Text = path;
            try
            {
                var details = _pdf.GetPageDetails(path);
                _organizePages.Clear();
                for (int i = 0; i < details.Count; i++)
                {
                    var d = details[i];
                    string sz = (int)d.WidthPt + " x " + (int)d.HeightPt + " pt"
                               + (d.Rotate != 0 ? "  (R" + d.Rotate + ")" : string.Empty);
                    _organizePages.Add(new PageItem
                    {
                        SourceFile      = path,
                        SourcePageIndex = i,
                        DisplayNumber   = i + 1,
                        PageLabel       = "Page " + (i + 1),
                        SizeInfo        = sz
                    });
                }
                OrganizePageInfo.Text = string.Format(App.S("Rt_PagesLoaded"), details.Count);
                RefreshOrganizeIndices();
            }
            catch (Exception ex)
            {
                ShowError(OrganizeStatus, OrganizeStatusText, "Failed to load PDF: " + ex.Message);
                OrganizePageInfo.Text = string.Empty;
            }
        }

        private void OrganizePageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateOrganizeButtonStates();
        }

        private void UpdateOrganizeButtonStates()
        {
            int idx           = OrganizePageList.SelectedIndex;
            int selectedCount = OrganizePageList.SelectedItems.Count;
            OrganizeDeleteBtn.IsEnabled  = selectedCount > 0;
            OrganizeMoveUp.IsEnabled     = selectedCount == 1 && idx > 0;
            OrganizeMoveDown.IsEnabled   = selectedCount == 1 && idx < _organizePages.Count - 1;
        }

        private void OrganizeDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = OrganizePageList.SelectedItems.Cast<PageItem>().ToList();
            foreach (var item in selected)
                _organizePages.Remove(item);
            RefreshOrganizeIndices();
        }

        private void OrganizeMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int idx = OrganizePageList.SelectedIndex;
            if (idx <= 0) return;
            _organizePages.Move(idx, idx - 1);
            OrganizePageList.SelectedIndex = idx - 1;
            RefreshOrganizeIndices();
        }

        private void OrganizeMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int idx = OrganizePageList.SelectedIndex;
            if (idx < 0 || idx >= _organizePages.Count - 1) return;
            _organizePages.Move(idx, idx + 1);
            OrganizePageList.SelectedIndex = idx + 1;
            RefreshOrganizeIndices();
        }

        private void OrganizeInsertToggle_Click(object sender, RoutedEventArgs e)
        {
            InsertSubPanel.Visibility = InsertSubPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void InsertBrowseSource_Click(object sender, RoutedEventArgs e)
        {
            string path = BrowsePdf();
            if (path == null) return;
            InsertSourcePath.Text = path;
            try
            {
                int count = _pdf.GetPageCount(path);
                InsertSourceInfo.Text = count + " pages";
            }
            catch { InsertSourceInfo.Text = string.Empty; }
        }

        private void InsertAddToList_Click(object sender, RoutedEventArgs e)
        {
            string srcPath = InsertSourcePath.Text.Trim();
            if (string.IsNullOrEmpty(srcPath) || !File.Exists(srcPath))
            {
                ShowError(OrganizeStatus, OrganizeStatusText, App.S("Err_NoInsertSource"));
                return;
            }

            List<int> pages;
            try
            {
                int totalPages = _pdf.GetPageCount(srcPath);
                string rangeStr = InsertPageRange.Text.Trim();
                pages = string.IsNullOrEmpty(rangeStr)
                    ? Enumerable.Range(1, totalPages).ToList()
                    : PdfService.ParsePageRange(rangeStr, totalPages);
            }
            catch (Exception ex)
            {
                ShowError(OrganizeStatus, OrganizeStatusText, ex.Message);
                return;
            }

            // Determine insert position
            int insertAfter = _organizePages.Count;
            string posText  = InsertPosition.Text.Trim();
            if (!string.IsNullOrEmpty(posText))
            {
                if (int.TryParse(posText, out int pos))
                    insertAfter = Math.Max(0, Math.Min(pos, _organizePages.Count));
                else
                {
                    ShowError(OrganizeStatus, OrganizeStatusText, "Insert position must be a whole number.");
                    return;
                }
            }

            List<PageDetail> details;
            try { details = _pdf.GetPageDetails(srcPath); }
            catch (Exception ex)
            {
                ShowError(OrganizeStatus, OrganizeStatusText, "Failed to read source PDF: " + ex.Message);
                return;
            }

            int idx = insertAfter;
            foreach (int pageNum in pages)
            {
                int srcIdx = pageNum - 1;
                var d      = details[srcIdx];
                string sz  = (int)d.WidthPt + " x " + (int)d.HeightPt + " pt"
                            + (d.Rotate != 0 ? "  (R" + d.Rotate + ")" : string.Empty);
                _organizePages.Insert(idx++, new PageItem
                {
                    SourceFile      = srcPath,
                    SourcePageIndex = srcIdx,
                    DisplayNumber   = 0,
                    PageLabel       = "Page " + pageNum,
                    SizeInfo        = sz
                });
            }
            RefreshOrganizeIndices();
            InsertSubPanel.Visibility = Visibility.Collapsed;
            OrganizeStatus.Visibility = Visibility.Collapsed;
        }

        private void OrganizeBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            string path = SavePdf("Save Organized Document");
            if (path != null) OrganizeOutputPath.Text = path;
        }

        private async void OrganizeExecute_Click(object sender, RoutedEventArgs e)
        {
            if (_organizePages.Count == 0)
            {
                ShowError(OrganizeStatus, OrganizeStatusText, App.S("Err_NoPages"));
                return;
            }
            string dst = OrganizeOutputPath.Text.Trim();
            if (!ValidateOutput(dst)) return;

            var sourcePages = _organizePages
                .Select(p => new KeyValuePair<string, int>(p.SourceFile, p.SourcePageIndex))
                .ToList();
            int count = _organizePages.Count;

            SetBusy(OrganizeBtn, true);
            try
            {
                await Task.Run(() => _pdf.BuildDocument(sourcePages, dst));
                ShowSuccess(OrganizeStatus, OrganizeStatusText,
                    string.Format(App.S("Ok_Saved"), count, Path.GetFileName(dst)));
            }
            catch (Exception ex)
            {
                ShowError(OrganizeStatus, OrganizeStatusText, "Failed: " + ex.Message);
            }
            finally { SetBusy(OrganizeBtn, false); }
        }

        private void RefreshOrganizeIndices()
        {
            for (int i = 0; i < _organizePages.Count; i++)
                _organizePages[i].DisplayNumber = i + 1;
            OrganizePageList.Items.Refresh();
            UpdateOrganizeButtonStates();
        }

        private void RefreshOrganizeEmptyState()
        {
            OrganizeEmptyState.Visibility = _organizePages.Count == 0
                ? Visibility.Visible : Visibility.Hidden;
        }

        // ── Security ────────────────────────────────────────────────

        private void SecTabEncrypt_Checked(object sender, RoutedEventArgs e)
        {
            if (EncryptSubPanel == null) return;
            EncryptSubPanel.Visibility    = Visibility.Visible;
            RemovePassSubPanel.Visibility = Visibility.Collapsed;
        }

        private void SecTabRemove_Checked(object sender, RoutedEventArgs e)
        {
            if (RemovePassSubPanel == null) return;
            EncryptSubPanel.Visibility    = Visibility.Collapsed;
            RemovePassSubPanel.Visibility = Visibility.Visible;
        }

        // Encrypt tab
        private void EncBrowseSource_Click(object sender, RoutedEventArgs e)
        {
            string path = BrowsePdf();
            if (path == null) return;
            EncSourcePath.Text = path;
            try
            {
                int count = _pdf.GetPageCount(path);
                EncPageInfo.Text = string.Format(App.S("Rt_PagesDetected"), count);
            }
            catch
            {
                EncPageInfo.Text = App.S("Rt_PasswordProtected");
            }
        }

        private void EncBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            string path = SavePdf("Encrypt PDF");
            if (path != null) EncOutputPath.Text = path;
        }

        private async void EncExecute_Click(object sender, RoutedEventArgs e)
        {
            string src      = EncSourcePath.Text.Trim();
            string dst      = EncOutputPath.Text.Trim();
            string srcPwd   = EncSourcePwd.Password;
            string userPwd  = EncUserPwd.Password;
            string userPwd2 = EncUserPwdConfirm.Password;
            string ownerPwd = EncOwnerPwd.Password;

            if (!ValidateSource(src)) return;
            if (string.IsNullOrEmpty(userPwd))
            {
                ShowError(SecurityStatus, SecurityStatusText, App.S("Err_UserPwdRequired"));
                return;
            }
            if (userPwd != userPwd2)
            {
                ShowError(SecurityStatus, SecurityStatusText, App.S("Err_PwdMismatch"));
                return;
            }
            if (!ValidateOutput(dst)) return;

            bool permitPrint  = EncPermitPrint.IsChecked  == true;
            bool permitCopy   = EncPermitCopy.IsChecked   == true;
            bool permitModify = EncPermitModify.IsChecked == true;

            SetBusy(EncBtn, true);
            try
            {
                await Task.Run(() => _pdf.EncryptPdf(src, srcPwd, userPwd, ownerPwd,
                                                     permitPrint, permitCopy, permitModify, dst));
                ShowSuccess(SecurityStatus, SecurityStatusText,
                    string.Format(App.S("Ok_Encrypted"), Path.GetFileName(dst)));
            }
            catch (Exception ex)
            {
                ShowError(SecurityStatus, SecurityStatusText, "Encryption failed: " + ex.Message);
            }
            finally { SetBusy(EncBtn, false); }
        }

        // Remove password tab
        private void RemBrowseSource_Click(object sender, RoutedEventArgs e)
        {
            string path = BrowsePdf();
            if (path == null) return;
            RemSourcePath.Text = path;
            RemPageInfo.Text   = App.S("Rt_EnterPwdToRemove");
        }

        private void RemBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            string path = SavePdf(App.S("Dlg_SavePdf"));
            if (path != null) RemOutputPath.Text = path;
        }

        private async void RemExecute_Click(object sender, RoutedEventArgs e)
        {
            string src = RemSourcePath.Text.Trim();
            string dst = RemOutputPath.Text.Trim();
            string pwd = RemPwd.Password;

            if (!ValidateSource(src)) return;
            if (!ValidateOutput(dst)) return;

            SetBusy(RemBtn, true);
            try
            {
                await Task.Run(() => _pdf.RemovePassword(src, pwd, dst));
                ShowSuccess(SecurityStatus, SecurityStatusText,
                    string.Format(App.S("Ok_PwdRemoved"), Path.GetFileName(dst)));
            }
            catch (Exception ex)
            {
                ShowError(SecurityStatus, SecurityStatusText,
                    string.Format(App.S("Err_RemovePwdFailed"), ex.Message));
            }
            finally { SetBusy(RemBtn, false); }
        }

        // ── Watermark ─────────────────────────────────────────────────────

        private void WmBrowseSource_Click(object sender, RoutedEventArgs e)
        {
            string path = BrowsePdf();
            if (path == null) return;
            WmSourcePath.Text = path;
            try
            {
                _watermarkPageCount = _pdf.GetPageCount(path);
                WmPageInfo.Text     = string.Format(App.S("Rt_PagesDetected"), _watermarkPageCount);
            }
            catch
            {
                WmPageInfo.Text     = App.S("Rt_UnableToRead");
                _watermarkPageCount = 0;
            }
        }

        private void WmBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            string path = SavePdf(App.S("Dlg_SavePdf"));
            if (path != null) WmOutputPath.Text = path;
        }

        private void WmOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (WmOpacityLabel != null)
                WmOpacityLabel.Text = (int)e.NewValue + "%";
        }

        private void WmAllPages_Checked(object sender, RoutedEventArgs e)
        {
            if (WmPageRange != null) WmPageRange.IsEnabled = false;
        }

        private void WmAllPages_Unchecked(object sender, RoutedEventArgs e)
        {
            if (WmPageRange != null) WmPageRange.IsEnabled = true;
        }

        private async void WmExecute_Click(object sender, RoutedEventArgs e)
        {
            string src = WmSourcePath.Text.Trim();
            string dst = WmOutputPath.Text.Trim();

            if (!ValidateSource(src)) return;
            if (_watermarkPageCount == 0) { ShowError(WatermarkStatus, WatermarkStatusText, App.S("Err_NoSourcePdf")); return; }
            if (string.IsNullOrWhiteSpace(WmText.Text)) { ShowError(WatermarkStatus, WatermarkStatusText, App.S("Err_WmTextEmpty")); return; }
            if (!ValidateOutput(dst)) return;

            WatermarkOptions opts;
            try { opts = BuildWatermarkOptions(); }
            catch (Exception ex) { ShowError(WatermarkStatus, WatermarkStatusText, ex.Message); return; }

            SetBusy(WmBtn, true);
            int pageCount = opts.PageNumbers?.Count ?? _watermarkPageCount;
            try
            {
                await Task.Run(() => _pdf.AddTextWatermark(src, opts, dst));
                ShowSuccess(WatermarkStatus, WatermarkStatusText,
                    "Watermark applied to " + pageCount + " page(s) -> " + Path.GetFileName(dst));
            }
            catch (Exception ex) { ShowError(WatermarkStatus, WatermarkStatusText, "Failed: " + ex.Message); }
            finally { SetBusy(WmBtn, false); }
        }

        private WatermarkOptions BuildWatermarkOptions()
        {
            var opts = new WatermarkOptions();

            opts.Text     = WmText.Text.Trim();
            opts.FontBold = WmBoldChk.IsChecked == true;

            if (!double.TryParse(WmFontSize.Text, out double fs) || fs < 4 || fs > 600)
                throw new ArgumentException("Font size must be a number between 4 and 600.");
            opts.FontSize = fs;

            opts.Opacity = WmOpacity.Value / 100.0;

            // Color
            if      (WmColorDarkGray.IsChecked == true) { opts.ColorR = 64;  opts.ColorG = 64;  opts.ColorB = 64;  }
            else if (WmColorBlack.IsChecked    == true) { opts.ColorR = 17;  opts.ColorG = 17;  opts.ColorB = 17;  }
            else if (WmColorRed.IsChecked      == true) { opts.ColorR = 231; opts.ColorG = 76;  opts.ColorB = 60;  }
            else if (WmColorOrange.IsChecked   == true) { opts.ColorR = 230; opts.ColorG = 126; opts.ColorB = 34;  }
            else if (WmColorYellow.IsChecked   == true) { opts.ColorR = 241; opts.ColorG = 196; opts.ColorB = 15;  }
            else if (WmColorGreen.IsChecked    == true) { opts.ColorR = 39;  opts.ColorG = 174; opts.ColorB = 96;  }
            else if (WmColorTeal.IsChecked     == true) { opts.ColorR = 22;  opts.ColorG = 160; opts.ColorB = 133; }
            else if (WmColorBlue.IsChecked     == true) { opts.ColorR = 41;  opts.ColorG = 128; opts.ColorB = 185; }
            else if (WmColorNavy.IsChecked     == true) { opts.ColorR = 26;  opts.ColorG = 58;  opts.ColorB = 107; }
            else if (WmColorPurple.IsChecked   == true) { opts.ColorR = 142; opts.ColorG = 68;  opts.ColorB = 173; }
            else if (WmColorPink.IsChecked     == true) { opts.ColorR = 233; opts.ColorG = 30;  opts.ColorB = 140; }
            else                                         { opts.ColorR = 160; opts.ColorG = 160; opts.ColorB = 160; } // Light Gray

            // Position
            if      (WmPos_TL.IsChecked == true) opts.Position = WatermarkPosition.TopLeft;
            else if (WmPos_TC.IsChecked == true) opts.Position = WatermarkPosition.TopCenter;
            else if (WmPos_TR.IsChecked == true) opts.Position = WatermarkPosition.TopRight;
            else if (WmPos_ML.IsChecked == true) opts.Position = WatermarkPosition.MiddleLeft;
            else if (WmPos_MR.IsChecked == true) opts.Position = WatermarkPosition.MiddleRight;
            else if (WmPos_BL.IsChecked == true) opts.Position = WatermarkPosition.BottomLeft;
            else if (WmPos_BC.IsChecked == true) opts.Position = WatermarkPosition.BottomCenter;
            else if (WmPos_BR.IsChecked == true) opts.Position = WatermarkPosition.BottomRight;
            else                                  opts.Position = WatermarkPosition.Center;

            // Rotation
            if      (WmRot0.IsChecked  == true) opts.Rotation =   0;
            else if (WmRot45.IsChecked == true) opts.Rotation =  45;
            else if (WmRot90.IsChecked == true) opts.Rotation =  90;
            else                                 opts.Rotation = -45;

            // Page range
            if (WmAllPagesChk.IsChecked != true)
            {
                string rangeStr = WmPageRange.Text.Trim();
                if (string.IsNullOrEmpty(rangeStr))
                    throw new ArgumentException(App.S("Err_EnterPagesOrAllWm"));
                opts.PageNumbers = PdfService.ParsePageRange(rangeStr, _watermarkPageCount);
            }

            return opts;
        }

        // ── Split ────────────────────────────────────────────────

        private void SplitBrowseSource_Click(object sender, RoutedEventArgs e)
        {
            string path = BrowsePdf();
            if (path == null) return;
            SplitSourcePath.Text = path;
            SplitPrefix.Text     = Path.GetFileNameWithoutExtension(path);
            try
            {
                _splitPageCount      = _pdf.GetPageCount(path);
                SplitPageInfo.Text   = string.Format(App.S("Rt_PagesDetected"), _splitPageCount);
            }
            catch
            {
                SplitPageInfo.Text   = App.S("Rt_UnableToRead");
                _splitPageCount      = 0;
            }
        }

        private void SplitBrowseOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = App.S("Dlg_SelectFolder"),
                ShowNewFolderButton = true
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                SplitOutputFolder.Text = dialog.SelectedPath;
        }

        private void SplitTabCount_Checked(object sender, RoutedEventArgs e)
        {
            if (SplitCountPanel == null) return;
            SplitCountPanel.Visibility  = Visibility.Visible;
            SplitRangesPanel.Visibility = Visibility.Collapsed;
        }

        private void SplitTabRanges_Checked(object sender, RoutedEventArgs e)
        {
            if (SplitRangesPanel == null) return;
            SplitCountPanel.Visibility  = Visibility.Collapsed;
            SplitRangesPanel.Visibility = Visibility.Visible;
        }

        private async void SplitExecute_Click(object sender, RoutedEventArgs e)
        {
            string src    = SplitSourcePath.Text.Trim();
            string folder = SplitOutputFolder.Text.Trim();
            string prefix = SplitPrefix.Text.Trim();

            if (!ValidateSource(src)) return;
            if (_splitPageCount == 0)
            { ShowError(SplitStatus, SplitStatusText, App.S("Err_NoSourcePdf")); return; }
            if (string.IsNullOrWhiteSpace(folder))
            { ShowError(SplitStatus, SplitStatusText, App.S("Err_NoOutputFolder")); return; }
            if (string.IsNullOrWhiteSpace(prefix))
            { ShowError(SplitStatus, SplitStatusText, App.S("Err_NoPrefix")); return; }

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            List<string> outputFiles;
            SetBusy(SplitBtn, true);
            try
            {
                if (SplitTabCount.IsChecked == true)
                {
                    if (!int.TryParse(SplitPagesPerFile.Text, out int ppf) || ppf < 1)
                    { ShowError(SplitStatus, SplitStatusText, App.S("Err_NoPagesPerFile")); return; }
                    outputFiles = await Task.Run(() => _pdf.SplitPdfByCount(src, ppf, folder, prefix));
                }
                else
                {
                    string ranges = SplitRangesInput.Text.Trim();
                    if (string.IsNullOrEmpty(ranges))
                    { ShowError(SplitStatus, SplitStatusText, App.S("Err_NoRanges")); return; }
                    outputFiles = await Task.Run(() => _pdf.SplitPdfByRanges(src, ranges, folder, prefix));
                }
                ShowSuccess(SplitStatus, SplitStatusText,
                    outputFiles.Count + " file(s) saved to: " + folder);
            }
            catch (Exception ex) { ShowError(SplitStatus, SplitStatusText, "Failed: " + ex.Message); }
            finally { SetBusy(SplitBtn, false); }
        }

        // ── Metadata ─────────────────────────────────────────────

        private void MetaBrowseSource_Click(object sender, RoutedEventArgs e)
        {
            string path = BrowsePdf();
            if (path == null) return;
            MetaSourcePath.Text = path;
            MetaPageInfo.Text   = App.S("Rt_ClickLoadMeta");
        }

        private void MetaLoad_Click(object sender, RoutedEventArgs e)
        {
            string src = MetaSourcePath.Text.Trim();
            if (!ValidateSource(src)) return;
            try
            {
                var meta  = _pdf.GetPdfMetadata(src);
                int count = _pdf.GetPageCount(src);
                MetaPageInfo.Text     = string.Format(App.S("Rt_PagesDetected"), count);
                MetaTitle.Text        = meta.Title;
                MetaAuthor.Text       = meta.Author;
                MetaSubject.Text      = meta.Subject;
                MetaKeywords.Text     = meta.Keywords;
                MetaCreator.Text      = meta.Creator;
                MetaProducer.Text     = meta.Producer;
                MetaCreationDate.Text = meta.CreationDate;
                // Auto-suggest output alongside source
                if (string.IsNullOrEmpty(MetaOutputPath.Text) ||
                    MetaOutputPath.Text.StartsWith("Click"))
                {
                    string dir  = Path.GetDirectoryName(src);
                    string name = Path.GetFileNameWithoutExtension(src) + "_metadata.pdf";
                    MetaOutputPath.Text = Path.Combine(dir, name);
                }
                MetadataStatus.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ShowError(MetadataStatus, MetadataStatusText, "Failed to load: " + ex.Message);
            }
        }

        private void MetaBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            string path = SavePdf("Save Metadata");
            if (path != null) MetaOutputPath.Text = path;
        }

        private async void MetaExecute_Click(object sender, RoutedEventArgs e)
        {
            string src = MetaSourcePath.Text.Trim();
            string dst = MetaOutputPath.Text.Trim();
            if (!ValidateSource(src)) return;
            if (!ValidateOutput(dst)) return;

            var meta = new PdfMetadata
            {
                Title    = MetaTitle.Text,
                Author   = MetaAuthor.Text,
                Subject  = MetaSubject.Text,
                Keywords = MetaKeywords.Text,
                Creator  = MetaCreator.Text
            };

            SetBusy(MetaBtn, true);
            try
            {
                await Task.Run(() => _pdf.SetPdfMetadata(src, meta, dst));
                ShowSuccess(MetadataStatus, MetadataStatusText,
                    "Metadata saved -> " + Path.GetFileName(dst));
            }
            catch (Exception ex) { ShowError(MetadataStatus, MetadataStatusText, "Failed: " + ex.Message); }
            finally { SetBusy(MetaBtn, false); }
        }

#if ENABLE_ACRYLIC
        #region DWM Acrylic Backdrop (Windows 11 22H2+)

        public enum DWM_SYSTEMBACKDROP_TYPE
        {
            DWMSBT_AUTO            = 0,
            DWMSBT_NONE            = 1,
            DWMSBT_MAINWINDOW      = 2,  // Mica
            DWMSBT_TRANSIENTWINDOW = 3,  // Acrylic
            DWMSBT_TABBEDWINDOW    = 4   // Mica Alt
        }

        private static class DwmApi
        {
            public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
            public const int DWMWA_CAPTION_COLOR       = 35;  // Win11 Build 22000+

            [DllImport("dwmapi.dll", PreserveSig = true)]
            public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
        }

        private static class NonClientRegionAPI
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct MARGINS
            {
                public int cxLeftWidth;
                public int cxRightWidth;
                public int cyTopHeight;
                public int cyBottomHeight;
            }

            [DllImport("dwmapi.dll")]
            public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);
        }

        private static bool IsWindowsVersionSupported(int minBuild = 22621)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    var buildStr = key?.GetValue("CurrentBuild") as string;
                    return int.TryParse(buildStr, out int buildNum) && buildNum >= minBuild;
                }
            }
            catch { return false; }
        }

        private void ApplyAcrylicBackdrop()
        {
            if (!IsWindowsVersionSupported())
                return;

            this.Background = Brushes.Transparent;

            var hwnd = new WindowInteropHelper(this).Handle;

            int backdropType = (int)DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW;
            DwmApi.DwmSetWindowAttribute(hwnd, DwmApi.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, Marshal.SizeOf(typeof(int)));

            // Give the title bar a solid colour so it is not acrylic-transparent.
            // COLORREF is 0x00BBGGRR; #F3F3F3 → R=G=B=0xF3, so value is 0x00F3F3F3.
            int captionColor = 0x00F3F3F3;
            DwmApi.DwmSetWindowAttribute(hwnd, DwmApi.DWMWA_CAPTION_COLOR, ref captionColor, Marshal.SizeOf(typeof(int)));

            HwndSource src = HwndSource.FromHwnd(hwnd);
            src.CompositionTarget.BackgroundColor = Color.FromArgb(0, 0, 0, 0);

            var margins = new NonClientRegionAPI.MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            NonClientRegionAPI.DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyAcrylicBackdrop();
        }

        #endregion
#endif // ENABLE_ACRYLIC
    }
}
