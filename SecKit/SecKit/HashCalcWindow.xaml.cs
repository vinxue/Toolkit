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
    /// Interaction logic for HashCalcWindow.xaml
    /// </summary>
    public partial class HashCalcWindow : UserControl
    {
        public HashCalcWindow()
        {
            InitializeComponent();
        }

        private void ClearHashTextBoxes()
        {
            CRC32TextBox.Clear();
            MD5TextBox.Clear();
            SHA1TextBox.Clear();
            SHA256TextBox.Clear();
            SHA384TextBox.Clear();
            SHA512TextBox.Clear();
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string filePath = files[0];
                    txtFilePath.Text = filePath;
                    ClearHashTextBoxes();
                    ComputeHashValueAsync(filePath);
                }
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                txtFilePath.Text = openFileDialog.FileName;
                ClearHashTextBoxes();
                ComputeHashValueAsync(openFileDialog.FileName);
            }
        }

#pragma warning disable SYSLIB0021
        private async void ComputeHashValueAsync(string filePath)
        {
            bool calcCRC32 = CRC32CheckBox.IsChecked == true;
            bool calcMD5 = MD5CheckBox.IsChecked == true;
            bool calcSHA1 = SHA1CheckBox.IsChecked == true;
            bool calcSHA256 = SHA256CheckBox.IsChecked == true;
            bool calcSHA384 = SHA384CheckBox.IsChecked == true;
            bool calcSHA512 = SHA512CheckBox.IsChecked == true;

            var result = await Task.Run(() =>
            {
                return new
                {
                    CRC32 = calcCRC32 ? ComputeCRC32(filePath) : string.Empty,
                    MD5 = calcMD5 ? ComputeHash(filePath, new MD5CryptoServiceProvider()) : string.Empty,
                    SHA1 = calcSHA1 ? ComputeHash(filePath, new SHA1CryptoServiceProvider()) : string.Empty,
                    SHA256 = calcSHA256 ? ComputeHash(filePath, new SHA256CryptoServiceProvider()) : string.Empty,
                    SHA384 = calcSHA384 ? ComputeHash(filePath, new SHA384CryptoServiceProvider()) : string.Empty,
                    SHA512 = calcSHA512 ? ComputeHash(filePath, new SHA512CryptoServiceProvider()) : string.Empty
                };
            });

            CRC32TextBox.Text = result.CRC32;
            MD5TextBox.Text = result.MD5;
            SHA1TextBox.Text = result.SHA1;
            SHA256TextBox.Text = result.SHA256;
            SHA384TextBox.Text = result.SHA384;
            SHA512TextBox.Text = result.SHA512;
        }
#pragma warning restore SYSLIB0021

        private string ComputeCRC32(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            {
                var crc32 = new Crc32();
                byte[] hashBytes = crc32.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        private string ComputeHash(string filePath, HashAlgorithm algorithm)
        {
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hashBytes = algorithm.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    public class Crc32 : HashAlgorithm
    {
        public const uint DefaultPolynomial = 0xedb88320u;
        public const uint DefaultSeed = 0xffffffffu;

        private static uint[] defaultTable;

        private readonly uint seed;
        private readonly uint[] table;
        private uint hash;

        public Crc32()
            : this(DefaultPolynomial, DefaultSeed)
        {
        }

        public Crc32(uint polynomial, uint seed)
        {
            table = InitializeTable(polynomial);
            this.seed = hash = seed;
        }

        public override void Initialize()
        {
            hash = seed;
        }

        protected override void HashCore(byte[] buffer, int start, int length)
        {
            for (int i = start; i < start + length; i++)
            {
                byte b = buffer[i];
                hash = (hash >> 8) ^ table[(hash & 0xff) ^ b];
            }
        }

        protected override byte[] HashFinal()
        {
            hash = ~hash;
            var hashBuffer = UInt32ToBigEndianBytes(hash);
            HashValue = hashBuffer;
            return hashBuffer;
        }

        public override int HashSize => 32;

        public static uint Compute(byte[] buffer)
        {
            return Compute(DefaultPolynomial, DefaultSeed, buffer);
        }

        public static uint Compute(uint seed, byte[] buffer)
        {
            return Compute(DefaultPolynomial, seed, buffer);
        }

        public static uint Compute(uint polynomial, uint seed, byte[] buffer)
        {
            var table = InitializeTable(polynomial);
            uint hash = seed;
            foreach (byte b in buffer)
            {
                hash = (hash >> 8) ^ table[(hash & 0xff) ^ b];
            }
            return ~hash;
        }

        private static uint[] InitializeTable(uint polynomial)
        {
            if (polynomial == DefaultPolynomial && defaultTable != null)
                return defaultTable;

            var createTable = new uint[256];
            for (var i = 0; i < 256; i++)
            {
                var entry = (uint)i;
                for (var j = 0; j < 8; j++)
                    if ((entry & 1) == 1)
                        entry = (entry >> 1) ^ polynomial;
                    else
                        entry >>= 1;
                createTable[i] = entry;
            }

            if (polynomial == DefaultPolynomial)
                defaultTable = createTable;

            return createTable;
        }

        private static byte[] UInt32ToBigEndianBytes(uint x)
        {
            return new[]
            {
            (byte)((x >> 24) & 0xff),
            (byte)((x >> 16) & 0xff),
            (byte)((x >> 8) & 0xff),
            (byte)(x & 0xff)
            };
        }
    }
}
