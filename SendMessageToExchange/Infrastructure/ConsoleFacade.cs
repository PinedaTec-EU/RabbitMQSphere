using System;
using System.IO;

namespace SendMessageToExchange.Infrastructure;

public static class ConsoleFacade
{
    private static bool _debugEnabled;

    public static bool IsDebugEnabled => _debugEnabled;

    public static TextWriter Error => System.Console.Error;

    public static event ConsoleCancelEventHandler? CancelKeyPress
    {
        add => System.Console.CancelKeyPress += value;
        remove => System.Console.CancelKeyPress -= value;
    }

    public static void ConfigureDebug(bool isEnabled) => _debugEnabled = isEnabled;

    public static void WriteLine() => System.Console.WriteLine();

    public static void WriteLine(string? value) => System.Console.WriteLine(value);

    public static void WriteLine(string? format, params object?[]? args)
    {
        if (format is null)
        {
            System.Console.WriteLine();
            return;
        }

        System.Console.WriteLine(format, args ?? Array.Empty<object?>());
    }

    public static void WriteLineIfDebug(string? value)
    {
        if (_debugEnabled && value is not null)
        {
            System.Console.WriteLine(value);
        }
    }

    public static void WriteLineIfDebug(string? format, params object?[]? args)
    {
        if (_debugEnabled && format is not null)
        {
            System.Console.WriteLine(format, args ?? Array.Empty<object?>());
        }
    }
}
