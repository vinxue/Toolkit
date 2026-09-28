using System.Windows;

namespace Nexus
{
    /// <summary>
    /// A WinUI ContentDialog look-alike that follows the Fluent theme, unlike the
    /// Win32 MessageBox. It is a separate window because WebView2 would paint over
    /// any in-window overlay.
    /// </summary>
    public partial class ContentDialogWindow : Window
    {
        private ContentDialogWindow(Window? owner, string title, string message, string? primaryText, string closeText)
        {
            InitializeComponent();
            Owner = owner;
            TitleText.Text = title;
            MessageText.Text = message;
            CloseButton.Content = closeText;

            if (primaryText is null)
            {
                PrimaryButton.Visibility = Visibility.Collapsed;
                CloseButton.IsDefault = true;
            }
            else
            {
                PrimaryButton.Content = primaryText;
            }
        }

        public static bool Confirm(Window? owner, string title, string message, string primaryText, string closeText = "Cancel") =>
            new ContentDialogWindow(owner, title, message, primaryText, closeText).ShowDialog() == true;

        public static void ShowMessage(Window? owner, string title, string message, string closeText = "OK") =>
            new ContentDialogWindow(owner, title, message, null, closeText).ShowDialog();

        private void PrimaryButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    }
}
