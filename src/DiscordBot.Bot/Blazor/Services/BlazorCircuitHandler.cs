using System.Security.Claims;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Metrics;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Serilog.Context;

namespace DiscordBot.Bot.Blazor.Services;

/// <summary>
/// Observes Blazor Interactive Server circuit open/close, the one piece of per-circuit
/// instrumentation the app has - circuit interactions travel over the SignalR hub, never an HTTP
/// request, so they never pass through <c>CorrelationIdMiddleware</c>, <c>ApiMetricsMiddleware</c>
/// or <c>UseSerilogRequestLogging</c> (see the circuit notes in <c>docs/articles/metrics.md</c>
/// and <c>docs/articles/tracing.md</c>). Registered scoped, so one instance lives for the whole
/// circuit; that is also what makes <see cref="CircuitClientInfoService"/> - populated here -
/// visible to every component the circuit renders.
/// </summary>
public sealed class BlazorCircuitHandler(
    CircuitClientInfoService clientInfo,
    IHttpContextAccessor httpContextAccessor,
    ILogger<BlazorCircuitHandler> logger,
    BlazorMetrics metrics) : CircuitHandler
{
    private string? _userId;

    /// <inheritdoc />
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        HandleCircuitOpened(circuit.Id, httpContextAccessor.HttpContext);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        HandleCircuitClosed(circuit.Id);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The testable core of <see cref="OnCircuitOpenedAsync"/>. <see cref="Circuit"/> has no
    /// public constructor, so tests call this directly with a synthetic circuit ID rather than a
    /// real <see cref="Circuit"/> instance.
    /// </summary>
    /// <param name="circuitId">The circuit's ID.</param>
    /// <param name="httpContext">
    /// The <see cref="HttpContext"/> of the request that negotiated the circuit. Available at
    /// circuit-open time even though it is not available for the rest of the circuit's life
    /// (see "HttpContext is only available during prerendering" in
    /// <c>docs/architecture/patterns.md</c>); may be <c>null</c> in tests or unusual hosting
    /// setups, in which case IP/UA/user are left unset and a fresh correlation ID is generated.
    /// </param>
    internal void HandleCircuitOpened(string circuitId, HttpContext? httpContext)
    {
        var remoteIp = httpContext?.Connection.RemoteIpAddress;
        var userAgentHeader = httpContext?.Request.Headers.UserAgent.ToString();
        var userAgent = string.IsNullOrEmpty(userAgentHeader) ? null : userAgentHeader;
        var correlationId = httpContext?.GetCorrelationId() ?? GenerateCorrelationId();
        _userId = httpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        clientInfo.Populate(remoteIp, userAgent, circuitId, correlationId);

        metrics.CircuitOpened();

        // Scoped to this one log call only - OnCircuitOpenedAsync and OnCircuitClosedAsync are
        // different async flows (there is no single ambient context spanning a circuit's whole
        // life), so a PushProperty here that outlived the call would scope nothing meaningful
        // and could clobber another flow's enricher stack. A component that needs the
        // correlation ID reliably reads it from CircuitClientInfoService instead.
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            logger.LogInformation(
                "Blazor circuit opened. UserId: {UserId}, CircuitId: {CircuitId}, CorrelationId: {CorrelationId}",
                _userId ?? "anonymous",
                circuitId,
                correlationId);
        }
    }

    /// <summary>
    /// The testable core of <see cref="OnCircuitClosedAsync"/>.
    /// </summary>
    internal void HandleCircuitClosed(string circuitId)
    {
        metrics.CircuitClosed();

        using (LogContext.PushProperty("CorrelationId", clientInfo.CorrelationId))
        {
            logger.LogInformation(
                "Blazor circuit closed. UserId: {UserId}, CircuitId: {CircuitId}, CorrelationId: {CorrelationId}",
                _userId ?? "anonymous",
                circuitId,
                clientInfo.CorrelationId);
        }
    }

    private static string GenerateCorrelationId() => Guid.NewGuid().ToString("N")[..16];
}
