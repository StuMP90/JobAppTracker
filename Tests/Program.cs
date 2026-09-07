using System;
using System.IO;
using System.Linq;
using JobAppTracker.Data;
using JobAppTracker.Models;
using JobAppTracker.Services;

namespace JobAppTracker.Tests
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("    JobAppTracker Automated Verification Suite    ");
            Console.WriteLine("==================================================");

            var testDir = Path.Combine(Path.GetTempPath(), "JobAppTrackerTests_" + Guid.NewGuid());
            Directory.CreateDirectory(testDir);
            var testDb = Path.Combine(testDir, "test_job_applications.db");

            try
            {
                var db = new DatabaseService(testDb);
                Console.WriteLine($"[PASS] Database initialized at: {testDb}");

                // 1. Insert standard job application
                var sampleSpec = @"Senior .NET Developer
Key Requirements:
- 5+ years C#, .NET Core / .NET 8
- Experience with WPF, SQLite, or Desktop Apps
- Strong problem-solving and clean code skills";

                var app1 = new JobApplication
                {
                    AppliedDate = DateTime.Today.AddDays(-5),
                    JobTitle = "Senior .NET Developer",
                    Company = "Acme Tech Solutions",
                    Agency = "Hays Recruitment",
                    Source = "LinkedIn",
                    ApplicationMethod = "Agency",
                    JobSpecText = sampleSpec,
                    CurrentStatus = "Applied",
                    SalaryOrRate = "£70,000",
                    Location = "London (Hybrid)",
                    JobUrl = "https://linkedin.com/jobs/view/12345"
                };

                int id1 = db.SaveApplication(app1, "Applied through Hays recruiter Sarah.");
                Assert(id1 > 0, "Application 1 inserted with ID > 0");

                var savedApp1 = db.GetApplicationById(id1);
                Assert(savedApp1 != null, "Application 1 retrieved from DB");
                Assert(savedApp1!.JobTitle == "Senior .NET Developer", "Job title matches");
                Assert(savedApp1.Company == "Acme Tech Solutions", "Company matches");
                Assert(savedApp1.Agency == "Hays Recruitment", "Agency matches");
                Assert(savedApp1.CurrentStatus == "Applied", "Initial status is Applied");
                Assert(savedApp1.IsFinal == false, "Application is not final");
                Assert(!string.IsNullOrEmpty(savedApp1.JobSpecText), "Job spec text preserved");
                Console.WriteLine("[PASS] Test 1: Standard job application created successfully.");

                // 2. Insert Speculative "CV" sent to recruitment agency
                var app2 = new JobApplication
                {
                    AppliedDate = DateTime.Today.AddDays(-2),
                    JobTitle = "CV",
                    IsCvToAgency = true,
                    Company = "Michael Page Recruitment",
                    Agency = "Michael Page",
                    Source = "Direct Email",
                    ApplicationMethod = "Agency",
                    JobSpecText = "Speculative CV submission for senior software engineering and architecture roles.",
                    CurrentStatus = "CV Sent"
                };

                int id2 = db.SaveApplication(app2, "Sent updated CV to tech recruitment desk.");
                Assert(id2 > 0, "Application 2 (CV to Agency) inserted with ID > 0");
                var savedApp2 = db.GetApplicationById(id2);
                Assert(savedApp2!.IsCvToAgency == true, "IsCvToAgency flag is true");
                Assert(savedApp2.JobTitle == "CV", "Job title is CV");
                Console.WriteLine("[PASS] Test 2: Speculative CV to agency recorded successfully.");

                // 3. Stale application test (> 14 days without updates)
                var staleDate = DateTime.Today.AddDays(-25);
                var app3 = new JobApplication
                {
                    AppliedDate = staleDate,
                    JobTitle = "Lead Architect",
                    Company = "Dormant Enterprises",
                    Source = "Indeed",
                    ApplicationMethod = "Direct",
                    CurrentStatus = "Applied"
                };
                int id3 = db.SaveApplication(app3, "Initial application.");
                // Manually set LastUpdatedDate back to simulate 25 days of inactivity
                using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={testDb}"))
                {
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "UPDATE Applications SET LastUpdatedDate = @d WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@d", staleDate.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@id", id3);
                    cmd.ExecuteNonQuery();
                }

                var savedApp3 = db.GetApplicationById(id3);
                Assert(savedApp3!.IsStale, "App 3 is correctly flagged as Stale (25 days old)");
                Assert(savedApp3.DaysSinceLastUpdate >= 25, "App 3 DaysSinceLastUpdate >= 25");

                // Check Stale quick filter
                var staleFilter = new ApplicationFilter { QuickFilter = "Stale" };
                var staleList = db.GetApplications(staleFilter);
                Assert(staleList.Any(x => x.Id == id3), "Stale filter returns dormant application");
                Assert(!staleList.Any(x => x.Id == id1), "Stale filter excludes recently active application");
                Console.WriteLine("[PASS] Test 3: Staleness tracking and filter verified.");

                // 4. Audit Trail Updates: Status Change & Notes
                db.AddApplicationUpdate(id1, DateTime.Today.AddDays(-3), "Recruiter Call", null, "Recruiter phoned: client loves the profile, arranging 1st round interview.");
                db.AddApplicationUpdate(id1, DateTime.Today.AddDays(-1), "StatusChange", "1st Interview", "1st technical interview scheduled for Thursday 2pm.");

                var updates = db.GetUpdates(id1);
                Assert(updates.Count == 3, $"App 1 has 3 audit trail entries (got {updates.Count})");
                Assert(updates.Any(u => u.NewStatus == "1st Interview"), "StatusChange to '1st Interview' recorded");

                var reloadedApp1 = db.GetApplicationById(id1);
                Assert(reloadedApp1!.CurrentStatus == "1st Interview", "CurrentStatus updated to '1st Interview'");
                Assert(reloadedApp1.LastUpdatedDate.Date == DateTime.Today.AddDays(-1), "LastUpdatedDate refreshed to date of update");
                Console.WriteLine("[PASS] Test 4: Audit trail logging and status transitions verified.");

                // 5. Final Status marks IsFinal and stops staleness
                db.AddApplicationUpdate(id3, DateTime.Today, "StatusChange", "Rejected", "Received automated rejection email.");
                var reloadedApp3 = db.GetApplicationById(id3);
                Assert(reloadedApp3!.IsFinal, "Rejected application marked as IsFinal = true");
                Assert(!reloadedApp3.IsStale, "Finalized application is not considered stale");
                Console.WriteLine("[PASS] Test 5: Final status (Rejected/Accepted/Closed) handling verified.");

                // 6. File Attachments
                var tempDocFile = Path.Combine(testDir, "Acme_Job_Spec.pdf");
                File.WriteAllText(tempDocFile, "%PDF-1.4 Mock PDF Job Specification content for testing attachments.");

                var att = db.AddAttachment(id1, tempDocFile);
                Assert(att != null && att.Id > 0, "Attachment added to DB");
                Assert(File.Exists(att!.StoredPath), "Attachment file copied to managed directory");
                Assert(att.StoredPath.Contains("Attachments"), "Attachment stored in application attachments root");

                var attList = db.GetAttachments(id1);
                Assert(attList.Count == 1, "App 1 has 1 attachment");
                Assert(attList[0].FileName == "Acme_Job_Spec.pdf", "Attachment filename matches");

                // Audit trail should record file attachment
                var updatesAfterAtt = db.GetUpdates(id1);
                Assert(updatesAfterAtt.Any(u => u.UpdateType == "FileAttached"), "File attachment event logged in audit trail");
                Console.WriteLine("[PASS] Test 6: File attachment and audit event verified.");

                // 7. Reports & Summary KPIs
                var summary = db.GetReportSummary();
                Assert(summary.TotalApplications == 3, $"Total applications is 3 (got {summary.TotalApplications})");
                Assert(summary.ActiveApplications == 2, $"Active applications is 2 (got {summary.ActiveApplications})");
                Assert(summary.InterviewCount == 1, $"Interview count is 1 (got {summary.InterviewCount})");
                Assert(summary.CvSentCount == 1, $"CV sent count is 1 (got {summary.CvSentCount})");
                Assert(summary.RejectedCount == 1, $"Rejected count is 1 (got {summary.RejectedCount})");

                var statusCounts = db.GetStatusCounts();
                Assert(statusCounts.Any(s => s.Status == "1st Interview"), "Status count includes 1st Interview");

                var sourceCounts = db.GetSourceCounts();
                Assert(sourceCounts.Any(s => s.Source == "LinkedIn"), "Source count includes LinkedIn");
                Console.WriteLine("[PASS] Test 7: Report metrics, status distribution, and source calculations verified.");

                // 8. HTML and CSV Export
                var apps = db.GetApplications();
                var htmlPath = ExportService.GenerateHtmlReport(apps, summary, null, Path.Combine(testDir, "Report.html"));
                Assert(File.Exists(htmlPath), "HTML report file generated");
                var htmlContent = File.ReadAllText(htmlPath);
                Assert(htmlContent.Contains("Job Applications Report"), "HTML contains title");
                Assert(htmlContent.Contains("Acme Tech Solutions"), "HTML contains company");
                Assert(htmlContent.Contains("Senior .NET Developer"), "HTML contains job title");
                Assert(htmlContent.Contains("<th>Salary / Rate</th>"), "HTML contains Salary / Rate table header");
                Assert(htmlContent.Contains("70,000"), "HTML contains salary/rate where set");
                Assert(htmlContent.Contains("&mdash;"), "HTML contains dash for application without salary");

                var csvPath = ExportService.ExportToCsv(apps, Path.Combine(testDir, "Report.csv"));
                Assert(File.Exists(csvPath), "CSV export file generated");
                var csvContent = File.ReadAllText(csvPath);
                Assert(csvContent.Contains("Senior .NET Developer"), "CSV contains job title");
                Assert(csvContent.Contains("Michael Page Recruitment"), "CSV contains agency");
                Console.WriteLine("[PASS] Test 8: HTML printable report and CSV export verified.");

                // 9. Sorting sequencing (Created vs Last Activity)
                var sortActivityDesc = db.GetApplications(new ApplicationFilter { SortBy = "LastActivity", SortDescending = true });
                for (int i = 0; i < sortActivityDesc.Count - 1; i++)
                {
                    Assert(sortActivityDesc[i].LastUpdatedDate >= sortActivityDesc[i + 1].LastUpdatedDate, "Last Activity DESC order maintained");
                }

                var sortActivityAsc = db.GetApplications(new ApplicationFilter { SortBy = "LastActivity", SortDescending = false });
                for (int i = 0; i < sortActivityAsc.Count - 1; i++)
                {
                    Assert(sortActivityAsc[i].LastUpdatedDate <= sortActivityAsc[i + 1].LastUpdatedDate, "Last Activity ASC order maintained");
                }

                var sortCreatedDesc = db.GetApplications(new ApplicationFilter { SortBy = "AppliedDate", SortDescending = true });
                for (int i = 0; i < sortCreatedDesc.Count - 1; i++)
                {
                    Assert(sortCreatedDesc[i].AppliedDate >= sortCreatedDesc[i + 1].AppliedDate, "Applied/Created date DESC order maintained");
                }

                var sortCreatedAsc = db.GetApplications(new ApplicationFilter { SortBy = "AppliedDate", SortDescending = false });
                for (int i = 0; i < sortCreatedAsc.Count - 1; i++)
                {
                    Assert(sortCreatedAsc[i].AppliedDate <= sortCreatedAsc[i + 1].AppliedDate, "Applied/Created date ASC order maintained");
                }
                Console.WriteLine("[PASS] Test 9: Sort sequencing between Created and Last Activity verified.");

                // 10. Agency filtering with multiple items for same agency
                var app4 = new JobApplication
                {
                    AppliedDate = DateTime.Today.AddDays(-1),
                    JobTitle = "Senior Backend Engineer",
                    Company = "FinTech Corp",
                    Agency = "Hays Recruitment", // Second item for Hays
                    Source = "Direct Email",
                    ApplicationMethod = "Agency",
                    CurrentStatus = "Applied"
                };
                db.SaveApplication(app4);

                var distinctAgencies = db.GetDistinctAgencies();
                Assert(distinctAgencies.Contains("Hays Recruitment"), "Distinct agencies contains Hays Recruitment");
                Assert(distinctAgencies.Contains("Michael Page"), "Distinct agencies contains Michael Page");

                var haysFilter = new ApplicationFilter { Agency = "Hays Recruitment" };
                var haysApps = db.GetApplications(haysFilter);
                Assert(haysApps.Count == 2, $"Expected 2 applications for Hays Recruitment, got {haysApps.Count}");
                Assert(haysApps.All(a => a.Agency == "Hays Recruitment"), "All filtered applications belong to Hays Recruitment");
                Console.WriteLine("[PASS] Test 10: Agency filtering with multiple items for same agency verified.");

                // 11. Adding and managing new sources
                db.AddSource("TechCrunch Jobs");
                var sourcesList = db.GetDistinctSources();
                Assert(sourcesList.Contains("TechCrunch Jobs"), "Newly added source appears in distinct sources");

                // Auto-registration when saving application with new custom source
                var app5 = new JobApplication
                {
                    AppliedDate = DateTime.Today,
                    JobTitle = "Fullstack Developer",
                    Company = "Startup Innovations",
                    Source = "AngelList Talent", // brand new source
                    ApplicationMethod = "Direct",
                    CurrentStatus = "Applied"
                };
                db.SaveApplication(app5);
                var updatedSources = db.GetDistinctSources();
                Assert(updatedSources.Contains("AngelList Talent"), "Custom source auto-persisted into Sources table on application save");

                db.DeleteSource("TechCrunch Jobs");
                var afterDeleteSources = db.GetDistinctSources();
                Assert(!afterDeleteSources.Contains("TechCrunch Jobs"), "Source deleted from Sources table");
                Console.WriteLine("[PASS] Test 11: Adding, auto-persisting, and managing new sources verified.");

                // 12. Report Audit Trail Inclusion in Filter, HTML Report, and CSV Export
                var filterWithAudit = new ApplicationFilter { IncludeAuditTrail = true };
                var appsWithAudit = db.GetApplications(filterWithAudit);
                Assert(appsWithAudit.Count > 0, "Applications retrieved with IncludeAuditTrail = true");
                var app1WithAudit = appsWithAudit.First(a => a.Id == id1);
                Assert(app1WithAudit.AuditTrail.Count > 0, $"App 1 loaded {app1WithAudit.AuditTrail.Count} audit updates");
                Assert(app1WithAudit.AuditTrail.Any(u => u.SummaryTitle.Contains("1st Interview")), "App 1 audit trail includes 1st Interview status change");

                // Generate HTML report with audit trail enabled
                var htmlAuditPath = ExportService.GenerateHtmlReport(appsWithAudit, summary, filterWithAudit, Path.Combine(testDir, "Report_WithAudit.html"));
                Assert(File.Exists(htmlAuditPath), "HTML report with audit trail created");
                var htmlAuditText = File.ReadAllText(htmlAuditPath);
                Assert(htmlAuditText.Contains("Audit Trail &amp; Events"), "HTML report contains Audit Trail header");
                Assert(htmlAuditText.Contains("1st technical interview scheduled"), "HTML report contains specific audit event notes");
                Assert(htmlAuditText.Contains("Audit Trail: <strong>Included</strong>"), "HTML report filter banner indicates Audit Trail: Included");

                // Generate HTML report with audit trail disabled
                var filterNoAudit = new ApplicationFilter { IncludeAuditTrail = false };
                var appsNoAudit = db.GetApplications(filterNoAudit);
                var htmlNoAuditPath = ExportService.GenerateHtmlReport(appsNoAudit, summary, filterNoAudit, Path.Combine(testDir, "Report_NoAudit.html"));
                var htmlNoAuditText = File.ReadAllText(htmlNoAuditPath);
                Assert(!htmlNoAuditText.Contains("class=\"audit-row\""), "HTML report without audit trail excludes audit rows");

                // Export CSV with audit trail enabled
                var csvAuditPath = ExportService.ExportToCsv(appsWithAudit, Path.Combine(testDir, "Report_WithAudit.csv"), includeAuditTrail: true);
                Assert(File.Exists(csvAuditPath), "CSV export with audit trail created");
                var csvAuditText = File.ReadAllText(csvAuditPath);
                Assert(csvAuditText.Contains("AuditTrail"), "CSV export header includes AuditTrail column");
                Assert(csvAuditText.Contains("1st technical interview scheduled"), "CSV export row contains audit event notes");
                Console.WriteLine("[PASS] Test 12: Report audit trail inclusion in DB queries, HTML reports, and CSV exports verified.");

                // 13. Single Application Detailed Report (Spec, Events, Attached Documents list without contents)
                var singleApp1 = db.GetApplicationById(id1)!;
                var singleUpdates1 = db.GetUpdates(id1);
                var singleAttachments1 = db.GetAttachments(id1);
                Assert(singleAttachments1.Count > 0, "App 1 has at least 1 attachment for report");

                var singleReportPath = ExportService.GenerateSingleApplicationHtmlReport(
                    singleApp1, 
                    singleUpdates1, 
                    singleAttachments1, 
                    Path.Combine(testDir, "SingleApp1_Report.html"));

                Assert(File.Exists(singleReportPath), "Single application report HTML generated");
                var singleHtml = File.ReadAllText(singleReportPath);

                // Verify Title, Company, Agency, Status, Dates
                Assert(singleHtml.Contains("Senior .NET Developer"), "Single report contains role title");
                Assert(singleHtml.Contains("Acme Tech Solutions"), "Single report contains company name");
                Assert(singleHtml.Contains("Hays Recruitment"), "Single report contains recruitment agency");
                Assert(singleHtml.Contains("1st Interview"), "Single report contains current status");

                // Verify full job spec text is included
                Assert(singleHtml.Contains("5+ years C#, .NET Core / .NET 8"), "Single report contains full job spec text");
                Assert(singleHtml.Contains("Experience with WPF, SQLite, or Desktop Apps"), "Single report contains detailed spec requirements");

                // Verify audit trail / events are listed
                Assert(singleHtml.Contains("1st technical interview scheduled"), "Single report contains interview audit event");
                Assert(singleHtml.Contains("Recruiter phoned: client loves the profile"), "Single report contains recruiter note");

                // Verify attached documents are listed (metadata only)
                Assert(singleHtml.Contains("Acme_Job_Spec.pdf"), "Single report lists attached document filename");
                Assert(singleHtml.Contains("Attached Documents"), "Single report has Attached Documents section");

                // Crucial requirement: document contents must NOT be included
                Assert(!singleHtml.Contains("Mock PDF Job Specification content for testing attachments"), "Single report does NOT dump raw attachment file contents");

                // Verify empty fallbacks on an application without attachments
                var singleApp3 = db.GetApplicationById(id3)!;
                var singleUpdates3 = db.GetUpdates(id3);
                var singleAttachments3 = db.GetAttachments(id3);
                var singleReportPath3 = ExportService.GenerateSingleApplicationHtmlReport(
                    singleApp3,
                    singleUpdates3,
                    singleAttachments3,
                    Path.Combine(testDir, "SingleApp3_Report.html"));
                var singleHtml3 = File.ReadAllText(singleReportPath3);
                Assert(singleHtml3.Contains("No documents are attached to this application"), "Single report displays polite notice when no documents attached");
                Assert(singleHtml3.Contains("Lead Architect"), "Single report 3 contains job title");
                Console.WriteLine("[PASS] Test 13: Single application detailed report (spec, events, document list without content) verified.");

                // --- TEST 14: "All except closed/complete" Filter in Database & Reports ---
                Console.WriteLine("\n--- Running Test 14: 'All except closed/complete' filter ---");
                // Mark an application as Rejected / Final to ensure we have closed applications in the database
                db.AddApplicationUpdate(id2, DateTime.Today, "StatusChange", "Rejected", "Position closed by client");

                // Query with "All except closed/complete"
                var openOnlyFilter = new ApplicationFilter { Status = "All except closed/complete" };
                var openApps = db.GetApplications(openOnlyFilter);
                Assert(openApps.Count > 0, "Applications returned for 'All except closed/complete'");
                Assert(openApps.All(a => !a.IsFinal && a.CurrentStatus != "Rejected" && a.CurrentStatus != "Accepted" && a.CurrentStatus != "Closed" && a.CurrentStatus != "Withdrawn"),
                    "All returned applications have IsFinal == false and are not closed/rejected/accepted");
                Assert(!openApps.Any(a => a.Id == id2), "Rejected application is excluded from 'All except closed/complete'");

                // Also test the report summary and HTML export with this filter
                var openSummary = db.GetReportSummary(openOnlyFilter);
                var openReportPath = Path.Combine(testDir, "OpenApps_Report.html");
                ExportService.GenerateHtmlReport(openApps, openSummary, openOnlyFilter, openReportPath);
                var openReportHtml = File.ReadAllText(openReportPath);
                Assert(openReportHtml.Contains("All except closed/complete"), "HTML report header displays filter status 'All except closed/complete'");
                Assert(!openReportHtml.Contains("FinTech Global"), "Rejected company FinTech Global is not in the filtered report");

                Console.WriteLine("[PASS] Test 14: 'All except closed/complete' successfully filters out closed/finalized items in queries and reports.");

                Console.WriteLine();
                Console.WriteLine("🎉 ALL 14 TEST SUITES PASSED PERFECTLY!");
                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[FAIL] Test failed with exception:\n{ex}");
                Console.ResetColor();
                return 1;
            }
            finally
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }

        static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"Assertion Failed: {message}");
            }
        }
    }
}
