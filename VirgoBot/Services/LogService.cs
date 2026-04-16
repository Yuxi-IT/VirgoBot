namespace VirgoBot.Services;

public class LogService
{
    private readonly List<LogEntry> _logs = new();
    private readonly Lock _lock = new();
    private int _nextId = 1;
    private const int MaxEntries = 1000;

    public void Add(string level, string component, string message)
    {
        lock (_lock)
        {
            var entry = new LogEntry
            {
                Id = _nextId++,
                Level = level,
                Component = component,
                Message = message,
                Timestamp = DateTime.UtcNow
            };

            _logs.Add(entry);

            if (_logs.Count > MaxEntries)
            {
                _logs.RemoveAt(0);
            }
        }
    }

    public (List<LogEntry> Logs, int Total) GetLogs(string? level = null, int limit = 100, int offset = 0)
    {
        lock (_lock)
        {
            IEnumerable<LogEntry> filtered = _logs;

            if (!string.IsNullOrEmpty(level))
            {
                filtered = filtered.Where(l => l.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
            }

            var total = filtered.Count();
            var logs = filtered
                .OrderByDescending(l => l.Id)
                .Skip(offset)
                .Take(limit)
                .ToList();

            return (logs, total);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _logs.Clear();
            _nextId = 1;
        }
    }
}

public class LogEntry
{
    public int Id { get; set; }
    public string Level { get; set; } = "";
    public string Component { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime Timestamp { get; set; }
}
