using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using JobAppTracker.Data;
using JobAppTracker.Models;
using JobAppTracker.Services;

namespace JobAppTracker.Views
{
    public partial class ApplicationEditWindow : Window
    {
        private readonly DatabaseService _db;
        private readonly JobApplication? _originalApp;
        private readonly List<string> _selectedFilePaths = new();

        public JobApplication? SavedApplication { get; private set; }

        public ApplicationEditWindow(DatabaseService db, JobApplication? existingApp = null, bool defaultAsCvToAgency = false)
        {
            InitializeComponent();
            _db = db;
            _originalApp = existingApp;

            // Populate combo options
            CmbSource.ItemsSource = _db.GetDistinctSources();
            CmbMethod.ItemsSource = _db.GetDistinctMethods();
            CmbStatus.ItemsSource = _db.GetDistinctStatuses();

            if (existingApp != null)
            {
                TxtHeader.Text = $"✏️ Edit Application #{existingApp.Id}";
                DpAppliedDate.SelectedDate = existingApp.AppliedDate;
                TxtJobTitle.Text = existingApp.JobTitle;
                ChkCvToAgency.IsChecked = existingApp.IsCvToAgency;
                TxtCompany.Text = existingApp.Company;
                TxtAgency.Text = existingApp.Agency ?? string.Empty;
                CmbSource.Text = existingApp.Source;
                CmbMethod.Text = existingApp.ApplicationMethod;
                CmbStatus.Text = existingApp.CurrentStatus;
                TxtSalary.Text = existingApp.SalaryOrRate ?? string.Empty;
                TxtLocation.Text = existingApp.Location ?? string.Empty;
                TxtJobUrl.Text = existingApp.JobUrl ?? string.Empty;
                TxtJobSpec.Text = existingApp.JobSpecText ?? string.Empty;

                PnlInitialNotes.Visibility = Visibility.Collapsed;
                BtnViewReport.Visibility = Visibility.Visible;
            }
            else
            {
                DpAppliedDate.SelectedDate = DateTime.Today;
                CmbSource.Text = "LinkedIn";
                CmbMethod.Text = "Direct";
                CmbStatus.Text = "Applied";

                if (defaultAsCvToAgency)
                {
                    ChkCvToAgency.IsChecked = true;
                    TxtJobTitle.Text = "CV";
                    CmbMethod.Text = "Agency";
                    CmbStatus.Text = "CV Sent";
                    TxtInitialNote.Text = "Speculative CV sent to recruitment agency.";
                }
            }
        }

        private void ChkCvToAgency_Checked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtJobTitle.Text) || TxtJobTitle.Text == "Job Title")
            {
                TxtJobTitle.Text = "CV";
            }
            if (CmbMethod.Text == "Direct")
            {
                CmbMethod.Text = "Agency";
            }
            if (CmbStatus.Text == "Applied")
            {
                CmbStatus.Text = "CV Sent";
            }
        }

        private void ChkCvToAgency_Unchecked(object sender, RoutedEventArgs e)
        {
            if (TxtJobTitle.Text == "CV")
            {
                TxtJobTitle.Text = string.Empty;
            }
        }

        private void BtnAddSource_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AddSourceDialog { Owner = this };
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.SourceResult))
            {
                _db.AddSource(dlg.SourceResult);
                var sources = _db.GetDistinctSources();
                CmbSource.ItemsSource = sources;
                CmbSource.Text = dlg.SourceResult;
            }
        }

        private void TxtJobSpec_TextChanged(object sender, TextChangedEventArgs e)
        {
            int count = TxtJobSpec.Text?.Length ?? 0;
            TxtSpecCount.Text = $"{count:N0} characters";
        }

        private void BtnAddFiles_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Job Spec or Document to Attach",
                Filter = "Documents (*.pdf;*.docx;*.doc;*.txt;*.rtf)|*.pdf;*.docx;*.doc;*.txt;*.rtf|All Files (*.*)|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
            {
                foreach (var file in dlg.FileNames)
                {
                    if (!_selectedFilePaths.Contains(file))
                    {
                        _selectedFilePaths.Add(file);
                    }
                }
                LstFiles.ItemsSource = null;
                LstFiles.ItemsSource = _selectedFilePaths;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!DpAppliedDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Please select an application date.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                DpAppliedDate.Focus();
                return;
            }

            var jobTitle = TxtJobTitle.Text.Trim();
            if (string.IsNullOrWhiteSpace(jobTitle))
            {
                MessageBox.Show("Please specify a Job Title (or 'CV' for agency submissions).", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtJobTitle.Focus();
                return;
            }

            var company = TxtCompany.Text.Trim();
            if (string.IsNullOrWhiteSpace(company))
            {
                MessageBox.Show("Please specify the Company / Employer (or Agency name if speculative).", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtCompany.Focus();
                return;
            }

            var source = string.IsNullOrWhiteSpace(CmbSource.Text) ? "Other" : CmbSource.Text.Trim();
            var method = string.IsNullOrWhiteSpace(CmbMethod.Text) ? "Direct" : CmbMethod.Text.Trim();
            var status = string.IsNullOrWhiteSpace(CmbStatus.Text) ? "Applied" : CmbStatus.Text.Trim();

            var app = _originalApp ?? new JobApplication();
            app.AppliedDate = DpAppliedDate.SelectedDate.Value;
            app.JobTitle = jobTitle;
            app.IsCvToAgency = ChkCvToAgency.IsChecked == true;
            app.Company = company;
            app.Agency = string.IsNullOrWhiteSpace(TxtAgency.Text) ? null : TxtAgency.Text.Trim();
            app.Source = source;
            app.ApplicationMethod = method;
            app.CurrentStatus = status;
            app.SalaryOrRate = string.IsNullOrWhiteSpace(TxtSalary.Text) ? null : TxtSalary.Text.Trim();
            app.Location = string.IsNullOrWhiteSpace(TxtLocation.Text) ? null : TxtLocation.Text.Trim();
            app.JobUrl = string.IsNullOrWhiteSpace(TxtJobUrl.Text) ? null : TxtJobUrl.Text.Trim();
            app.JobSpecText = string.IsNullOrWhiteSpace(TxtJobSpec.Text) ? null : TxtJobSpec.Text;

            var initialNote = TxtInitialNote.Text.Trim();
            int newId = _db.SaveApplication(app, initialNote, _selectedFilePaths);
            app.Id = newId;
            SavedApplication = app;

            DialogResult = true;
            Close();
        }

        private void BtnViewReport_Click(object sender, RoutedEventArgs e)
        {
            if (_originalApp == null) return;
            try
            {
                var updates = _db.GetUpdates(_originalApp.Id);
                var attachments = _db.GetAttachments(_originalApp.Id);
                var path = ExportService.GenerateSingleApplicationHtmlReport(_originalApp, updates, attachments);
                ExportService.OpenInBrowser(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not generate application report: {ex.Message}", "Report Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
