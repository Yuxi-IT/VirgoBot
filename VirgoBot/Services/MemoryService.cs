using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace VirgoBot.Services;

public class MemoryService : IDisposable
{
    private SqliteConnection _conn;
    private int _messageLimit;
    private bool _disposed;

    private const string MemorysDirectory = "memorys";

    public string CurrentDbName { get; private set; } = "";

    public MemoryService(string? dbFileName = null, int messageLimit = 20)
    {
        _messageLimit = messageLimit;
        Directory.CreateDirectory(MemorysDirectory);

        if (string.IsNullOrWhiteSpace(dbFileName))
            dbFileName = $"{Guid.NewGuid()}.db";

        CurrentDbName = dbFileName;
        var fullPath = Path.Combine(MemorysDirectory, dbFileName);
        _conn = new SqliteConnection($"Data Source={fullPath};Cache=Shared");
        _conn.Open();
        ExecutePragma(_conn);
        InitDatabase();
    }

    public void SwitchDatabase(string dbFileName)
    {
        if (string.IsNullOrWhiteSpace(dbFileName))
            throw new ArgumentException("Database file name cannot be empty");

        _conn.Close();
        _conn.Dispose();

        CurrentDbName = dbFileName;
        var fullPath = Path.Combine(MemorysDirectory, dbFileName);
        _conn = new SqliteConnection($"Data Source={fullPath};Cache=Shared");
        _conn.Open();
        ExecutePragma(_conn);
        InitDatabase();
    }

    public string CreateSession()
    {
        var dbFileName = $"{Guid.NewGuid()}.db";
        var fullPath = Path.Combine(MemorysDirectory, dbFileName);

        using var conn = new SqliteConnection($"Data Source={fullPath};Cache=Shared");
        conn.Open();
        ExecutePragma(conn);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS messages (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                role TEXT NOT NULL,
                content TEXT NOT NULL,
                created_at DATETIME DEFAULT (datetime('now','localtime'))
            );
            CREATE TABLE IF NOT EXISTS soul (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                content TEXT NOT NULL,
                created_at DATETIME DEFAULT (datetime('now','localtime'))
            );
            CREATE TABLE IF NOT EXISTS session_meta (
                key TEXT PRIMARY KEY,
                value TEXT
            );
            CREATE TABLE IF NOT EXISTS task_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                task_id TEXT NOT NULL,
                status TEXT NOT NULL,
                result TEXT DEFAULT '',
                duration_ms INTEGER DEFAULT 0,
                executed_at DATETIME DEFAULT (datetime('now','localtime'))
            );
            CREATE TABLE IF NOT EXISTS soul_versions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                soul_id INTEGER NOT NULL,
                content TEXT NOT NULL,
                tags TEXT,
                weight REAL,
                version INTEGER NOT NULL,
                changed_at DATETIME DEFAULT (datetime('now','localtime'))
            );
            CREATE TABLE IF NOT EXISTS soul_context_links (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                soul_id INTEGER,
                message_id INTEGER,
                link_type TEXT DEFAULT 'derived'
            );
            CREATE TABLE IF NOT EXISTS feedback (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                message_id INTEGER,
                rating INTEGER,
                comment TEXT,
                skill_name TEXT,
                tool_name TEXT,
                created_at DATETIME DEFAULT (datetime('now','localtime'))
            )";
        cmd.ExecuteNonQuery();
        conn.Close();
        return dbFileName;
    }

    public void DeleteSession(string dbFileName)
    {
        if (string.IsNullOrWhiteSpace(dbFileName))
            throw new ArgumentException("Database file name cannot be empty");

        var fullPath = Path.Combine(MemorysDirectory, dbFileName);
        if (File.Exists(fullPath))
        {
            SqliteConnection.ClearAllPools();
            File.Delete(fullPath);
            var walPath = fullPath + "-wal";
            var shmPath = fullPath + "-shm";
            if (File.Exists(walPath)) File.Delete(walPath);
            if (File.Exists(shmPath)) File.Delete(shmPath);
        }
    }

    public List<SessionInfo> GetAllSessions()
    {
        Directory.CreateDirectory(MemorysDirectory);
        var sessions = new List<SessionInfo>();

        foreach (var file in Directory.GetFiles(MemorysDirectory, "*.db"))
        {
            var fileName = Path.GetFileName(file);
            var fileInfo = new FileInfo(file);
            int messageCount = 0, soulCount = 0;
            string? sessionName = null;

            try
            {
                using var conn = new SqliteConnection($"Data Source={file};Mode=ReadOnly");
                conn.Open();

                using var msgCmd = conn.CreateCommand();
                msgCmd.CommandText = "SELECT COUNT(*) FROM messages";
                messageCount = Convert.ToInt32(msgCmd.ExecuteScalar());

                using var soulCmd = conn.CreateCommand();
                soulCmd.CommandText = "SELECT COUNT(*) FROM soul";
                soulCount = Convert.ToInt32(soulCmd.ExecuteScalar());

                using var metaCheck = conn.CreateCommand();
                metaCheck.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='session_meta'";
                if (metaCheck.ExecuteScalar() != null)
                {
                    using var nameCmd = conn.CreateCommand();
                    nameCmd.CommandText = "SELECT value FROM session_meta WHERE key = 'session_name'";
                    sessionName = nameCmd.ExecuteScalar() as string;
                }
                conn.Close();
            }
            catch { }

            sessions.Add(new SessionInfo
            {
                FileName = fileName, SessionName = sessionName,
                MessageCount = messageCount, SoulCount = soulCount,
                LastModified = fileInfo.LastWriteTimeUtc, Size = fileInfo.Length,
                IsCurrent = fileName == CurrentDbName
            });
        }

        return sessions.OrderByDescending(s => s.LastModified).ToList();
    }

    private static void ExecutePragma(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL;";
        cmd.ExecuteNonQuery();
    }

    private void InitDatabase()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS messages (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                role TEXT NOT NULL,
                content TEXT NOT NULL,
                created_at DATETIME DEFAULT (datetime('now','localtime'))
            );
            CREATE TABLE IF NOT EXISTS soul (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                content TEXT NOT NULL,
                created_at DATETIME DEFAULT (datetime('now','localtime'))
            );
            CREATE TABLE IF NOT EXISTS session_meta (
                key TEXT PRIMARY KEY,
                value TEXT
            );
            CREATE TABLE IF NOT EXISTS task_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                task_id TEXT NOT NULL,
                status TEXT NOT NULL,
                result TEXT DEFAULT '',
                duration_ms INTEGER DEFAULT 0,
                executed_at DATETIME DEFAULT (datetime('now','localtime'))
            );
            CREATE TABLE IF NOT EXISTS soul_versions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                soul_id INTEGER NOT NULL,
                content TEXT NOT NULL,
                tags TEXT,
                weight REAL,
                version INTEGER NOT NULL,
                changed_at DATETIME DEFAULT (datetime('now','localtime'))
            );
            CREATE TABLE IF NOT EXISTS soul_context_links (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                soul_id INTEGER,
                message_id INTEGER,
                link_type TEXT DEFAULT 'derived'
            );
            CREATE TABLE IF NOT EXISTS feedback (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                message_id INTEGER,
                rating INTEGER,
                comment TEXT,
                skill_name TEXT,
                tool_name TEXT,
                created_at DATETIME DEFAULT (datetime('now','localtime'))
            )";
        cmd.ExecuteNonQuery();

        MigrateDropUserIdColumn();
        MigrateSoulColumns();
    }

    /// <summary>
    /// Migrate old databases that have user_id column — recreate table without it.
    /// </summary>
    private void MigrateDropUserIdColumn()
    {
        try
        {
            using var checkCmd = _conn.CreateCommand();
            checkCmd.CommandText = "PRAGMA table_info(messages)";
            bool hasUserId = false;
            using var reader = checkCmd.ExecuteReader();
            while (reader.Read())
            {
                if (reader.GetString(1) == "user_id") { hasUserId = true; break; }
            }
            reader.Close();
            if (!hasUserId) return;

            using var migrate = _conn.CreateCommand();
            migrate.CommandText = @"
                CREATE TABLE messages_new (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    role TEXT NOT NULL,
                    content TEXT NOT NULL,
                    created_at DATETIME DEFAULT (datetime('now','localtime'))
                );
                INSERT INTO messages_new (id, role, content, created_at)
                    SELECT id, role, content, created_at FROM messages;
                DROP TABLE messages;
                ALTER TABLE messages_new RENAME TO messages;";
            migrate.ExecuteNonQuery();
        }
        catch { }
    }

    /// <summary>
    /// Migrate soul table to add tags, weight, access_count, last_accessed, source, forgotten columns.
    /// </summary>
    private void MigrateSoulColumns()
    {
        try
        {
            using var checkCmd = _conn.CreateCommand();
            checkCmd.CommandText = "PRAGMA table_info(soul)";
            var existingCols = new HashSet<string>();
            using var reader = checkCmd.ExecuteReader();
            while (reader.Read())
                existingCols.Add(reader.GetString(1));
            reader.Close();

            var migrations = new Dictionary<string, string>
            {
                ["tags"] = "ALTER TABLE soul ADD COLUMN tags TEXT DEFAULT ''",
                ["weight"] = "ALTER TABLE soul ADD COLUMN weight REAL DEFAULT 1.0",
                ["access_count"] = "ALTER TABLE soul ADD COLUMN access_count INTEGER DEFAULT 0",
                ["last_accessed"] = "ALTER TABLE soul ADD COLUMN last_accessed DATETIME",
                ["source"] = "ALTER TABLE soul ADD COLUMN source TEXT DEFAULT 'user'",
                ["forgotten"] = "ALTER TABLE soul ADD COLUMN forgotten INTEGER DEFAULT 0"
            };

            foreach (var (col, sql) in migrations)
            {
                if (!existingCols.Contains(col))
                {
                    using var migrate = _conn.CreateCommand();
                    migrate.CommandText = sql;
                    migrate.ExecuteNonQuery();
                }
            }
        }
        catch { }
    }

    public void UpdateMessageLimit(int newLimit) => _messageLimit = newLimit;

    public long SaveMessage(string role, object content)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO messages (role, content, created_at) VALUES (@role, @content, @time); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@role", role);
        cmd.Parameters.AddWithValue("@content", JsonSerializer.Serialize(content));
        cmd.Parameters.AddWithValue("@time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        return (long)cmd.ExecuteScalar()!;
    }

    public List<object> LoadMessages(int? limit = null)
    {
        var roundLimit = limit ?? _messageLimit;

        // Load all messages from newest to oldest, then pick last N rounds.
        // A "round" = one user message + one assistant message (tool messages don't count toward rounds).
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, role, content FROM messages ORDER BY id DESC";

        var rows = new List<(int id, string role, string content)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            rows.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        reader.Close();

        // Walk from newest to oldest, count rounds (user+assistant pairs), collect rows
        int rounds = 0;
        int cutoffIndex = rows.Count; // exclusive upper bound (rows are newest-first)
        for (int i = 0; i < rows.Count; i++)
        {
            var role = rows[i].role;
            if (role == "user" || role == "assistant")
            {
                if (role == "user") rounds++;
                if (rounds > roundLimit)
                {
                    cutoffIndex = i;
                    break;
                }
            }
        }

        var selected = rows.Take(cutoffIndex).ToList();
        selected.Reverse(); // back to chronological order

        return selected.Select(r =>
        {
            var content = JsonSerializer.Deserialize<JsonElement>(r.content);
            return (object)new { role = r.role, content };
        }).ToList();
    }

    public void ClearOldMessages(int? keepLast = null)
    {
        var effectiveKeep = keepLast ?? _messageLimit;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM messages WHERE id NOT IN (
                SELECT id FROM messages ORDER BY id DESC LIMIT @keep
            )";
        cmd.Parameters.AddWithValue("@keep", effectiveKeep);
        cmd.ExecuteNonQuery();
    }

    public void ClearAllMessages()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM messages";
        cmd.ExecuteNonQuery();
    }

    public int GetMessageCount()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM messages";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /* PLACEHOLDER_PAGINATION_AND_SOUL */

    public (List<MessageRecord> Messages, int Total) LoadMessagesWithPagination(int limit, int offset)
    {
        using var countCmd = _conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM messages";
        var total = Convert.ToInt32(countCmd.ExecuteScalar());

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, role, content, created_at FROM messages ORDER BY id DESC LIMIT @limit OFFSET @offset";
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        var messages = new List<MessageRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var contentJson = reader.GetString(2);
            string contentText;
            try
            {
                var content = JsonSerializer.Deserialize<JsonElement>(contentJson);
                if (content.ValueKind == JsonValueKind.Array)
                {
                    var parts = new List<string>();
                    foreach (var item in content.EnumerateArray())
                    {
                        if (item.TryGetProperty("text", out var textEl))
                            parts.Add(textEl.GetString() ?? "");
                        else if (item.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "tool_use")
                            parts.Add($"[tool: {item.GetProperty("name").GetString()}]");
                    }
                    contentText = string.Join(" ", parts);
                }
                else if (content.ValueKind == JsonValueKind.String)
                    contentText = content.GetString() ?? "";
                else if (content.ValueKind == JsonValueKind.Object &&
                         content.TryGetProperty("text", out var objTextEl))
                    contentText = objTextEl.GetString() ?? "";
                else if (content.ValueKind == JsonValueKind.Object &&
                         content.TryGetProperty("tool_call_id", out _) &&
                         content.TryGetProperty("content", out var toolResultEl))
                    contentText = ExtractTextContent(toolResultEl);
                else
                    contentText = contentJson;
            }
            catch { contentText = contentJson; }

            messages.Add(new MessageRecord
            {
                Id = reader.GetInt32(0),
                Role = reader.GetString(1),
                Content = contentText,
                CreatedAt = reader.IsDBNull(3) ? DateTime.Now : DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.AssumeLocal)
            });
        }
        messages.Reverse();
        return (messages, total);
    }

    public List<SoulRecord> GetAllSoulEntries()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, content, created_at, tags, weight, access_count, last_accessed, source, forgotten FROM soul WHERE forgotten = 0 ORDER BY weight DESC, id ASC";
        var entries = new List<SoulRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(ReadSoulRecord(reader));
        }
        return entries;
    }

    public List<SoulRecord> SearchSoul(string? keyword = null, string? tagFilter = null, int limit = 50)
    {
        using var cmd = _conn.CreateCommand();
        var conditions = new List<string> { "forgotten = 0" };
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            conditions.Add("content LIKE @keyword");
            cmd.Parameters.AddWithValue("@keyword", $"%{keyword}%");
        }
        if (!string.IsNullOrWhiteSpace(tagFilter))
        {
            conditions.Add("tags LIKE @tag");
            cmd.Parameters.AddWithValue("@tag", $"%{tagFilter}%");
        }
        cmd.CommandText = $"SELECT id, content, created_at, tags, weight, access_count, last_accessed, source, forgotten FROM soul WHERE {string.Join(" AND ", conditions)} ORDER BY weight DESC, id ASC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);
        var entries = new List<SoulRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            entries.Add(ReadSoulRecord(reader));
        return entries;
    }

    public List<SoulRecord> GetTopSoulByWeight(int limit = 10)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, content, created_at, tags, weight, access_count, last_accessed, source, forgotten FROM soul WHERE forgotten = 0 ORDER BY weight DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);
        var entries = new List<SoulRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            entries.Add(ReadSoulRecord(reader));
        return entries;
    }

    public SoulRecord? GetSoulEntry(int id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, content, created_at, tags, weight, access_count, last_accessed, source, forgotten FROM soul WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadSoulRecord(reader) : null;
    }

    public void AddSoulEntry(string content, string? tags = null, double weight = 1.0, string source = "user")
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO soul (content, tags, weight, source, created_at) VALUES (@content, @tags, @weight, @source, @time)";
        cmd.Parameters.AddWithValue("@content", content);
        cmd.Parameters.AddWithValue("@tags", tags ?? "");
        cmd.Parameters.AddWithValue("@weight", Math.Clamp(weight, 0.0, 1.0));
        cmd.Parameters.AddWithValue("@source", source);
        cmd.Parameters.AddWithValue("@time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
    }

    public void DeleteSoulEntry(int id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM soul WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void UpdateSoulEntry(int id, string content, string? tags = null, double? weight = null)
    {
        // Save version before update
        var existing = GetSoulEntry(id);
        if (existing != null)
        {
            SaveSoulVersion(existing);
        }

        using var cmd = _conn.CreateCommand();
        var sets = new List<string> { "content = @content" };
        cmd.Parameters.AddWithValue("@content", content);
        if (tags != null)
        {
            sets.Add("tags = @tags");
            cmd.Parameters.AddWithValue("@tags", tags);
        }
        if (weight.HasValue)
        {
            sets.Add("weight = @weight");
            cmd.Parameters.AddWithValue("@weight", Math.Clamp(weight.Value, 0.0, 1.0));
        }
        cmd.Parameters.AddWithValue("@id", id);
        cmd.CommandText = $"UPDATE soul SET {string.Join(", ", sets)} WHERE id = @id";
        cmd.ExecuteNonQuery();
    }

    public void UpdateSoulTags(int id, string tags)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE soul SET tags = @tags WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@tags", tags);
        cmd.ExecuteNonQuery();
    }

    public void UpdateSoulWeight(int id, double weight)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE soul SET weight = @weight WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@weight", Math.Clamp(weight, 0.0, 1.0));
        cmd.ExecuteNonQuery();
    }

    public void IncrementSoulAccess(int id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE soul SET access_count = access_count + 1, last_accessed = @time WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Save current state of a soul entry as a version record before modification.
    /// </summary>
    private void SaveSoulVersion(SoulRecord entry)
    {
        using var cmd = _conn.CreateCommand();
        // Get max version for this soul_id
        cmd.CommandText = "SELECT COALESCE(MAX(version), 0) + 1 FROM soul_versions WHERE soul_id = @soulId";
        cmd.Parameters.AddWithValue("@soulId", entry.Id);
        var nextVersion = Convert.ToInt32(cmd.ExecuteScalar());
        cmd.Parameters.Clear();

        cmd.CommandText = "INSERT INTO soul_versions (soul_id, content, tags, weight, version) VALUES (@soulId, @content, @tags, @weight, @version)";
        cmd.Parameters.AddWithValue("@soulId", entry.Id);
        cmd.Parameters.AddWithValue("@content", entry.Content);
        cmd.Parameters.AddWithValue("@tags", entry.Tags);
        cmd.Parameters.AddWithValue("@weight", entry.Weight);
        cmd.Parameters.AddWithValue("@version", nextVersion);
        cmd.ExecuteNonQuery();
    }

    public List<SoulRecord> GetSoulVersionHistory(int soulId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, content, '' as created_at, tags, weight, 0 as access_count, NULL as last_accessed, '' as source, 0 as forgotten, version, changed_at FROM soul_versions WHERE soul_id = @soulId ORDER BY version DESC";
        cmd.Parameters.AddWithValue("@soulId", soulId);
        var versions = new List<SoulRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            versions.Add(new SoulRecord
            {
                Id = reader.GetInt32(0),
                Content = reader.GetString(1),
                Tags = reader.GetString(3),
                Weight = reader.GetDouble(4),
            });
        }
        return versions;
    }

    public List<(int Version, string Content, string Tags, double Weight, DateTime ChangedAt)> GetSoulHistory(int soulId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT version, content, tags, weight, changed_at FROM soul_versions WHERE soul_id = @soulId ORDER BY version DESC";
        cmd.Parameters.AddWithValue("@soulId", soulId);
        var history = new List<(int, string, string, double, DateTime)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            history.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDouble(3),
                DateTime.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.AssumeLocal)
            ));
        }
        return history;
    }

    public bool RollbackSoulEntry(int id, int targetVersion)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT content, tags, weight FROM soul_versions WHERE soul_id = @soulId AND version = @version";
        cmd.Parameters.AddWithValue("@soulId", id);
        cmd.Parameters.AddWithValue("@version", targetVersion);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return false;
        var content = reader.GetString(0);
        var tags = reader.GetString(1);
        var weight = reader.GetDouble(2);
        reader.Close();

        UpdateSoulEntry(id, content, tags, weight);
        return true;
    }

    public void LinkSoulToMessage(int soulId, int messageId, string linkType = "derived")
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO soul_context_links (soul_id, message_id, link_type) VALUES (@soulId, @msgId, @type)";
        cmd.Parameters.AddWithValue("@soulId", soulId);
        cmd.Parameters.AddWithValue("@msgId", messageId);
        cmd.Parameters.AddWithValue("@type", linkType);
        cmd.ExecuteNonQuery();
    }

    public int ApplyDecay(double baseDecayRate = 0.01, double minWeightBeforeArchive = 0.1, double accessBoost = 0.05)
    {
        var entries = GetAllSoulEntries();
        var now = DateTime.Now;
        int archived = 0;

        foreach (var entry in entries)
        {
            var daysSinceAccess = entry.LastAccessed.HasValue
                ? (now - entry.LastAccessed.Value).TotalDays
                : (now - entry.CreatedAt).TotalDays;

            var newWeight = entry.Weight - baseDecayRate * daysSinceAccess + entry.AccessCount * accessBoost;
            newWeight = Math.Clamp(newWeight, 0.0, 1.0);

            using var cmd = _conn.CreateCommand();
            if (newWeight < minWeightBeforeArchive)
            {
                cmd.CommandText = "UPDATE soul SET forgotten = 1, weight = @weight WHERE id = @id";
                archived++;
            }
            else
            {
                cmd.CommandText = "UPDATE soul SET weight = @weight WHERE id = @id";
            }
            cmd.Parameters.AddWithValue("@weight", newWeight);
            cmd.Parameters.AddWithValue("@id", entry.Id);
            cmd.ExecuteNonQuery();
        }

        return archived;
    }

    /// <summary>
    /// Manually archive (soft-delete) a soul entry.
    /// </summary>
    public bool ForgetSoulEntry(int id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE soul SET forgotten = 1 WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Record task execution history.
    /// </summary>
    public void RecordTaskHistory(string taskId, string status, string result, long durationMs)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO task_history (task_id, status, result, duration_ms) VALUES (@tid, @status, @result, @dur)";
        cmd.Parameters.AddWithValue("@tid", taskId);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@result", result);
        cmd.Parameters.AddWithValue("@dur", durationMs);
        cmd.ExecuteNonQuery();
    }

    public List<object> GetTaskHistory(string taskId, int limit = 50)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, task_id, status, result, duration_ms, executed_at FROM task_history WHERE task_id = @tid ORDER BY id DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@tid", taskId);
        cmd.Parameters.AddWithValue("@limit", limit);
        var history = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            history.Add(new
            {
                id = reader.GetInt32(0),
                taskId = reader.GetString(1),
                status = reader.GetString(2),
                result = reader.GetString(3),
                durationMs = reader.GetInt32(4),
                executedAt = reader.GetString(5)
            });
        }
        return history;
    }

    public void DeleteTaskHistory(string taskId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM task_history WHERE task_id = @tid";
        cmd.Parameters.AddWithValue("@tid", taskId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Record user feedback for a message.
    /// </summary>
    public void RecordFeedback(int? messageId, int rating, string? comment = null, string? skillName = null, string? toolName = null)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO feedback (message_id, rating, comment, skill_name, tool_name) VALUES (@msgId, @rating, @comment, @skill, @tool)";
        cmd.Parameters.AddWithValue("@msgId", messageId.HasValue ? messageId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@rating", rating);
        cmd.Parameters.AddWithValue("@comment", comment ?? "");
        cmd.Parameters.AddWithValue("@skill", skillName ?? "");
        cmd.Parameters.AddWithValue("@tool", toolName ?? "");
        cmd.ExecuteNonQuery();
    }

    public List<object> GetFeedbackBySkill(string skillName, int limit = 100)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, rating, comment, created_at FROM feedback WHERE skill_name = @skill ORDER BY id DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@skill", skillName);
        cmd.Parameters.AddWithValue("@limit", limit);
        var items = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new
            {
                id = reader.GetInt32(0),
                rating = reader.GetInt32(1),
                comment = reader.GetString(2),
                createdAt = reader.GetString(3)
            });
        }
        return items;
    }

    private static SoulRecord ReadSoulRecord(SqliteDataReader reader)
    {
        return new SoulRecord
        {
            Id = reader.GetInt32(0),
            Content = reader.GetString(1),
            CreatedAt = reader.IsDBNull(2) ? DateTime.Now : DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.AssumeLocal),
            Tags = reader.IsDBNull(3) ? "" : reader.GetString(3),
            Weight = reader.IsDBNull(4) ? 1.0 : reader.GetDouble(4),
            AccessCount = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
            LastAccessed = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6), null, System.Globalization.DateTimeStyles.AssumeLocal),
            Source = reader.IsDBNull(7) ? "user" : reader.GetString(7),
            Forgotten = !reader.IsDBNull(8) && reader.GetInt32(8) > 0
        };
    }

    public string GetAllSoulContent()
    {
        var entries = GetAllSoulEntries();
        return string.Join("\n", entries.Select(e => e.Content));
    }

    public int GetSoulCount()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM soul";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public string? GetSessionName()
    {
        try
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM session_meta WHERE key = 'session_name'";
            return cmd.ExecuteScalar() as string;
        }
        catch { return null; }
    }

    public void SetSessionName(string name)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO session_meta (key, value) VALUES ('session_name', @name)";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.ExecuteNonQuery();
    }

    public static string? GetSessionNameStatic(string dbFilePath)
    {
        try
        {
            using var conn = new SqliteConnection($"Data Source={dbFilePath};Mode=ReadOnly");
            conn.Open();
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='session_meta'";
            if (checkCmd.ExecuteScalar() == null) return null;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM session_meta WHERE key = 'session_name'";
            return cmd.ExecuteScalar() as string;
        }
        catch { return null; }
    }

    public void DeleteMessage(int messageId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM messages WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", messageId);
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (!_disposed) { _conn.Dispose(); _disposed = true; }
    }

    private static string ExtractTextContent(JsonElement content)
    {
        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString() ?? string.Empty,
            JsonValueKind.Object when content.TryGetProperty("text", out var textEl) =>
                textEl.GetString() ?? string.Empty,
            JsonValueKind.Object when content.TryGetProperty("content", out var contentEl) =>
                ExtractTextContent(contentEl),
            _ => content.ToString()
        };
    }
}

public class SessionInfo
{
    public string FileName { get; set; } = "";
    public string? SessionName { get; set; }
    public int MessageCount { get; set; }
    public int SoulCount { get; set; }
    public DateTime LastModified { get; set; }
    public long Size { get; set; }
    public bool IsCurrent { get; set; }
}

public class MessageRecord
{
    public int Id { get; set; }
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class SoulRecord
{
    public int Id { get; set; }
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string Tags { get; set; } = "";
    public double Weight { get; set; } = 1.0;
    public int AccessCount { get; set; } = 0;
    public DateTime? LastAccessed { get; set; }
    public string Source { get; set; } = "user";
    public bool Forgotten { get; set; } = false;
}
