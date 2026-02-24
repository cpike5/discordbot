using DiscordBot.Core.Utilities;
using Serilog.Core;
using Serilog.Events;

namespace DiscordBot.Bot.Logging;

/// <summary>
/// Serilog enricher that sanitizes string property values using <see cref="LogSanitizer"/>.
/// Runs regex-based PII/token redaction on all string properties at Warning level and above
/// to minimize performance overhead on high-volume Debug/Information messages.
/// </summary>
public class LogSanitizingEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Only sanitize Warning+ to minimize regex overhead on high-volume log levels
        if (logEvent.Level < LogEventLevel.Warning)
            return;

        var propertiesToUpdate = new List<LogEventProperty>();

        foreach (var property in logEvent.Properties)
        {
            if (property.Value is ScalarValue { Value: string stringValue }
                && !string.IsNullOrEmpty(stringValue))
            {
                var sanitized = LogSanitizer.SanitizeString(stringValue);
                if (sanitized != stringValue)
                {
                    propertiesToUpdate.Add(
                        propertyFactory.CreateProperty(property.Key, sanitized));
                }
            }
        }

        foreach (var property in propertiesToUpdate)
        {
            logEvent.AddOrUpdateProperty(property);
        }
    }
}
