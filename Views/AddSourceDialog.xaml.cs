using System.Windows;
using System.Windows.Input;

namespace JobAppTracker.Views
{
    public partial class AddSourceDialog : Window
    {
        public string SourceResult { get; private set; } = string.Empty;

        public AddSourceDialog()
        {
            InitializeComponent();
            Loaded += (s, e) => TxtSourceName.Focus();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            ConfirmAdd();
        }

        private void TxtSourceName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmAdd();
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void ConfirmAdd()
        {
            var text = TxtSourceName.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("Please enter a valid source name.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtSourceName.Focus();
                return;
            }

            SourceResult = text;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
