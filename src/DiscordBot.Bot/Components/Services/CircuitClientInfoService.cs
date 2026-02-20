using Microsoft.AspNetCore.Components.Server.Circuits;

namespace DiscordBot.Bot.Components.Services;

/// <summary>
/// Captures client IP address during the initial HTTP request of a Blazor circuit
/// and makes it available for the circuit's lifetime (for audit logging).
/// </summary>
public class CircuitClientInfoService : CircuitHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CircuitClientInfoService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Client IP address captured from the initial HTTP request.
    /// Returns null if IP could not be determined.
    /// </summary>
    public string? ClientIpAddress { get; private set; }

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        // UseForwardedHeaders middleware already populates RemoteIpAddress
        // with the real client IP from trusted proxies, so we don't need
        // to manually parse X-Forwarded-For here.
        ClientIpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        return Task.CompletedTask;
    }
}
