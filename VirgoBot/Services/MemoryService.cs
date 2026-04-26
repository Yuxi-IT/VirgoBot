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
            )";
        cmd.ExecuteNonQuery();
        conn.Close();
        return dbFileName;
    }

    public void DeleteSession(string dbFileName)
    {
        if (string.IsNullOrWhiteSpace(dbFileName))
            throw new ArgumentException("Database file name cannot be empty");
        if (dbFileName == CurrentDbName)
            throw new InvalidOperationException("Cannot delete the currently active session");

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
            )";
        cmd.ExecuteNonQuery();

        MigrateDropUserIdColumn();
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

    public void UpdateMessageLimit(int newLimit) => _messageLimit = newLimit;

    public void SaveMessage(string role, object content)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO messages (role, content, created_at) VALUES (@role, @content, @time)";
        cmd.Parameters.AddWithValue("@role", role);
        cmd.Parameters.AddWithValue("@content", JsonSerializer.Serialize(content));
        cmd.Parameters.AddWithValue("@time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
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
        cmd.CommandText = "SELECT id, content, created_at FROM soul ORDER BY id ASC";
        var entries = new List<SoulRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(new SoulRecord
            {
                Id = reader.GetInt32(0),
                Content = reader.GetString(1),
                CreatedAt = reader.IsDBNull(2) ? DateTime.Now : DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.AssumeLocal)
            });
        }
        return entries;
    }

    public void AddSoulEntry(string content)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO soul (content, created_at) VALUES (@content, @time)";
        cmd.Parameters.AddWithValue("@content", content);
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

    public void UpdateSoulEntry(int id, string content)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE soul SET content = @content WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@content", content);
        cmd.ExecuteNonQuery();
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
}
