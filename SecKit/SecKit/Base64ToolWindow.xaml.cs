using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace SecKit
{
    public partial class Base64ToolWindow : UserControl
    {
        private const int MaximumInputCharacters = 1024 * 1024;
        private byte[]? resultBytes;
        private bool resultIsEncoded;
        private CancellationTokenSource? cancellation;
        private Window? owner;
        private bool closeAfterCancel;

        public Base64ToolWindow()
        {
            InitializeComponent();
        }

        private bool IsFileMode => FileMode.IsChecked == true;
        private bool IsUrlSafe => Variant.SelectedIndex == 1;
        private bool IsHex => DataFormat.SelectedIndex == 1;

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (owner == null)
            {
                owner = Window.GetWindow(this);
                if (owner != null)
                    owner.Closing += Owner_Closing;
            }
        }

        private void Owner_Closing(object? sender, CancelEventArgs e)
        {
            if (cancellation == null)
                return;
            e.Cancel = true;
            closeAfterCancel = true;
            cancellation.Cancel();
        }

        private void Mode_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
                return;
            TextPanel.Visibility = IsFileMode ? Visibility.Collapsed : Visibility.Visible;
            FilePanel.Visibility = IsFileMode ? Visibility.Visible : Visibility.Collapsed;
            CopyButton.Visibility = SaveButton.Visibility = IsFileMode ? Visibility.Collapsed : Visibility.Visible;
            InvalidateResult();
        }

        private void Options_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsInitialized)
                InvalidateResult();
        }

        private void Input_Changed(object sender, TextChangedEventArgs e)
        {
            if (IsInitialized)
                InvalidateResult();
        }

        private void InvalidateResult()
        {
            resultBytes = null;
            OutputText.Clear();
            OutputLabel.Text = "Output";
            SavedPathText.Text = string.Empty;
            bool tooLarge = !IsFileMode && InputText.Text.Length > MaximumInputCharacters;
            SetStatus(tooLarge ? "Text input exceeds 1 Mi characters. Use File mode for larger data." : "Ready", tooLarge);
            UpdateActions();
        }

        private void UpdateActions()
        {
            bool busy = cancellation != null;
            SettingsBar.IsEnabled = TextPanel.IsEnabled = FilePanel.IsEnabled = ClearButton.IsEnabled = !busy;
            EncodeButton.IsEnabled = DecodeButton.IsEnabled = !busy
                && (IsFileMode ? File.Exists(FilePathText.Text) : InputText.Text.Length <= MaximumInputCharacters);
            CopyButton.IsEnabled = SaveButton.IsEnabled = !busy && !IsFileMode && resultBytes != null;
            CancelButton.Visibility = FileProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            CommandManager.InvalidateRequerySuggested();
        }

        private void SetStatus(string message, bool error = false)
        {
            StatusText.Text = message;
            StatusText.Foreground = error ? Brushes.Firebrick : Brushes.DimGray;
        }

        private async void Encode_Click(object sender, RoutedEventArgs e) => await ConvertAsync(true);
        private async void Decode_Click(object sender, RoutedEventArgs e) => await ConvertAsync(false);

        private async Task ConvertAsync(bool encode)
        {
            if (cancellation != null)
                return;
            if (IsFileMode)
            {
                await ConvertFileAsync(encode);
                return;
            }

            InvalidateResult();
            if (InputText.Text.Length > MaximumInputCharacters)
                return;
            try
            {
                if (encode)
                {
                    byte[] input = Base64Converter.ParseInput(InputText.Text, IsHex);
                    string output = Base64Converter.Encode(input, IsUrlSafe);
                    OutputText.Text = output;
                    OutputLabel.Text = IsUrlSafe ? "Output (Base64URL)" : "Output (Base64)";
                    resultBytes = Encoding.ASCII.GetBytes(output);
                    resultIsEncoded = true;
                    SetStatus($"Encoded {input.Length:N0} bytes to {output.Length:N0} characters.");
                }
                else
                {
                    resultBytes = Base64Converter.Decode(InputText.Text, IsUrlSafe);
                    resultIsEncoded = false;
                    try
                    {
                        OutputText.Text = Base64Converter.FormatOutput(resultBytes, IsHex);
                        OutputLabel.Text = IsHex ? "Output (Hex)" : "Output (UTF-8)";
                        SetStatus($"Decoded {resultBytes.Length:N0} bytes. Save writes the original bytes.");
                    }
                    catch (DecoderFallbackException)
                    {
                        OutputText.Text = Base64Converter.FormatOutput(resultBytes, true);
                        OutputLabel.Text = "Output (Hex)";
                        SetStatus($"Decoded {resultBytes.Length:N0} bytes; not valid UTF-8. Showing Hex; save preserves the bytes.");
                    }
                }
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException)
            {
                SetStatus(exception.Message, true);
            }
            UpdateActions();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "All files (*.*)|*.*" };
            if (dialog.ShowDialog() == true)
                SelectFile(dialog.FileName);
        }

        private void SelectFile(string path)
        {
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists)
                    throw new IOException("Select a regular file.");
                FileMode.IsChecked = true;
                FilePathText.Text = file.FullName;
                FileDetails.Text = $"{file.Length:N0} bytes";
                InvalidateResult();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                SetStatus(exception.Message, true);
            }
        }

        private void File_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;
            e.Effects = cancellation == null ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void File_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;
            e.Handled = true;
            if (cancellation != null)
                return;
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length == 1)
                SelectFile(paths[0]);
            else
                SetStatus("Select one input file at a time.", true);
        }

        private async Task ConvertFileAsync(bool encode)
        {
            string inputPath = FilePathText.Text;
            var dialog = new SaveFileDialog
            {
                FileName = Path.GetFileName(inputPath) + (encode ? ".b64" : ".decoded"),
                Filter = encode ? "Base64 files (*.b64)|*.b64|All files (*.*)|*.*" : "All files (*.*)|*.*",
                AddExtension = encode,
                DefaultExt = encode ? ".b64" : string.Empty,
                OverwritePrompt = true
            };
            if (dialog.ShowDialog() != true)
                return;

            bool urlSafe = IsUrlSafe;
            InvalidateResult();
            await RunFileOperationAsync(async token =>
            {
                var progress = new Progress<double>(value =>
                {
                    if (cancellation != null)
                        FileProgress.Value = value;
                });
                await Base64Converter.ConvertFileAsync(inputPath, dialog.FileName, encode, urlSafe, progress, token);
            }, dialog.FileName);
        }

        private async Task RunFileOperationAsync(Func<CancellationToken, Task> operation, string outputPath)
        {
            cancellation = new CancellationTokenSource();
            FileProgress.Value = 0;
            UpdateActions();
            SetStatus("Working...");
            try
            {
                await operation(cancellation.Token);
                SavedPathText.Text = outputPath;
                SetStatus("Saved successfully.");
            }
            catch (OperationCanceledException)
            {
                SetStatus("Canceled. Existing output was not changed.");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or ArgumentException)
            {
                SetStatus(exception.Message, true);
            }
            finally
            {
                cancellation.Dispose();
                cancellation = null;
                UpdateActions();
                if (closeAfterCancel)
                    owner?.Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => cancellation?.Cancel();

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetDataObject(OutputText.Text, true);
                SetStatus("Output copied.");
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                SetStatus("Clipboard is busy. Try again.", true);
            }
        }

        private void Result_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = resultBytes != null && cancellation == null && !IsFileMode;
            e.Handled = true;
        }

        private async void Save_Executed(object sender, ExecutedRoutedEventArgs e) => await SaveResultAsync();
        private async void Save_Click(object sender, RoutedEventArgs e) => await SaveResultAsync();

        private async Task SaveResultAsync()
        {
            if (resultBytes == null || cancellation != null || IsFileMode)
                return;
            byte[] bytes = resultBytes;
            var dialog = new SaveFileDialog
            {
                FileName = resultIsEncoded ? "encoded.b64" : "decoded.bin",
                Filter = resultIsEncoded ? "Base64 files (*.b64)|*.b64|All files (*.*)|*.*" : "All files (*.*)|*.*",
                AddExtension = resultIsEncoded,
                DefaultExt = resultIsEncoded ? ".b64" : string.Empty,
                OverwritePrompt = true
            };
            if (dialog.ShowDialog() != true)
                return;
            await RunFileOperationAsync(async token =>
            {
                string temporaryPath = Path.Combine(Path.GetDirectoryName(dialog.FileName)!, $".seckit-{Guid.NewGuid():N}.tmp");
                try
                {
                    await File.WriteAllBytesAsync(temporaryPath, bytes, token);
                    token.ThrowIfCancellationRequested();
                    File.Move(temporaryPath, dialog.FileName, true);
                }
                finally
                {
                    try { File.Delete(temporaryPath); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }, dialog.FileName);
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            InputText.Clear();
            FilePathText.Clear();
            FileDetails.Text = string.Empty;
            InvalidateResult();
        }
    }
}
