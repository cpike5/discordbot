using System.Diagnostics.Metrics;

namespace DiscordBot.Bot.Metrics;

/// <summary>
/// Defines metrics for Blazor Interactive Server circuit lifecycle.
/// Recorded by <see cref="DiscordBot.Bot.Blazor.Services.BlazorCircuitHandler"/>. Circuit
/// interactions travel over the SignalR hub, not an HTTP request, so they never pass through
/// <see cref="ApiMetrics"/> / <c>ApiMetricsMiddleware</c> — this is the only per-circuit
/// instrumentation the app has. See "Blazor components" in
/// <c>docs/architecture/patterns.md</c> and the circuit notes in
/// <c>docs/articles/metrics.md</c>.
/// </summary>
public sealed class BlazorMetrics : IDisposable
{
    public const string MeterName = "DiscordBot.Blazor";

    private readonly Meter _meter;
    private readonly Counter<long> _circuitsOpened;
    private readonly UpDownCounter<long> _activeCircuits;

    public BlazorMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _circuitsOpened = _meter.CreateCounter<long>(
            name: "blazor.circuits.opened_total",
            unit: "{circuits}",
            description: "Total number of Blazor Interactive Server circuits opened");

        _activeCircuits = _meter.CreateUpDownCounter<long>(
            name: "blazor.circuits.active",
            unit: "{circuits}",
            description: "Number of currently open Blazor Interactive Server circuits");
    }

    /// <summary>
    /// Records a circuit opening: increments both the lifetime counter and the active gauge.
    /// </summary>
    public void CircuitOpened()
    {
        _circuitsOpened.Add(1);
        _activeCircuits.Add(1);
    }

    /// <summary>
    /// Records a circuit closing: decrements the active gauge.
    /// </summary>
    public void CircuitClosed() => _activeCircuits.Add(-1);

    public void Dispose() => _meter.Dispose();
}
