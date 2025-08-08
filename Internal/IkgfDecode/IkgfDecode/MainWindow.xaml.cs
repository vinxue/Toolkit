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
using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace IkgfDecode
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

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct KeyFileStruct
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] KeyFormat;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] KeyId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] KeyTypeId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] KeyData;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] IVData;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] Sha1;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct IVFileStruct
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] IVFormat;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] KeyId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] KeyTypeId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] IVData;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] Sha1;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    txtFilePath.Text = files[0];
                    ParseAndShow(files[0]);
                }
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                txtFilePath.Text = openFileDialog.FileName;
                ParseAndShow(openFileDialog.FileName);
            }
        }

        private void Radio_Checked(object sender, RoutedEventArgs e)
        {
            if (txtOutput != null)
            {
                txtOutput.Clear();
            }

            if (txtFilePath != null)
            {
                txtFilePath.Clear();
            }
        }

        private void ParseAndShow(string filePath)
        {
            try
            {
                if (rbKey.IsChecked == true)
                {
                    txtOutput.Text = ParseKeyFile(filePath);
                }
                else
                {
                    txtOutput.Text = ParseIVFile(filePath);
                }
            }
            catch (Exception ex)
            {
                txtOutput.Text = "Failed to parse file: " + ex.Message;
            }
        }

        private string ParseKeyFile(string filePath)
        {
            byte[] data = File.ReadAllBytes(filePath);
            if (data.Length != 86)
            {
                throw new Exception("Invalid Key file length, expected 86 bytes.");
            }

            KeyFileStruct keyStruct = ByteArrayToStructure<KeyFileStruct>(data);

            using (SHA1 sha1Alg = SHA1.Create())
            {
                byte[] calcSha1 = sha1Alg.ComputeHash(data, 0, 66);
                if (!CompareBytes(keyStruct.Sha1, calcSha1))
                {
                    throw new Exception("SHA-1 checksum mismatch.");
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Key Format:  {BitConverter.ToString(keyStruct.KeyFormat).Replace("-", "").ToLower()}");
            sb.AppendLine($"Key ID:      {BitConverter.ToString(keyStruct.KeyId).Replace("-", "").ToLower()}");
            sb.AppendLine($"Key Type ID: {BitConverter.ToString(keyStruct.KeyTypeId).Replace("-", "").ToLower()}");
            sb.AppendLine($"Key Data:    {BitConverter.ToString(keyStruct.KeyData).Replace("-", "").ToLower()}");
            sb.AppendLine($"IV Data:     {BitConverter.ToString(keyStruct.IVData).Replace("-", "").ToLower()}");
            sb.AppendLine($"SHA-1:       {BitConverter.ToString(keyStruct.Sha1).Replace("-", "").ToLower()}");
            return sb.ToString();
        }

        private string ParseIVFile(string filePath)
        {
            byte[] data = File.ReadAllBytes(filePath);
            if (data.Length != 54)
            {
                throw new Exception("Invalid IV file length, expected 54 bytes.");
            }

            IVFileStruct ivStruct = ByteArrayToStructure<IVFileStruct>(data);

            using (SHA1 sha1Alg = SHA1.Create())
            {
                byte[] calcSha1 = sha1Alg.ComputeHash(data, 0, 34);
                if (!CompareBytes(ivStruct.Sha1, calcSha1))
                {
                    throw new Exception("SHA-1 checksum mismatch.");
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"IV Format:   {BitConverter.ToString(ivStruct.IVFormat).Replace("-", "").ToLower()}");
            sb.AppendLine($"Key ID:      {BitConverter.ToString(ivStruct.KeyId).Replace("-", "").ToLower()}");
            sb.AppendLine($"Key Type ID: {BitConverter.ToString(ivStruct.KeyTypeId).Replace("-", "").ToLower()}");
            sb.AppendLine($"IV Data:     {BitConverter.ToString(ivStruct.IVData).Replace("-", "").ToLower()}");
            sb.AppendLine($"SHA-1:       {BitConverter.ToString(ivStruct.Sha1).Replace("-", "").ToLower()}");
            return sb.ToString();
        }

        private static T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }
        }

        private bool CompareBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
