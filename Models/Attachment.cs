using System;
using System.IO;

namespace JobAppTracker.Models
{
    public class Attachment
    {
        public int Id { get; set; }
        public int ApplicationId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string StoredPath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime AttachedDate { get; set; } = DateTime.Now;

        public string Extension => Path.GetExtension(FileName).ToLowerInvariant();

        public string FileIcon
        {
            get
            {
                return Extension switch
                {
                    ".pdf" => "📕",
                    ".doc" or ".docx" => "📘",
                    ".txt" or ".rtf" or ".md" => "📄",
                    ".png" or ".jpg" or ".jpeg" => "🖼️",
                    _ => "📁"
                };
            }
        }

        public string FormattedSize
        {
            get
            {
                if (FileSize < 1024) return $"{FileSize} B";
                if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:F1} KB";
                return $"{FileSize / (1024.0 * 1024.0):F2} MB";
            }
        }

        public string AttachedDateFormatted => AttachedDate.ToString("yyyy-MM-dd HH:mm");
    }
}
