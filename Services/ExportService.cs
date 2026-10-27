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
            string? destinationPath = null,
            ReportSummary? overallSummary = null)
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
                @page {
                    size: auto;
                    margin: 8mm 10mm;
                }
                body {
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
                    background-color: #f8fafc;
                    color: #1e293b;
                    margin: 0;
                    padding: 16px;
                    line-height: 1.35;
                }
                .container {
                    max-width: 1200px;
                    margin: 0 auto;
                    background: #ffffff;
                    border-radius: 6px;
                    box-shadow: 0 1px 3px rgba(0,0,0,0.1);
                    padding: 20px 24px;
                }
                .header {
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    border-bottom: 2px solid #e2e8f0;
                    padding-bottom: 10px;
                    margin-bottom: 14px;
                }
                h1 {
                    margin: 0;
                    color: #0f172a;
                    font-size: 22px;
                }
                .meta {
                    color: #64748b;
                    font-size: 12px;
                    margin-top: 3px;
                }
                .print-btn {
                    background: #2563eb;
                    color: white;
                    border: none;
                    padding: 6px 14px;
                    border-radius: 6px;
                    cursor: pointer;
                    font-size: 13px;
                    font-weight: 500;
                }
                .print-btn:hover { background: #1d4ed8; }
                .kpi-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fit, minmax(130px, 1fr));
                    gap: 10px;
                    margin-bottom: 16px;
                }
                .kpi-card {
                    background: #f1f5f9;
                    border-radius: 6px;
                    padding: 8px 12px;
                    text-align: center;
                    border-left: 4px solid #94a3b8;
                }
                .kpi-card.active { border-left-color: #2563eb; }
                .kpi-card.stale { border-left-color: #ea580c; background: #fff7ed; }
                .kpi-card.interview { border-left-color: #d97706; }
                .kpi-card.accepted { border-left-color: #16a34a; background: #f0fdf4; }
                .kpi-val { font-size: 20px; font-weight: 700; color: #0f172a; white-space: nowrap; line-height: 1.2; }
                .kpi-card.stale .kpi-val { color: #c2410c; }
                .kpi-total-sub { font-size: 12px; font-weight: 500; color: #64748b; margin-left: 3px; }
                .kpi-card.stale .kpi-total-sub { color: #ea580c; opacity: 0.85; }
                .kpi-label { font-size: 11px; color: #64748b; text-transform: uppercase; margin-top: 2px; letter-spacing: 0.3px; }
                
                table {
                    width: 100%;
                    border-collapse: collapse;
                    font-size: 12px;
                    text-align: left;
                }
                th {
                    background-color: #f8fafc;
                    color: #475569;
                    font-weight: 600;
                    padding: 8px 10px;
                    border-bottom: 2px solid #e2e8f0;
                }
                td {
                    padding: 7px 10px;
                    border-bottom: 1px solid #e2e8f0;
                    vertical-align: top;
                }
                tr:hover { background-color: #f8fafc; }
                .badge {
                    display: inline-block;
                    padding: 2px 7px;
                    border-radius: 10px;
                    font-size: 11px;
                    font-weight: 600;
                    color: #fff;
                    white-space: nowrap;
                }
                .badge-stale {
                    background-color: #ea580c;
                    color: white;
                    padding: 2px 5px;
                    border-radius: 4px;
                    font-size: 10.5px;
                    font-weight: bold;
                    display: inline-block;
                    margin-top: 3px;
                }
                .job-title { font-weight: 600; color: #0f172a; font-size: 13px; }
                .company-name { color: #334155; }
                .agency-tag { color: #64748b; font-size: 11px; }
                .spec-snippet { color: #64748b; font-size: 11.5px; max-width: 320px; overflow: hidden; text-overflow: ellipsis; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; }
                
                .audit-row td {
                    background-color: #f8fafc;
                    padding: 6px 12px 10px 24px;
                    border-bottom: 2px solid #e2e8f0;
                }
                .audit-box {
                    background: #ffffff;
                    border: 1px solid #e2e8f0;
                    border-left: 3px solid #3b82f6;
                    border-radius: 5px;
                    padding: 8px 12px;
                }
                .audit-title {
                    font-size: 10.5px;
                    font-weight: 700;
                    color: #475569;
                    text-transform: uppercase;
                    letter-spacing: 0.5px;
                    margin-bottom: 4px;
                }
                .audit-timeline {
                    list-style: none;
                    margin: 0;
                    padding: 0;
                }
                .audit-item {
                    display: flex;
                    align-items: baseline;
                    padding: 3px 0;
                    border-bottom: 1px dashed #f1f5f9;
                    font-size: 11.5px;
                }
                .audit-item:last-child { border-bottom: none; }
                .audit-date {
                    font-weight: 600;
                    color: #64748b;
                    min-width: 80px;
                    font-size: 10.5px;
                }
                .audit-event {
                    font-weight: 600;
                    color: #1e293b;
                    min-width: 170px;
                }
                .audit-notes {
                    color: #475569;
                    flex: 1;
                }
                .audit-empty {
                    font-size: 11px;
                    color: #94a3b8;
                    font-style: italic;
                }

                .status-summary-box {
                    background: #ffffff;
                    border: 1px solid #e2e8f0;
                    border-left: 3px solid #6366f1;
                    border-radius: 5px;
                    padding: 6px 10px;
                    font-size: 11.5px;
                    display: flex;
                    align-items: center;
                    flex-wrap: wrap;
                    gap: 5px;
                }
                .status-summary-title {
                    font-size: 10.5px;
                    font-weight: 700;
                    color: #475569;
                    text-transform: uppercase;
                    letter-spacing: 0.5px;
                    margin-right: 4px;
                }
                .status-step-pill {
                    display: inline-flex;
                    align-items: center;
                    background: #f1f5f9;
                    border: 1px solid #cbd5e1;
                    border-radius: 4px;
                    padding: 2px 6px;
                    font-size: 10.5px;
                }
                .status-step-date {
                    font-weight: 600;
                    color: #64748b;
                    margin-right: 4px;
                }
                .status-step-name {
                    font-weight: 600;
                    color: #0f172a;
                }
                .status-step-arrow {
                    color: #94a3b8;
                    font-weight: bold;
                    margin: 0 2px;
                }

                @media print {
                    @page {
                        margin: 8mm 10mm;
                        size: auto;
                    }
                    body {
                        background: #fff;
                        padding: 0;
                        font-size: 10px;
                        color: #0f172a;
                        -webkit-print-color-adjust: exact;
                        print-color-adjust: exact;
                    }
                    .container {
                        box-shadow: none;
                        padding: 0;
                        max-width: 100%;
                        border-radius: 0;
                    }
                    .print-btn { display: none; }
                    .header {
                        padding-bottom: 6px;
                        margin-bottom: 8px;
                    }
                    h1 {
                        font-size: 17px;
                    }
                    .meta {
                        font-size: 9.5px;
                    }
                    .kpi-grid {
                        grid-template-columns: repeat(6, 1fr);
                        gap: 6px;
                        margin-bottom: 8px;
                    }
                    .kpi-card {
                        padding: 4px 6px;
                        border-radius: 4px;
                        border-left-width: 3px;
                    }
                    .kpi-val {
                        font-size: 14px;
                    }
                    .kpi-total-sub {
                        font-size: 9px;
                    }
                    .kpi-label {
                        font-size: 8.5px;
                        margin-top: 1px;
                    }
                    .filter-banner {
                        margin-bottom: 8px !important;
                        padding: 4px 8px !important;
                        font-size: 9.5px !important;
                    }
                    table {
                        font-size: 9.5px;
                        line-height: 1.25;
                    }
                    thead {
                        display: table-header-group;
                    }
                    th {
                        padding: 4px 6px;
                        font-size: 9.5px;
                        background-color: #f1f5f9;
                        border-bottom: 1.5px solid #94a3b8;
                    }
                    td {
                        padding: 4px 6px;
                        border-bottom: 1px solid #e2e8f0;
                    }
                    tr {
                        break-inside: avoid;
                        page-break-inside: avoid;
                    }
                    .job-title {
                        font-size: 10.5px;
                    }
                    .company-name {
                        font-size: 9.5px;
                    }
                    .agency-tag {
                        font-size: 8.5px;
                    }
                    .spec-snippet {
                        font-size: 8.5px;
                        line-height: 1.2;
                        max-width: none;
                        -webkit-line-clamp: 2;
                    }
                    .badge {
                        font-size: 8.5px;
                        padding: 1px 5px;
                    }
                    .badge-stale {
                        font-size: 8px;
                        padding: 1px 4px;
                        margin-top: 1px;
                    }
                    .audit-row td {
                        padding: 3px 6px 4px 16px;
                        background-color: #f8fafc;
                    }
                    .audit-box {
                        break-inside: avoid;
                        page-break-inside: avoid;
                        border-color: #cbd5e1;
                        padding: 4px 8px;
                        margin: 2px 0;
                    }
                    .audit-title {
                        font-size: 8.5px;
                        margin-bottom: 2px;
                    }
                    .audit-item {
                        padding: 1.5px 0;
                        font-size: 9px;
                    }
                    .audit-date {
                        min-width: 65px;
                        font-size: 8.5px;
                    }
                    .audit-event {
                        min-width: 140px;
                    }
                    .status-summary-box {
                        break-inside: avoid;
                        page-break-inside: avoid;
                        border-color: #cbd5e1;
                        padding: 3px 6px;
                        margin: 2px 0;
                        gap: 3px;
                    }
                    .status-summary-title {
                        font-size: 8.5px;
                        margin-right: 3px;
                    }
                    .status-step-pill {
                        padding: 1px 4px;
                        font-size: 8.5px;
                    }
                    .status-step-date {
                        margin-right: 3px;
                    }
                }
            ");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div class=\"container\">");
            
            bool isFiltered = overallSummary != null && (
                overallSummary.TotalApplications != summary.TotalApplications ||
                (filter != null && (
                    !string.IsNullOrWhiteSpace(filter.SearchText) ||
                    (filter.Status != "All" && !string.IsNullOrWhiteSpace(filter.Status)) ||
                    (filter.Agency != "All" && !string.IsNullOrWhiteSpace(filter.Agency)) ||
                    (filter.Source != "All" && !string.IsNullOrWhiteSpace(filter.Source)) ||
                    (filter.Method != "All" && !string.IsNullOrWhiteSpace(filter.Method)) ||
                    (filter.QuickFilter != "All" && !string.IsNullOrWhiteSpace(filter.QuickFilter)) ||
                    filter.FromDate.HasValue ||
                    filter.ToDate.HasValue
                ))
            );

            string FormatKpi(int currentVal, int? overallVal)
            {
                if (isFiltered && overallVal.HasValue)
                {
                    return $"{currentVal} <span class=\"kpi-total-sub\">of {overallVal.Value}</span>";
                }
                return currentVal.ToString();
            }

            // Header
            sb.AppendLine("<div class=\"header\">");
            sb.AppendLine("<div>");
            sb.AppendLine("<h1>📋 Job Applications Report</h1>");
            if (isFiltered && overallSummary != null)
            {
                sb.AppendLine($"<div class=\"meta\">Generated: {DateTime.Now:dddd, MMMM d, yyyy h:mm tt} &bull; Showing <strong>{applications.Count} of {overallSummary.TotalApplications}</strong> total applications</div>");
            }
            else
            {
                sb.AppendLine($"<div class=\"meta\">Generated: {DateTime.Now:dddd, MMMM d, yyyy h:mm tt} &bull; Total entries: {applications.Count}</div>");
            }
            sb.AppendLine("</div>");
            sb.AppendLine("<button class=\"print-btn\" onclick=\"window.print()\">🖨️ Print / Save as PDF</button>");
            sb.AppendLine("</div>");

            var totalValStr = FormatKpi(summary.TotalApplications, overallSummary?.TotalApplications);
            var activeValStr = FormatKpi(summary.ActiveApplications, overallSummary?.ActiveApplications);
            var staleValStr = FormatKpi(summary.StaleApplications, overallSummary?.StaleApplications);
            var interviewValStr = FormatKpi(summary.InterviewCount, overallSummary?.InterviewCount);
            var offersValStr = FormatKpi(summary.OfferCount + summary.AcceptedCount, overallSummary != null ? overallSummary.OfferCount + overallSummary.AcceptedCount : null);
            var rejectedValStr = FormatKpi(summary.RejectedCount + summary.WithdrawnCount, overallSummary != null ? overallSummary.RejectedCount + overallSummary.WithdrawnCount : null);

            // Summary KPIs
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"<div class=\"kpi-card\"><div class=\"kpi-val\">{totalValStr}</div><div class=\"kpi-label\">Total Tracked</div></div>");
            sb.AppendLine($"<div class=\"kpi-card active\"><div class=\"kpi-val\">{activeValStr}</div><div class=\"kpi-label\">Active / In-Progress</div></div>");
            sb.AppendLine($"<div class=\"kpi-card stale\"><div class=\"kpi-val\">{staleValStr}</div><div class=\"kpi-label\">⚠️ Stale (14d+)</div></div>");
            sb.AppendLine($"<div class=\"kpi-card interview\"><div class=\"kpi-val\">{interviewValStr}</div><div class=\"kpi-label\">Interviews</div></div>");
            sb.AppendLine($"<div class=\"kpi-card accepted\"><div class=\"kpi-val\">{offersValStr}</div><div class=\"kpi-label\">Offers / Accepted</div></div>");
            sb.AppendLine($"<div class=\"kpi-card\"><div class=\"kpi-val\">{rejectedValStr}</div><div class=\"kpi-label\">Rejected / Closed</div></div>");
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

                if (filter.FromDate.HasValue || filter.ToDate.HasValue)
                {
                    var dateTypeLabel = filter.DateFilterType == "LastActivity" ? "Last Activity Date" : "Applied / Created Date";
                    var fromStr = filter.FromDate.HasValue ? filter.FromDate.Value.ToString("yyyy-MM-dd") : "any";
                    var toStr = filter.ToDate.HasValue ? filter.ToDate.Value.ToString("yyyy-MM-dd") : "any";
                    filterParts.Add($"Date Range ({dateTypeLabel}): <strong>{fromStr}</strong> to <strong>{toStr}</strong>");
                }

                if (filter.AuditReportMode == "StatusChangesOnly")
                {
                    filterParts.Add("History: <strong>Status changes &amp; dates only</strong>");
                }
                else if (filter.AuditReportMode == "Full" || filter.IncludeAuditTrail)
                {
                    filterParts.Add("Audit Trail: <strong>Included</strong> (Full History)");
                }

                sb.AppendLine("<div class=\"filter-banner\" style=\"background: #f8fafc; border: 1px solid #cbd5e1; border-radius: 6px; padding: 8px 12px; margin-bottom: 12px; font-size: 12px; color: #475569;\">");
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

            bool showFullAudit = filter != null && (filter.AuditReportMode == "Full" || filter.IncludeAuditTrail);
            bool showStatusOnly = filter != null && filter.AuditReportMode == "StatusChangesOnly";

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

                // Short status changes & dates sub-row if enabled
                if (showStatusOnly)
                {
                    sb.AppendLine("<tr class=\"audit-row\">");
                    sb.AppendLine("<td colspan=\"8\">");
                    sb.AppendLine("<div class=\"status-summary-box\">");
                    sb.AppendLine("<span class=\"status-summary-title\">🔄 Status Changes:</span>");
                    
                    var statusUpdates = app.StatusHistoryUpdates;
                    if (statusUpdates.Count > 0)
                    {
                        for (int i = 0; i < statusUpdates.Count; i++)
                        {
                            var u = statusUpdates[i];
                            if (i > 0) sb.Append("<span class=\"status-step-arrow\">&rarr;</span>");
                            var statusLabel = WebUtility.HtmlEncode(u.NewStatus ?? u.SummaryTitle);
                            sb.Append($"<span class=\"status-step-pill\"><span class=\"status-step-date\">{u.UpdateDateFormatted}</span><span class=\"status-step-name\">{statusLabel}</span></span>");
                        }
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.AppendLine($"<span class=\"status-step-pill\"><span class=\"status-step-date\">{app.AppliedDateFormatted}</span><span class=\"status-step-name\">{WebUtility.HtmlEncode(app.CurrentStatus)}</span></span>");
                    }

                    sb.AppendLine("</div>");
                    sb.AppendLine("</td>");
                    sb.AppendLine("</tr>");
                }
                // Full Audit Trail sub-row if enabled
                else if (showFullAudit)
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
            return ExportToCsv(applications, destinationPath, includeAuditTrail ? "Full" : "None");
        }

        public static string ExportToCsv(List<JobApplication> applications, string? destinationPath, string historyMode)
        {
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "JobAppTrackerReports");
                Directory.CreateDirectory(tempDir);
                destinationPath = Path.Combine(tempDir, $"JobApplications_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }

            bool isFullAudit = historyMode == "Full";
            bool isStatusOnly = historyMode == "StatusChangesOnly";

            var sb = new StringBuilder();
            if (isFullAudit)
            {
                sb.AppendLine("Id,AppliedDate,JobTitle,IsCvToAgency,Company,Agency,Source,ApplicationMethod,CurrentStatus,IsFinal,LastUpdatedDate,DaysInactive,IsStale,Salary,Location,JobUrl,AuditTrail");
            }
            else if (isStatusOnly)
            {
                sb.AppendLine("Id,AppliedDate,JobTitle,IsCvToAgency,Company,Agency,Source,ApplicationMethod,CurrentStatus,IsFinal,LastUpdatedDate,DaysInactive,IsStale,Salary,Location,JobUrl,StatusHistory");
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

                if (isFullAudit)
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
                else if (isStatusOnly)
                {
                    fields.Add(EscapeCsv(a.StatusHistorySummaryText));
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
                @page {
                    size: auto;
                    margin: 8mm 10mm;
                }
                body {
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
                    background-color: #f8fafc;
                    color: #1e293b;
                    margin: 0;
                    padding: 16px;
                    line-height: 1.4;
                }
                .container {
                    max-width: 900px;
                    margin: 0 auto;
                    background: #ffffff;
                    border-radius: 6px;
                    box-shadow: 0 1px 3px rgba(0,0,0,0.1);
                    padding: 24px 28px;
                }
                .header-bar {
                    display: flex;
                    justify-content: space-between;
                    align-items: flex-start;
                    border-bottom: 2px solid #e2e8f0;
                    padding-bottom: 12px;
                    margin-bottom: 16px;
                }
                .title-area h1 {
                    margin: 0 0 4px 0;
                    color: #0f172a;
                    font-size: 22px;
                    font-weight: 700;
                }
                .company-sub {
                    font-size: 15px;
                    color: #475569;
                    font-weight: 500;
                }
                .agency-sub {
                    font-size: 13px;
                    color: #64748b;
                    margin-top: 2px;
                }
                .print-btn {
                    background: #2563eb;
                    color: white;
                    border: none;
                    padding: 6px 14px;
                    border-radius: 6px;
                    cursor: pointer;
                    font-size: 13px;
                    font-weight: 600;
                    white-space: nowrap;
                }
                .print-btn:hover { background: #1d4ed8; }
                .badges {
                    display: flex;
                    flex-wrap: wrap;
                    gap: 6px;
                    margin-top: 8px;
                }
                .badge {
                    display: inline-block;
                    padding: 3px 8px;
                    border-radius: 12px;
                    font-size: 11px;
                    font-weight: 600;
                    color: #fff;
                }
                .badge-pill {
                    background-color: #f1f5f9;
                    color: #475569;
                    border: 1px solid #cbd5e1;
                    padding: 2px 7px;
                    border-radius: 5px;
                    font-size: 11px;
                    font-weight: 500;
                }
                .badge-stale {
                    background-color: #ea580c;
                    color: white;
                    padding: 3px 8px;
                    border-radius: 5px;
                    font-size: 11px;
                    font-weight: bold;
                }

                .details-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
                    gap: 10px;
                    background: #f8fafc;
                    border: 1px solid #e2e8f0;
                    border-radius: 6px;
                    padding: 12px 16px;
                    margin-bottom: 18px;
                }
                .detail-item {
                    display: flex;
                    flex-direction: column;
                }
                .detail-label {
                    font-size: 10px;
                    font-weight: 700;
                    text-transform: uppercase;
                    color: #64748b;
                    letter-spacing: 0.5px;
                    margin-bottom: 2px;
                }
                .detail-val {
                    font-size: 13px;
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
                    margin-bottom: 18px;
                }
                .section-header {
                    font-size: 15px;
                    font-weight: 700;
                    color: #0f172a;
                    border-bottom: 1px solid #e2e8f0;
                    padding-bottom: 6px;
                    margin-bottom: 10px;
                    display: flex;
                    justify-content: space-between;
                    align-items: baseline;
                }
                .section-count {
                    font-size: 11px;
                    font-weight: 600;
                    color: #64748b;
                }

                .spec-box {
                    background: #fcfcfd;
                    border: 1px solid #e2e8f0;
                    border-left: 3px solid #3b82f6;
                    border-radius: 5px;
                    padding: 12px 16px;
                    white-space: pre-wrap;
                    font-size: 12px;
                    line-height: 1.45;
                    color: #334155;
                    font-family: inherit;
                    max-height: 600px;
                    overflow-y: auto;
                }

                .empty-notice {
                    color: #94a3b8;
                    font-style: italic;
                    font-size: 12px;
                    background: #f8fafc;
                    border: 1px dashed #cbd5e1;
                    padding: 10px;
                    border-radius: 5px;
                    text-align: center;
                }

                .event-table {
                    width: 100%;
                    border-collapse: collapse;
                    font-size: 12px;
                }
                .event-table th {
                    background-color: #f8fafc;
                    color: #475569;
                    font-weight: 600;
                    padding: 6px 10px;
                    text-align: left;
                    border-bottom: 2px solid #e2e8f0;
                    font-size: 11.5px;
                }
                .event-table td {
                    padding: 6px 10px;
                    border-bottom: 1px solid #f1f5f9;
                    vertical-align: top;
                }
                .event-table tr:hover { background-color: #f8fafc; }
                .event-date { font-weight: 600; color: #475569; white-space: nowrap; width: 90px; }
                .event-type { font-weight: 600; color: #0f172a; width: 200px; }
                .event-notes { color: #334155; white-space: pre-wrap; }

                .doc-table {
                    width: 100%;
                    border-collapse: collapse;
                    font-size: 12px;
                }
                .doc-table th {
                    background-color: #f8fafc;
                    color: #475569;
                    font-weight: 600;
                    padding: 6px 10px;
                    text-align: left;
                    border-bottom: 2px solid #e2e8f0;
                    font-size: 11.5px;
                }
                .doc-table td {
                    padding: 6px 10px;
                    border-bottom: 1px solid #f1f5f9;
                    vertical-align: middle;
                }
                .doc-name { font-weight: 600; color: #0f172a; }
                .doc-meta { color: #64748b; font-size: 11.5px; }
                .doc-note {
                    font-size: 10.5px;
                    color: #64748b;
                    margin-top: 4px;
                    font-style: italic;
                }

                .footer {
                    margin-top: 22px;
                    padding-top: 10px;
                    border-top: 1px solid #e2e8f0;
                    display: flex;
                    justify-content: space-between;
                    font-size: 10px;
                    color: #94a3b8;
                }

                @media print {
                    @page {
                        margin: 8mm 10mm;
                        size: auto;
                    }
                    body {
                        background: #fff;
                        padding: 0;
                        font-size: 9.5px;
                        color: #0f172a;
                        -webkit-print-color-adjust: exact;
                        print-color-adjust: exact;
                    }
                    .container {
                        box-shadow: none;
                        padding: 0;
                        max-width: 100%;
                        border-radius: 0;
                    }
                    .print-btn { display: none; }
                    .header-bar {
                        padding-bottom: 6px;
                        margin-bottom: 8px;
                        border-bottom: 1.5px solid #cbd5e1;
                    }
                    .title-area h1 {
                        font-size: 16px;
                        margin: 0 0 2px 0;
                    }
                    .company-sub {
                        font-size: 12px;
                    }
                    .agency-sub {
                        font-size: 10.5px;
                    }
                    .badges {
                        margin-top: 4px;
                        gap: 4px;
                    }
                    .badge {
                        font-size: 8.5px;
                        padding: 1px 5px;
                    }
                    .badge-pill {
                        font-size: 8.5px;
                        padding: 1px 5px;
                    }
                    .badge-stale {
                        font-size: 8px;
                        padding: 1px 4px;
                    }
                    .details-grid {
                        display: grid;
                        grid-template-columns: repeat(4, 1fr);
                        gap: 4px 8px;
                        padding: 6px 10px;
                        margin-bottom: 8px;
                        border-radius: 4px;
                    }
                    .detail-label {
                        font-size: 8px;
                        margin-bottom: 1px;
                    }
                    .detail-val {
                        font-size: 10px;
                    }
                    .section {
                        margin-bottom: 8px;
                        break-inside: auto;
                        page-break-inside: auto;
                    }
                    .section-header {
                        font-size: 11px;
                        padding-bottom: 2px;
                        margin-bottom: 4px;
                        break-after: avoid;
                        page-break-after: avoid;
                    }
                    .section-count {
                        font-size: 9px;
                    }
                    .spec-box {
                        padding: 6px 8px;
                        font-size: 9px;
                        line-height: 1.35;
                        max-height: none;
                        overflow: visible;
                        border-color: #cbd5e1;
                        break-inside: auto;
                        page-break-inside: auto;
                    }
                    .empty-notice {
                        padding: 6px;
                        font-size: 9px;
                    }
                    .event-table {
                        font-size: 9px;
                    }
                    .event-table thead {
                        display: table-header-group;
                    }
                    .event-table th {
                        padding: 3px 6px;
                        font-size: 9px;
                        background-color: #f1f5f9;
                        border-bottom: 1.5px solid #94a3b8;
                    }
                    .event-table td {
                        padding: 3px 6px;
                        font-size: 9px;
                        line-height: 1.25;
                    }
                    .event-table tr {
                        break-inside: avoid;
                        page-break-inside: avoid;
                    }
                    .event-date { width: 75px; }
                    .event-type { width: 140px; }
                    .doc-table {
                        font-size: 9px;
                    }
                    .doc-table thead {
                        display: table-header-group;
                    }
                    .doc-table th {
                        padding: 3px 6px;
                        font-size: 9px;
                        background-color: #f1f5f9;
                        border-bottom: 1.5px solid #94a3b8;
                    }
                    .doc-table td {
                        padding: 3px 6px;
                        font-size: 9px;
                    }
                    .doc-table tr {
                        break-inside: avoid;
                        page-break-inside: avoid;
                    }
                    .doc-note {
                        font-size: 8.5px;
                        margin-top: 2px;
                    }
                    .footer {
                        margin-top: 10px;
                        padding-top: 4px;
                        font-size: 8px;
                        border-top: 1px solid #cbd5e1;
                    }
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

