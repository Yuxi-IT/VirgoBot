using Spectre.Console;
using VirgoBot.Services;

namespace VirgoBot.Utilities;

public static class ColorLog
{
    private static LogService? _logService;

    public static void SetLogService(LogService logService)
    {
        _logService = logService;
    }

    private static string Esc(string s) => Markup.Escape(s);

    public static void Info(string prefix, string message)
    {
        var time = DateTime.Now.ToString("HH:mm:ss");
        AnsiConsole.MarkupLine($"[cyan][[{Esc(time)}]] [[{Esc(prefix)}]][/] {Esc(message)}");
        _logService?.Add("Info", prefix, message);
    }

    public static void Success(string prefix, string message)
    {
        var time = DateTime.Now.ToString("HH:mm:ss");
        AnsiConsole.MarkupLine($"[green][[{Esc(time)}]] [[{Esc(prefix)}]][/] {Esc(message)}");
        _logService?.Add("Success", prefix, message);
    }

    public static void Warning(string prefix, string message)
    {
        var time = DateTime.Now.ToString("HH:mm:ss");
        AnsiConsole.MarkupLine($"[yellow][[{Esc(time)}]] [[{Esc(prefix)}]][/] {Esc(message)}");
        _logService?.Add("Warn", prefix, message);
    }

    public static void Error(string prefix, string message)
    {
        var time = DateTime.Now.ToString("HH:mm:ss");
        AnsiConsole.MarkupLine($"[red][[{Esc(time)}]] [[{Esc(prefix)}]][/] {Esc(message)}");
        _logService?.Add("Error", prefix, message);
    }

    public static void Debug(string prefix, string message, ConsoleColor color = ConsoleColor.DarkYellow)
    {
        var time = DateTime.Now.ToString("HH:mm:ss");
        AnsiConsole.MarkupLine($"[magenta][[{Esc(time)}]] [[{Esc(prefix)}]][/] [darkorange3]{Esc(message)}[/]");
        _logService?.Add("Info", prefix, message);
    }
}
