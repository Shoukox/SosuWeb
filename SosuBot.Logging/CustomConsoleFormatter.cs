using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace SosuBot.Logging;

internal class CustomConsoleFormatter : ConsoleFormatter, IDisposable
{
    private readonly IDisposable? _optionsReloadToken;
    private CustomConsoleFormatterOptions _formatterOptions;

    public CustomConsoleFormatter(IOptionsMonitor<CustomConsoleFormatterOptions> options) : base(
        "CustomConsoleFormatter")
    {
        (_optionsReloadToken, _formatterOptions) =
            (options.OnChange(ReloadLoggerOptions), options.CurrentValue);
    }

    public void Dispose()
    {
        _optionsReloadToken?.Dispose();
    }

    private void ReloadLoggerOptions(CustomConsoleFormatterOptions options)
    {
        _formatterOptions = options;
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var message =
            logEntry.Formatter?.Invoke(
                logEntry.State, logEntry.Exception);

        if (message is null) return;
        WriteLogLevel(textWriter, logEntry.LogLevel);
        WriteTimestamp(textWriter);
        WriteObjectName(textWriter, logEntry.Category);
        WriteOutputMessage(textWriter, message);
        WriteException(textWriter, logEntry.Exception);
    }

    private void WriteLogLevel(TextWriter textWriter, LogLevel logLevel)
    {
        var foregroundColor = logLevel switch
        {
            LogLevel.Trace => GetForegroundColorEscapeCode(ConsoleColor.Cyan),
            LogLevel.Debug => GetForegroundColorEscapeCode(ConsoleColor.Cyan),
            LogLevel.Information => GetForegroundColorEscapeCode(ConsoleColor.Cyan),
            LogLevel.Warning => GetForegroundColorEscapeCode(ConsoleColor.Yellow),
            LogLevel.Error => GetForegroundColorEscapeCode(ConsoleColor.Red),
            LogLevel.Critical => GetForegroundColorEscapeCode(ConsoleColor.Red),
            LogLevel.None => GetForegroundColorEscapeCode(ConsoleColor.White),

            _ => throw new NotSupportedException()
        };

        var logLevelString = Enum.GetName(logLevel) ?? "_";
        var message = $"[{logLevelString[0]}]";

        textWriter.Write(foregroundColor);
        textWriter.Write(message);
    }

    private void WriteTimestamp(TextWriter textWriter)
    {
        if (_formatterOptions.TimestampFormat == null)
            return;

        var dateTime = _formatterOptions.UseUtcTimestamp ? DateTime.UtcNow : DateTime.Now;
        var timestamp = dateTime.ToString(_formatterOptions.TimestampFormat);

        var message = $"[{timestamp}]";

        textWriter.Write(GetForegroundColorEscapeCode(ConsoleColor.Green));
        textWriter.Write(message);
    }

    private void WriteObjectName(TextWriter textWriter, string category)
    {
        var message = $"[{category}]";

        textWriter.Write(GetForegroundColorEscapeCode(ConsoleColor.Magenta));
        textWriter.Write(message);
    }

    private void WriteOutputMessage(TextWriter textWriter, string message)
    {
        var outputMessage = ": " + message;
        textWriter.Write(GetForegroundColorEscapeCode(ConsoleColor.White));
        textWriter.WriteLine(outputMessage);
    }

    private void WriteException(TextWriter textWriter, Exception? exception)
    {
        if (exception is null) return;

        textWriter.Write(GetForegroundColorEscapeCode(ConsoleColor.DarkMagenta));
        textWriter.WriteLine("Exception: {0}", exception);
    }

    private static string GetForegroundColorEscapeCode(ConsoleColor color)
    {
        return color switch
        {
            ConsoleColor.Black => "\x1B[30m",
            ConsoleColor.DarkRed => "\x1B[31m",
            ConsoleColor.DarkGreen => "\x1B[32m",
            ConsoleColor.DarkYellow => "\x1B[33m",
            ConsoleColor.DarkBlue => "\x1B[34m",
            ConsoleColor.DarkMagenta => "\x1B[35m",
            ConsoleColor.DarkCyan => "\x1B[36m",
            ConsoleColor.Gray => "\x1B[37m",
            ConsoleColor.Red => "\x1B[1m\x1B[31m",
            ConsoleColor.Green => "\x1B[1m\x1B[32m",
            ConsoleColor.Yellow => "\x1B[1m\x1B[33m",
            ConsoleColor.Blue => "\x1B[1m\x1B[34m",
            ConsoleColor.Magenta => "\x1B[1m\x1B[35m",
            ConsoleColor.Cyan => "\x1B[1m\x1B[36m",
            ConsoleColor.White => "\x1B[1m\x1B[37m",
            _ => ""
        };
    }

    private static string GetBackgroundColorEscapeCode(ConsoleColor color)
    {
        return color switch
        {
            ConsoleColor.Black => "\x1B[40m",
            ConsoleColor.DarkRed => "\x1B[41m",
            ConsoleColor.DarkGreen => "\x1B[42m",
            ConsoleColor.DarkYellow => "\x1B[43m",
            ConsoleColor.DarkBlue => "\x1B[44m",
            ConsoleColor.DarkMagenta => "\x1B[45m",
            ConsoleColor.DarkCyan => "\x1B[46m",
            ConsoleColor.Gray => "\x1B[47m",
            _ => ""
        };
    }
}