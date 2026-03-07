using Microsoft.Data.Sqlite;

namespace VirgoBot.Helpers;

public class ContactService
{
    private readonly string _dbPath;

    public ContactService(string dbPath = "config/contacts.db")
    {
        _dbPath = dbPath;
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        InitDatabase();
    }

    private void InitDatabase()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS contacts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                email TEXT,
                phone TEXT,
                notes TEXT,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP
            )";
        cmd.ExecuteNonQuery();
    }

    public void AddContact(string name, string? email = null, string? phone = null, string? notes = null)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO contacts (name, email, phone, notes) VALUES (@name, @email, @phone, @notes)";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@email", email ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@phone", phone ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@notes", notes ?? (object)DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public List<Contact> GetAllContacts()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, email, phone, notes FROM contacts ORDER BY name";

        var contacts = new List<Contact>();
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            contacts.Add(new Contact
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Email = reader.IsDBNull(2) ? null : reader.GetString(2),
                Phone = reader.IsDBNull(3) ? null : reader.GetString(3),
                Notes = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }

        return contacts;
    }

    public Contact? FindContact(string keyword)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, email, phone, notes FROM contacts WHERE name LIKE @kw OR email LIKE @kw OR phone LIKE @kw LIMIT 1";
        cmd.Parameters.AddWithValue("@kw", $"%{keyword}%");

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Contact
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Email = reader.IsDBNull(2) ? null : reader.GetString(2),
                Phone = reader.IsDBNull(3) ? null : reader.GetString(3),
                Notes = reader.IsDBNull(4) ? null : reader.GetString(4)
            };
        }

        return null;
    }

    public void UpdateContact(int id, string? name = null, string? email = null, string? phone = null, string? notes = null)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        var updates = new List<string>();
        var cmd = conn.CreateCommand();

        if (name != null)
        {
            updates.Add("name = @name");
            cmd.Parameters.AddWithValue("@name", name);
        }
        if (email != null)
        {
            updates.Add("email = @email");
            cmd.Parameters.AddWithValue("@email", email);
        }
        if (phone != null)
        {
            updates.Add("phone = @phone");
            cmd.Parameters.AddWithValue("@phone", phone);
        }
        if (notes != null)
        {
            updates.Add("notes = @notes");
            cmd.Parameters.AddWithValue("@notes", notes);
        }

        if (updates.Count == 0) return;

        cmd.CommandText = $"UPDATE contacts SET {string.Join(", ", updates)} WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteContact(int id)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM contacts WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }
}

public class Contact
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
}
