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
using System.Security.Cryptography;
using System.IO;
using Microsoft.Win32;

namespace SecKit
{
    /// <summary>
    /// Interaction logic for AesCipherWindow.xaml
    /// </summary>
    public partial class AesCipherWindow : UserControl
    {
        public AesCipherWindow()
        {
            InitializeComponent();
            txtIV.TextChanged += HexTextBox_TextChanged;
            txtKey.TextChanged += HexTextBox_TextChanged;
        }

        private void HexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            var caretIndex = textBox.CaretIndex;
            var formattedText = FormatHexString(textBox.Text);
            if (textBox.Text != formattedText)
            {
                textBox.Text = formattedText;
                textBox.CaretIndex = caretIndex;
            }
        }

        private string FormatHexString(string input)
        {
            var cleaned = new StringBuilder();
            foreach (var c in input)
            {
                if (Uri.IsHexDigit(c))
                {
                    cleaned.Append(c);
                }
            }
            return cleaned.ToString();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                txtFilePath.Text = openFileDialog.FileName;
            }
        }

        private byte[] HexStringToBytes(string hex)
        {
            hex = hex.Replace(" ", "");
            if (hex.Length % 2 != 0)
                throw new ArgumentException("Invalid hex string length");

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        private async void ProcessFile(bool encrypt)
        {
            string outputFile = null;
            try
            {
                var iv = HexStringToBytes(txtIV.Text);
                var key = HexStringToBytes(txtKey.Text);
                var inputFile = txtFilePath.Text;
                var usePadding = chkPadding.IsChecked == true;

                if (iv.Length != 16) throw new ArgumentException("IV must be 16 bytes");
                if (key.Length != 32) throw new ArgumentException("Key must be 32 bytes");
                if (string.IsNullOrEmpty(inputFile)) throw new ArgumentException("Please select a file");

                outputFile = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(inputFile),
                    System.IO.Path.GetFileName(inputFile) +
                    (encrypt ? ".enc" : ".dec"));

                using (var aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.BlockSize = 128;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = usePadding ? PaddingMode.PKCS7 : PaddingMode.None;
                    aes.IV = iv;
                    aes.Key = key;

                    using (var inputStream = new FileStream(inputFile, FileMode.Open))
                    using (var outputStream = new FileStream(outputFile, FileMode.Create))
                    {
                        var transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
                        using (var cryptoStream = new CryptoStream(outputStream, transform, CryptoStreamMode.Write))
                        {
                            await inputStream.CopyToAsync(cryptoStream);
                        }
                    }
                }

                MessageBox.Show($"File saved to:\n{outputFile}", "Success");
            }
            catch (Exception ex)
            {
                if (outputFile != null && File.Exists(outputFile))
                {
                    File.Delete(outputFile);
                }
                MessageBox.Show($"Error: {ex.Message}", "Error");
            }
        }

        private void BtnEncrypt_Click(object sender, RoutedEventArgs e) => ProcessFile(true);
        private void BtnDecrypt_Click(object sender, RoutedEventArgs e) => ProcessFile(false);

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    txtFilePath.Text = files[0];
                }
            }
        }
    }
}
