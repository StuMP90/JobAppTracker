using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using JobAppTracker.Models;
using JobAppTracker.Services;
using JobAppTracker.ViewModels;
using JobAppTracker.Views;

namespace JobAppTracker
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;
        }

        private void BtnNewApp_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ApplicationEditWindow(_vm.Database)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                _vm.LoadFilterOptions();
                _vm.ApplyFilters();
                if (dlg.SavedApplication != null)
                {
                    _vm.SelectedApplication = _vm.Applications.FirstOrDefault(x => x.Id == dlg.SavedApplication.Id);
                }
            }
        }

        private void BtnQuickCv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ApplicationEditWindow(_vm.Database, defaultAsCvToAgency: true)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                _vm.LoadFilterOptions();
                _vm.ApplyFilters();
                if (dlg.SavedApplication != null)
                {
                    _vm.SelectedApplication = _vm.Applications.FirstOrDefault(x => x.Id == dlg.SavedApplication.Id);
                }
            }
        }

        private void BtnEditSelected_Click(object sender, RoutedEventArgs e)
        {
            EditSelected();
        }

        private void MenuEdit_Click(object sender, RoutedEventArgs e)
        {
            EditSelected();
        }

        private void GridApps_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_vm.SelectedApplication != null)
            {
                EditSelected();
            }
        }

        private void EditSelected()
        {
            if (_vm.SelectedApplication == null) return;

            var dlg = new ApplicationEditWindow(_vm.Database, _vm.SelectedApplication)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                int currentId = _vm.SelectedApplication.Id;
                _vm.LoadFilterOptions();
                _vm.ApplyFilters();
                _vm.SelectedApplication = _vm.Applications.FirstOrDefault(x => x.Id == currentId);
            }
        }

        private void BtnAddUpdate_Click(object sender, RoutedEventArgs e)
        {
            AddUpdate();
        }

        private void MenuAddUpdate_Click(object sender, RoutedEventArgs e)
        {
            AddUpdate();
        }

        private void AddUpdate()
        {
            if (_vm.SelectedApplication == null) return;

            var dlg = new AddUpdateWindow(_vm.Database, _vm.SelectedApplication)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                int currentId = _vm.SelectedApplication.Id;
                _vm.ApplyFilters();
                _vm.SelectedApplication = _vm.Applications.FirstOrDefault(x => x.Id == currentId);
            }
        }

        private void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            _vm.DeleteCurrentApplication();
        }

        private void MenuDelete_Click(object sender, RoutedEventArgs e)
        {
            _vm.DeleteCurrentApplication();
        }

        private void MenuAttach_Click(object sender, RoutedEventArgs e)
        {
            _vm.AddAttachmentCommand.Execute(null);
        }

        private void MenuCopySpec_Click(object sender, RoutedEventArgs e)
        {
            _vm.CopyJobSpecCommand.Execute(null);
        }

        private void MenuOpenUrl_Click(object sender, RoutedEventArgs e)
        {
            _vm.OpenJobUrlCommand.Execute(null);
        }

        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ReportsWindow(_vm.Database)
            {
                Owner = this
            };
            dlg.ShowDialog();
        }

        private void BtnPrintReport_Click(object sender, RoutedEventArgs e)
        {
            GenerateAndOpenSingleReport(_vm.SelectedApplication);
        }

        private void MenuPrintReport_Click(object sender, RoutedEventArgs e)
        {
            GenerateAndOpenSingleReport(_vm.SelectedApplication);
        }

        private void GenerateAndOpenSingleReport(JobApplication? app)
        {
            if (app == null) return;
            try
            {
                var updates = _vm.Database.GetUpdates(app.Id);
                var attachments = _vm.Database.GetAttachments(app.Id);
                var path = ExportService.GenerateSingleApplicationHtmlReport(app, updates, attachments);
                ExportService.OpenInBrowser(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not generate application report: {ex.Message}", "Report Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SettingsWindow(_vm.Database, _vm.StaleDaysThreshold)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                _vm.StaleDaysThreshold = dlg.UpdatedStaleDays;
                _vm.LoadFilterOptions();
                _vm.ApplyFilters();
            }
            else
            {
                _vm.LoadFilterOptions();
            }
        }

        private void BtnEditAuditItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is ApplicationUpdate update)
            {
                EditAuditUpdate(update);
            }
        }

        private void EditAuditItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is ApplicationUpdate update)
            {
                EditAuditUpdate(update);
            }
        }

        private void EditAuditUpdate(ApplicationUpdate update)
        {
            if (_vm.SelectedApplication == null || update == null) return;

            var dlg = new Views.EditUpdateWindow(_vm.Database, _vm.SelectedApplication, update)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                int currentId = _vm.SelectedApplication.Id;
                _vm.ApplyFilters();
                _vm.SelectedApplication = System.Linq.Enumerable.FirstOrDefault(_vm.Applications, a => a.Id == currentId);
                _vm.LoadSelectedDetails();
            }
        }

        private void BtnCopyAuditItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is ApplicationUpdate update)
            {
                CopyAuditUpdate(update, copyFull: true);
            }
        }

        private void CopyAuditNote_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is ApplicationUpdate update)
            {
                CopyAuditUpdate(update, copyFull: false);
            }
        }

        private void CopyAuditEntry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is ApplicationUpdate update)
            {
                CopyAuditUpdate(update, copyFull: true);
            }
        }

        private void CopyAuditUpdate(ApplicationUpdate update, bool copyFull)
        {
            if (update == null) return;
            string textToCopy;
            if (copyFull)
            {
                var notesPart = !string.IsNullOrWhiteSpace(update.Notes) ? $"\nNotes: {update.Notes}" : "";
                textToCopy = $"[{update.UpdateDateFormatted}] {update.SummaryTitle}{notesPart}";
            }
            else
            {
                textToCopy = !string.IsNullOrWhiteSpace(update.Notes) ? update.Notes : update.SummaryTitle;
            }

            try
            {
                Clipboard.SetText(textToCopy);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not copy to clipboard: {ex.Message}", "Clipboard Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}