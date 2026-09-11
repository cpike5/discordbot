using System.Net;

namespace DiscordBot.Bot.Blazor.Services;

/// <summary>
/// Scoped, per-circuit holder for the request context that started the circuit. Interactive
/// Server components run over a SignalR connection with no <see cref="Microsoft.AspNetCore.Http.HttpContext"/>
/// of their own (see "HttpContext is only available during prerendering" in
/// <c>docs/architecture/patterns.md</c>), so anything a component would otherwise have read off
/// <c>HttpContext</c> for audit logging - the caller's IP and user agent - is captured once, at
/// circuit start, by <see cref="BlazorCircuitHandler"/> and exposed here instead.
/// </summary>
/// <remarks>
/// Registered scoped, one instance per circuit (a circuit is one DI scope), so every component
/// resolving this service within the same circuit sees the same values.
/// </remarks>
public sealed class CircuitClientInfoService
{
    /// <summary>
    /// The remote IP address of the HTTP request that opened this circuit, or <c>null</c> if it
    /// could not be determined.
    /// </summary>
    public IPAddress? RemoteIp { get; private set; }

    /// <summary>
    /// The <c>User-Agent</c> header of the HTTP request that opened this circuit, or
    /// <c>null</c> if it was absent.
    /// </summary>
    public string? UserAgent { get; private set; }

    /// <summary>
    /// The Blazor <see cref="Microsoft.AspNetCore.Components.Server.Circuits.Circuit.Id"/> for
    /// this circuit. Empty until <see cref="Populate"/> has been called.
    /// </summary>
    public string CircuitId { get; private set; } = string.Empty;

    /// <summary>
    /// The correlation ID associated with this circuit - either the one already on the request
    /// that opened it (see <see cref="DiscordBot.Bot.Extensions.HttpContextExtensions.GetCorrelationId"/>),
    /// or a freshly generated one. Empty until <see cref="Populate"/> has been called.
    /// </summary>
    public string CorrelationId { get; private set; } = string.Empty;

    /// <summary>
    /// Sets the captured client info for this circuit. Called exactly once, by
    /// <see cref="BlazorCircuitHandler"/> when the circuit opens.
    /// </summary>
    public void Populate(IPAddress? remoteIp, string? userAgent, string circuitId, string correlationId)
    {
        RemoteIp = remoteIp;
        UserAgent = userAgent;
        CircuitId = circuitId;
        CorrelationId = correlationId;
    }
}
