using System;

namespace JobAppTracker.Models
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public string CurrentVersion { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;
        public string ReleaseName { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string ReleasePageUrl { get; set; } = string.Empty;
        public string? DownloadUrl { get; set; }
        public string? ErrorMessage { get; set; }
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    }
}
