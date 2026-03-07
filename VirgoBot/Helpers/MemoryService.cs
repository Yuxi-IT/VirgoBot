using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace VirgoBot.Helpers;

public class MemoryService
{
    private readonly string _dbPath;

    public MemoryService(string dbPath = "memory.db")
    {
        _dbPath = Path.Combine("config", dbPath);
        InitDatabase();
    }

    private void InitDatabase()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        var cmd = conn.CreateCommand();
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
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO messages (user_id, role, content) VALUES (@uid, @role, @content)";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@role", role);
        cmd.Parameters.AddWithValue("@content", JsonSerializer.Serialize(content));
        cmd.ExecuteNonQuery();
    }

    public List<object> LoadMessages(long userId, int limit = 20)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT role, content FROM messages WHERE user_id = @uid ORDER BY id DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@limit", limit);

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

    public void ClearOldMessages(long userId, int keepLast = 20)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        var cmd = conn.CreateCommand();
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
        cmd.Parameters.AddWithValue("@keep", keepLast);
        cmd.ExecuteNonQuery();
    }

    public void ClearAllMessages(long userId)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM messages WHERE user_id = @uid";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.ExecuteNonQuery();
    }
}
