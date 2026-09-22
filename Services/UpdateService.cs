using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JobAppTracker.Models;

namespace JobAppTracker.Services
{
    public class UpdateService
    {
        public const string GitHubRepoOwner = "StuMP90";
        public const string GitHubRepoName = "JobAppTracker";
        public const string LatestReleaseApiUrl = $"https://api.github.com/repos/{GitHubRepoOwner}/{GitHubRepoName}/releases/latest";

        private static readonly HttpClient _httpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(6)
            };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("JobAppTracker-Updater", "1.0"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            return client;
        }

        public static string GetCurrentVersionString()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "1.0.2";
        }

        public static bool TryParseVersion(string? raw, out Version version)
        {
            version = new Version(0, 0, 0);
            if (string.IsNullOrWhiteSpace(raw)) return false;

            // Strip leading 'v' or 'V' and any whitespace
            string cleaned = raw.Trim().TrimStart('v', 'V');

            // Strip pre-release suffix if present (e.g., 1.0.3-preview -> 1.0.3)
            int dashIndex = cleaned.IndexOf('-');
            if (dashIndex > 0)
            {
                cleaned = cleaned.Substring(0, dashIndex);
            }

            // Ensure we have at least 2 parts (e.g. 1.0 -> 1.0.0)
            var parts = cleaned.Split('.');
            if (parts.Length == 1 && int.TryParse(parts[0], out int major))
            {
                cleaned = $"{major}.0.0";
            }
            else if (parts.Length == 2 && int.TryParse(parts[0], out int maj) && int.TryParse(parts[1], out int min))
            {
                cleaned = $"{maj}.{min}.0";
            }

            return Version.TryParse(cleaned, out version!);
        }

        public static bool IsNewerVersion(string latestVersionTag, string currentVersionString)
        {
            if (TryParseVersion(latestVersionTag, out var latest) &&
                TryParseVersion(currentVersionString, out var current))
            {
                return latest > current;
            }
            return false;
        }

        public static UpdateInfo ParseReleaseJson(string json, string currentVersion)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
            string releaseName = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
            string releaseNotes = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
            string htmlUrl = root.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? "" : "";

            string? downloadUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.TryGetProperty("name", out var assetNameProp) &&
                        asset.TryGetProperty("browser_download_url", out var dlProp))
                    {
                        var name = assetNameProp.GetString() ?? "";
                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = dlProp.GetString();
                            // Prefer explicit JobAppTrackerSetup.exe if found
                            if (name.Equals("JobAppTrackerSetup.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                break;
                            }
                        }
                    }
                }
            }

            // Fallback download URL to the release page if no direct exe asset exists
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                downloadUrl = htmlUrl;
            }

            bool isNewer = IsNewerVersion(tagName, currentVersion);

            return new UpdateInfo
            {
                IsUpdateAvailable = isNewer,
                CurrentVersion = currentVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? currentVersion : $"v{currentVersion}",
                LatestVersion = tagName.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? tagName : $"v{tagName}",
                ReleaseName = string.IsNullOrWhiteSpace(releaseName) ? tagName : releaseName,
                ReleaseNotes = releaseNotes,
                ReleasePageUrl = htmlUrl,
                DownloadUrl = downloadUrl
            };
        }

        public static async Task<UpdateInfo> CheckForUpdatesAsync(string? currentVersion = null)
        {
            currentVersion ??= GetCurrentVersionString();

            try
            {
                using var response = await _httpClient.GetAsync(LatestReleaseApiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    return new UpdateInfo
                    {
                        CurrentVersion = $"v{currentVersion}",
                        ErrorMessage = $"GitHub returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase})."
                    };
                }

                string json = await response.Content.ReadAsStringAsync();
                return ParseReleaseJson(json, currentVersion);
            }
            catch (TaskCanceledException)
            {
                return new UpdateInfo
                {
                    CurrentVersion = $"v{currentVersion}",
                    ErrorMessage = "Update check timed out. Please verify your internet connection."
                };
            }
            catch (HttpRequestException ex)
            {
                return new UpdateInfo
                {
                    CurrentVersion = $"v{currentVersion}",
                    ErrorMessage = $"Network connection error: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new UpdateInfo
                {
                    CurrentVersion = $"v{currentVersion}",
                    ErrorMessage = $"Unable to check for updates: {ex.Message}"
                };
            }
        }
    }
}
