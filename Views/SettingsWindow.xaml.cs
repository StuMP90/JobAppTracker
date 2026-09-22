using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using JobAppTracker.Data;
using JobAppTracker.Models;
using JobAppTracker.Services;
using JobAppTracker.ViewModels;

namespace JobAppTracker.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly DatabaseService _db;
        public int UpdatedStaleDays { get; private set; }
        private UpdateInfo? _latestCheckResult;

        public SettingsWindow(DatabaseService db, int currentStaleDays)
        {
            InitializeComponent();
            _db = db;
            UpdatedStaleDays = currentStaleDays;

            TxtStaleDays.Text = currentStaleDays.ToString();
            TxtDbPath.Text = db.DatabasePath;
            TxtAppVersion.Text = $"JobAppTracker {MainViewModel.AppVersion}";
            TxtSettingsVersionBadge.Text = MainViewModel.AppVersion;

            string autoCheck = _db.GetSetting("CheckForUpdatesOnStartup", "true");
            ChkAutoUpdateStartup.IsChecked = autoCheck.Equals("true", StringComparison.OrdinalIgnoreCase);

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

        private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            BtnCheckUpdates.IsEnabled = false;
            BtnCheckUpdates.Content = "⏳ Checking...";
            BorderUpdateResult.Visibility = Visibility.Visible;
            BorderUpdateResult.Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
            BorderUpdateResult.BorderBrush = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
            TxtUpdateStatus.Text = "Connecting to GitHub Releases...";
            TxtUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
            PnlUpdateActions.Visibility = Visibility.Collapsed;

            try
            {
                _latestCheckResult = await UpdateService.CheckForUpdatesAsync(MainViewModel.AppVersion.TrimStart('v'));

                if (_latestCheckResult.HasError)
                {
                    BorderUpdateResult.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF2, 0xF2));
                    BorderUpdateResult.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFE, 0xCA, 0xCA));
                    TxtUpdateStatus.Text = $"⚠️ {_latestCheckResult.ErrorMessage}";
                    TxtUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                    PnlUpdateActions.Visibility = Visibility.Collapsed;
                }
                else if (_latestCheckResult.IsUpdateAvailable)
                {
                    BorderUpdateResult.Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xFD, 0xFA));
                    BorderUpdateResult.BorderBrush = new SolidColorBrush(Color.FromRgb(0x99, 0xF6, 0xE4));
                    TxtUpdateStatus.Text = $"🎉 New version available: {_latestCheckResult.LatestVersion} (current is {MainViewModel.AppVersion})";
                    TxtUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x76, 0x6E));
                    PnlUpdateActions.Visibility = Visibility.Visible;
                }
                else
                {
                    BorderUpdateResult.Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xFD, 0xF4));
                    BorderUpdateResult.BorderBrush = new SolidColorBrush(Color.FromRgb(0xBB, 0xF7, 0xD0));
                    TxtUpdateStatus.Text = $"✅ You are using the latest version of JobAppTracker ({MainViewModel.AppVersion}).";
                    TxtUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x16, 0x65, 0x34));
                    PnlUpdateActions.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                BorderUpdateResult.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF2, 0xF2));
                BorderUpdateResult.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFE, 0xCA, 0xCA));
                TxtUpdateStatus.Text = $"⚠️ Error checking for updates: {ex.Message}";
                TxtUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                PnlUpdateActions.Visibility = Visibility.Collapsed;
            }
            finally
            {
                BtnCheckUpdates.IsEnabled = true;
                BtnCheckUpdates.Content = "🔍 Check for Updates Now";
            }
        }

        private void BtnDownloadUpdate_Click(object sender, RoutedEventArgs e)
        {
            var url = !string.IsNullOrEmpty(_latestCheckResult?.DownloadUrl)
                ? _latestCheckResult.DownloadUrl
                : _latestCheckResult?.ReleasePageUrl;

            if (!string.IsNullOrEmpty(url))
            {
                AttachmentService.OpenUrl(url);
            }
        }

        private void BtnViewReleaseNotes_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_latestCheckResult?.ReleasePageUrl))
            {
                AttachmentService.OpenUrl(_latestCheckResult.ReleasePageUrl);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtStaleDays.Text.Trim(), out int days) && days > 0)
            {
                UpdatedStaleDays = days;
                _db.SetSetting("StaleDaysThreshold", days.ToString());
                _db.SetSetting("CheckForUpdatesOnStartup", ChkAutoUpdateStartup.IsChecked == true ? "true" : "false");
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
