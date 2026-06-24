using DiscordBot.Bot.Logging;
using DiscordBot.Core.Utilities;
using FluentAssertions;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace DiscordBot.Tests.Logging;

/// <summary>
/// Tests for <see cref="SensitiveDataEnricher"/>, which redacts sensitive data from log event
/// property values before they reach any sink.
/// </summary>
public class SensitiveDataEnricherTests
{
    private static readonly MessageTemplateParser TemplateParser = new();

    [Fact]
    public void Enrich_RedactsPiiInPropertyValue()
    {
        var enricher = new SensitiveDataEnricher(new LogSanitizationOptions());
        var logEvent = CreateEvent(new LogEventProperty("Message", new ScalarValue("Contact user@example.com")));

        enricher.Enrich(logEvent, NoOpPropertyFactory.Instance);

        GetString(logEvent, "Message").Should().NotContain("user@example.com").And.Contain("[EMAIL]");
    }

    [Fact]
    public void Enrich_RedactsValueOfSensitivelyNamedProperty()
    {
        var enricher = new SensitiveDataEnricher(new LogSanitizationOptions());
        var logEvent = CreateEvent(new LogEventProperty("ApiKey", new ScalarValue("super-secret-value-1234567890")));

        enricher.Enrich(logEvent, NoOpPropertyFactory.Instance);

        GetString(logEvent, "ApiKey").Should().Be("[API_KEY]");
    }

    [Fact]
    public void Enrich_WhenDisabled_LeavesValuesUnchanged()
    {
        var enricher = new SensitiveDataEnricher(new LogSanitizationOptions { Enabled = false });
        var logEvent = CreateEvent(new LogEventProperty("Message", new ScalarValue("Contact user@example.com")));

        enricher.Enrich(logEvent, NoOpPropertyFactory.Instance);

        GetString(logEvent, "Message").Should().Be("Contact user@example.com");
    }

    [Fact]
    public void Enrich_AppliesCustomPattern()
    {
        var options = new LogSanitizationOptions
        {
            CustomPatterns =
            {
                ["internalId"] = new CustomPattern { Pattern = @"SECRET-\d+", Replacement = "[CUSTOM]" }
            }
        };
        var enricher = new SensitiveDataEnricher(options);
        var logEvent = CreateEvent(new LogEventProperty("Message", new ScalarValue("code SECRET-123 end")));

        enricher.Enrich(logEvent, NoOpPropertyFactory.Instance);

        GetString(logEvent, "Message").Should().Be("code [CUSTOM] end");
    }

    [Fact]
    public void Enrich_WithInvalidCustomPattern_DoesNotThrow()
    {
        var options = new LogSanitizationOptions
        {
            CustomPatterns =
            {
                ["bad"] = new CustomPattern { Pattern = "(unclosed", Replacement = "[X]" }
            }
        };

        // Construction must tolerate an invalid operator-supplied regex.
        var act = () => new SensitiveDataEnricher(options);

        act.Should().NotThrow();
    }

    private static LogEvent CreateEvent(params LogEventProperty[] properties)
    {
        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            TemplateParser.Parse("test message"),
            properties);
    }

    private static string GetString(LogEvent logEvent, string propertyName)
    {
        logEvent.Properties.TryGetValue(propertyName, out var value).Should().BeTrue();
        return ((ScalarValue)value!).Value!.ToString()!;
    }

    private sealed class NoOpPropertyFactory : ILogEventPropertyFactory
    {
        public static readonly NoOpPropertyFactory Instance = new();

        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
            => new(name, new ScalarValue(value));
    }
}
