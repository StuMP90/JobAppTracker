using System;
using System.Diagnostics;
using System.IO;
using JobAppTracker.Models;

namespace JobAppTracker.Services
{
    public class AttachmentService
    {
        public static void OpenFile(Attachment attachment)
        {
            if (attachment == null || string.IsNullOrWhiteSpace(attachment.StoredPath))
                return;

            if (!File.Exists(attachment.StoredPath))
            {
                throw new FileNotFoundException($"Attachment file not found at: {attachment.StoredPath}");
            }

            var psi = new ProcessStartInfo
            {
                FileName = attachment.StoredPath,
                UseShellExecute = true
            };
            Process.Start(psi);
        }

        public static void OpenFileLocation(Attachment attachment)
        {
            if (attachment == null || string.IsNullOrWhiteSpace(attachment.StoredPath))
                return;

            var folder = Path.GetDirectoryName(attachment.StoredPath);
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{attachment.StoredPath}\"",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
        }

        public static void OpenUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
    }
}
