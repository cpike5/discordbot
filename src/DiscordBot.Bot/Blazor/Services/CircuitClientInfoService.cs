namespace DiscordBot.Bot.Blazor.Services;

/// <summary>
/// Captures the client IP address and user agent from the HTTP request that
/// started the circuit (or the current request during prerender/static SSR),
/// so audit-log enqueues from Blazor components can record an address after
/// the circuit has outlived its originating request. Scoped: one instance per
/// circuit / per request.
/// </summary>
public class CircuitClientInfoService
{
    /// <summary>The client's remote IP address, or null if unavailable.</summary>
    public string? IpAddress { get; }

    /// <summary>The client's User-Agent header, or null if unavailable.</summary>
    public string? UserAgent { get; }

    public CircuitClientInfoService(IHttpContextAccessor httpContextAccessor)
    {
        // For a circuit, the accessor still exposes the SignalR connection's
        // originating HttpContext at construction time; capture the values now
        // because the context is not reliably available later in circuit code.
        var context = httpContextAccessor.HttpContext;
        IpAddress = context?.Connection.RemoteIpAddress?.ToString();
        UserAgent = context?.Request.Headers.UserAgent.ToString();
    }
}
