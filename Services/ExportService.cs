using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using JobAppTracker.Models;

namespace JobAppTracker.Services
{
    public class ExportService
    {
        public static string GenerateHtmlReport(
            List<JobApplication> applications, 
            ReportSummary summary, 
            ApplicationFilter? filter,
            string? destinationPath = null)
        {
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "JobAppTrackerReports");
                Directory.CreateDirectory(tempDir);
                destinationPath = Path.Combine(tempDir, $"JobReport_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            }

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"UTF-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("<title>Job Applications Report - " + DateTime.Now.ToString("yyyy-MM-dd") + "</title>");
            sb.AppendLine("<style>");
            sb.AppendLine(@"
                body {
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
                    background-color: #f8fafc;
                    color: #1e293b;
                    margin: 0;
                    padding: 24px;
                }
                .container {
                    max-width: 1200px;
                    margin: 0 auto;
                    background: #ffffff;
                    border-radius: 8px;
                    box-shadow: 0 1px 3px rgba(0,0,0,0.1);
                    padding: 32px;
                }
                .header {
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    border-bottom: 2px solid #e2e8f0;
                    padding-bottom: 16px;
                    margin-bottom: 24px;
                }
                h1 {
                    margin: 0;
                    color: #0f172a;
                    font-size: 26px;
                }
                .meta {
                    color: #64748b;
                    font-size: 13px;
                }
                .print-btn {
                    background: #2563eb;
                    color: white;
                    border: none;
                    padding: 8px 16px;
                    border-radius: 6px;
                    cursor: pointer;
                    font-size: 14px;
                    font-weight: 500;
                }
                .print-btn:hover { background: #1d4ed8; }
                .kpi-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
                    gap: 16px;
                    margin-bottom: 28px;
                }
                .kpi-card {
                    background: #f1f5f9;
                    border-radius: 8px;
                    padding: 14px 18px;
                    text-align: center;
                    border-left: 4px solid #94a3b8;
                }
                .kpi-card.active { border-left-color: #2563eb; }
                .kpi-card.stale { border-left-color: #ea580c; background: #fff7ed; }
                .kpi-card.interview { border-left-color: #d97706; }
                .kpi-card.accepted { border-left-color: #16a34a; background: #f0fdf4; }
                .kpi-val { font-size: 24px; font-weight: 700; color: #0f172a; }
                .kpi-label { font-size: 12px; color: #64748b; text-transform: uppercase; margin-top: 4px; }
                
                table {
                    width: 100%;
                    border-collapse: collapse;
                    font-size: 13px;
                    text-align: left;
                }
                th {
                    background-color: #f8fafc;
                    color: #475569;
                    font-weight: 600;
                    padding: 10px 12px;
                    border-bottom: 2px solid #e2e8f0;
                }
                td {
                    padding: 10px 12px;
                    border-bottom: 1px solid #e2e8f0;
                    vertical-align: top;
                }
                tr:hover { background-color: #f8fafc; }
                .badge {
                    display: inline-block;
                    padding: 3px 8px;
                    border-radius: 12px;
                    font-size: 11px;
                    font-weight: 600;
                    color: #fff;
                    white-space: nowrap;
                }
                .badge-stale {
                    background-color: #ea580c;
                    color: white;
                    padding: 2px 6px;
                    border-radius: 4px;
                    font-size: 11px;
                    font-weight: bold;
                    display: inline-block;
                    margin-top: 3px;
                }
                .job-title { font-weight: 600; color: #0f172a; font-size: 14px; }
                .company-name { color: #334155; }
                .agency-tag { color: #64748b; font-size: 12px; }
                .spec-snippet { color: #64748b; font-size: 12px; max-width: 320px; overflow: hidden; text-overflow: ellipsis; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; }
                
                .audit-row td {
                    background-color: #f8fafc;
                    padding: 8px 16px 14px 28px;
                    border-bottom: 2px solid #e2e8f0;
                }
                .audit-box {
                    background: #ffffff;
                    border: 1px solid #e2e8f0;
                    border-left: 3px solid #3b82f6;
                    border-radius: 6px;
                    padding: 10px 14px;
                }
                .audit-title {
                    font-size: 11px;
                    font-weight: 700;
                    color: #475569;
                    text-transform: uppercase;
                    letter-spacing: 0.5px;
                    margin-bottom: 6px;
                }
                .audit-timeline {
                    list-style: none;
                    margin: 0;
                    padding: 0;
                }
                .audit-item {
                    display: flex;
                    align-items: baseline;
                    padding: 4px 0;
                    border-bottom: 1px dashed #f1f5f9;
                    font-size: 12px;
                }
                .audit-item:last-child { border-bottom: none; }
                .audit-date {
                    font-weight: 600;
                    color: #64748b;
                    min-width: 85px;
                    font-size: 11px;
                }
                .audit-event {
                    font-weight: 600;
                    color: #1e293b;
                    min-width: 180px;
                }
                .audit-notes {
                    color: #475569;
                    flex: 1;
                }
                .audit-empty {
                    font-size: 12px;
                    color: #94a3b8;
                    font-style: italic;
                }

                @media print {
                    body { background: #fff; padding: 0; }
                    .container { box-shadow: none; padding: 0; max-width: 100%; }
                    .print-btn { display: none; }
                    .audit-box { break-inside: avoid; border-color: #cbd5e1; }
                }
            ");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div class=\"container\">");
            
            // Header
            sb.AppendLine("<div class=\"header\">");
            sb.AppendLine("<div>");
            sb.AppendLine("<h1>📋 Job Applications Report</h1>");
            sb.AppendLine($"<div class=\"meta\">Generated: {DateTime.Now:dddd, MMMM d, yyyy h:mm tt} &bull; Total entries: {applications.Count}</div>");
            sb.AppendLine("</div>");
            sb.AppendLine("<button class=\"print-btn\" onclick=\"window.print()\">🖨️ Print / Save as PDF</button>");
            sb.AppendLine("</div>");

            // Summary KPIs
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"<div class=\"kpi-card\"><div class=\"kpi-val\">{summary.TotalApplications}</div><div class=\"kpi-label\">Total Tracked</div></div>");
            sb.AppendLine($"<div class=\"kpi-card active\"><div class=\"kpi-val\">{summary.ActiveApplications}</div><div class=\"kpi-label\">Active / In-Progress</div></div>");
            sb.AppendLine($"<div class=\"kpi-card stale\"><div class=\"kpi-val\">{summary.StaleApplications}</div><div class=\"kpi-label\">⚠️ Stale (14d+)</div></div>");
            sb.AppendLine($"<div class=\"kpi-card interview\"><div class=\"kpi-val\">{summary.InterviewCount}</div><div class=\"kpi-label\">Interviews</div></div>");
            sb.AppendLine($"<div class=\"kpi-card accepted\"><div class=\"kpi-val\">{summary.AcceptedCount}</div><div class=\"kpi-label\">Offers / Accepted</div></div>");
            sb.AppendLine($"<div class=\"kpi-card\"><div class=\"kpi-val\">{summary.RejectedCount}</div><div class=\"kpi-label\">Rejected / Closed</div></div>");
            sb.AppendLine("</div>");

            // Filter & Sequencing details banner
            if (filter != null)
            {
                var filterParts = new List<string>();
                var sortName = filter.SortBy == "AppliedDate" || filter.SortBy == "Created" ? "Application / Created Date" : "Last Activity Date";
                var sortDir = filter.SortDescending ? "Newest First" : "Oldest First";
                filterParts.Add($"Sequenced by: <strong>{sortName} ({sortDir})</strong>");

                if (!string.IsNullOrWhiteSpace(filter.SearchText)) filterParts.Add($"Search: \"{WebUtility.HtmlEncode(filter.SearchText)}\"");
                if (filter.Status != "All") filterParts.Add($"Status: {filter.Status}");
                if (!string.IsNullOrWhiteSpace(filter.Agency) && filter.Agency != "All") filterParts.Add($"Agency: {WebUtility.HtmlEncode(filter.Agency)}");
                if (filter.Source != "All") filterParts.Add($"Source: {filter.Source}");
                if (filter.Method != "All") filterParts.Add($"Method: {filter.Method}");
                if (filter.QuickFilter != "All") filterParts.Add($"Tab: {filter.QuickFilter}");
                if (filter.IncludeAuditTrail) filterParts.Add("Audit Trail: <strong>Included</strong>");

                sb.AppendLine("<div style=\"background: #f8fafc; border: 1px solid #cbd5e1; border-radius: 6px; padding: 10px 14px; margin-bottom: 20px; font-size: 13px; color: #475569;\">");
                sb.AppendLine(string.Join(" &bull; ", filterParts));
                sb.AppendLine("</div>");
            }

            // Table
            sb.AppendLine("<table>");
            sb.AppendLine("<thead>");
            sb.AppendLine("<tr>");
            sb.AppendLine("<th>Applied Date</th>");
            sb.AppendLine("<th>Role / Job Title</th>");
            sb.AppendLine("<th>Company & Agency</th>");
            sb.AppendLine("<th>Salary / Rate</th>");
            sb.AppendLine("<th>Source / Method</th>");
            sb.AppendLine("<th>Status</th>");
            sb.AppendLine("<th>Last Activity</th>");
            sb.AppendLine("<th>Summary / Notes</th>");
            sb.AppendLine("</tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");

            bool showAudit = filter != null && filter.IncludeAuditTrail;

            foreach (var app in applications)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{app.AppliedDateFormatted}</td>");
                
                // Job Title
                sb.AppendLine("<td>");
                sb.AppendLine($"<div class=\"job-title\">{WebUtility.HtmlEncode(app.DisplayTitle)}</div>");
                if (app.IsCvToAgency)
                {
                    sb.AppendLine("<span style=\"background:#e0f2fe; color:#0369a1; padding:2px 6px; border-radius:4px; font-size:11px;\">Speculative CV</span>");
                }
                sb.AppendLine("</td>");

                // Company & Agency
                sb.AppendLine("<td>");
                sb.AppendLine($"<div class=\"company-name\"><strong>{WebUtility.HtmlEncode(app.Company)}</strong></div>");
                if (!string.IsNullOrWhiteSpace(app.Agency))
                {
                    sb.AppendLine($"<div class=\"agency-tag\">Agency: {WebUtility.HtmlEncode(app.Agency)}</div>");
                }
                sb.AppendLine("</td>");

                // Salary / Rate
                sb.AppendLine("<td>");
                if (!string.IsNullOrWhiteSpace(app.SalaryOrRate))
                {
                    sb.AppendLine($"<strong style=\"color:#047857;\">{WebUtility.HtmlEncode(app.SalaryOrRate)}</strong>");
                }
                else
                {
                    sb.AppendLine("<span style=\"color:#94a3b8;\">&mdash;</span>");
                }
                sb.AppendLine("</td>");

                // Source & Method
                sb.AppendLine($"<td>{WebUtility.HtmlEncode(app.Source)}<br><small style=\"color:#64748b;\">{WebUtility.HtmlEncode(app.ApplicationMethod)}</small></td>");

                // Status
                sb.AppendLine("<td>");
                sb.AppendLine($"<span class=\"badge\" style=\"background-color: {app.StatusBadgeColor};\">{WebUtility.HtmlEncode(app.CurrentStatus)}</span>");
                if (app.IsStale)
                {
                    sb.AppendLine($"<br><span class=\"badge-stale\">⚠️ Stale ({app.DaysSinceLastUpdate}d)</span>");
                }
                sb.AppendLine("</td>");

                // Last Activity
                sb.AppendLine($"<td>{app.LastUpdatedFormatted}<br><small style=\"color:#64748b;\">{app.StalenessText}</small></td>");

                // Spec / Notes summary
                sb.AppendLine("<td>");
                if (!string.IsNullOrWhiteSpace(app.JobSpecText))
                {
                    var snippet = app.JobSpecText.Length > 160 ? app.JobSpecText.Substring(0, 160) + "..." : app.JobSpecText;
                    sb.AppendLine($"<div class=\"spec-snippet\">{WebUtility.HtmlEncode(snippet)}</div>");
                }
                else
                {
                    sb.AppendLine("<span style=\"color:#94a3b8; font-style:italic;\">No spec text</span>");
                }
                sb.AppendLine("</td>");

                sb.AppendLine("</tr>");

                // Audit Trail sub-row if enabled
                if (showAudit)
                {
                    sb.AppendLine("<tr class=\"audit-row\">");
                    sb.AppendLine("<td colspan=\"8\">");
                    sb.AppendLine("<div class=\"audit-box\">");
                    sb.AppendLine("<div class=\"audit-title\">📜 Audit Trail &amp; Events</div>");
                    
                    if (app.AuditTrail != null && app.AuditTrail.Count > 0)
                    {
                        sb.AppendLine("<ul class=\"audit-timeline\">");
                        foreach (var update in app.AuditTrail)
                        {
                            sb.AppendLine("<li class=\"audit-item\">");
                            sb.AppendLine($"<span class=\"audit-date\">{update.UpdateDateFormatted}</span>");
                            sb.AppendLine($"<span class=\"audit-event\">{update.TypeIcon} {WebUtility.HtmlEncode(update.SummaryTitle)}</span>");
                            var noteHtml = !string.IsNullOrWhiteSpace(update.Notes) ? WebUtility.HtmlEncode(update.Notes) : "<span style=\"color:#94a3b8; font-style:italic;\">(No notes)</span>";
                            sb.AppendLine($"<span class=\"audit-notes\">{noteHtml}</span>");
                            sb.AppendLine("</li>");
                        }
                        sb.AppendLine("</ul>");
                    }
                    else
                    {
                        sb.AppendLine("<div class=\"audit-empty\">No update events recorded for this application.</div>");
                    }

                    sb.AppendLine("</div>");
                    sb.AppendLine("</td>");
                    sb.AppendLine("</tr>");
                }
            }

            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");

            sb.AppendLine("</div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            File.WriteAllText(destinationPath, sb.ToString(), Encoding.UTF8);
            return destinationPath;
        }

        public static string ExportToCsv(List<JobApplication> applications, string? destinationPath = null, bool includeAuditTrail = false)
        {
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "JobAppTrackerReports");
                Directory.CreateDirectory(tempDir);
                destinationPath = Path.Combine(tempDir, $"JobApplications_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }

            var sb = new StringBuilder();
            if (includeAuditTrail)
            {
                sb.AppendLine("Id,AppliedDate,JobTitle,IsCvToAgency,Company,Agency,Source,ApplicationMethod,CurrentStatus,IsFinal,LastUpdatedDate,DaysInactive,IsStale,Salary,Location,JobUrl,AuditTrail");
            }
            else
            {
                sb.AppendLine("Id,AppliedDate,JobTitle,IsCvToAgency,Company,Agency,Source,ApplicationMethod,CurrentStatus,IsFinal,LastUpdatedDate,DaysInactive,IsStale,Salary,Location,JobUrl");
            }

            foreach (var a in applications)
            {
                var fields = new List<string>
                {
                    a.Id.ToString(),
                    EscapeCsv(a.AppliedDateFormatted),
                    EscapeCsv(a.JobTitle),
                    a.IsCvToAgency ? "Yes" : "No",
                    EscapeCsv(a.Company),
                    EscapeCsv(a.Agency ?? ""),
                    EscapeCsv(a.Source),
                    EscapeCsv(a.ApplicationMethod),
                    EscapeCsv(a.CurrentStatus),
                    a.IsFinal ? "Yes" : "No",
                    EscapeCsv(a.LastUpdatedFormatted),
                    a.DaysSinceLastUpdate.ToString(),
                    a.IsStale ? "Yes" : "No",
                    EscapeCsv(a.SalaryOrRate ?? ""),
                    EscapeCsv(a.Location ?? ""),
                    EscapeCsv(a.JobUrl ?? "")
                };

                if (includeAuditTrail)
                {
                    string auditTrailText = "";
                    if (a.AuditTrail != null && a.AuditTrail.Count > 0)
                    {
                        var eventStrings = System.Linq.Enumerable.Select(a.AuditTrail, u =>
                        {
                            var noteClean = string.IsNullOrWhiteSpace(u.Notes) 
                                ? "" 
                                : ": " + u.Notes.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
                            return $"[{u.UpdateDateFormatted} ({u.SummaryTitle}){noteClean}]";
                        });
                        auditTrailText = string.Join(" | ", eventStrings);
                    }
                    fields.Add(EscapeCsv(auditTrailText));
                }

                sb.AppendLine(string.Join(",", fields));
            }

            File.WriteAllText(destinationPath, sb.ToString(), Encoding.UTF8);
            return destinationPath;
        }

        private static string EscapeCsv(string val)
        {
            if (string.IsNullOrEmpty(val)) return "\"\"";
            return "\"" + val.Replace("\"", "\"\"") + "\"";
        }

        public static string GenerateSingleApplicationHtmlReport(
            JobApplication app, 
            List<ApplicationUpdate> updates, 
            List<Attachment> attachments, 
            string? destinationPath = null)
        {
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "JobAppTrackerReports");
                Directory.CreateDirectory(tempDir);
                var safeTitle = string.Join("_", app.DisplayTitle.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
                if (safeTitle.Length > 30) safeTitle = safeTitle.Substring(0, 30);
                if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "Application";
                destinationPath = Path.Combine(tempDir, $"ApplicationReport_{app.Id}_{safeTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            }

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"UTF-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine($"<title>{WebUtility.HtmlEncode(app.DisplayTitle)} - {WebUtility.HtmlEncode(app.Company)} Report</title>");
            sb.AppendLine("<style>");
            sb.AppendLine(@"
                body {
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
                    background-color: #f8fafc;
                    color: #1e293b;
                    margin: 0;
                    padding: 24px;
                    line-height: 1.5;
                }
                .container {
                    max-width: 900px;
                    margin: 0 auto;
                    background: #ffffff;
                    border-radius: 8px;
                    box-shadow: 0 1px 3px rgba(0,0,0,0.1);
                    padding: 32px 36px;
                }
                .header-bar {
                    display: flex;
                    justify-content: space-between;
                    align-items: flex-start;
                    border-bottom: 2px solid #e2e8f0;
                    padding-bottom: 20px;
                    margin-bottom: 24px;
                }
                .title-area h1 {
                    margin: 0 0 6px 0;
                    color: #0f172a;
                    font-size: 26px;
                    font-weight: 700;
                }
                .company-sub {
                    font-size: 16px;
                    color: #475569;
                    font-weight: 500;
                }
                .agency-sub {
                    font-size: 14px;
                    color: #64748b;
                    margin-top: 2px;
                }
                .print-btn {
                    background: #2563eb;
                    color: white;
                    border: none;
                    padding: 8px 18px;
                    border-radius: 6px;
                    cursor: pointer;
                    font-size: 14px;
                    font-weight: 600;
                    white-space: nowrap;
                }
                .print-btn:hover { background: #1d4ed8; }
                .badges {
                    display: flex;
                    flex-wrap: wrap;
                    gap: 8px;
                    margin-top: 12px;
                }
                .badge {
                    display: inline-block;
                    padding: 4px 10px;
                    border-radius: 12px;
                    font-size: 12px;
                    font-weight: 600;
                    color: #fff;
                }
                .badge-pill {
                    background-color: #f1f5f9;
                    color: #475569;
                    border: 1px solid #cbd5e1;
                    padding: 3px 9px;
                    border-radius: 6px;
                    font-size: 12px;
                    font-weight: 500;
                }
                .badge-stale {
                    background-color: #ea580c;
                    color: white;
                    padding: 4px 10px;
                    border-radius: 6px;
                    font-size: 12px;
                    font-weight: bold;
                }

                .details-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
                    gap: 14px;
                    background: #f8fafc;
                    border: 1px solid #e2e8f0;
                    border-radius: 8px;
                    padding: 16px 20px;
                    margin-bottom: 28px;
                }
                .detail-item {
                    display: flex;
                    flex-direction: column;
                }
                .detail-label {
                    font-size: 11px;
                    font-weight: 700;
                    text-transform: uppercase;
                    color: #64748b;
                    letter-spacing: 0.5px;
                    margin-bottom: 2px;
                }
                .detail-val {
                    font-size: 14px;
                    color: #0f172a;
                    font-weight: 600;
                    word-break: break-word;
                }
                .detail-val a {
                    color: #2563eb;
                    text-decoration: none;
                }
                .detail-val a:hover {
                    text-decoration: underline;
                }

                .section {
                    margin-bottom: 28px;
                }
                .section-header {
                    font-size: 17px;
                    font-weight: 700;
                    color: #0f172a;
                    border-bottom: 1px solid #e2e8f0;
                    padding-bottom: 8px;
                    margin-bottom: 14px;
                    display: flex;
                    justify-content: space-between;
                    align-items: baseline;
                }
                .section-count {
                    font-size: 12px;
                    font-weight: 600;
                    color: #64748b;
                }

                .spec-box {
                    background: #fcfcfd;
                    border: 1px solid #e2e8f0;
                    border-left: 4px solid #3b82f6;
                    border-radius: 6px;
                    padding: 16px 20px;
                    white-space: pre-wrap;
                    font-size: 13px;
                    line-height: 1.6;
                    color: #334155;
                    font-family: inherit;
                    max-height: 600px;
                    overflow-y: auto;
                }

                .empty-notice {
                    color: #94a3b8;
                    font-style: italic;
                    font-size: 13px;
                    background: #f8fafc;
                    border: 1px dashed #cbd5e1;
                    padding: 14px;
                    border-radius: 6px;
                    text-align: center;
                }

                .event-table {
                    width: 100%;
                    border-collapse: collapse;
                    font-size: 13px;
                }
                .event-table th {
                    background-color: #f8fafc;
                    color: #475569;
                    font-weight: 600;
                    padding: 8px 12px;
                    text-align: left;
                    border-bottom: 2px solid #e2e8f0;
                    font-size: 12px;
                }
                .event-table td {
                    padding: 10px 12px;
                    border-bottom: 1px solid #f1f5f9;
                    vertical-align: top;
                }
                .event-table tr:hover { background-color: #f8fafc; }
                .event-date { font-weight: 600; color: #475569; white-space: nowrap; width: 100px; }
                .event-type { font-weight: 600; color: #0f172a; width: 220px; }
                .event-notes { color: #334155; white-space: pre-wrap; }

                .doc-table {
                    width: 100%;
                    border-collapse: collapse;
                    font-size: 13px;
                }
                .doc-table th {
                    background-color: #f8fafc;
                    color: #475569;
                    font-weight: 600;
                    padding: 8px 12px;
                    text-align: left;
                    border-bottom: 2px solid #e2e8f0;
                    font-size: 12px;
                }
                .doc-table td {
                    padding: 10px 12px;
                    border-bottom: 1px solid #f1f5f9;
                    vertical-align: middle;
                }
                .doc-name { font-weight: 600; color: #0f172a; }
                .doc-meta { color: #64748b; font-size: 12px; }
                .doc-note {
                    font-size: 11px;
                    color: #64748b;
                    margin-top: 6px;
                    font-style: italic;
                }

                .footer {
                    margin-top: 36px;
                    padding-top: 16px;
                    border-top: 1px solid #e2e8f0;
                    display: flex;
                    justify-content: space-between;
                    font-size: 11px;
                    color: #94a3b8;
                }

                @media print {
                    body { background: #fff; padding: 0; }
                    .container { box-shadow: none; padding: 0; max-width: 100%; }
                    .print-btn { display: none; }
                    .spec-box { max-height: none; overflow: visible; border-color: #cbd5e1; }
                    .section { break-inside: avoid; }
                    .event-table tr { break-inside: avoid; }
                    .doc-table tr { break-inside: avoid; }
                }
            ");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div class=\"container\">");

            // Header Bar
            sb.AppendLine("<div class=\"header-bar\">");
            sb.AppendLine("<div class=\"title-area\">");
            sb.AppendLine($"<h1>{WebUtility.HtmlEncode(app.DisplayTitle)}</h1>");
            sb.AppendLine($"<div class=\"company-sub\"><strong>Company:</strong> {WebUtility.HtmlEncode(app.Company)}</div>");
            if (!string.IsNullOrWhiteSpace(app.Agency))
            {
                sb.AppendLine($"<div class=\"agency-sub\"><strong>Recruitment Agency:</strong> {WebUtility.HtmlEncode(app.Agency)}</div>");
            }
            
            sb.AppendLine("<div class=\"badges\">");
            sb.AppendLine($"<span class=\"badge\" style=\"background-color: {app.StatusBadgeColor};\">{WebUtility.HtmlEncode(app.CurrentStatus)}</span>");
            if (app.IsCvToAgency)
            {
                sb.AppendLine("<span class=\"badge-pill\" style=\"background:#e0f2fe; color:#0369a1; border-color:#bae6fd;\">📄 Speculative CV Submission</span>");
            }
            if (app.IsStale)
            {
                sb.AppendLine($"<span class=\"badge-stale\">⚠️ Stale ({app.DaysSinceLastUpdate}d inactive)</span>");
            }
            else if (app.IsFinal)
            {
                sb.AppendLine("<span class=\"badge-pill\" style=\"background:#f1f5f9; color:#475569;\">Outcome Finalized</span>");
            }
            else
            {
                sb.AppendLine("<span class=\"badge-pill\" style=\"background:#f0fdf4; color:#16a34a; border-color:#bbf7d0;\">Active Application</span>");
            }
            sb.AppendLine("</div>");

            sb.AppendLine("</div>");
            sb.AppendLine("<button class=\"print-btn\" onclick=\"window.print()\">🖨️ Print / Save as PDF</button>");
            sb.AppendLine("</div>");

            // Key Info Grid
            sb.AppendLine("<div class=\"details-grid\">");
            sb.AppendLine($"<div class=\"detail-item\"><span class=\"detail-label\">Applied Date</span><span class=\"detail-val\">{app.AppliedDateFormatted}</span></div>");
            sb.AppendLine($"<div class=\"detail-item\"><span class=\"detail-label\">Last Activity</span><span class=\"detail-val\">{app.LastUpdatedFormatted} ({app.StalenessText})</span></div>");
            sb.AppendLine($"<div class=\"detail-item\"><span class=\"detail-label\">Source</span><span class=\"detail-val\">{WebUtility.HtmlEncode(app.Source)}</span></div>");
            sb.AppendLine($"<div class=\"detail-item\"><span class=\"detail-label\">Application Method</span><span class=\"detail-val\">{WebUtility.HtmlEncode(app.ApplicationMethod)}</span></div>");
            
            if (!string.IsNullOrWhiteSpace(app.SalaryOrRate))
            {
                sb.AppendLine($"<div class=\"detail-item\"><span class=\"detail-label\">Salary / Rate</span><span class=\"detail-val\">{WebUtility.HtmlEncode(app.SalaryOrRate)}</span></div>");
            }
            if (!string.IsNullOrWhiteSpace(app.Location))
            {
                sb.AppendLine($"<div class=\"detail-item\"><span class=\"detail-label\">Location</span><span class=\"detail-val\">{WebUtility.HtmlEncode(app.Location)}</span></div>");
            }
            if (!string.IsNullOrWhiteSpace(app.JobUrl))
            {
                sb.AppendLine($"<div class=\"detail-item\"><span class=\"detail-label\">Job Posting URL</span><span class=\"detail-val\"><a href=\"{WebUtility.HtmlEncode(app.JobUrl)}\" target=\"_blank\">Open Posting 🔗</a></span></div>");
            }
            sb.AppendLine($"<div class=\"detail-item\"><span class=\"detail-label\">Attachments</span><span class=\"detail-val\">{attachments.Count} file(s)</span></div>");
            sb.AppendLine($"<div class=\"detail-item\"><span class=\"detail-label\">Audit Events</span><span class=\"detail-val\">{updates.Count} recorded</span></div>");
            sb.AppendLine("</div>");

            // Section 1: Job Specification
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("<div class=\"section-header\"><span>📄 Job Specification &amp; Role Details</span></div>");
            if (!string.IsNullOrWhiteSpace(app.JobSpecText))
            {
                sb.AppendLine($"<div class=\"spec-box\">{WebUtility.HtmlEncode(app.JobSpecText)}</div>");
            }
            else
            {
                sb.AppendLine("<div class=\"empty-notice\">No job specification text was recorded for this application.</div>");
            }
            sb.AppendLine("</div>");

            // Section 2: Audit Trail & Events
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine($"<div class=\"section-header\"><span>📜 Audit Trail &amp; History Events</span><span class=\"section-count\">{updates.Count} event(s)</span></div>");
            if (updates != null && updates.Count > 0)
            {
                sb.AppendLine("<table class=\"event-table\">");
                sb.AppendLine("<thead><tr><th>Date</th><th>Event / Action</th><th>Notes &amp; Details</th></tr></thead>");
                sb.AppendLine("<tbody>");
                foreach (var u in updates)
                {
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td class=\"event-date\">{u.UpdateDateFormatted}</td>");
                    sb.AppendLine($"<td class=\"event-type\">{u.TypeIcon} {WebUtility.HtmlEncode(u.SummaryTitle)}</td>");
                    var notesText = !string.IsNullOrWhiteSpace(u.Notes) 
                        ? WebUtility.HtmlEncode(u.Notes) 
                        : "<span style=\"color:#94a3b8; font-style:italic;\">(No notes)</span>";
                    sb.AppendLine($"<td class=\"event-notes\">{notesText}</td>");
                    sb.AppendLine("</tr>");
                }
                sb.AppendLine("</tbody>");
                sb.AppendLine("</table>");
            }
            else
            {
                sb.AppendLine("<div class=\"empty-notice\">No update events recorded for this application.</div>");
            }
            sb.AppendLine("</div>");

            // Section 3: Attached Documents
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine($"<div class=\"section-header\"><span>📎 Attached Documents</span><span class=\"section-count\">{attachments.Count} file(s)</span></div>");
            if (attachments != null && attachments.Count > 0)
            {
                sb.AppendLine("<table class=\"doc-table\">");
                sb.AppendLine("<thead><tr><th>Document Name</th><th>File Type</th><th>File Size</th><th>Date Attached</th></tr></thead>");
                sb.AppendLine("<tbody>");
                foreach (var doc in attachments)
                {
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td class=\"doc-name\">{doc.FileIcon} {WebUtility.HtmlEncode(doc.FileName)}</td>");
                    sb.AppendLine($"<td class=\"doc-meta\">{doc.Extension.TrimStart('.').ToUpperInvariant()}</td>");
                    sb.AppendLine($"<td class=\"doc-meta\">{doc.FormattedSize}</td>");
                    sb.AppendLine($"<td class=\"doc-meta\">{doc.AttachedDateFormatted}</td>");
                    sb.AppendLine("</tr>");
                }
                sb.AppendLine("</tbody>");
                sb.AppendLine("</table>");
                sb.AppendLine("<div class=\"doc-note\">* Files are managed securely in local application storage. Document contents are not embedded in this summary report.</div>");
            }
            else
            {
                sb.AppendLine("<div class=\"empty-notice\">No documents are attached to this application.</div>");
            }
            sb.AppendLine("</div>");

            // Footer
            sb.AppendLine("<div class=\"footer\">");
            sb.AppendLine($"<div>Generated: {DateTime.Now:yyyy-MM-dd HH:mm} &bull; Application ID: #{app.Id}</div>");
            sb.AppendLine("<div>Job Application Tracker &bull; Confidential Candidate Dossier</div>");
            sb.AppendLine("</div>");

            sb.AppendLine("</div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            File.WriteAllText(destinationPath, sb.ToString(), Encoding.UTF8);
            return destinationPath;
        }

        public static void OpenInBrowser(string filePath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
    }
}

