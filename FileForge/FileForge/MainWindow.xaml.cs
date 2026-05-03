using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using FileForge.Views;

namespace FileForge
{
    public partial class MainWindow : Window
    {
        // Lazy<T> ensures each view is created at most once, on first access.
        private readonly Dictionary<RadioButton, Lazy<UserControl>> _views =
            new Dictionary<RadioButton, Lazy<UserControl>>();

        public MainWindow()
        {
            InitializeComponent();
            InitViews();
            SetVersion();
        }

        private void InitViews()
        {
            _views[navAppend]    = new Lazy<UserControl>(() => new AppendView());
            _views[navSplit]     = new Lazy<UserControl>(() => new SplitView());
            _views[navMerge]     = new Lazy<UserControl>(() => new MergeView());
            _views[navRegion]    = new Lazy<UserControl>(() => new RegionView());
            _views[navAlign]     = new Lazy<UserControl>(() => new AlignView());
            _views[navTimestamp] = new Lazy<UserControl>(() => new TimestampView());
            _views[navHash]      = new Lazy<UserControl>(() => new HashView());
            _views[navSearch]    = new Lazy<UserControl>(() => new SearchView());
            _views[navHex]       = new Lazy<UserControl>(() => new HexView());
            _views[navDiff]      = new Lazy<UserControl>(() => new DiffView());
            _views[navPatch]     = new Lazy<UserControl>(() => new PatchView());

            // Load the default (Append) view immediately to avoid a blank content area.
            contentArea.Content = _views[navAppend].Value;
        }

        private void SetVersion()
        {
            var ver = System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            txtVersion.Text = $"v{ver.FileMajorPart}.{ver.FileMinorPart}.{ver.FileBuildPart}.{ver.FilePrivatePart}";
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton btn && _views.TryGetValue(btn, out var lazy))
            {
                contentArea.Content = lazy.Value;
                Title = $"FileForge - {btn.Content}";
            }
        }
    }
}

