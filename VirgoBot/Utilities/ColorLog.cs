using VirgoBot.Services;

namespace VirgoBot.Utilities;

public static class ColorLog
{
    private static LogService? _logService;

    public static void SetLogService(LogService logService)
    {
        _logService = logService;
    }

    public static void Info(string prefix, string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"[{DateTime.Now:HH:mm:ss}] [{prefix}] ");
        Console.ResetColor();
        Console.WriteLine(message);
        _logService?.Add("Info", prefix, message);
    }

    public static void Success(string prefix, string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"[{DateTime.Now:HH:mm:ss}] [{prefix}] ");
        Console.ResetColor();
        Console.WriteLine(message);
        _logService?.Add("Success", prefix, message);
    }

    public static void Warning(string prefix, string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"[{DateTime.Now:HH:mm:ss}] [{prefix}] ");
        Console.ResetColor();
        Console.WriteLine(message);
        _logService?.Add("Warn", prefix, message);
    }

    public static void Error(string prefix, string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write($"[{DateTime.Now:HH:mm:ss}] [{prefix}] ");
        Console.ResetColor();
        Console.WriteLine(message);
        _logService?.Add("Error", prefix, message);
    }

    public static void Debug(string prefix, string message, ConsoleColor color = ConsoleColor.DarkYellow)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write($"[{DateTime.Now:HH:mm:ss}] [{prefix}] ");
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
        _logService?.Add("Info", prefix, message);
    }
}
