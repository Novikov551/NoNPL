using System;
namespace NoNPL;
public static class AdvancedConsole
{
    private static int _lastProgressLine = -1;

    public static void Write(string message,
        ConsoleMessageType type = ConsoleMessageType.Default)
    {

        var previousColor = Console.ForegroundColor;
        SetConsoleColor(type);
        Console.Write(message);
        Console.ForegroundColor = previousColor;
    }

    public static void WriteLine(string message, 
        ConsoleMessageType type = ConsoleMessageType.Default)
    {
        var previousColor = Console.ForegroundColor;
        SetConsoleColor(type);
        Console.WriteLine(message);
        Console.ForegroundColor = previousColor;
    }

    public static void WriteProgress(double current,
        string message = "",
        int barLength = 50,
        ConsoleMessageType type = ConsoleMessageType.Default)
    {

        var percent = current;
        var progress = (int)(percent * barLength);

        var bar = new string('█', progress) + new string('░', barLength - progress);

        var position = Console.GetCursorPosition();

        if (_lastProgressLine != position.Top && _lastProgressLine != -1)
        {
            Console.SetCursorPosition(0, _lastProgressLine);
        }

        var previousColor = Console.ForegroundColor;

        SetConsoleColor(type);
        Console.Write($"\r{message} [{bar}] ({percent:P1})");
        Console.ForegroundColor = previousColor;

        _lastProgressLine = Console.GetCursorPosition().Top;
    }

    public static void WriteProgress(long current,
        long total,
        string message = "",
        int barLength = 50,
        ConsoleMessageType type = ConsoleMessageType.Default)
    {

        var percent = (double)current / total;
        var progress = (int)(percent * barLength);

        var bar = new string('█', progress) + new string('░', barLength - progress);

        var position = Console.GetCursorPosition();

        if (_lastProgressLine != position.Top && _lastProgressLine != -1)
        {
            Console.SetCursorPosition(0, _lastProgressLine);
        }

        var previousColor = Console.ForegroundColor;

        SetConsoleColor(type);
        Console.Write($"\r{message} [{bar}] {current}/{total} ({percent:P1})");
        Console.ForegroundColor = previousColor;

        _lastProgressLine = Console.GetCursorPosition().Top;

        if (current >= total)
        {
            Console.WriteLine();
            _lastProgressLine = -1;
        }
    }

    public static void WriteHeader(string title)
    {
        var line = new string('=', title.Length + 4);
        WriteLine(line, ConsoleMessageType.Default);
        WriteLine($"  {title}  ", ConsoleMessageType.Success);
        WriteLine(line, ConsoleMessageType.Warning);
    }

    public static void WriteMenu(params string[] items)
    {
        WriteLine("\n📋 МЕНЮ:", ConsoleMessageType.Default);
        for (int i = 0; i < items.Length; i++)
        {
            Write($"  {i + 1}. ", ConsoleMessageType.Success);
            WriteLine(items[i]);
        }
        Write("Выберите пункт: ", ConsoleMessageType.Warning);
    }

    public static string ReadLine(ConsoleMessageType type = ConsoleMessageType.Default)
    {
        var previousColor = Console.ForegroundColor;
        SetConsoleColor(type);
        var input = Console.ReadLine();
        Console.ForegroundColor = previousColor;
        return input;
    }

    private static void SetConsoleColor(ConsoleMessageType type)
    {
        Console.ForegroundColor = type switch
        {
            ConsoleMessageType.Info => ConsoleColor.Cyan,
            ConsoleMessageType.Success => ConsoleColor.Green,
            ConsoleMessageType.Warning => ConsoleColor.Yellow,
            ConsoleMessageType.Error => ConsoleColor.Red,
            ConsoleMessageType.Debug => ConsoleColor.Magenta,
            _ => ConsoleColor.Gray
        };
    }
}

public enum ConsoleMessageType
{
    Default,
    Info,
    Success,
    Warning,
    Error,
    Debug
}
