using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Diagnostics;

namespace StickyNotes
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private FileManager _fileManager;

        public MainWindow()
        {
            InitializeComponent();
            // Initialize file manager
            _fileManager = new FileManager(ContentRichTextBox);
            ContentRichTextBox.Focus();
            UpdateTitleDisplay();
            UpdateStatusBar();
            AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnRequestNavigate));
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        // Override the closing event to show save confirmation
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            var result = _fileManager.ShowSaveConfirmationDialog(this);
            switch (result)
            {
                case SaveDialogResult.Save:
                    if (!_fileManager.SaveFile(this))
                    {
                        e.Cancel = true; // Save was cancelled
                        return;
                    }
                    break;
                case SaveDialogResult.Cancel:
                    e.Cancel = true;
                    return;
                case SaveDialogResult.DontSave:
                    // Continue closing
                    break;
                default:
                    break;
            }

            base.OnClosing(e);
        }

        // Toggle topmost state
        private void ToggleTopmost()
        {
            this.Topmost = !this.Topmost;
            UpdateTitleDisplay();
        }

        // Update title display
        private void UpdateTitleDisplay()
        {
            string baseTitle = "Sticky Notes";

            if (!string.IsNullOrEmpty(_fileManager.CurrentFileName))
            {
                baseTitle += $" - {_fileManager.CurrentFileName}";

                if (_fileManager.IsContentChanged)
                {
                    baseTitle += "*";
                }
            }

            if (this.Topmost)
            {
                TitleTextBlock.Text = baseTitle + " 📌"; // Show pin icon for topmost
            }
            else
            {
                TitleTextBlock.Text = baseTitle;     // Normal state
            }
        }

        // Handle keyboard shortcuts
        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Ctrl+P to toggle topmost
            if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ToggleTopmost();
                e.Handled = true;
            }
            // Ctrl+S to save
            else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _fileManager.SaveFile(this);
                UpdateTitleDisplay();
                e.Handled = true;
            }
            // Ctrl+O to open
            else if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _fileManager.OpenFile(this);
                UpdateTitleDisplay();
                e.Handled = true;
            }
            // Ctrl+N to new document
            else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _fileManager.NewDocument(this);
                UpdateTitleDisplay();
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }

        // Update status bar information
        private void UpdateStatusBar()
        {
            UpdatePositionInfo();
            UpdateFontSizeInfo();
            UpdateCharacterCount();
        }

        // Update line and column position
        private void UpdatePositionInfo()
        {
            try
            {
                var caretPosition = ContentRichTextBox.CaretPosition;
                var textRange = new TextRange(ContentRichTextBox.Document.ContentStart, caretPosition);
                var text = textRange.Text;

                // Calculate line number
                int lineNumber = text.Count(c => c == '\n') + 1;

                // Calculate column number
                int lastNewLineIndex = text.LastIndexOf('\n');
                int columnNumber = lastNewLineIndex == -1 ? text.Length + 1 : text.Length - lastNewLineIndex;

                PositionTextBlock.Text = $"Ln {lineNumber}, Col {columnNumber}";
            }
            catch
            {
                PositionTextBlock.Text = "Ln 1, Col 1";
            }
        }

        // Update font size information
        private void UpdateFontSizeInfo()
        {
            try
            {
                var selection = ContentRichTextBox.Selection;
                var fontSize = selection.GetPropertyValue(TextElement.FontSizeProperty);

                if (fontSize != DependencyProperty.UnsetValue && fontSize is double size)
                {
                    FontSizeTextBlock.Text = $"{size:F0}pt";
                }
                else
                {
                    // Mixed font sizes in selection
                    FontSizeTextBlock.Text = "Mixed";
                }
            }
            catch
            {
                FontSizeTextBlock.Text = $"{ContentRichTextBox.FontSize:F0}pt";
            }
        }

        // Update character count
        private void UpdateCharacterCount()
        {
            try
            {
                var textRange = new TextRange(ContentRichTextBox.Document.ContentStart, ContentRichTextBox.Document.ContentEnd);
                var text = textRange.Text;

                // Remove the trailing newline that RichTextBox adds
                if (text.EndsWith("\r\n"))
                {
                    text = text.Substring(0, text.Length - 2);
                }
                else if (text.EndsWith("\n") || text.EndsWith("\r"))
                {
                    text = text.Substring(0, text.Length - 1);
                }

                int charCount = text.Length;
                CharCountTextBlock.Text = $"{charCount} chars";
            }
            catch
            {
                CharCountTextBlock.Text = "0 chars";
            }
        }

        // Event handlers
        private void ContentRichTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdateStatusBar();
        }

        private void ContentRichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _fileManager.IsContentChanged = true;
            UpdateTitleDisplay(); // Update title to show unsaved changes
            UpdateStatusBar(); // Update all status info when text changes
        }

        private void ContentRichTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var rtb = sender as RichTextBox;

            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Key == Key.OemCloseBrackets || e.Key == Key.OemOpenBrackets)
                {
                    // Delay refresh to ensure command has been executed
                    Dispatcher.BeginInvoke(new Action(UpdateFontSizeInfo), DispatcherPriority.Background);
                }
                else if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    PastePlainText(rtb);
                    e.Handled = true;
                }
            }
        }

        private void PastePlainText(RichTextBox richTextBox)
        {
            if (richTextBox == null || !Clipboard.ContainsText())
            {
                return;
            }

            try
            {
                string plainText = Clipboard.ContainsText(TextDataFormat.UnicodeText)
                    ? Clipboard.GetText(TextDataFormat.UnicodeText)
                    : Clipboard.GetText();

                if (!string.IsNullOrEmpty(plainText))
                {
                    if (!richTextBox.Selection.IsEmpty)
                    {
                        richTextBox.Selection.Text = plainText;
                    }
                    else
                    {
                        var caret = richTextBox.CaretPosition;
                        caret.InsertTextInRun(plainText);
                    }
                }
            }
            catch
            {
                // Do nothing
            }
        }

        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
            catch (Exception)
            {
                // Do nothing
            }
        }
    }
}
