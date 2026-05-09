using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PdfKit
{
    public partial class StatusBanner : UserControl
    {
        public StatusBanner()
        {
            InitializeComponent();
        }

        public void ShowSuccess(string msg)
        {
            Root.Background   = new SolidColorBrush(Color.FromRgb(0xEA, 0xFA, 0xF1));
            Root.BorderBrush  = new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71));
            TxtMsg.Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x84, 0x49));
            TxtMsg.Text       = "\u2713  " + msg;
            Visibility        = Visibility.Visible;
        }

        public void ShowError(string msg)
        {
            Root.Background   = new SolidColorBrush(Color.FromRgb(0xFD, 0xED, 0xEC));
            Root.BorderBrush  = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
            TxtMsg.Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B));
            TxtMsg.Text       = "\u2717  " + msg;
            Visibility        = Visibility.Visible;
        }

        public void Dismiss() => Visibility = Visibility.Collapsed;

        private void Dismiss_Click(object sender, RoutedEventArgs e) => Dismiss();
    }
}
