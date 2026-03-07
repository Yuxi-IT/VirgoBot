using System.Text.Json;

namespace VirgoBot.Helpers;

public class StickerService
{
    private readonly List<StickerInfo> _stickers;
    private readonly string _stickerPath;
    private readonly Random _random = new();

    public StickerService(string stickerPath)
    {
        _stickerPath = stickerPath;
        var json = File.ReadAllText(Path.Combine(stickerPath, "stickers.json"));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _stickers = JsonSerializer.Deserialize<List<StickerInfo>>(json, options) ?? new();
    }

    public string? GetSticker(string emotion)
    {
        var candidates = _stickers.Where(s => s.Tags.Any(t => t.Contains(emotion, StringComparison.OrdinalIgnoreCase))).ToList();
        if (candidates.Count == 0) return null;
        var sticker = candidates[_random.Next(candidates.Count)];
        return Path.Combine(_stickerPath, sticker.Filename);
    }

    public string GetStickerList(int page = 1)
    {
        const int pageSize = 10;
        var totalPages = Math.Min(5, (_stickers.Count + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, totalPages);

        var items = _stickers.Skip((page - 1) * pageSize).Take(pageSize);
        var result = $"第{page}/{totalPages}页:\n";
        foreach (var s in items)
            result += $"- {s.Filename}: {string.Join(", ", s.Tags.Take(5))}\n";
        return result;
    }

    public string? GetStickerByFilename(string filename)
    {
        var sticker = _stickers.FirstOrDefault(s => s.Filename == filename);
        return sticker != null ? Path.Combine(_stickerPath, sticker.Filename) : null;
    }
}

public class StickerInfo
{
    public string Filename { get; set; } = "";
    public List<string> Tags { get; set; } = new();
}
