namespace VirgoBot.Helpers;

public static class ColorLog
{
    public static void Info(string prefix, string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"[{DateTime.Now:HH:mm:ss}] [{prefix}] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void Success(string prefix, string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"[{DateTime.Now:HH:mm:ss}] [{prefix}] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void Warning(string prefix, string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"[{DateTime.Now:HH:mm:ss}] [{prefix}] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void Error(string prefix, string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write($"[{DateTime.Now:HH:mm:ss}] [{prefix}] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void Debug(string prefix, string message, ConsoleColor color = ConsoleColor.DarkYellow)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write($"[{DateTime.Now:HH:mm:ss}] [{prefix}] ");
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}
