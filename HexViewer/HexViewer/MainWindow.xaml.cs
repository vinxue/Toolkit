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
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace HexViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<TextBox> highBitTextBoxes = new List<TextBox>();
        private List<TextBox> lowBitTextBoxes = new List<TextBox>();
        private List<Label> highHexLabels = new List<Label>();
        private List<Label> lowHexLabels = new List<Label>();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;

            InitializeControls();
            InitializeDefaultValues();

            // Add event handlers for hex input boxes
            txtHexInput.PreviewTextInput += HexTextBox_PreviewTextInput;
            txtHexInput.TextChanged += HexTextBox_TextChanged;

            txtBitFieldValue.PreviewTextInput += HexTextBox_PreviewTextInput;
            txtBitFieldValue.TextChanged += HexTextBox_TextChanged;

            // Add event handlers for numeric input boxes
            txtEndBit.PreviewTextInput += NumericTextBox_PreviewTextInput;
            txtStartBit.PreviewTextInput += NumericTextBox_PreviewTextInput;
        }

        #region DWM API for Window Style
        public static class DwmApi
        {
            public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
            public const int DWMSBT_MAINWINDOW = 2;
            public const int DWMSBT_TRANSIENTWINDOW = 3;

            [DllImport("dwmapi.dll", PreserveSig = true)]
            public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
        }

        public static class NonClientRegionAPI
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct MARGINS
            {
                public int cxLeftWidth;      // width of left border that retains its size
                public int cxRightWidth;     // width of right border that retains its size
                public int cyTopHeight;      // height of top border that retains its size
                public int cyBottomHeight;   // height of bottom border that retains its size
            };

            [DllImport("dwmapi.dll")]
            public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            int backdropType = DwmApi.DWMSBT_MAINWINDOW;
            DwmApi.DwmSetWindowAttribute(hwnd, DwmApi.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, Marshal.SizeOf(typeof(int)));

            HwndSource mainWindowSrc = HwndSource.FromHwnd(hwnd);
            mainWindowSrc.CompositionTarget.BackgroundColor = Color.FromArgb(0, 0, 0, 0);
            NonClientRegionAPI.MARGINS margins = new NonClientRegionAPI.MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            NonClientRegionAPI.DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }
        #endregion

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void InitializeControls()
        {
            CreateBitControls();
        }

        private void CreateBitControls()
        {
            // Clear existing controls
            ClearAllControls();

            // Create high 32 bits (63 down to 32)
            CreateBitSection(63, 32, spHighBitNumbers, spHighBitValues, spHighHexValues,
                             highBitTextBoxes, highHexLabels);

            // Create low 32 bits (31 down to 0)
            CreateBitSection(31, 0, spLowBitNumbers, spLowBitValues, spLowHexValues,
                             lowBitTextBoxes, lowHexLabels);
        }

        private void ClearAllControls()
        {
            spHighBitNumbers.Children.Clear();
            spHighBitValues.Children.Clear();
            spHighHexValues.Children.Clear();
            spLowBitNumbers.Children.Clear();
            spLowBitValues.Children.Clear();
            spLowHexValues.Children.Clear();

            highBitTextBoxes.Clear();
            lowBitTextBoxes.Clear();
            highHexLabels.Clear();
            lowHexLabels.Clear();
        }

        private void CreateBitSection(int startBit, int endBit,
                                     StackPanel bitNumbersPanel,
                                     StackPanel bitValuesPanel,
                                     StackPanel hexValuesPanel,
                                     List<TextBox> bitTextBoxes,
                                     List<Label> hexLabels)
        {
            // Create bit controls
            for (int i = startBit; i >= endBit; i--)
            {
                CreateBitControls(i, bitNumbersPanel, bitValuesPanel, bitTextBoxes, endBit);
            }

            // Create hex labels (8 nibbles for 32 bits)
            for (int i = 0; i < 8; i++)
            {
                CreateHexLabel(i, hexValuesPanel, hexLabels);
            }
        }

        private void CreateBitControls(int bitIndex, StackPanel bitNumbersPanel,
                                      StackPanel bitValuesPanel, List<TextBox> bitTextBoxes,
                                      int endBit)
        {
            bool needsSpacing = bitIndex % 8 == 0 && bitIndex != endBit;
            var spacing = needsSpacing ? new Thickness(2, 2, 20, 2) : new Thickness(2);

            // Create bit number label
            var bitNumberLabel = CreateBitNumberLabel(bitIndex.ToString(), spacing);
            bitNumbersPanel.Children.Add(bitNumberLabel);

            // Create bit value textbox
            var bitTextBox = CreateBitTextBox(bitIndex, spacing);
            bitValuesPanel.Children.Add(bitTextBox);
            bitTextBoxes.Add(bitTextBox);
        }

        private Label CreateBitNumberLabel(string content, Thickness margin)
        {
            return new Label
            {
                Content = content,
                Width = 20,
                Height = 20,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = margin,
                Padding = new Thickness(0)
            };
        }

        private TextBox CreateBitTextBox(int bitIndex, Thickness margin)
        {
            var textBox = new TextBox
            {
                Text = "0",
                Width = 20,
                Height = 25,
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                MaxLength = 1,
                FontWeight = FontWeights.Bold,
                Margin = margin,
                Tag = bitIndex
            };

            // Attach event handlers
            textBox.PreviewTextInput += BitTextBox_PreviewTextInput;
            textBox.MouseDoubleClick += BitTextBox_MouseDoubleClick;
            textBox.TextChanged += BitTextBox_TextChanged;

            return textBox;
        }

        private void CreateHexLabel(int index, StackPanel hexValuesPanel, List<Label> hexLabels)
        {
            var hexLabel = new Label
            {
                Content = "0",
                Width = 92, // 4×20 + 3×4 = 92 pixels
                Height = 20,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(0),
                Margin = GetHexLabelMargin(index)
            };

            hexValuesPanel.Children.Add(hexLabel);
            hexLabels.Add(hexLabel);
        }

        private Thickness GetHexLabelMargin(int index)
        {
            if (index == 0)
            {
                return new Thickness(2, 0, 0, 0);           // First label
            }
            else if (index == 1 || index == 3 || index == 5)
            {
                return new Thickness(4, 0, 20, 0);          // 2nd, 4th, 6th labels with group spacing
            }
            else if (index == 7)
            {
                return new Thickness(4, 0, 0, 0);           // Last label
            }
            else
            {
                return new Thickness(2, 0, 0, 0);           // Other labels
            }
        }

        private void InitializeDefaultValues()
        {
            txtHexInput.Text = "0";
            txtBitFieldValue.Text = "0";
            txtStartBit.Text = "0";
            txtEndBit.Text = "0";
        }

        // PreviewTextInput event handler for hex TextBoxes
        private void HexTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow hexadecimal characters (0-9, A-F, a-f)
            e.Handled = !e.Text.All(c => Uri.IsHexDigit(c));
        }

        // PreviewTextInput event handler for numeric TextBoxes
        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow numeric characters (0-9)
            e.Handled = !e.Text.All(c => char.IsDigit(c));
        }

        // TextChanged event handler for hex TextBoxes
        private void HexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;

            var upperText = textBox.Text.ToUpper();
            if (textBox.Text != upperText)
            {
                var caretIndex = textBox.CaretIndex;
                textBox.Text = upperText;
                textBox.CaretIndex = Math.Min(caretIndex, textBox.Text.Length);
            }
        }

        private void BtnDecode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtHexInput.Text))
                {
                    MessageBox.Show("Please input a hex data.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ulong inputData = Convert.ToUInt64(txtHexInput.Text, 16);

                if (!ValidateBitRange())
                {
                    MessageBox.Show("Please input a valid Start/End Bit.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                UpdateBitDisplay(inputData);
                UpdateHexDisplay(inputData);
                UpdateBitFieldValue(inputData);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Invalid hex input: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateBitRange()
        {
            if (!int.TryParse(txtStartBit.Text, out int startBit) ||
                !int.TryParse(txtEndBit.Text, out int endBit))
            {
                return false;
            }

            if (startBit > 63 || endBit > 63 || startBit > endBit)
            {
                return false;
            }

            return true;
        }

        private void UpdateTextBoxColor(TextBox textBox, ulong bitValue)
        {
            textBox.Foreground = bitValue == 1 ? new SolidColorBrush(Color.FromRgb(0x00, 0x67, 0xC0)) : SystemColors.ControlTextBrush;
        }

        private void UpdateBitDisplay(ulong inputData)
        {
            // Update high bits (63-32)
            foreach (var textBox in highBitTextBoxes)
            {
                int bitIndex = (int)textBox.Tag;
                ulong bit = (inputData >> bitIndex) & 1;
                textBox.Text = bit.ToString();
                UpdateTextBoxColor(textBox, bit);
            }

            // Update low bits (31-0)
            foreach (var textBox in lowBitTextBoxes)
            {
                int bitIndex = (int)textBox.Tag;
                ulong bit = (inputData >> bitIndex) & 1;
                textBox.Text = bit.ToString();
                UpdateTextBoxColor(textBox, bit);
            }
        }

        private void UpdateHexDisplay(ulong inputData)
        {
            // Update high hex values (each value corresponds to 4 bits, i.e., one nibble)
            for (int i = 0; i < 8; i++)
            {
                int nibbleIndex = 15 - i; // Start from the highest nibble (bit 63-60, 59-56, ...)
                ulong nibbleValue = (inputData >> (nibbleIndex * 4)) & 0xF;
                highHexLabels[i].Content = nibbleValue.ToString("X");
            }

            // Update low hex values (each value corresponds to 4 bits, i.e., one nibble)
            for (int i = 0; i < 8; i++)
            {
                int nibbleIndex = 7 - i; // From nibble 7 to nibble 0 (bit 31-28, 27-24, ...)
                ulong nibbleValue = (inputData >> (nibbleIndex * 4)) & 0xF;
                lowHexLabels[i].Content = nibbleValue.ToString("X");
            }
        }

        private void UpdateBitFieldValue(ulong inputData)
        {
            if (!int.TryParse(txtStartBit.Text, out int startBit) ||
                !int.TryParse(txtEndBit.Text, out int endBit))
            {
                return;
            }

            ulong bitFieldValue = BitFieldRead64(inputData, (ulong)startBit, (ulong)endBit);
            txtBitFieldValue.Text = bitFieldValue.ToString("X");
        }

        private void BitTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow 0 and 1
            e.Handled = !Regex.IsMatch(e.Text, "^[01]$");
        }

        private void BitTextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // Toggle bit value
                string currentValue = textBox.Text;
                string newValue = (currentValue == "0") ? "1" : "0";
                textBox.Text = newValue;
                UpdateTextBoxColor(textBox, ulong.Parse(newValue));
            }
        }

        private void BitTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && !string.IsNullOrEmpty(textBox.Text))
            {
                EncodeHexValue();
            }
        }

        private void EncodeHexValue()
        {
            try
            {
                ulong hexValue = 0;

                // Combine all bit values
                var allTextBoxes = highBitTextBoxes.Concat(lowBitTextBoxes);

                foreach (var textBox in allTextBoxes)
                {
                    int bitIndex = (int)textBox.Tag;
                    if (ulong.TryParse(textBox.Text, out ulong bit) && bit <= 1)
                    {
                        hexValue = BitFieldWrite64(hexValue, (ulong)bitIndex, (ulong)bitIndex, bit);
                    }
                }

                // Update hex input (temporarily remove event handler to avoid recursion)
                txtHexInput.TextChanged -= HexTextBox_TextChanged;
                txtHexInput.Text = hexValue.ToString("x").ToUpper();
                txtHexInput.TextChanged += HexTextBox_TextChanged;

                // Update hex display
                UpdateHexDisplay(hexValue);

                // Update bitfield value if valid range
                if (!ValidateBitRange())
                {
                    MessageBox.Show("Please input a valid Start/End Bit.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                UpdateBitFieldValue(hexValue);
            }
            catch (Exception)
            {
                // Silently ignore errors during encoding to avoid infinite loops
            }
        }

        private void ChkSetBitField_Checked(object sender, RoutedEventArgs e)
        {
            btnSetBitField.IsEnabled = true;
            txtBitFieldValue.IsEnabled = true;
        }

        private void ChkSetBitField_Unchecked(object sender, RoutedEventArgs e)
        {
            btnSetBitField.IsEnabled = false;
            txtBitFieldValue.IsEnabled = false;
        }

        private void BtnSetBitField_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtHexInput.Text))
                {
                    MessageBox.Show("Please input a hex data.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!ValidateBitRange())
                {
                    MessageBox.Show("Please input a valid Start/End Bit.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ulong inputData = Convert.ToUInt64(txtHexInput.Text, 16);
                ulong newBitField = Convert.ToUInt64(txtBitFieldValue.Text, 16);

                int startBit = int.Parse(txtStartBit.Text);
                int endBit = int.Parse(txtEndBit.Text);

                // Validate new bit field value
                ulong maxValue = (1UL << (endBit - startBit + 1)) - 1;
                if (newBitField > maxValue)
                {
                    MessageBox.Show("Please input a valid new value of bit field.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ulong newInputData = BitFieldWrite64(inputData, (ulong)startBit, (ulong)endBit, newBitField);

                // Update displays
                txtHexInput.Text = newInputData.ToString("x").ToUpper();
                UpdateBitDisplay(newInputData);
                UpdateHexDisplay(newInputData);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting bit field: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Bit Field Operations (from original MFC code)

        private static ulong RShiftU64(ulong operand, ulong count)
        {
            return count >= 64 ? 0 : operand >> (int)count;
        }

        private static ulong LShiftU64(ulong operand, ulong count)
        {
            return count >= 64 ? 0 : operand << (int)count;
        }

        private static ulong BitFieldRead64(ulong operand, ulong startBit, ulong endBit)
        {
            return endBit >= 64 || startBit > endBit ? 0 : RShiftU64(operand & ~LShiftU64(ulong.MaxValue - 1, endBit), startBit);
        }

        private static ulong BitFieldWrite64(ulong operand, ulong startBit, ulong endBit, ulong value)
        {
            if (endBit >= 64 || startBit > endBit)
            {
                return operand;
            }

            ulong mask = ~LShiftU64(ulong.MaxValue - 1, endBit);
            ulong clearedOperand = operand & ~mask;
            ulong shiftedValue = LShiftU64(value, startBit) & mask;

            return clearedOperand | shiftedValue;
        }

        #endregion
    }
}
