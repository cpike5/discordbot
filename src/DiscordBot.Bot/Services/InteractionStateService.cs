using System.Collections.Concurrent;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for managing temporary state data for Discord component interactions.
/// Uses an in-memory concurrent dictionary with automatic expiration.
/// </summary>
public class InteractionStateService : IInteractionStateService, IMemoryReportable
{
    private readonly ConcurrentDictionary<string, object> _states = new();
    private readonly ILogger<InteractionStateService> _logger;
    private readonly CachingOptions _cachingOptions;

    public InteractionStateService(
        ILogger<InteractionStateService> logger,
        IOptions<CachingOptions> cachingOptions)
    {
        _logger = logger;
        _cachingOptions = cachingOptions.Value;
    }

    /// <inheritdoc />
    public string CreateState<T>(ulong userId, T data, TimeSpan? expiry = null)
    {
        var correlationId = GenerateCorrelationId();
        var expiryDuration = expiry ?? TimeSpan.FromMinutes(_cachingOptions.InteractionStateExpiryMinutes);

        var state = new InteractionState<T>
        {
            CorrelationId = correlationId,
            UserId = userId,
            Data = data,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(expiryDuration)
        };

        if (_states.TryAdd(correlationId, state))
        {
            _logger.LogDebug(
                "Created interaction state {CorrelationId} for user {UserId}, expires at {ExpiresAt}",
                correlationId,
                userId,
                state.ExpiresAt);
            return correlationId;
        }

        _logger.LogWarning(
            "Failed to create interaction state {CorrelationId} for user {UserId} - correlation ID collision",
            correlationId,
            userId);
        throw new InvalidOperationException($"Failed to create state with correlation ID {correlationId}");
    }

    /// <inheritdoc />
    public bool TryGetState<T>(string correlationId, out T? state)
    {
        state = default;

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            _logger.LogTrace("TryGetState called with null/empty correlation ID");
            return false;
        }

        if (!_states.TryGetValue(correlationId, out var obj))
        {
            _logger.LogTrace("State not found for correlation ID {CorrelationId}", correlationId);
            return false;
        }

        if (obj is not InteractionState<T> interactionState)
        {
            _logger.LogWarning(
                "State type mismatch for correlation ID {CorrelationId}: expected {ExpectedType}, got {ActualType}",
                correlationId,
                typeof(T).Name,
                obj.GetType().Name);
            return false;
        }

        if (interactionState.IsExpired)
        {
            _logger.LogDebug(
                "State expired for correlation ID {CorrelationId} (expired at {ExpiresAt})",
                correlationId,
                interactionState.ExpiresAt);
            _states.TryRemove(correlationId, out _);
            return false;
        }

        state = interactionState.Data;
        _logger.LogTrace("Retrieved state for correlation ID {CorrelationId}", correlationId);
        return true;
    }

    /// <inheritdoc />
    public bool TryRemoveState(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            _logger.LogTrace("TryRemoveState called with null/empty correlation ID");
            return false;
        }

        if (_states.TryRemove(correlationId, out _))
        {
            _logger.LogDebug("Removed state for correlation ID {CorrelationId}", correlationId);
            return true;
        }

        _logger.LogTrace("State not found for removal, correlation ID {CorrelationId}", correlationId);
        return false;
    }

    /// <inheritdoc />
    public int CleanupExpired()
    {
        var expiredKeys = new List<string>();
        var now = DateTime.UtcNow;

        foreach (var kvp in _states)
        {
            // Check if the value has an ExpiresAt property using reflection or dynamic typing
            var stateType = kvp.Value.GetType();
            var expiresAtProperty = stateType.GetProperty("ExpiresAt");

            if (expiresAtProperty != null)
            {
                var expiresAt = (DateTime?)expiresAtProperty.GetValue(kvp.Value);
                if (expiresAt.HasValue && now >= expiresAt.Value)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }
        }

        var removedCount = 0;
        foreach (var key in expiredKeys)
        {
            if (_states.TryRemove(key, out _))
            {
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired interaction states", removedCount);
        }
        else
        {
            _logger.LogTrace("No expired interaction states to clean up");
        }

        return removedCount;
    }

    /// <inheritdoc />
    public int ActiveStateCount => _states.Count;

    /// <summary>
    /// Generates an 8-character correlation ID.
    /// </summary>
    private static string GenerateCorrelationId()
    {
        return Guid.NewGuid().ToString("N")[..8];
    }

    #region IMemoryReportable Implementation

    /// <inheritdoc/>
    public string ServiceName => "Interaction State";

    /// <inheritdoc/>
    public ServiceMemoryReportDto GetMemoryReport()
    {
        // ConcurrentDictionary overhead per entry: ~80 bytes
        // InteractionState<T> object: ~40 bytes base (correlationId, userId, timestamps)
        // Average state data estimated at ~200 bytes
        const int entryOverheadBytes = 80;
        const int stateBaseBytes = 40;
        const int avgDataBytes = 200;
        const int perEntryEstimate = entryOverheadBytes + stateBaseBytes + avgDataBytes;

        var count = _states.Count;
        var estimatedBytes = count * perEntryEstimate;

        return new ServiceMemoryReportDto
        {
            ServiceName = ServiceName,
            EstimatedBytes = estimatedBytes,
            ItemCount = count,
            Details = $"Active states: {count} (expiry: {_cachingOptions.InteractionStateExpiryMinutes} min)"
        };
    }

    #endregion
}
