using System.Text.RegularExpressions;
using DiscordBot.Core.Utilities;
using Serilog.Core;
using Serilog.Events;

namespace DiscordBot.Bot.Logging;

/// <summary>
/// Serilog enricher that redacts sensitive data (tokens, API keys, PII) from log event
/// property values before they reach any sink. Property values whose <em>name</em> matches a
/// sensitive key are fully replaced with a marker; all other string values are passed through
/// <see cref="LogSanitizer"/> (built-in patterns) and any operator-configured custom patterns.
/// </summary>
/// <remarks>
/// Message-template literals are author-written constants and are not sanitized; sensitive
/// values flow through the structured <c>{Property}</c> substitutions handled here.
/// </remarks>
public sealed class SensitiveDataEnricher : ILogEventEnricher
{
    private readonly bool _enabled;
    private readonly string[] _additionalSensitiveKeys;
    private readonly (Regex Regex, string Replacement)[] _customPatterns;

    /// <summary>
    /// Initializes a new instance of the <see cref="SensitiveDataEnricher"/> class.
    /// </summary>
    /// <param name="options">The log sanitization options.</param>
    public SensitiveDataEnricher(LogSanitizationOptions options)
    {
        _enabled = options.Enabled;
        _additionalSensitiveKeys = options.AdditionalSensitiveKeys?.ToArray() ?? Array.Empty<string>();

        var patterns = new List<(Regex, string)>();
        if (options.CustomPatterns != null)
        {
            foreach (var pattern in options.CustomPatterns.Values)
            {
                if (string.IsNullOrEmpty(pattern.Pattern))
                {
                    continue;
                }

                try
                {
                    patterns.Add((
                        new Regex(pattern.Pattern, RegexOptions.Compiled),
                        string.IsNullOrEmpty(pattern.Replacement) ? "[REDACTED]" : pattern.Replacement));
                }
                catch (ArgumentException)
                {
                    // Ignore invalid operator-supplied regex patterns rather than break logging.
                }
            }
        }

        _customPatterns = patterns.ToArray();
    }

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!_enabled || logEvent.Properties.Count == 0)
        {
            return;
        }

        List<LogEventProperty>? updates = null;

        foreach (var kvp in logEvent.Properties)
        {
            if (kvp.Value is not ScalarValue { Value: string original })
            {
                continue;
            }

            string sanitized;
            if (LogSanitizer.IsSensitiveKey(kvp.Key, _additionalSensitiveKeys))
            {
                sanitized = LogSanitizer.GetMarkerForKey(kvp.Key);
            }
            else
            {
                sanitized = LogSanitizer.SanitizeString(original) ?? original;
                foreach (var (regex, replacement) in _customPatterns)
                {
                    sanitized = regex.Replace(sanitized, replacement);
                }
            }

            if (!string.Equals(sanitized, original, StringComparison.Ordinal))
            {
                (updates ??= new List<LogEventProperty>()).Add(
                    new LogEventProperty(kvp.Key, new ScalarValue(sanitized)));
            }
        }

        if (updates == null)
        {
            return;
        }

        foreach (var property in updates)
        {
            logEvent.AddOrUpdateProperty(property);
        }
    }
}
