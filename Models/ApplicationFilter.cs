using System;

namespace JobAppTracker.Models
{
    public class ApplicationFilter
    {
        public string? SearchText { get; set; }
        public string Status { get; set; } = "All";
        public string Source { get; set; } = "All";
        public string Method { get; set; } = "All";
        public string QuickFilter { get; set; } = "All"; // All, Active, Stale, Final, Interviews
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string DateFilterType { get; set; } = "AppliedDate"; // "AppliedDate" (Created/Applied) or "LastActivity"
        public string? Company { get; set; }
        public string? Agency { get; set; }
        public string SortBy { get; set; } = "LastActivity"; // "LastActivity" or "AppliedDate"
        public bool SortDescending { get; set; } = true;
        public string AuditReportMode { get; set; } = "None"; // "None", "StatusChangesOnly", "Full"
        public bool IncludeAuditTrail 
        { 
            get => AuditReportMode == "Full"; 
            set => AuditReportMode = value ? "Full" : (AuditReportMode == "Full" ? "None" : AuditReportMode); 
        }
    }

    public class ReportSummary
    {
        public int TotalApplications { get; set; }
        public int ActiveApplications { get; set; }
        public int StaleApplications { get; set; }
        public int InterviewCount { get; set; }
        public int OfferCount { get; set; }
        public int AcceptedCount { get; set; }
        public int RejectedCount { get; set; }
        public int WithdrawnCount { get; set; }
        public int CvSentCount { get; set; }
    }

    public class StatusCount
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class SourceCount
    {
        public string Source { get; set; } = string.Empty;
        public int Total { get; set; }
        public int SuccessOrInterview { get; set; }
    }
}
