namespace VirgoBot.Helpers;

public static class ColorLog
{
    public static void Info(string prefix, string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"[{prefix}] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void Success(string prefix, string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"[{prefix}] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void Warning(string prefix, string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"[{prefix}] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void Error(string prefix, string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write($"[{prefix}] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void Debug(string prefix, string message)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write($"[{prefix}] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }
}
