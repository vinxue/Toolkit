using System;
using System.IO;
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

namespace HashCalc
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string filePath = files[0];
                    FilePathTextBlock.Text = filePath;

                    if (CRC32CheckBox.IsChecked == true)
                    {
                        CRC32TextBox.Text = ComputeCRC32(filePath);
                    }
                    else
                    {
                        CRC32TextBox.Clear();
                    }

                    if (MD5CheckBox.IsChecked == true)
                    {
                        MD5TextBox.Text = ComputeHash(filePath, new MD5CryptoServiceProvider());
                    }
                    else
                    {
                        MD5TextBox.Clear();
                    }

                    if (SHA1CheckBox.IsChecked == true)
                    {
                        SHA1TextBox.Text = ComputeHash(filePath, new SHA1CryptoServiceProvider());
                    }
                    else
                    {
                        SHA1TextBox.Clear();
                    }

                    if (SHA256CheckBox.IsChecked == true)
                    {
                        SHA256TextBox.Text = ComputeHash(filePath, new SHA256CryptoServiceProvider());
                    }
                    else
                    {
                        SHA256TextBox.Clear();
                    }

                    if (SHA384CheckBox.IsChecked == true)
                    {
                        SHA384TextBox.Text = ComputeHash(filePath, new SHA384CryptoServiceProvider());
                    }
                    else
                    {
                        SHA384TextBox.Clear();
                    }

                    if (SHA512CheckBox.IsChecked == true)
                    {
                        SHA512TextBox.Text = ComputeHash(filePath, new SHA512CryptoServiceProvider());
                    }
                    else
                    {
                        SHA512TextBox.Clear();
                    }
                }
            }
        }

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
