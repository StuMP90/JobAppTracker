using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using JobAppTracker.Data;
using JobAppTracker.Models;

namespace JobAppTracker.Views
{
    public partial class EditUpdateWindow : Window
    {
        private const string NoneStatus = "(None / No status change)";
        private readonly DatabaseService _db;
        private readonly JobApplication _app;
        private readonly ApplicationUpdate _update;

        public EditUpdateWindow(DatabaseService db, JobApplication app, ApplicationUpdate update)
        {
            InitializeComponent();
            _db = db;
            _app = app;
            _update = update;

            TxtAppHeader.Text = $"For: {app.DisplayTitle} at {app.Company}";
            TxtEventDate.Text = $"{update.UpdateDate:yyyy-MM-dd} (locked)";
            TxtNotes.Text = update.Notes;

            // Populate Status dropdown
            var statuses = new List<string> { NoneStatus };
            statuses.AddRange(_db.GetDistinctStatuses());
            CmbStatus.ItemsSource = statuses;

            if (!string.IsNullOrWhiteSpace(update.NewStatus))
            {
                if (!statuses.Contains(update.NewStatus))
                {
                    statuses.Add(update.NewStatus);
                    CmbStatus.ItemsSource = null;
                    CmbStatus.ItemsSource = statuses;
                }
                CmbStatus.SelectedItem = update.NewStatus;
            }
            else if (string.Equals(update.UpdateType, "Created", StringComparison.OrdinalIgnoreCase))
            {
                CmbStatus.SelectedItem = app.CurrentStatus;
            }
            else
            {
                CmbStatus.SelectedItem = NoneStatus;
            }

            // Match UpdateType
            SelectUpdateTypeItem(update.UpdateType);
        }

        private void SelectUpdateTypeItem(string updateType)
        {
            foreach (var obj in CmbUpdateType.Items)
            {
                if (obj is ComboBoxItem item)
                {
                    var text = item.Content?.ToString() ?? string.Empty;
                    if (updateType == "StatusChange" && text == "Status Change")
                    {
                        CmbUpdateType.SelectedItem = item;
                        return;
                    }
                    if (updateType == "Interview" && text.StartsWith("Interview"))
                    {
                        CmbUpdateType.SelectedItem = item;
                        return;
                    }
                    if (updateType == "FollowUp" && text.StartsWith("Follow-up"))
                    {
                        CmbUpdateType.SelectedItem = item;
                        return;
                    }
                    if (string.Equals(updateType, text, StringComparison.OrdinalIgnoreCase))
                    {
                        CmbUpdateType.SelectedItem = item;
                        return;
                    }
                }
            }

            // Fallback
            CmbUpdateType.SelectedIndex = 0;
        }

        private void CmbUpdateType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbUpdateType.SelectedItem is ComboBoxItem item)
            {
                var text = item.Content?.ToString();
                if (text == "Status Change" && Equals(CmbStatus.SelectedItem, NoneStatus))
                {
                    CmbStatus.SelectedItem = _app.CurrentStatus;
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var notes = TxtNotes.Text.Trim();
            if (string.IsNullOrWhiteSpace(notes))
            {
                MessageBox.Show("Please enter notes or details describing this audit event.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtNotes.Focus();
                return;
            }

            var typeItem = (ComboBoxItem)CmbUpdateType.SelectedItem;
            var typeText = typeItem?.Content?.ToString() ?? "Note";
            string updateType = typeText switch
            {
                "Status Change" => "StatusChange",
                "Created" => "Created",
                "Interview Scheduled / Held" => "Interview",
                "Follow-up Sent" => "FollowUp",
                _ => "Note"
            };

            string? selectedStatus = CmbStatus.SelectedItem?.ToString();
            if (selectedStatus == NoneStatus || string.IsNullOrWhiteSpace(selectedStatus))
            {
                selectedStatus = null;
            }

            // If user explicitly chose 'Status Change' type but status is None, prompt
            if (updateType == "StatusChange" && string.IsNullOrWhiteSpace(selectedStatus))
            {
                MessageBox.Show("Please select an associated status for this Status Change event, or change the event type.", "Status Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                CmbStatus.Focus();
                return;
            }

            _db.UpdateApplicationUpdate(_update.Id, updateType, selectedStatus, notes);

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
