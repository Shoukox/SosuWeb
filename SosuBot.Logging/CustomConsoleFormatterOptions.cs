using Microsoft.Extensions.Logging.Console;

namespace SosuBot.Logging;

internal class CustomConsoleFormatterOptions : ConsoleFormatterOptions
{
    public string? CustomPrefix { get; set; }
}