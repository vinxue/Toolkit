using System;
using System.Windows;
using FileForge.Core;

namespace FileForge.Views
{
    public partial class PatchEntryDialog : Window
    {
        public PatchEntry Result { get; private set; }

        public PatchEntryDialog(PatchEntry existing = null)
        {
            InitializeComponent();
            if (existing != null)
            {
                txtOffset.Text = existing.OffsetDisplay;
                txtBytes.Text  = existing.BytesDisplay;
                txtDesc.Text   = existing.Description ?? "";
            }
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!FileEngine.TryParseOffset(txtOffset.Text.Trim(), out long offset))
                    throw new Exception("Invalid offset — use hex (0x…) or decimal.");

                byte[] bytes = FileEngine.ParseHexBytes(txtBytes.Text);
                if (bytes.Length == 0) throw new Exception("New bytes cannot be empty.");

                Result = new PatchEntry
                {
                    Offset      = offset,
                    NewBytes    = bytes,
                    Description = txtDesc.Text.Trim()
                };
                DialogResult = true;
            }
            catch (Exception ex)
            {
                txtError.Text = ex.Message;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
