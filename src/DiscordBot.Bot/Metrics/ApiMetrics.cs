using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DiscordBot.Bot.Metrics;

/// <summary>
/// Defines metrics for API request tracking.
/// Supplements ASP.NET Core built-in metrics with Discord bot-specific measurements.
/// </summary>
public sealed class ApiMetrics : IDisposable
{
    public const string MeterName = "DiscordBot.Api";

    private readonly Meter _meter;
    private readonly Counter<long> _requestCounter;
    private readonly Histogram<double> _requestDuration;
    private readonly UpDownCounter<long> _activeRequests;

    public ApiMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _requestCounter = _meter.CreateCounter<long>(
            name: "discordbot.api.request.count",
            unit: "{requests}",
            description: "Total number of API requests");

        _requestDuration = _meter.CreateHistogram<double>(
            name: "discordbot.api.request.duration",
            unit: "ms",
            description: "Duration of API request handling");

        _activeRequests = _meter.CreateUpDownCounter<long>(
            name: "discordbot.api.request.active",
            unit: "{requests}",
            description: "Number of currently active API requests");
    }

    /// <summary>
    /// Records an API request with duration and status code.
    /// </summary>
    /// <param name="endpoint">The normalized endpoint path.</param>
    /// <param name="method">The HTTP method (GET, POST, etc.).</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="durationMs">The request duration in milliseconds.</param>
    public void RecordRequest(
        string endpoint,
        string method,
        int statusCode,
        double durationMs)
    {
        var tags = new TagList
        {
            { "endpoint", endpoint },
            { "method", method },
            { "status_code", statusCode.ToString() }
        };

        _requestCounter.Add(1, tags);
        _requestDuration.Record(durationMs, tags);
    }

    /// <summary>
    /// Increments the active request counter.
    /// </summary>
    public void IncrementActiveRequests() => _activeRequests.Add(1);

    /// <summary>
    /// Decrements the active request counter.
    /// </summary>
    public void DecrementActiveRequests() => _activeRequests.Add(-1);

    public void Dispose() => _meter.Dispose();
}
