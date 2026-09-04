using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Read-side of the guild assistant's usage metrics and interaction log, bundled behind one
/// dependency so <c>AssistantService</c> doesn't need direct references to both repositories
/// just to serve the admin dashboard/API.
/// </summary>
public interface IAssistantTelemetryReader
{
    Task<AssistantUsageMetrics?> GetUsageMetricsAsync(
        ulong guildId, DateTime date, CancellationToken cancellationToken = default);

    Task<IEnumerable<AssistantUsageMetrics>> GetUsageMetricsRangeAsync(
        ulong guildId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    Task<IEnumerable<AssistantInteractionLog>> GetRecentInteractionsAsync(
        ulong guildId, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>Records a failed request against today's usage metrics (best-effort).</summary>
    Task IncrementFailedRequestAsync(ulong guildId, CancellationToken cancellationToken = default);
}
