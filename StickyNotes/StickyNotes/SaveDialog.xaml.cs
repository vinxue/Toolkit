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
using System.Windows.Shapes;

namespace StickyNotes
{
    /// <summary>
    /// Interaction logic for SaveDialog.xaml
    /// </summary>
    public partial class SaveDialog : Window
    {
        public bool SaveClicked { get; private set; }
        public bool DontSaveClicked { get; private set; }
        public SaveDialog()
        {
            InitializeComponent();
            SaveButton.Focus(); // Set focus to Save button (default)
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveClicked = true;
            DialogResult = true;
        }

        private void DontSaveButton_Click(object sender, RoutedEventArgs e)
        {
            DontSaveClicked = true;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // Handle Escape key
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
            }
            base.OnKeyDown(e);
        }
    }
}
