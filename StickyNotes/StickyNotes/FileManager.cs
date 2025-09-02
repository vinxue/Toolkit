using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.Win32;

namespace StickyNotes
{
    public enum SaveDialogResult
    {
        Save,
        DontSave,
        Cancel
    }

    public class FileManager
    {
        private readonly RichTextBox _richTextBox;
        private string _currentFilePath;

        public bool IsContentChanged { get; set; }

        public string CurrentFileName => string.IsNullOrEmpty(_currentFilePath)
            ? ""
            : Path.GetFileName(_currentFilePath);

        public FileManager(RichTextBox richTextBox)
        {
            _richTextBox = richTextBox;
            IsContentChanged = false;
        }

        // Show save confirmation dialog
        public SaveDialogResult ShowSaveConfirmationDialog(Window owner)
        {
            if (!IsContentChanged || !HasContent())
            {
                return SaveDialogResult.DontSave;
            }

            var dialog = new SaveDialog();
            dialog.Owner = owner;

            var result = dialog.ShowDialog();

            if (result == true)
            {
                if (dialog.SaveClicked)
                {
                    return SaveDialogResult.Save;
                }
                else if (dialog.DontSaveClicked)
                {
                    return SaveDialogResult.DontSave;
                }
            }

            return SaveDialogResult.Cancel;
        }

        // Check if there's any content to save
        public bool HasContent()
        {
            try
            {
                var textRange = new TextRange(_richTextBox.Document.ContentStart, _richTextBox.Document.ContentEnd);
                var text = textRange.Text;

                if (text.EndsWith("\r\n"))
                {
                    text = text.Substring(0, text.Length - 2);
                }
                else if (text.EndsWith("\n") || text.EndsWith("\r"))
                {
                    text = text.Substring(0, text.Length - 1);
                }

                return !string.IsNullOrEmpty(text);
            }
            catch
            {
                return false;
            }
        }

        // Save file with dialog (Ctrl+S)
        public bool SaveFile(Window owner)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                return SaveFileAs(owner);
            }
            else
            {
                return SaveToFile(_currentFilePath);
            }
        }

        // Save file as with dialog
        public bool SaveFileAs(Window owner)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Rich Text Format (*.rtf)|*.rtf|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                DefaultExt = "rtf",
                FileName = CurrentFileName
            };

            if (saveFileDialog.ShowDialog(owner) == true)
            {
                _currentFilePath = saveFileDialog.FileName;
                return SaveToFile(_currentFilePath);
            }

            return false;
        }

        // Open file with dialog (Ctrl+O)
        public bool OpenFile(Window owner)
        {
            // Check if current content needs to be saved
            var saveResult = ShowSaveConfirmationDialog(owner);
            switch (saveResult)
            {
                case SaveDialogResult.Save:
                    if (!SaveFile(owner))
                    {
                        return false; // Save was cancelled
                    }

                    break;
                case SaveDialogResult.Cancel:
                    return false; // User cancelled
                case SaveDialogResult.DontSave:
                    // Continue with opening
                    break;
                default:
                    break;
            }

            var openFileDialog = new OpenFileDialog
            {
                Filter = "Rich Text Format (*.rtf)|*.rtf|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                DefaultExt = "rtf"
            };

            if (openFileDialog.ShowDialog(owner) == true)
            {
                return LoadFromFile(openFileDialog.FileName);
            }

            return false;
        }

        // Save content to specific file
        private bool SaveToFile(string filePath)
        {
            try
            {
                using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
                {
                    TextRange range = new TextRange(_richTextBox.Document.ContentStart, _richTextBox.Document.ContentEnd);

                    // Determine format based on file extension
                    string extension = Path.GetExtension(filePath).ToLower();
                    string dataFormat = extension == ".rtf" ? DataFormats.Rtf : DataFormats.Text;

                    range.Save(fileStream, dataFormat);
                }

                IsContentChanged = false;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save file: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // Load content from specific file
        private bool LoadFromFile(string filePath)
        {
            try
            {
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
                {
                    TextRange range = new TextRange(_richTextBox.Document.ContentStart, _richTextBox.Document.ContentEnd);

                    // Determine format based on file extension
                    string extension = Path.GetExtension(filePath).ToLower();
                    string dataFormat = extension == ".rtf" ? DataFormats.Rtf : DataFormats.Text;

                    range.Load(fileStream, dataFormat);
                }

                _currentFilePath = filePath;
                IsContentChanged = false;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open file: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // Create new document
        public bool NewDocument(Window owner)
        {
            var saveResult = ShowSaveConfirmationDialog(owner);
            switch (saveResult)
            {
                case SaveDialogResult.Save:
                    if (!SaveFile(owner))
                    {
                        return false; // Save was cancelled
                    }
                    break;
                case SaveDialogResult.Cancel:
                    return false; // User cancelled
                case SaveDialogResult.DontSave:
                    // Continue with new document
                    break;
                default:
                    break;
            }

            // Clear the document
            _richTextBox.Document = new FlowDocument();
            _currentFilePath = null;
            IsContentChanged = false;
            return true;
        }
    }
}
