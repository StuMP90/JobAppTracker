using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using JobAppTracker.Data;
using JobAppTracker.Models;
using JobAppTracker.Services;

namespace JobAppTracker.Views
{
    public partial class ReportsWindow : Window
    {
        private readonly DatabaseService _db;
        private List<JobApplication> _currentList = new();
        private ReportSummary _currentSummary = new();
        private bool _isInitialized = false;

        public ReportsWindow(DatabaseService db)
        {
            InitializeComponent();
            _db = db;

            // Load filters
            var statuses = new List<string> { "All", "All except closed/complete" };
            statuses.AddRange(_db.GetDistinctStatuses());
            CmbStatus.ItemsSource = statuses;
            CmbStatus.SelectedIndex = 0;

            var sources = new List<string> { "All" };
            sources.AddRange(_db.GetDistinctSources());
            CmbSource.ItemsSource = sources;
            CmbSource.SelectedIndex = 0;

            var agencies = new List<string> { "All" };
            agencies.AddRange(_db.GetDistinctAgencies());
            CmbAgency.ItemsSource = agencies;
            CmbAgency.SelectedIndex = 0;

            CmbSort.ItemsSource = new List<string>
            {
                "Last Activity (Recent First)",
                "Last Activity (Oldest First)",
                "Created Date (Recent First)",
                "Created Date (Oldest First)"
            };
            CmbSort.SelectedIndex = 0;

            CmbDateType.ItemsSource = new List<string>
            {
                "Applied / Created Date",
                "Last Activity Date"
            };
            CmbDateType.SelectedIndex = 0;

            _isInitialized = true;
            RefreshReport();
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (!_isInitialized) return;
            RefreshReport();
        }

        private void HistoryMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            RefreshReport();
        }

        private void RefreshReport()
        {
            var filter = BuildFilter();
            _currentList = _db.GetApplications(filter);
            _currentSummary = _db.GetReportSummary(filter);

            GridReport.RowDetailsVisibilityMode = (filter.AuditReportMode != "None")
                ? DataGridRowDetailsVisibilityMode.Visible 
                : DataGridRowDetailsVisibilityMode.VisibleWhenSelected;

            GridReport.ItemsSource = _currentList;

            TxtTotalCount.Text = _currentSummary.TotalApplications.ToString();
            TxtActiveCount.Text = _currentSummary.ActiveApplications.ToString();
            TxtStaleCount.Text = _currentSummary.StaleApplications.ToString();
            TxtInterviewCount.Text = (_currentSummary.InterviewCount + _currentSummary.OfferCount).ToString();
            TxtFinalCount.Text = (_currentSummary.AcceptedCount + _currentSummary.RejectedCount + _currentSummary.WithdrawnCount).ToString();

            TxtFooterStatus.Text = $"Showing {_currentList.Count} application(s) matching current criteria";
        }

        private void GridReport_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridReport.SelectedItem is JobApplication selectedApp && selectedApp.AuditTrail.Count == 0 && selectedApp.Id > 0)
            {
                selectedApp.AuditTrail = _db.GetUpdates(selectedApp.Id);
            }
        }

        private ApplicationFilter BuildFilter()
        {
            string historyMode = "None";
            if (RbHistoryStatusOnly != null && RbHistoryStatusOnly.IsChecked == true)
            {
                historyMode = "StatusChangesOnly";
            }
            else if (RbHistoryFull != null && RbHistoryFull.IsChecked == true)
            {
                historyMode = "Full";
            }

            string dateType = "AppliedDate";
            if (CmbDateType?.SelectedItem?.ToString() == "Last Activity Date")
            {
                dateType = "LastActivity";
            }

            var filter = new ApplicationFilter
            {
                SearchText = TxtSearch.Text?.Trim(),
                Status = CmbStatus.SelectedItem?.ToString() ?? "All",
                Agency = CmbAgency.SelectedItem?.ToString() ?? "All",
                Source = CmbSource.SelectedItem?.ToString() ?? "All",
                DateFilterType = dateType,
                FromDate = DpFromDate.SelectedDate,
                ToDate = DpToDate.SelectedDate,
                AuditReportMode = historyMode
            };

            var sort = CmbSort.SelectedItem?.ToString();
            switch (sort)
            {
                case "Last Activity (Oldest First)":
                    filter.SortBy = "LastActivity";
                    filter.SortDescending = false;
                    break;
                case "Created Date (Recent First)":
                    filter.SortBy = "AppliedDate";
                    filter.SortDescending = true;
                    break;
                case "Created Date (Oldest First)":
                    filter.SortBy = "AppliedDate";
                    filter.SortDescending = false;
                    break;
                default:
                    filter.SortBy = "LastActivity";
                    filter.SortDescending = true;
                    break;
            }

            return filter;
        }

        private void BtnResetFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = string.Empty;
            CmbStatus.SelectedIndex = 0;
            CmbAgency.SelectedIndex = 0;
            CmbSource.SelectedIndex = 0;
            CmbSort.SelectedIndex = 0;
            if (CmbDateType != null) CmbDateType.SelectedIndex = 0;
            DpFromDate.SelectedDate = null;
            DpToDate.SelectedDate = null;
            if (RbHistoryNone != null) RbHistoryNone.IsChecked = true;
            RefreshReport();
        }

        private void BtnExportHtml_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var filter = BuildFilter();
                var path = ExportService.GenerateHtmlReport(_currentList, _currentSummary, filter);
                ExportService.OpenInBrowser(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not export HTML report: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new SaveFileDialog
                {
                    Title = "Export Report to CSV",
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = $"JobReport_{DateTime.Now:yyyyMMdd}.csv"
                };
                if (dlg.ShowDialog() == true)
                {
                    string historyMode = "None";
                    if (RbHistoryStatusOnly != null && RbHistoryStatusOnly.IsChecked == true) historyMode = "StatusChangesOnly";
                    else if (RbHistoryFull != null && RbHistoryFull.IsChecked == true) historyMode = "Full";

                    ExportService.ExportToCsv(_currentList, dlg.FileName, historyMode);
                    MessageBox.Show($"Exported {_currentList.Count} items to:\n{dlg.FileName}", "Export Completed", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not export CSV: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCopySummary_Click(object sender, RoutedEventArgs e)
        {
            var filter = BuildFilter();
            var sb = new StringBuilder();
            sb.AppendLine("=== Job Applications Summary Report ===");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Total Tracked: {_currentSummary.TotalApplications}");
            sb.AppendLine($"Active / In-Progress: {_currentSummary.ActiveApplications}");
            sb.AppendLine($"Stale (14d+ inactive): {_currentSummary.StaleApplications}");
            sb.AppendLine($"Interviews: {_currentSummary.InterviewCount}");
            sb.AppendLine($"Offers: {_currentSummary.OfferCount}");
            sb.AppendLine($"Accepted: {_currentSummary.AcceptedCount}");
            sb.AppendLine($"Rejected / Closed: {_currentSummary.RejectedCount + _currentSummary.WithdrawnCount}");
            sb.AppendLine();
            sb.AppendLine("Applications:");

            foreach (var a in _currentList)
            {
                var salaryText = !string.IsNullOrWhiteSpace(a.SalaryOrRate) ? $" | Salary: {a.SalaryOrRate}" : "";
                sb.AppendLine($"- [{a.AppliedDateFormatted}] {a.DisplayTitle} at {a.Company}{salaryText} | Status: {a.CurrentStatus} | Source: {a.Source} | Last Activity: {a.StalenessText}");
                
                if (filter.AuditReportMode == "StatusChangesOnly")
                {
                    sb.AppendLine($"    • Status History: {a.StatusHistorySummaryText}");
                }
                else if (filter.AuditReportMode == "Full" && a.AuditTrail != null && a.AuditTrail.Count > 0)
                {
                    foreach (var u in a.AuditTrail)
                    {
                        var noteSnippet = string.IsNullOrWhiteSpace(u.Notes) ? "" : $" - {u.Notes}";
                        sb.AppendLine($"    • {u.UpdateDateFormatted} [{u.SummaryTitle}]{noteSnippet}");
                    }
                }
            }

            Clipboard.SetText(sb.ToString());
            MessageBox.Show("Summary copied to clipboard!", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnSingleReport_Click(object sender, RoutedEventArgs e)
        {
            OpenSelectedApplicationReport();
        }

        private void MenuSingleReport_Click(object sender, RoutedEventArgs e)
        {
            OpenSelectedApplicationReport();
        }

        private void GridReport_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (GridReport.SelectedItem is JobApplication)
            {
                OpenSelectedApplicationReport();
            }
        }

        private void OpenSelectedApplicationReport()
        {
            if (GridReport.SelectedItem is not JobApplication app)
            {
                MessageBox.Show("Please select an application from the table first.", "Select Application", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var updates = _db.GetUpdates(app.Id);
                var attachments = _db.GetAttachments(app.Id);
                var path = ExportService.GenerateSingleApplicationHtmlReport(app, updates, attachments);
                ExportService.OpenInBrowser(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not generate application report: {ex.Message}", "Report Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
