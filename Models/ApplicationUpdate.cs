using System;

namespace JobAppTracker.Models
{
    public class ApplicationUpdate
    {
        public int Id { get; set; }
        public int ApplicationId { get; set; }
        public DateTime UpdateDate { get; set; } = DateTime.Today;
        public string UpdateType { get; set; } = "Note"; // StatusChange, Note, Interview, FollowUp, FileAttached, Created
        public string? PreviousStatus { get; set; }
        public string? NewStatus { get; set; }
        public string Notes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string UpdateDateFormatted => UpdateDate.ToString("yyyy-MM-dd");

        public string SummaryTitle
        {
            get
            {
                if (UpdateType == "StatusChange" && !string.IsNullOrWhiteSpace(NewStatus))
                {
                    return string.IsNullOrWhiteSpace(PreviousStatus) 
                        ? $"Status set to {NewStatus}" 
                        : $"Status changed: {PreviousStatus} ➔ {NewStatus}";
                }
                return UpdateType;
            }
        }

        public string TypeIcon
        {
            get
            {
                return UpdateType switch
                {
                    "StatusChange" => "🔄",
                    "Interview" => "📅",
                    "FollowUp" => "✉️",
                    "FileAttached" => "📎",
                    "Created" => "✨",
                    _ => "📝"
                };
            }
        }
    }
}
