using System;

namespace JobAppTracker.Models
{
    public class JobApplication
    {
        public int Id { get; set; }
        public DateTime AppliedDate { get; set; } = DateTime.Today;
        public string JobTitle { get; set; } = string.Empty;
        public bool IsCvToAgency { get; set; }
        public string Company { get; set; } = string.Empty;
        public string? Agency { get; set; }
        public string Source { get; set; } = "LinkedIn";
        public string ApplicationMethod { get; set; } = "Direct";
        public string? JobSpecText { get; set; }
        public string CurrentStatus { get; set; } = "Applied";
        public bool IsFinal { get; set; }
        public DateTime LastUpdatedDate { get; set; } = DateTime.Today;
        public string? SalaryOrRate { get; set; }
        public string? Location { get; set; }
        public string? JobUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // UI & Computed helper properties
        public int AttachmentCount { get; set; }
        public int UpdateCount { get; set; }
        public System.Collections.Generic.List<ApplicationUpdate> AuditTrail { get; set; } = new();
        public bool HasAuditTrail => AuditTrail.Count > 0;

        public int DaysSinceLastUpdate => Math.Max(0, (DateTime.Today - LastUpdatedDate.Date).Days);

        public bool IsStale => !IsFinal && DaysSinceLastUpdate >= 14;

        public string StalenessText
        {
            get
            {
                if (IsFinal) return "Closed / Final";
                if (DaysSinceLastUpdate == 0) return "Updated today";
                if (DaysSinceLastUpdate == 1) return "Updated yesterday";
                if (DaysSinceLastUpdate >= 14) return $"⚠️ Stale ({DaysSinceLastUpdate}d inactive)";
                return $"{DaysSinceLastUpdate}d ago";
            }
        }

        public string AppliedDateFormatted => AppliedDate.ToString("yyyy-MM-dd");
        public string LastUpdatedFormatted => LastUpdatedDate.ToString("yyyy-MM-dd");

        public string DisplayTitle => IsCvToAgency ? $"📄 CV sent to {Agency ?? Company}" : JobTitle;

        public string DisplayCompanyAgency
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Agency) && !string.IsNullOrWhiteSpace(Company))
                {
                    return $"{Company} (via {Agency})";
                }
                return !string.IsNullOrWhiteSpace(Company) ? Company : (Agency ?? string.Empty);
            }
        }

        // Short status changes & dates helper (without full text details)
        public System.Collections.Generic.List<ApplicationUpdate> StatusHistoryUpdates
        {
            get
            {
                if (AuditTrail == null || AuditTrail.Count == 0) return new();

                // Sort chronologically: by UpdateDate ASC, then CreatedAt ASC, then Id ASC
                var sorted = System.Linq.Enumerable.ToList(
                    System.Linq.Enumerable.ThenBy(
                        System.Linq.Enumerable.ThenBy(
                            System.Linq.Enumerable.OrderBy(AuditTrail, u => u.UpdateDate),
                            u => u.CreatedAt
                        ),
                        u => u.Id
                    )
                );

                var result = new System.Collections.Generic.List<ApplicationUpdate>();
                string? lastStatus = null;

                foreach (var u in sorted)
                {
                    // Determine the status associated with this update:
                    // 1. NewStatus if specified
                    // 2. Or for 'Created' update, fall back to PreviousStatus or CurrentStatus
                    string? status = !string.IsNullOrWhiteSpace(u.NewStatus)
                        ? u.NewStatus
                        : (string.Equals(u.UpdateType, "Created", StringComparison.OrdinalIgnoreCase)
                            ? (!string.IsNullOrWhiteSpace(u.PreviousStatus) ? u.PreviousStatus : CurrentStatus)
                            : null);

                    if (string.IsNullOrWhiteSpace(status)) continue;

                    // Deduplicate consecutive identical statuses
                    if (!string.Equals(status, lastStatus, StringComparison.OrdinalIgnoreCase))
                    {
                        // Ensure NewStatus is populated so badges/labels display cleanly
                        if (string.IsNullOrWhiteSpace(u.NewStatus))
                        {
                            u.NewStatus = status;
                        }

                        result.Add(u);
                        lastStatus = status;
                    }
                }

                // If no status updates were captured, fall back to initial status
                if (result.Count == 0 && !string.IsNullOrWhiteSpace(CurrentStatus))
                {
                    result.Add(new ApplicationUpdate
                    {
                        ApplicationId = Id,
                        UpdateDate = AppliedDate,
                        UpdateType = "Created",
                        NewStatus = CurrentStatus,
                        CreatedAt = CreatedAt
                    });
                }

                return result;
            }
        }

        public string StatusHistorySummaryText
        {
            get
            {
                var list = StatusHistoryUpdates;
                if (list.Count == 0)
                {
                    return $"{AppliedDateFormatted}: {CurrentStatus}";
                }
                var entries = System.Linq.Enumerable.Select(list, u => $"{u.UpdateDateFormatted}: {u.NewStatus ?? u.SummaryTitle}");
                return string.Join(" ➔ ", entries);
            }
        }

        // Color helper for modern UI cards/badges
        public string StatusBadgeColor
        {
            get
            {
                return CurrentStatus switch
                {
                    "Applied" => "#2563EB",            // Blue
                    "CV Sent" => "#0284C7",            // Sky Blue
                    "Screening" => "#7C3AED",          // Purple
                    "1st Interview" => "#D97706",      // Amber
                    "2nd Interview" => "#D97706",      // Amber
                    "Final Interview" => "#EA580C",    // Orange
                    "Offer Received" => "#059669",     // Emerald
                    "Accepted" => "#16A34A",           // Green
                    "Rejected" => "#DC2626",           // Red
                    "Withdrawn" or "Closed" or "Closed due to inactivity" => "#64748B", // Slate gray
                    "Discussed, bad fit" => "#8B5CF6", // Violet
                    "Employer changed role" => "#B45309", // Dark amber
                    "Role Cancelled / On Hold" => "#6B7280", // Gray
                    _ => "#4B5563"
                };
            }
        }
    }
}
