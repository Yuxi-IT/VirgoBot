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
        {
            dbFileName = $"{Guid.NewGuid()}.db";
        }

        CurrentDbName = dbFileName;
        var fullPath = Path.Combine(MemorysDirectory, dbFileName);
        _conn = new SqliteConnection($"Data Source={fullPath};Cache=Shared");
        _conn.Open();

        using var pragmaCmd = _conn.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA journal_mode=WAL;";
        pragmaCmd.ExecuteNonQuery();

        InitDatabase();
    }

    public void SwitchDatabase(string dbFileName)
    {
        if (string.IsNullOrWhiteSpace(dbFileName))
            throw new ArgumentException("Database file name cannot be empty");

        // Close current connection
        _conn.Close();
        _conn.Dispose();

        CurrentDbName = dbFileName;
        var fullPath = Path.Combine(MemorysDirectory, dbFileName);
        _conn = new SqliteConnection($"Data Source={fullPath};Cache=Shared");
        _conn.Open();

        using var pragmaCmd = _conn.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA journal_mode=WAL;";
        pragmaCmd.ExecuteNonQuery();

        InitDatabase();
    }

    public string CreateSession()
    {
        var dbFileName = $"{Guid.NewGuid()}.db";
        var fullPath = Path.Combine(MemorysDirectory, dbFileName);

        // Create and initialize the new database
        using var conn = new SqliteConnection($"Data Source={fullPath};Cache=Shared");
        conn.Open();

        using var pragmaCmd = conn.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA journal_mode=WAL;";
        pragmaCmd.ExecuteNonQuery();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS messages (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER NOT NULL,
                role TEXT NOT NULL,
                content TEXT NOT NULL,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP
            )";
        cmd.ExecuteNonQuery();

        using var soulCmd = conn.CreateCommand();
        soulCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS soul (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                content TEXT NOT NULL,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP
            )";
        soulCmd.ExecuteNonQuery();

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
            File.Delete(fullPath);
            // Also delete WAL and SHM files if they exist
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

            int messageCount = 0;
            int soulCount = 0;

            try
            {
                using var conn = new SqliteConnection($"Data Source={file};Cache=Shared;Mode=ReadOnly");
                conn.Open();

                using var msgCmd = conn.CreateCommand();
                msgCmd.CommandText = "SELECT COUNT(*) FROM messages";
                messageCount = Convert.ToInt32(msgCmd.ExecuteScalar());

                using var soulCmd = conn.CreateCommand();
                soulCmd.CommandText = "SELECT COUNT(*) FROM soul";
                soulCount = Convert.ToInt32(soulCmd.ExecuteScalar());

                conn.Close();
            }
            catch
            {
                // If we can't read the db, just use defaults
            }

            sessions.Add(new SessionInfo
            {
                FileName = fileName,
                MessageCount = messageCount,
                SoulCount = soulCount,
                LastModified = fileInfo.LastWriteTimeUtc,
                Size = fileInfo.Length,
                IsCurrent = fileName == CurrentDbName
            });
        }

        return sessions.OrderByDescending(s => s.LastModified).ToList();
    }

    private void InitDatabase()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS messages (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER NOT NULL,
                role TEXT NOT NULL,
                content TEXT NOT NULL,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP
            )";
        cmd.ExecuteNonQuery();

        using var soulCmd = _conn.CreateCommand();
        soulCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS soul (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                content TEXT NOT NULL,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP
            )";
        soulCmd.ExecuteNonQuery();
    }

    public void UpdateMessageLimit(int newLimit)
    {
        _messageLimit = newLimit;
    }

    public void SaveMessage(long userId, string role, object content)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO messages (user_id, role, content) VALUES (@uid, @role, @content)";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@role", role);
        cmd.Parameters.AddWithValue("@content", JsonSerializer.Serialize(content));
        cmd.ExecuteNonQuery();
    }

    public List<object> LoadMessages(long userId, int? limit = null)
    {
        var effectiveLimit = limit ?? _messageLimit;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT role, content FROM messages WHERE user_id = @uid ORDER BY id DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@limit", effectiveLimit);

        var messages = new List<object>();
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var role = reader.GetString(0);
            var contentJson = reader.GetString(1);
            var content = JsonSerializer.Deserialize<JsonElement>(contentJson);
            messages.Add(new { role, content });
        }

        messages.Reverse();
        return messages;
    }

    public void ClearOldMessages(long userId, int? keepLast = null)
    {
        var effectiveKeep = keepLast ?? _messageLimit;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM messages
            WHERE user_id = @uid
            AND id NOT IN (
                SELECT id FROM messages
                WHERE user_id = @uid
                ORDER BY id DESC
                LIMIT @keep
            )";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@keep", effectiveKeep);
        cmd.ExecuteNonQuery();
    }

    public void ClearAllMessages(long userId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM messages WHERE user_id = @uid";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.ExecuteNonQuery();
    }

    public List<long> GetAllUserIds()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT user_id FROM messages ORDER BY user_id";

        var userIds = new List<long>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            userIds.Add(reader.GetInt64(0));
        }
        return userIds;
    }

    public int GetMessageCount(long userId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM messages WHERE user_id = @uid";
        cmd.Parameters.AddWithValue("@uid", userId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public DateTime? GetLastActiveTime(long userId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT MAX(created_at) FROM messages WHERE user_id = @uid";
        cmd.Parameters.AddWithValue("@uid", userId);
        var result = cmd.ExecuteScalar();
        if (result is string dateStr)
            return DateTime.Parse(dateStr);
        return null;
    }

    public (List<MessageRecord> Messages, int Total) LoadMessagesWithPagination(long userId, int limit, int offset)
    {
        // Get total count
        using var countCmd = _conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM messages WHERE user_id = @uid";
        countCmd.Parameters.AddWithValue("@uid", userId);
        var total = Convert.ToInt32(countCmd.ExecuteScalar());

        // Get paginated messages
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, role, content, created_at FROM messages WHERE user_id = @uid ORDER BY id DESC LIMIT @limit OFFSET @offset";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        var messages = new List<MessageRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var contentJson = reader.GetString(2);
            // Try to extract plain text from JSON content
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
                {
                    contentText = content.GetString() ?? "";
                }
                else
                {
                    contentText = contentJson;
                }
            }
            catch
            {
                contentText = contentJson;
            }

            messages.Add(new MessageRecord
            {
                Id = reader.GetInt32(0),
                Role = reader.GetString(1),
                Content = contentText,
                CreatedAt = reader.IsDBNull(3) ? DateTime.UtcNow : DateTime.Parse(reader.GetString(3))
            });
        }

        messages.Reverse();
        return (messages, total);
    }

    // ===== Soul CRUD =====

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
                CreatedAt = reader.IsDBNull(2) ? DateTime.UtcNow : DateTime.Parse(reader.GetString(2))
            });
        }
        return entries;
    }

    public void AddSoulEntry(string content)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO soul (content) VALUES (@content)";
        cmd.Parameters.AddWithValue("@content", content);
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

    public void Dispose()
    {
        if (!_disposed)
        {
            _conn.Dispose();
            _disposed = true;
        }
    }
}

public class SessionInfo
{
    public string FileName { get; set; } = "";
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
