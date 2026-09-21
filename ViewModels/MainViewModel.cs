using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using JobAppTracker.Data;
using JobAppTracker.Models;
using JobAppTracker.Services;

namespace JobAppTracker.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private JobApplication? _selectedApplication;
        private string _searchText = string.Empty;
        private string _selectedStatus = "All";
        private string _selectedSource = "All";
        private string _selectedMethod = "All";
        private string _activeQuickFilter = "All"; // All, Active, Stale, Interviews, Final
        private int _staleDaysThreshold = 14;
        private ReportSummary _summary = new();

        public DatabaseService Database => _db;

        public ObservableCollection<JobApplication> Applications { get; } = new();
        public ObservableCollection<ApplicationUpdate> AuditUpdates { get; } = new();
        public ObservableCollection<Attachment> Attachments { get; } = new();

        public List<string> StatusOptions { get; private set; } = new();
        public List<string> SourceOptions { get; private set; } = new();
        public List<string> MethodOptions { get; private set; } = new();
        public List<string> AgencyOptions { get; private set; } = new();

        public JobApplication? SelectedApplication
        {
            get => _selectedApplication;
            set
            {
                if (SetField(ref _selectedApplication, value))
                {
                    LoadSelectedDetails();
                    OnPropertyChanged(nameof(HasSelectedApplication));
                }
            }
        }

        public bool HasSelectedApplication => SelectedApplication != null;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetField(ref _searchText, value))
                {
                    ApplyFilters();
                }
            }
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (SetField(ref _selectedStatus, value))
                {
                    ApplyFilters();
                }
            }
        }

        public string SelectedSource
        {
            get => _selectedSource;
            set
            {
                if (SetField(ref _selectedSource, value))
                {
                    ApplyFilters();
                }
            }
        }

        public string SelectedMethod
        {
            get => _selectedMethod;
            set
            {
                if (SetField(ref _selectedMethod, value))
                {
                    ApplyFilters();
                }
            }
        }

        private string _selectedAgency = "All";
        public string SelectedAgency
        {
            get => _selectedAgency;
            set
            {
                if (SetField(ref _selectedAgency, value))
                {
                    ApplyFilters();
                }
            }
        }

        public string ActiveQuickFilter
        {
            get => _activeQuickFilter;
            set
            {
                if (SetField(ref _activeQuickFilter, value))
                {
                    ApplyFilters();
                }
            }
        }

        private string _selectedSortSequence = "Last Activity (Recent First)";
        public List<string> SortOptions { get; } = new()
        {
            "Last Activity (Recent First)",
            "Last Activity (Oldest First)",
            "Created Date (Recent First)",
            "Created Date (Oldest First)"
        };

        public string SelectedSortSequence
        {
            get => _selectedSortSequence;
            set
            {
                if (SetField(ref _selectedSortSequence, value))
                {
                    ApplyFilters();
                }
            }
        }

        public int StaleDaysThreshold
        {
            get => _staleDaysThreshold;
            set
            {
                if (SetField(ref _staleDaysThreshold, value))
                {
                    _db.SetSetting("StaleDaysThreshold", value.ToString());
                    ApplyFilters();
                }
            }
        }

        public ReportSummary Summary
        {
            get => _summary;
            set => SetField(ref _summary, value);
        }

        // Commands
        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand SetQuickFilterCommand { get; }
        public ICommand OpenAttachmentCommand { get; }
        public ICommand OpenAttachmentFolderCommand { get; }
        public ICommand DeleteAttachmentCommand { get; }
        public ICommand AddAttachmentCommand { get; }
        public ICommand CopyJobSpecCommand { get; }
        public ICommand OpenJobUrlCommand { get; }
        public ICommand ExportHtmlCommand { get; }
        public ICommand ExportCsvCommand { get; }

        public MainViewModel(DatabaseService? db = null)
        {
            _db = db ?? new DatabaseService();

            if (int.TryParse(_db.GetSetting("StaleDaysThreshold", "14"), out int savedDays))
            {
                _staleDaysThreshold = savedDays;
            }

            RefreshCommand = new RelayCommand(() => ApplyFilters());
            ClearFiltersCommand = new RelayCommand(ClearFilters);
            SetQuickFilterCommand = new RelayCommand(param =>
            {
                if (param is string filter)
                {
                    ActiveQuickFilter = filter;
                }
            });

            OpenAttachmentCommand = new RelayCommand(param =>
            {
                if (param is Attachment att)
                {
                    try
                    {
                        AttachmentService.OpenFile(att);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to open attachment: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            });

            OpenAttachmentFolderCommand = new RelayCommand(param =>
            {
                if (param is Attachment att)
                {
                    AttachmentService.OpenFileLocation(att);
                }
            });

            DeleteAttachmentCommand = new RelayCommand(param =>
            {
                if (param is Attachment att)
                {
                    var res = MessageBox.Show($"Are you sure you want to remove attachment '{att.FileName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res == MessageBoxResult.Yes)
                    {
                        _db.DeleteAttachment(att.Id);
                        Attachments.Remove(att);
                        if (SelectedApplication != null)
                        {
                            SelectedApplication.AttachmentCount = Math.Max(0, SelectedApplication.AttachmentCount - 1);
                        }
                    }
                }
            });

            AddAttachmentCommand = new RelayCommand(() =>
            {
                if (SelectedApplication == null) return;
                var dlg = new OpenFileDialog
                {
                    Title = "Select File to Attach (PDF, Spec, Docx, etc.)",
                    Filter = "Documents (*.pdf;*.docx;*.doc;*.txt;*.rtf)|*.pdf;*.docx;*.doc;*.txt;*.rtf|All Files (*.*)|*.*",
                    Multiselect = true
                };
                if (dlg.ShowDialog() == true)
                {
                    foreach (var file in dlg.FileNames)
                    {
                        var att = _db.AddAttachment(SelectedApplication.Id, file);
                        Attachments.Insert(0, att);
                    }
                    SelectedApplication.AttachmentCount += dlg.FileNames.Length;
                    LoadSelectedDetails(); // Refresh audit trail for attachment event
                }
            });

            CopyJobSpecCommand = new RelayCommand(() =>
            {
                if (!string.IsNullOrWhiteSpace(SelectedApplication?.JobSpecText))
                {
                    Clipboard.SetText(SelectedApplication.JobSpecText);
                    MessageBox.Show("Job spec copied to clipboard!", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });

            OpenJobUrlCommand = new RelayCommand(() =>
            {
                if (!string.IsNullOrWhiteSpace(SelectedApplication?.JobUrl))
                {
                    try
                    {
                        AttachmentService.OpenUrl(SelectedApplication.JobUrl);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Could not open URL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            });

            ExportHtmlCommand = new RelayCommand(() =>
            {
                try
                {
                    var filter = GetCurrentFilter();
                    var list = _db.GetApplications(filter, StaleDaysThreshold);
                    var overallSummary = _db.GetReportSummary(null, StaleDaysThreshold);
                    var reportPath = ExportService.GenerateHtmlReport(list, Summary, filter, overallSummary: overallSummary);
                    ExportService.OpenInBrowser(reportPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error generating report: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });

            ExportCsvCommand = new RelayCommand(() =>
            {
                try
                {
                    var dlg = new SaveFileDialog
                    {
                        Title = "Export Applications to CSV",
                        Filter = "CSV Files (*.csv)|*.csv",
                        FileName = $"JobApplications_{DateTime.Now:yyyyMMdd}.csv"
                    };
                    if (dlg.ShowDialog() == true)
                    {
                        var filter = GetCurrentFilter();
                        var list = _db.GetApplications(filter, StaleDaysThreshold);
                        ExportService.ExportToCsv(list, dlg.FileName);
                        MessageBox.Show($"Exported {list.Count} applications to:\n{dlg.FileName}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting CSV: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });

            LoadFilterOptions();
            ApplyFilters();
        }

        public void LoadFilterOptions()
        {
            var statuses = new List<string> { "All", "All except closed/complete" };
            statuses.AddRange(_db.GetDistinctStatuses());
            StatusOptions = statuses;
            OnPropertyChanged(nameof(StatusOptions));

            var sources = new List<string> { "All" };
            sources.AddRange(_db.GetDistinctSources());
            SourceOptions = sources;
            OnPropertyChanged(nameof(SourceOptions));

            var methods = new List<string> { "All" };
            methods.AddRange(_db.GetDistinctMethods());
            MethodOptions = methods;
            OnPropertyChanged(nameof(MethodOptions));

            var agencies = new List<string> { "All" };
            agencies.AddRange(_db.GetDistinctAgencies());
            AgencyOptions = agencies;
            OnPropertyChanged(nameof(AgencyOptions));
        }

        public ApplicationFilter GetCurrentFilter()
        {
            var filter = new ApplicationFilter
            {
                SearchText = SearchText,
                Status = SelectedStatus,
                Source = SelectedSource,
                Method = SelectedMethod,
                Agency = SelectedAgency,
                QuickFilter = ActiveQuickFilter
            };

            switch (SelectedSortSequence)
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

        public void ApplyFilters()
        {
            var filter = GetCurrentFilter();
            var list = _db.GetApplications(filter, StaleDaysThreshold);

            Applications.Clear();
            foreach (var item in list)
            {
                Applications.Add(item);
            }

            Summary = _db.GetReportSummary(filter, StaleDaysThreshold);

            // Re-select or select first if list not empty
            if (SelectedApplication != null)
            {
                var match = Applications.FirstOrDefault(x => x.Id == SelectedApplication.Id);
                SelectedApplication = match ?? Applications.FirstOrDefault();
            }
            else
            {
                SelectedApplication = Applications.FirstOrDefault();
            }
        }

        public void ClearFilters()
        {
            _searchText = string.Empty;
            _selectedStatus = "All";
            _selectedSource = "All";
            _selectedMethod = "All";
            _selectedAgency = "All";
            _activeQuickFilter = "All";
            _selectedSortSequence = "Last Activity (Recent First)";

            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SelectedStatus));
            OnPropertyChanged(nameof(SelectedSource));
            OnPropertyChanged(nameof(SelectedMethod));
            OnPropertyChanged(nameof(SelectedAgency));
            OnPropertyChanged(nameof(ActiveQuickFilter));
            OnPropertyChanged(nameof(SelectedSortSequence));

            ApplyFilters();
        }

        public void LoadSelectedDetails()
        {
            AuditUpdates.Clear();
            Attachments.Clear();

            if (SelectedApplication == null) return;

            var updates = _db.GetUpdates(SelectedApplication.Id);
            foreach (var u in updates)
            {
                AuditUpdates.Add(u);
            }

            var atts = _db.GetAttachments(SelectedApplication.Id);
            foreach (var a in atts)
            {
                Attachments.Add(a);
            }
        }

        public void DeleteCurrentApplication()
        {
            if (SelectedApplication == null) return;

            var title = SelectedApplication.DisplayTitle;
            var res = MessageBox.Show($"Are you sure you want to permanently delete application for '{title}' at '{SelectedApplication.Company}'?\n\nThis will also delete its audit history and attachments.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (res == MessageBoxResult.Yes)
            {
                _db.DeleteApplication(SelectedApplication.Id);
                ApplyFilters();
            }
        }
    }
}
