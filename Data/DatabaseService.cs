using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;
using JobAppTracker.Models;

namespace JobAppTracker.Data
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly string _dbPath;
        private readonly string _attachmentsRoot;

        public string DatabasePath => _dbPath;
        public string AttachmentsRoot => _attachmentsRoot;

        public DatabaseService(string? customDbPath = null)
        {
            if (!string.IsNullOrWhiteSpace(customDbPath))
            {
                _dbPath = customDbPath;
            }
            else
            {
                var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JobAppTracker");
                Directory.CreateDirectory(appData);
                _dbPath = Path.Combine(appData, "job_applications.db");
            }

            var baseDir = Path.GetDirectoryName(_dbPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            _attachmentsRoot = Path.Combine(baseDir, "Attachments");
            Directory.CreateDirectory(_attachmentsRoot);

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            InitializeDatabase();
        }

        private SqliteConnection CreateConnection()
        {
            var conn = new SqliteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        public void InitializeDatabase()
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                PRAGMA foreign_keys = ON;

                CREATE TABLE IF NOT EXISTS Applications (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    AppliedDate TEXT NOT NULL,
                    JobTitle TEXT NOT NULL,
                    IsCvToAgency INTEGER NOT NULL DEFAULT 0,
                    Company TEXT NOT NULL,
                    Agency TEXT,
                    Source TEXT NOT NULL,
                    ApplicationMethod TEXT NOT NULL,
                    JobSpecText TEXT,
                    CurrentStatus TEXT NOT NULL,
                    IsFinal INTEGER NOT NULL DEFAULT 0,
                    LastUpdatedDate TEXT NOT NULL,
                    SalaryOrRate TEXT,
                    Location TEXT,
                    JobUrl TEXT,
                    CreatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS ApplicationUpdates (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ApplicationId INTEGER NOT NULL,
                    UpdateDate TEXT NOT NULL,
                    UpdateType TEXT NOT NULL,
                    PreviousStatus TEXT,
                    NewStatus TEXT,
                    Notes TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY(ApplicationId) REFERENCES Applications(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS Attachments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ApplicationId INTEGER NOT NULL,
                    FileName TEXT NOT NULL,
                    StoredPath TEXT NOT NULL,
                    FileSize INTEGER NOT NULL,
                    AttachedDate TEXT NOT NULL,
                    FOREIGN KEY(ApplicationId) REFERENCES Applications(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS Settings (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Sources (
                    Name TEXT PRIMARY KEY
                );

                CREATE INDEX IF NOT EXISTS idx_apps_status ON Applications(CurrentStatus);
                CREATE INDEX IF NOT EXISTS idx_apps_last_updated ON Applications(LastUpdatedDate);
                CREATE INDEX IF NOT EXISTS idx_updates_appid ON ApplicationUpdates(ApplicationId);
                CREATE INDEX IF NOT EXISTS idx_attachments_appid ON Attachments(ApplicationId);
            ";
            cmd.ExecuteNonQuery();

            // Seed standard sources if table empty
            cmd.CommandText = "SELECT COUNT(*) FROM Sources";
            long count = Convert.ToInt64(cmd.ExecuteScalar());
            if (count == 0)
            {
                var defaults = new[] { "LinkedIn", "Indeed", "Totaljobs", "Reed", "Company Website", "Referral", "Otta", "Glassdoor", "Direct Email", "Job Board", "Recruiter Outreach", "Other" };
                foreach (var s in defaults)
                {
                    cmd.CommandText = "INSERT OR IGNORE INTO Sources (Name) VALUES (@n)";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@n", s);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<JobApplication> GetApplications(ApplicationFilter? filter = null, int staleDays = 14)
        {
            var results = new List<JobApplication>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();

            var sb = new StringBuilder(@"
                SELECT a.Id, a.AppliedDate, a.JobTitle, a.IsCvToAgency, a.Company, a.Agency,
                       a.Source, a.ApplicationMethod, a.JobSpecText, a.CurrentStatus, a.IsFinal,
                       a.LastUpdatedDate, a.SalaryOrRate, a.Location, a.JobUrl, a.CreatedAt,
                       (SELECT COUNT(*) FROM Attachments att WHERE att.ApplicationId = a.Id) AS AttachmentCount,
                       (SELECT COUNT(*) FROM ApplicationUpdates u WHERE u.ApplicationId = a.Id) AS UpdateCount
                FROM Applications a
                WHERE 1=1
            ");

            if (filter != null)
            {
                if (!string.IsNullOrWhiteSpace(filter.SearchText))
                {
                    sb.Append(@" AND (a.JobTitle LIKE @search OR a.Company LIKE @search 
                                     OR (a.Agency IS NOT NULL AND a.Agency LIKE @search)
                                     OR (a.JobSpecText IS NOT NULL AND a.JobSpecText LIKE @search)
                                     OR a.Source LIKE @search)");
                    cmd.Parameters.AddWithValue("@search", $"%{filter.SearchText.Trim()}%");
                }

                if (!string.IsNullOrWhiteSpace(filter.Status) && filter.Status != "All")
                {
                    if (filter.Status.Equals("All except closed/complete", StringComparison.OrdinalIgnoreCase) ||
                        filter.Status.Equals("All except closed", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append(" AND a.IsFinal = 0 AND a.CurrentStatus NOT IN ('Accepted', 'Rejected', 'Withdrawn', 'Closed')");
                    }
                    else
                    {
                        sb.Append(" AND a.CurrentStatus = @status");
                        cmd.Parameters.AddWithValue("@status", filter.Status);
                    }
                }

                if (!string.IsNullOrWhiteSpace(filter.Source) && filter.Source != "All")
                {
                    sb.Append(" AND a.Source = @source");
                    cmd.Parameters.AddWithValue("@source", filter.Source);
                }

                if (!string.IsNullOrWhiteSpace(filter.Method) && filter.Method != "All")
                {
                    sb.Append(" AND a.ApplicationMethod = @method");
                    cmd.Parameters.AddWithValue("@method", filter.Method);
                }

                if (!string.IsNullOrWhiteSpace(filter.Company))
                {
                    sb.Append(" AND a.Company LIKE @company");
                    cmd.Parameters.AddWithValue("@company", $"%{filter.Company.Trim()}%");
                }

                if (!string.IsNullOrWhiteSpace(filter.Agency) && filter.Agency != "All")
                {
                    sb.Append(" AND a.Agency = @agency");
                    cmd.Parameters.AddWithValue("@agency", filter.Agency.Trim());
                }

                if (filter.FromDate.HasValue)
                {
                    sb.Append(" AND a.AppliedDate >= @fromDate");
                    cmd.Parameters.AddWithValue("@fromDate", filter.FromDate.Value.ToString("yyyy-MM-dd"));
                }

                if (filter.ToDate.HasValue)
                {
                    sb.Append(" AND a.AppliedDate <= @toDate");
                    cmd.Parameters.AddWithValue("@toDate", filter.ToDate.Value.ToString("yyyy-MM-dd"));
                }

                // Quick Filter Pills
                switch (filter.QuickFilter)
                {
                    case "Active":
                        sb.Append(" AND a.IsFinal = 0");
                        break;
                    case "Stale":
                        var thresholdDate = DateTime.Today.AddDays(-staleDays).ToString("yyyy-MM-dd");
                        sb.Append(" AND a.IsFinal = 0 AND a.LastUpdatedDate <= @thresholdDate");
                        cmd.Parameters.AddWithValue("@thresholdDate", thresholdDate);
                        break;
                    case "Final":
                        sb.Append(" AND a.IsFinal = 1");
                        break;
                    case "Interviews":
                        sb.Append(" AND (a.CurrentStatus LIKE '%Interview%' OR a.CurrentStatus LIKE '%Offer%')");
                        break;
                }
            }
            string sortCol = "a.LastUpdatedDate";
            if (filter != null)
            {
                if (filter.SortBy == "AppliedDate" || filter.SortBy == "Created")
                {
                    sortCol = "a.AppliedDate";
                }
                else if (filter.SortBy == "CreatedAt")
                {
                    sortCol = "a.CreatedAt";
                }
            }

            string sortDir = (filter != null && !filter.SortDescending) ? "ASC" : "DESC";
            sb.Append($" ORDER BY {sortCol} {sortDir}, a.Id {sortDir}");
            cmd.CommandText = sb.ToString();

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(MapJobApplication(reader));
            }

            if (filter != null && filter.IncludeAuditTrail && results.Count > 0)
            {
                var updatesMap = GetUpdatesForApplications(results.Select(r => r.Id));
                foreach (var app in results)
                {
                    if (updatesMap.TryGetValue(app.Id, out var updates))
                    {
                        app.AuditTrail = updates;
                    }
                }
            }

            return results;
        }

        public JobApplication? GetApplicationById(int id)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT a.Id, a.AppliedDate, a.JobTitle, a.IsCvToAgency, a.Company, a.Agency,
                       a.Source, a.ApplicationMethod, a.JobSpecText, a.CurrentStatus, a.IsFinal,
                       a.LastUpdatedDate, a.SalaryOrRate, a.Location, a.JobUrl, a.CreatedAt,
                       (SELECT COUNT(*) FROM Attachments att WHERE att.ApplicationId = a.Id) AS AttachmentCount,
                       (SELECT COUNT(*) FROM ApplicationUpdates u WHERE u.ApplicationId = a.Id) AS UpdateCount
                FROM Applications a
                WHERE a.Id = @id
            ";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return MapJobApplication(reader);
            }
            return null;
        }

        public int SaveApplication(JobApplication app, string? initialNote = null, List<string>? initialFiles = null)
        {
            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();

            app.IsFinal = IsFinalStatus(app.CurrentStatus);

            if (app.Id == 0)
            {
                // INSERT
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO Applications (
                        AppliedDate, JobTitle, IsCvToAgency, Company, Agency, Source,
                        ApplicationMethod, JobSpecText, CurrentStatus, IsFinal, LastUpdatedDate,
                        SalaryOrRate, Location, JobUrl, CreatedAt
                    ) VALUES (
                        @appliedDate, @jobTitle, @isCv, @company, @agency, @source,
                        @method, @spec, @status, @isFinal, @lastUpdated,
                        @salary, @location, @url, @createdAt
                    );
                    SELECT last_insert_rowid();
                ";
                cmd.Parameters.AddWithValue("@appliedDate", app.AppliedDate.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@jobTitle", app.JobTitle);
                cmd.Parameters.AddWithValue("@isCv", app.IsCvToAgency ? 1 : 0);
                cmd.Parameters.AddWithValue("@company", app.Company);
                cmd.Parameters.AddWithValue("@agency", (object?)app.Agency ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@source", app.Source);
                cmd.Parameters.AddWithValue("@method", app.ApplicationMethod);
                cmd.Parameters.AddWithValue("@spec", (object?)app.JobSpecText ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@status", app.CurrentStatus);
                cmd.Parameters.AddWithValue("@isFinal", app.IsFinal ? 1 : 0);
                cmd.Parameters.AddWithValue("@lastUpdated", app.AppliedDate.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@salary", (object?)app.SalaryOrRate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@location", (object?)app.Location ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@url", (object?)app.JobUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@createdAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                app.Id = Convert.ToInt32(cmd.ExecuteScalar());

                // Initial audit update
                using var updateCmd = conn.CreateCommand();
                updateCmd.Transaction = tx;
                updateCmd.CommandText = @"
                    INSERT INTO ApplicationUpdates (
                        ApplicationId, UpdateDate, UpdateType, PreviousStatus, NewStatus, Notes, CreatedAt
                    ) VALUES (
                        @appId, @updateDate, 'Created', NULL, @status, @notes, @createdAt
                    );
                ";
                updateCmd.Parameters.AddWithValue("@appId", app.Id);
                updateCmd.Parameters.AddWithValue("@updateDate", app.AppliedDate.ToString("yyyy-MM-dd"));
                updateCmd.Parameters.AddWithValue("@status", app.CurrentStatus);
                var notes = string.IsNullOrWhiteSpace(initialNote) 
                    ? (app.IsCvToAgency ? "Speculative CV sent to agency." : "Application submitted.")
                    : initialNote;
                updateCmd.Parameters.AddWithValue("@notes", notes);
                updateCmd.Parameters.AddWithValue("@createdAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                updateCmd.ExecuteNonQuery();

                // Process initial files if any
                if (initialFiles != null && initialFiles.Count > 0)
                {
                    foreach (var filePath in initialFiles)
                    {
                        if (File.Exists(filePath))
                        {
                            SaveAttachmentInternal(conn, tx, app.Id, filePath);
                        }
                    }
                }
            }
            else
            {
                // UPDATE
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    UPDATE Applications SET
                        AppliedDate = @appliedDate,
                        JobTitle = @jobTitle,
                        IsCvToAgency = @isCv,
                        Company = @company,
                        Agency = @agency,
                        Source = @source,
                        ApplicationMethod = @method,
                        JobSpecText = @spec,
                        CurrentStatus = @status,
                        IsFinal = @isFinal,
                        SalaryOrRate = @salary,
                        Location = @location,
                        JobUrl = @url
                    WHERE Id = @id
                ";
                cmd.Parameters.AddWithValue("@id", app.Id);
                cmd.Parameters.AddWithValue("@appliedDate", app.AppliedDate.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@jobTitle", app.JobTitle);
                cmd.Parameters.AddWithValue("@isCv", app.IsCvToAgency ? 1 : 0);
                cmd.Parameters.AddWithValue("@company", app.Company);
                cmd.Parameters.AddWithValue("@agency", (object?)app.Agency ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@source", app.Source);
                cmd.Parameters.AddWithValue("@method", app.ApplicationMethod);
                cmd.Parameters.AddWithValue("@spec", (object?)app.JobSpecText ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@status", app.CurrentStatus);
                cmd.Parameters.AddWithValue("@isFinal", app.IsFinal ? 1 : 0);
                cmd.Parameters.AddWithValue("@salary", (object?)app.SalaryOrRate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@location", (object?)app.Location ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@url", (object?)app.JobUrl ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            // Ensure source is saved to Sources table
            if (!string.IsNullOrWhiteSpace(app.Source))
            {
                using var srcCmd = conn.CreateCommand();
                srcCmd.Transaction = tx;
                srcCmd.CommandText = "INSERT OR IGNORE INTO Sources (Name) VALUES (@srcName)";
                srcCmd.Parameters.AddWithValue("@srcName", app.Source.Trim());
                srcCmd.ExecuteNonQuery();
            }

            tx.Commit();
            return app.Id;
        }

        public void AddApplicationUpdate(int applicationId, DateTime updateDate, string updateType, 
            string? newStatus, string notes)
        {
            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();

            string? currentStatus = null;
            using (var readCmd = conn.CreateCommand())
            {
                readCmd.Transaction = tx;
                readCmd.CommandText = "SELECT CurrentStatus FROM Applications WHERE Id = @id";
                readCmd.Parameters.AddWithValue("@id", applicationId);
                currentStatus = readCmd.ExecuteScalar()?.ToString();
            }

            string? prevStatus = currentStatus;
            bool statusChanged = !string.IsNullOrWhiteSpace(newStatus) && !string.Equals(newStatus, currentStatus, StringComparison.OrdinalIgnoreCase);

            if (statusChanged)
            {
                updateType = "StatusChange";
            }

            using (var insCmd = conn.CreateCommand())
            {
                insCmd.Transaction = tx;
                insCmd.CommandText = @"
                    INSERT INTO ApplicationUpdates (
                        ApplicationId, UpdateDate, UpdateType, PreviousStatus, NewStatus, Notes, CreatedAt
                    ) VALUES (
                        @appId, @date, @type, @prev, @new, @notes, @created
                    );
                ";
                insCmd.Parameters.AddWithValue("@appId", applicationId);
                insCmd.Parameters.AddWithValue("@date", updateDate.ToString("yyyy-MM-dd"));
                insCmd.Parameters.AddWithValue("@type", updateType);
                insCmd.Parameters.AddWithValue("@prev", (object?)prevStatus ?? DBNull.Value);
                insCmd.Parameters.AddWithValue("@new", (object?)newStatus ?? DBNull.Value);
                insCmd.Parameters.AddWithValue("@notes", notes);
                insCmd.Parameters.AddWithValue("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                insCmd.ExecuteNonQuery();
            }

            using (var updCmd = conn.CreateCommand())
            {
                updCmd.Transaction = tx;
                if (statusChanged && newStatus != null)
                {
                    bool isFinal = IsFinalStatus(newStatus);
                    updCmd.CommandText = @"
                        UPDATE Applications SET
                            LastUpdatedDate = @date,
                            CurrentStatus = @newStatus,
                            IsFinal = @isFinal
                        WHERE Id = @id
                    ";
                    updCmd.Parameters.AddWithValue("@newStatus", newStatus);
                    updCmd.Parameters.AddWithValue("@isFinal", isFinal ? 1 : 0);
                }
                else
                {
                    updCmd.CommandText = @"
                        UPDATE Applications SET
                            LastUpdatedDate = @date
                        WHERE Id = @id
                    ";
                }
                updCmd.Parameters.AddWithValue("@date", updateDate.ToString("yyyy-MM-dd"));
                updCmd.Parameters.AddWithValue("@id", applicationId);
                updCmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        public List<ApplicationUpdate> GetUpdates(int applicationId)
        {
            var list = new List<ApplicationUpdate>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, ApplicationId, UpdateDate, UpdateType, PreviousStatus, NewStatus, Notes, CreatedAt
                FROM ApplicationUpdates
                WHERE ApplicationId = @appId
                ORDER BY UpdateDate DESC, Id DESC
            ";
            cmd.Parameters.AddWithValue("@appId", applicationId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ApplicationUpdate
                {
                    Id = reader.GetInt32(0),
                    ApplicationId = reader.GetInt32(1),
                    UpdateDate = DateTime.Parse(reader.GetString(2)),
                    UpdateType = reader.GetString(3),
                    PreviousStatus = reader.IsDBNull(4) ? null : reader.GetString(4),
                    NewStatus = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Notes = reader.GetString(6),
                    CreatedAt = DateTime.Parse(reader.GetString(7))
                });
            }
            return list;
        }

        public Dictionary<int, List<ApplicationUpdate>> GetUpdatesForApplications(IEnumerable<int> appIds)
        {
            var dict = new Dictionary<int, List<ApplicationUpdate>>();
            var ids = appIds.Distinct().ToList();
            if (ids.Count == 0) return dict;

            foreach (var id in ids)
            {
                dict[id] = new List<ApplicationUpdate>();
            }

            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();

            var paramNames = new List<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                var p = $"@id{i}";
                paramNames.Add(p);
                cmd.Parameters.AddWithValue(p, ids[i]);
            }

            cmd.CommandText = $@"
                SELECT Id, ApplicationId, UpdateDate, UpdateType, PreviousStatus, NewStatus, Notes, CreatedAt
                FROM ApplicationUpdates
                WHERE ApplicationId IN ({string.Join(",", paramNames)})
                ORDER BY UpdateDate DESC, Id DESC
            ";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var appId = reader.GetInt32(1);
                var update = new ApplicationUpdate
                {
                    Id = reader.GetInt32(0),
                    ApplicationId = appId,
                    UpdateDate = DateTime.Parse(reader.GetString(2)),
                    UpdateType = reader.GetString(3),
                    PreviousStatus = reader.IsDBNull(4) ? null : reader.GetString(4),
                    NewStatus = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Notes = reader.GetString(6),
                    CreatedAt = DateTime.Parse(reader.GetString(7))
                };
                if (dict.TryGetValue(appId, out var list))
                {
                    list.Add(update);
                }
            }

            return dict;
        }

        public Attachment AddAttachment(int applicationId, string originalFilePath)
        {
            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();
            var att = SaveAttachmentInternal(conn, tx, applicationId, originalFilePath);

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO ApplicationUpdates (
                        ApplicationId, UpdateDate, UpdateType, PreviousStatus, NewStatus, Notes, CreatedAt
                    ) VALUES (
                        @appId, @date, 'FileAttached', NULL, NULL, @notes, @created
                    );
                    UPDATE Applications SET LastUpdatedDate = @date WHERE Id = @appId;
                ";
                cmd.Parameters.AddWithValue("@appId", applicationId);
                cmd.Parameters.AddWithValue("@date", DateTime.Today.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@notes", $"Attached file: {att.FileName}");
                cmd.Parameters.AddWithValue("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
            return att;
        }

        private Attachment SaveAttachmentInternal(SqliteConnection conn, SqliteTransaction tx, int applicationId, string originalFilePath)
        {
            var appFolder = Path.Combine(_attachmentsRoot, applicationId.ToString());
            Directory.CreateDirectory(appFolder);

            var fileName = Path.GetFileName(originalFilePath);
            var destPath = Path.Combine(appFolder, $"{Guid.NewGuid()}_{fileName}");
            File.Copy(originalFilePath, destPath, true);

            var fileInfo = new FileInfo(destPath);

            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO Attachments (ApplicationId, FileName, StoredPath, FileSize, AttachedDate)
                VALUES (@appId, @name, @path, @size, @date);
                SELECT last_insert_rowid();
            ";
            cmd.Parameters.AddWithValue("@appId", applicationId);
            cmd.Parameters.AddWithValue("@name", fileName);
            cmd.Parameters.AddWithValue("@path", destPath);
            cmd.Parameters.AddWithValue("@size", fileInfo.Length);
            cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            var id = Convert.ToInt32(cmd.ExecuteScalar());
            return new Attachment
            {
                Id = id,
                ApplicationId = applicationId,
                FileName = fileName,
                StoredPath = destPath,
                FileSize = fileInfo.Length,
                AttachedDate = DateTime.Now
            };
        }

        public List<Attachment> GetAttachments(int applicationId)
        {
            var list = new List<Attachment>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, ApplicationId, FileName, StoredPath, FileSize, AttachedDate
                FROM Attachments
                WHERE ApplicationId = @appId
                ORDER BY AttachedDate DESC
            ";
            cmd.Parameters.AddWithValue("@appId", applicationId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Attachment
                {
                    Id = reader.GetInt32(0),
                    ApplicationId = reader.GetInt32(1),
                    FileName = reader.GetString(2),
                    StoredPath = reader.GetString(3),
                    FileSize = reader.GetInt64(4),
                    AttachedDate = DateTime.Parse(reader.GetString(5))
                });
            }
            return list;
        }

        public void DeleteAttachment(int attachmentId)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT StoredPath FROM Attachments WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", attachmentId);
            var path = cmd.ExecuteScalar()?.ToString();

            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                try { File.Delete(path); } catch { }
            }

            cmd.CommandText = "DELETE FROM Attachments WHERE Id = @id";
            cmd.ExecuteNonQuery();
        }

        public void DeleteApplication(int applicationId)
        {
            var appFolder = Path.Combine(_attachmentsRoot, applicationId.ToString());
            if (Directory.Exists(appFolder))
            {
                try { Directory.Delete(appFolder, true); } catch { }
            }

            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Applications WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", applicationId);
            cmd.ExecuteNonQuery();
        }

        public ReportSummary GetReportSummary(ApplicationFilter? filter = null, int staleDays = 14)
        {
            var apps = GetApplications(filter, staleDays);
            var summary = new ReportSummary
            {
                TotalApplications = apps.Count,
                ActiveApplications = 0,
                StaleApplications = 0,
                InterviewCount = 0,
                OfferCount = 0,
                AcceptedCount = 0,
                RejectedCount = 0,
                WithdrawnCount = 0,
                CvSentCount = 0
            };

            foreach (var app in apps)
            {
                if (app.IsCvToAgency) summary.CvSentCount++;
                if (!app.IsFinal)
                {
                    summary.ActiveApplications++;
                    if (app.DaysSinceLastUpdate >= staleDays)
                    {
                        summary.StaleApplications++;
                    }
                }

                if (app.CurrentStatus.Contains("Interview", StringComparison.OrdinalIgnoreCase))
                    summary.InterviewCount++;
                else if (app.CurrentStatus.Equals("Offer Received", StringComparison.OrdinalIgnoreCase))
                    summary.OfferCount++;
                else if (app.CurrentStatus.Equals("Accepted", StringComparison.OrdinalIgnoreCase))
                    summary.AcceptedCount++;
                else if (app.CurrentStatus.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
                    summary.RejectedCount++;
                else if (app.CurrentStatus.Equals("Withdrawn", StringComparison.OrdinalIgnoreCase) || 
                         app.CurrentStatus.Equals("Closed", StringComparison.OrdinalIgnoreCase))
                    summary.WithdrawnCount++;
            }

            return summary;
        }

        public List<StatusCount> GetStatusCounts(ApplicationFilter? filter = null, int staleDays = 14)
        {
            var apps = GetApplications(filter, staleDays);
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var app in apps)
            {
                var s = app.CurrentStatus;
                dict[s] = dict.GetValueOrDefault(s, 0) + 1;
            }

            var list = new List<StatusCount>();
            int total = apps.Count;
            foreach (var kvp in dict)
            {
                list.Add(new StatusCount
                {
                    Status = kvp.Key,
                    Count = kvp.Value,
                    Percentage = total > 0 ? Math.Round((double)kvp.Value / total * 100, 1) : 0
                });
            }

            list.Sort((a, b) => b.Count.CompareTo(a.Count));
            return list;
        }

        public List<SourceCount> GetSourceCounts(ApplicationFilter? filter = null, int staleDays = 14)
        {
            var apps = GetApplications(filter, staleDays);
            var dict = new Dictionary<string, (int Total, int Success)>(StringComparer.OrdinalIgnoreCase);

            foreach (var app in apps)
            {
                var src = string.IsNullOrWhiteSpace(app.Source) ? "Other" : app.Source;
                var (curTotal, curSuccess) = dict.TryGetValue(src, out var existing) ? existing : (0, 0);
                bool isSuccessOrInterview = app.CurrentStatus.Contains("Interview", StringComparison.OrdinalIgnoreCase) 
                                            || app.CurrentStatus.Equals("Offer Received", StringComparison.OrdinalIgnoreCase)
                                            || app.CurrentStatus.Equals("Accepted", StringComparison.OrdinalIgnoreCase);
                dict[src] = (curTotal + 1, curSuccess + (isSuccessOrInterview ? 1 : 0));
            }

            var list = new List<SourceCount>();
            foreach (var kvp in dict)
            {
                list.Add(new SourceCount
                {
                    Source = kvp.Key,
                    Total = kvp.Value.Total,
                    SuccessOrInterview = kvp.Value.Success
                });
            }

            list.Sort((a, b) => b.Total.CompareTo(a.Total));
            return list;
        }

        public List<string> GetDistinctSources()
        {
            var list = new List<string>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            // Read from Sources table
            cmd.CommandText = "SELECT Name FROM Sources ORDER BY Name COLLATE NOCASE";
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var s = reader.GetString(0);
                    if (!list.Contains(s, StringComparer.OrdinalIgnoreCase))
                    {
                        list.Add(s);
                    }
                }
            }

            // Also include any sources from existing applications
            cmd.CommandText = "SELECT DISTINCT Source FROM Applications WHERE Source IS NOT NULL AND Source <> ''";
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var s = reader.GetString(0);
                    if (!list.Contains(s, StringComparer.OrdinalIgnoreCase))
                    {
                        list.Add(s);
                    }
                }
            }

            if (!list.Contains("Other", StringComparer.OrdinalIgnoreCase))
            {
                list.Add("Other");
            }
            return list;
        }

        public void AddSource(string sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName)) return;
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO Sources (Name) VALUES (@n)";
            cmd.Parameters.AddWithValue("@n", sourceName.Trim());
            cmd.ExecuteNonQuery();
        }

        public void DeleteSource(string sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName)) return;
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Sources WHERE Name = @n";
            cmd.Parameters.AddWithValue("@n", sourceName.Trim());
            cmd.ExecuteNonQuery();
        }

        public List<string> GetDistinctAgencies()
        {
            var list = new List<string>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT Agency FROM Applications WHERE Agency IS NOT NULL AND TRIM(Agency) <> '' ORDER BY Agency COLLATE NOCASE";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var s = reader.GetString(0).Trim();
                if (!string.IsNullOrWhiteSpace(s) && !list.Contains(s, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(s);
                }
            }
            return list;
        }

        public List<string> GetDistinctMethods()
        {
            var list = new List<string> { "Direct", "Agency", "LinkedIn Easy Apply", "Company Portal", "Email", "Referral", "Other" };
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT ApplicationMethod FROM Applications WHERE ApplicationMethod IS NOT NULL AND ApplicationMethod <> ''";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var s = reader.GetString(0);
                if (!list.Contains(s, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(s);
                }
            }
            return list;
        }

        public List<string> GetDistinctStatuses()
        {
            return new List<string>
            {
                "Applied",
                "CV Sent",
                "Screening",
                "1st Interview",
                "2nd Interview",
                "Final Interview",
                "Offer Received",
                "Accepted",
                "Rejected",
                "Withdrawn",
                "Closed"
            };
        }

        public static bool IsFinalStatus(string status)
        {
            return status switch
            {
                "Accepted" or "Rejected" or "Withdrawn" or "Closed" => true,
                _ => false
            };
        }

        public string GetSetting(string key, string defaultValue)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Settings WHERE Key = @k";
            cmd.Parameters.AddWithValue("@k", key);
            var val = cmd.ExecuteScalar()?.ToString();
            return val ?? defaultValue;
        }

        public void SetSetting(string key, string value)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Settings (Key, Value) VALUES (@k, @v) ON CONFLICT(Key) DO UPDATE SET Value = @v";
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@v", value);
            cmd.ExecuteNonQuery();
        }

        private static JobApplication MapJobApplication(SqliteDataReader reader)
        {
            return new JobApplication
            {
                Id = reader.GetInt32(0),
                AppliedDate = DateTime.Parse(reader.GetString(1)),
                JobTitle = reader.GetString(2),
                IsCvToAgency = reader.GetInt32(3) == 1,
                Company = reader.GetString(4),
                Agency = reader.IsDBNull(5) ? null : reader.GetString(5),
                Source = reader.GetString(6),
                ApplicationMethod = reader.GetString(7),
                JobSpecText = reader.IsDBNull(8) ? null : reader.GetString(8),
                CurrentStatus = reader.GetString(9),
                IsFinal = reader.GetInt32(10) == 1,
                LastUpdatedDate = DateTime.Parse(reader.GetString(11)),
                SalaryOrRate = reader.IsDBNull(12) ? null : reader.GetString(12),
                Location = reader.IsDBNull(13) ? null : reader.GetString(13),
                JobUrl = reader.IsDBNull(14) ? null : reader.GetString(14),
                CreatedAt = DateTime.Parse(reader.GetString(15)),
                AttachmentCount = reader.GetInt32(16),
                UpdateCount = reader.GetInt32(17)
            };
        }
    }
}
