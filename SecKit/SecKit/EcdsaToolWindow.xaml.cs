using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
using Microsoft.Win32;

namespace SecKit
{
    /// <summary>
    /// Interaction logic for EcdsaToolWindow.xaml
    /// </summary>
    public partial class EcdsaToolWindow : UserControl
    {
        public EcdsaToolWindow()
        {
            InitializeComponent();
            SignRadioButton.IsChecked = true;
            SignRadioButton.Checked += SignRadioButton_Checked;
            VerifyRadioButton.Checked += VerifyRadioButton_Checked;
        }

        private void SignRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (KeyFileLabel != null)
            {
                KeyFileLabel.Content = "Select Private Key";
            }
        }

        private void VerifyRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (KeyFileLabel != null)
            {
                KeyFileLabel.Content = "Select Public Key";
            }
        }

        private void KeyFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "PEM files (*.pem)|*.pem|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                KeyFileTextBox.Text = openFileDialog.FileName;
            }
        }

        private void DataFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                DataFileTextBox.Text = openFileDialog.FileName;
            }
        }

        private void ExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (SignRadioButton.IsChecked == true)
            {
                SignData();
            }
            else if (VerifyRadioButton.IsChecked == true)
            {
                VerifyData();
            }
        }

        private void SignData()
        {
            string privateKeyPath = KeyFileTextBox.Text;
            string dataFilePath = DataFileTextBox.Text;

            if (string.IsNullOrEmpty(privateKeyPath) || string.IsNullOrEmpty(dataFilePath))
            {
                MessageBox.Show("Please select both private key and data file.");
                return;
            }

            byte[] data = File.ReadAllBytes(dataFilePath);
            byte[] privateKey = File.ReadAllBytes(privateKeyPath);

            using (ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384))
            {
                ecdsa.ImportFromPem(File.ReadAllText(privateKeyPath));
                byte[] signature = ecdsa.SignData(data, HashAlgorithmName.SHA384);

                // string signedFilePath = System.IO.Path.ChangeExtension(dataFilePath, ".sig");
                string signedFilePath = dataFilePath + ".sig";
                using (FileStream fs = new FileStream(signedFilePath, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(signature, 0, signature.Length);
                    fs.Write(data, 0, data.Length);
                }

                MessageBox.Show($"Data signed successfully. Signed file: {signedFilePath}");
            }
        }

        private void VerifyData()
        {
            string publicKeyPath = KeyFileTextBox.Text;
            string dataFilePath = DataFileTextBox.Text;

            if (string.IsNullOrEmpty(publicKeyPath) || string.IsNullOrEmpty(dataFilePath))
            {
                MessageBox.Show("Please select both public key and data file.");
                return;
            }

            byte[] signedData = File.ReadAllBytes(dataFilePath);
            byte[] publicKey = File.ReadAllBytes(publicKeyPath);

            if (signedData.Length < 96)
            {
                MessageBox.Show("Invalid signed data file.");
                return;
            }

            byte[] signature = new byte[96];
            byte[] data = new byte[signedData.Length - 96];
            Array.Copy(signedData, 0, signature, 0, 96);
            Array.Copy(signedData, 96, data, 0, data.Length);

            using (ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384))
            {
                ecdsa.ImportFromPem(File.ReadAllText(publicKeyPath));
                bool isValid = ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA384);

                MessageBox.Show(isValid ? "Signature is valid." : "Signature is invalid.");
            }
        }

    }
}
