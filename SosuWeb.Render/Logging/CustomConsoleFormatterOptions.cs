using Microsoft.Extensions.Logging.Console;

namespace SosuWeb.Render.Logging;

internal class CustomConsoleFormatterOptions : ConsoleFormatterOptions
{
    public string? CustomPrefix { get; set; }
}