using System.ComponentModel;
using System.Windows;

namespace Memo;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModel.IsSaving)
        {
            MessageBox.Show(this, "Saving the recording. Please wait before closing.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            e.Cancel = true;
            return;
        }

        if (_viewModel.IsActive &&
            MessageBox.Show(this, "A recording is in progress. Exiting stops it and keeps it as a WAV file. Exit anyway?", Title,
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
