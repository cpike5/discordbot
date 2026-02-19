using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DiscordBot.Bot.Health;

/// <summary>
/// Health check that monitors the Discord gateway connection state.
/// Uses Degraded (not Unhealthy) for disconnected state to avoid Docker restarts
/// during normal reconnection cycles.
/// </summary>
public class DiscordGatewayHealthCheck : IHealthCheck
{
    private readonly IConnectionStateService _connectionStateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscordGatewayHealthCheck"/> class.
    /// </summary>
    /// <param name="connectionStateService">The connection state service.</param>
    public DiscordGatewayHealthCheck(IConnectionStateService connectionStateService)
    {
        _connectionStateService = connectionStateService;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var state = _connectionStateService.GetCurrentState();

        var result = state switch
        {
            GatewayConnectionState.Connected => HealthCheckResult.Healthy("Discord gateway is connected."),
            GatewayConnectionState.Connecting => HealthCheckResult.Degraded("Discord gateway is connecting."),
            GatewayConnectionState.Disconnected => HealthCheckResult.Degraded("Discord gateway is disconnected."),
            _ => HealthCheckResult.Degraded("Discord gateway state is unknown.")
        };

        return Task.FromResult(result);
    }
}
