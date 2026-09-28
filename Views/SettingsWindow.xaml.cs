using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using JobAppTracker.Data;
using JobAppTracker.ViewModels;

namespace JobAppTracker.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly DatabaseService _db;
        public int UpdatedStaleDays { get; private set; }

        public SettingsWindow(DatabaseService db, int currentStaleDays)
        {
            InitializeComponent();
            _db = db;
            UpdatedStaleDays = currentStaleDays;

            TxtStaleDays.Text = currentStaleDays.ToString();
            TxtDbPath.Text = db.DatabasePath;
            TxtAppVersion.Text = $"JobAppTracker {MainViewModel.AppVersion}";

            LoadSources();
        }

        private void LoadSources()
        {
            LstSources.ItemsSource = null;
            LstSources.ItemsSource = _db.GetDistinctSources();
        }

        private void BtnAddSource_Click(object sender, RoutedEventArgs e)
        {
            var text = TxtNewSource.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("Please enter a source name to add.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtNewSource.Focus();
                return;
            }

            _db.AddSource(text);
            TxtNewSource.Text = string.Empty;
            LoadSources();
        }

        private void BtnDeleteSource_Click(object sender, RoutedEventArgs e)
        {
            var src = (sender as FrameworkElement)?.Tag?.ToString();
            if (string.IsNullOrWhiteSpace(src)) return;

            var res = MessageBox.Show($"Are you sure you want to remove source '{src}' from the sources list?", "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                _db.DeleteSource(src);
                LoadSources();
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = Path.GetDirectoryName(_db.DatabasePath);
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtStaleDays.Text.Trim(), out int days) && days > 0)
            {
                UpdatedStaleDays = days;
                _db.SetSetting("StaleDaysThreshold", days.ToString());
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Please enter a valid positive number of days.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
