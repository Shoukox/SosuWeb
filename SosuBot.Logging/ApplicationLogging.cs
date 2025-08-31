using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SosuBot.Logging;

public class ApplicationLogging
{
    private static ILoggerFactory _loggerFactory = null!;
    public static ILoggerFactory LoggerFactory
    {
        get
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (_loggerFactory == null)
            {
                // Load configuration
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("logConfig.json", optional: false, reloadOnChange: true)
                    .Build();
                
                _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
                {
                    // Configure logging
                    builder.AddConfiguration(configuration.GetSection("Logging"));
                    builder.AddConsoleFormatter<CustomConsoleFormatter, CustomConsoleFormatterOptions>();
                    
                    // Add logging providers
                    builder.AddConsole();
                    builder.AddFile($"logs/{nameof(ApplicationLogging)}{DateTime.Now:dd.MM.yyyy-HH:mm:ss zz}.log");
                });
            }
            return _loggerFactory;
        }
        set => _loggerFactory = value;
    }

    public static ILogger CreateLogger(string loggerName) => LoggerFactory.CreateLogger(loggerName);
}