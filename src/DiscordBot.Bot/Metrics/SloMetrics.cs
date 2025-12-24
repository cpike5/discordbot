using System.Diagnostics.Metrics;

namespace DiscordBot.Bot.Metrics;

/// <summary>
/// Defines Service Level Objective (SLO) metrics for monitoring service reliability.
/// Tracks success rates, latency percentiles, error budgets, and uptime.
/// </summary>
public sealed class SloMetrics : IDisposable
{
    public const string MeterName = "DiscordBot.SLO";

    private readonly Meter _meter;

    // Observable gauge values
    private double _commandSuccessRate24h;
    private double _apiSuccessRate24h;
    private double _commandP99Latency1h;
    private double _errorBudgetRemaining;
    private double _uptimePercentage30d;

    public SloMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _meter.CreateObservableGauge(
            name: "discordbot.slo.command.success_rate_24h",
            observeValue: () => _commandSuccessRate24h,
            unit: "%",
            description: "Command success rate over the last 24 hours");

        _meter.CreateObservableGauge(
            name: "discordbot.slo.api.success_rate_24h",
            observeValue: () => _apiSuccessRate24h,
            unit: "%",
            description: "API request success rate over the last 24 hours");

        _meter.CreateObservableGauge(
            name: "discordbot.slo.command.p99_latency_1h",
            observeValue: () => _commandP99Latency1h,
            unit: "ms",
            description: "99th percentile command latency in the last hour");

        _meter.CreateObservableGauge(
            name: "discordbot.slo.error_budget.remaining",
            observeValue: () => _errorBudgetRemaining,
            unit: "%",
            description: "Remaining error budget percentage for the current period");

        _meter.CreateObservableGauge(
            name: "discordbot.slo.uptime.percentage_30d",
            observeValue: () => _uptimePercentage30d,
            unit: "%",
            description: "Uptime percentage over the last 30 days");
    }

    /// <summary>
    /// Updates the command success rate for the last 24 hours.
    /// </summary>
    /// <param name="successRate">Success rate as a percentage (0-100).</param>
    public void UpdateCommandSuccessRate24h(double successRate) => _commandSuccessRate24h = successRate;

    /// <summary>
    /// Updates the API success rate for the last 24 hours.
    /// </summary>
    /// <param name="successRate">Success rate as a percentage (0-100).</param>
    public void UpdateApiSuccessRate24h(double successRate) => _apiSuccessRate24h = successRate;

    /// <summary>
    /// Updates the p99 latency for commands in the last hour.
    /// </summary>
    /// <param name="latencyMs">The p99 latency in milliseconds.</param>
    public void UpdateCommandP99Latency1h(double latencyMs) => _commandP99Latency1h = latencyMs;

    /// <summary>
    /// Updates the remaining error budget percentage.
    /// </summary>
    /// <param name="budgetRemaining">Remaining error budget as a percentage (0-100).</param>
    public void UpdateErrorBudgetRemaining(double budgetRemaining) => _errorBudgetRemaining = budgetRemaining;

    /// <summary>
    /// Updates the uptime percentage for the last 30 days.
    /// </summary>
    /// <param name="uptimePercentage">Uptime as a percentage (0-100).</param>
    public void UpdateUptimePercentage30d(double uptimePercentage) => _uptimePercentage30d = uptimePercentage;

    public void Dispose() => _meter.Dispose();
}
