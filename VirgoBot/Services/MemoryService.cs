using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace VirgoBot.Services;

public class MemoryService : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly int _messageLimit;
    private bool _disposed;

    public MemoryService(string dbPath = "memory.db", int messageLimit = 20)
    {
        _messageLimit = messageLimit;
        var fullPath = Path.Combine("config", dbPath);
        _conn = new SqliteConnection($"Data Source={fullPath};Cache=Shared");
        _conn.Open();

        using var pragmaCmd = _conn.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA journal_mode=WAL;";
        pragmaCmd.ExecuteNonQuery();

        InitDatabase();
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

    public void Dispose()
    {
        if (!_disposed)
        {
            _conn.Dispose();
            _disposed = true;
        }
    }
}
