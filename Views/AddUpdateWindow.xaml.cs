using System;
using System.Windows;
using System.Windows.Controls;
using JobAppTracker.Data;
using JobAppTracker.Models;

namespace JobAppTracker.Views
{
    public partial class AddUpdateWindow : Window
    {
        private readonly DatabaseService _db;
        private readonly JobApplication _app;

        public AddUpdateWindow(DatabaseService db, JobApplication app)
        {
            InitializeComponent();
            _db = db;
            _app = app;

            TxtAppHeader.Text = $"For: {app.DisplayTitle} at {app.Company}";
            DpUpdateDate.SelectedDate = DateTime.Today;

            var statuses = _db.GetDistinctStatuses();
            CmbNewStatus.ItemsSource = statuses;
            CmbNewStatus.SelectedItem = app.CurrentStatus;
        }

        private void CmbUpdateType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbUpdateType.SelectedItem is ComboBoxItem item)
            {
                var text = item.Content.ToString();
                if (text == "Interview Scheduled / Held" && CmbNewStatus.Text == "Applied")
                {
                    CmbNewStatus.SelectedItem = "1st Interview";
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!DpAppliedDateValid()) return;

            var notes = TxtNotes.Text.Trim();
            if (string.IsNullOrWhiteSpace(notes))
            {
                MessageBox.Show("Please enter notes or details describing this update.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtNotes.Focus();
                return;
            }

            var typeItem = (ComboBoxItem)CmbUpdateType.SelectedItem;
            var updateType = typeItem?.Content?.ToString() ?? "Note";
            var newStatus = CmbNewStatus.SelectedItem?.ToString();
            var updateDate = DpUpdateDate.SelectedDate ?? DateTime.Today;

            _db.AddApplicationUpdate(_app.Id, updateDate, updateType, newStatus, notes);

            DialogResult = true;
            Close();
        }

        private bool DpAppliedDateValid()
        {
            if (!DpUpdateDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Please select an update date.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                DpUpdateDate.Focus();
                return false;
            }
            return true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
