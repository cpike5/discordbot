using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;

namespace DiscordBot.Infrastructure.Services;

/// <inheritdoc cref="IAssistantTelemetryReader" />
public class AssistantTelemetryReader : IAssistantTelemetryReader
{
    private readonly IAssistantUsageMetricsRepository _metricsRepository;
    private readonly IAssistantInteractionLogRepository _interactionLogRepository;

    public AssistantTelemetryReader(
        IAssistantUsageMetricsRepository metricsRepository,
        IAssistantInteractionLogRepository interactionLogRepository)
    {
        _metricsRepository = metricsRepository ?? throw new ArgumentNullException(nameof(metricsRepository));
        _interactionLogRepository = interactionLogRepository ?? throw new ArgumentNullException(nameof(interactionLogRepository));
    }

    /// <inheritdoc />
    public async Task<AssistantUsageMetrics?> GetUsageMetricsAsync(
        ulong guildId, DateTime date, CancellationToken cancellationToken = default)
    {
        var metrics = await _metricsRepository.GetRangeAsync(guildId, date.Date, date.Date, cancellationToken);
        return metrics.FirstOrDefault();
    }

    /// <inheritdoc />
    public Task<IEnumerable<AssistantUsageMetrics>> GetUsageMetricsRangeAsync(
        ulong guildId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return _metricsRepository.GetRangeAsync(guildId, startDate.Date, endDate.Date, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IEnumerable<AssistantInteractionLog>> GetRecentInteractionsAsync(
        ulong guildId, int limit = 50, CancellationToken cancellationToken = default)
    {
        return _interactionLogRepository.GetRecentByGuildAsync(guildId, limit, cancellationToken);
    }

    /// <inheritdoc />
    public Task IncrementFailedRequestAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        return _metricsRepository.IncrementFailedRequestAsync(guildId, DateTime.UtcNow.Date, cancellationToken);
    }
}
