using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WorldClock.ViewModels;

namespace WorldClock.Views
{
    public partial class AddTimezoneDialog : Window
    {
        private readonly MainViewModel _mainViewModel;
        public string SelectedTimezoneId { get; private set; }
        private AddTimezoneDialogViewModel _vm;

        public AddTimezoneDialog(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            _vm = new AddTimezoneDialogViewModel();
            DataContext = _vm;
            InitializeComponent();
            SearchBox.Focus();
        }

        #region Win11 Acrylic
        private static class DwmApi
        {
            public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
            public const int DWMSBT_TRANSIENTWINDOW   = 3;
            [DllImport("dwmapi.dll", PreserveSig = true)]
            public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        }
        private static class NonClientRegionAPI
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct MARGINS { public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight; }
            [DllImport("dwmapi.dll")]
            public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS m);
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int t = DwmApi.DWMSBT_TRANSIENTWINDOW;
            DwmApi.DwmSetWindowAttribute(hwnd, DwmApi.DWMWA_SYSTEMBACKDROP_TYPE, ref t, Marshal.SizeOf<int>());
            var m = new NonClientRegionAPI.MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            NonClientRegionAPI.DwmExtendFrameIntoClientArea(hwnd, ref m);
            var src = HwndSource.FromHwnd(hwnd);
            if (src?.CompositionTarget != null)
                src.CompositionTarget.BackgroundColor = Colors.Transparent;
        }
        #endregion

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            CommitSelection();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void TimezoneList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            CommitSelection();
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            _vm.SearchText = string.Empty;
            SearchBox.Focus();
        }

        private void CommitSelection()
        {
            if (_vm.SelectedTimezone == null)
            {
                MessageBox.Show("Please select a timezone.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var id = _vm.SelectedTimezone.Id;
            bool added = _mainViewModel.AddTimezoneById(id);
            if (!added)
            {
                MessageBox.Show($"'{_vm.SelectedTimezone.DisplayName}' is already in your clock list.",
                    "Already Added", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SelectedTimezoneId = id;
            DialogResult = true;
        }
    }
}
