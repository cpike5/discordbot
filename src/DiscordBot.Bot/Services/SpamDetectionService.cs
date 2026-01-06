using System.Security.Cryptography;
using System.Text;
using DiscordBot.Bot.Collections;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for detecting spam patterns in messages using in-memory sliding window tracking.
/// Thread-safe singleton service with bounded memory usage.
/// </summary>
public class SpamDetectionService : ISpamDetectionService, IMemoryReportable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SpamDetectionService> _logger;
    private readonly AutoModerationOptions _options;

    // Cache key pattern: spam:{guildId}:{userId}
    private const string CacheKeyPattern = "spam:{0}:{1}";

    // Maximum retention for old entries (5 minutes)
    private static readonly TimeSpan MaxRetention = TimeSpan.FromMinutes(5);

    // Estimated bytes per MessageRecord: DateTime (8) + string reference (8) + string content (~44 avg) + object overhead (8) = ~68 bytes
    private const int EstimatedBytesPerMessage = 68;

    /// <summary>
    /// Represents a message record for spam tracking.
    /// </summary>
    private record MessageRecord(DateTime Timestamp, string ContentHash) : ITimestamped;

    public SpamDetectionService(
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        ILogger<SpamDetectionService> logger,
        IOptions<AutoModerationOptions> options)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
        _options = options.Value;

        _logger.LogInformation(
            "SpamDetectionService initialized with MaxMessagesPerUser={MaxMessages}",
            _options.MaxMessagesPerUser);
    }

    /// <inheritdoc />
    public async Task<DetectionResultDto?> AnalyzeMessageAsync(
        ulong guildId,
        ulong userId,
        ulong channelId,
        string content,
        ulong messageId,
        DateTime accountCreated,
        CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "spam_detection",
            "analyze_message",
            guildId: guildId,
            userId: userId);

        activity?.SetTag(TracingConstants.Attributes.ChannelId, channelId.ToString());

        try
        {
            _logger.LogDebug("Analyzing message {MessageId} from user {UserId} in guild {GuildId} for spam patterns",
                messageId, userId, guildId);

            // Get spam configuration for guild using a scope for the scoped service
            using var scope = _scopeFactory.CreateScope();
            var configService = scope.ServiceProvider.GetRequiredService<IGuildModerationConfigService>();
            var config = await configService.GetSpamConfigAsync(guildId, ct);

            // If spam detection is disabled, return null
            if (!config.Enabled)
            {
                _logger.LogTrace("Spam detection is disabled for guild {GuildId}", guildId);
                BotActivitySource.SetSuccess(activity);
                return null;
            }

            // Calculate content hash for duplicate detection
            var contentHash = ComputeHash(NormalizeContent(content));

            // Record the message
            RecordMessage(guildId, userId, contentHash, DateTime.UtcNow);

            var window = TimeSpan.FromSeconds(config.WindowSeconds);

            // Check message flood
            var messageCount = GetMessageCount(guildId, userId, window);
            var threshold = config.MaxMessagesPerWindow;

            // Apply new account multiplier (accounts < 7 days old have stricter limits)
            var accountAge = DateTime.UtcNow - accountCreated;
            if (accountAge < TimeSpan.FromDays(7))
            {
                threshold = (int)(threshold * 0.7); // 30% stricter for new accounts
                _logger.LogDebug("Applied new account multiplier for user {UserId}, adjusted threshold from {Original} to {Adjusted}",
                    userId, config.MaxMessagesPerWindow, threshold);
            }

            // Check duplicate messages
            var duplicateCount = GetDuplicateCount(guildId, userId, contentHash, window);

            // Check @everyone/@here abuse
            var hasEveryoneOrHere = content.Contains("@everyone", StringComparison.OrdinalIgnoreCase) ||
                                    content.Contains("@here", StringComparison.OrdinalIgnoreCase);

            // Determine severity and create detection result
            DetectionResultDto? result = null;

            // Message flood checks with tiered severity
            if (messageCount >= threshold * 2)
            {
                _logger.LogWarning("Critical spam detected for user {UserId} in guild {GuildId}: {Count} messages in {Window}s (threshold: {Threshold})",
                    userId, guildId, messageCount, config.WindowSeconds, threshold);

                result = new DetectionResultDto
                {
                    RuleType = RuleType.Spam,
                    Severity = Severity.Critical,
                    Description = $"Message flood detected: {messageCount} messages in {config.WindowSeconds} seconds (threshold: {threshold})",
                    Evidence = new Dictionary<string, object>
                    {
                        ["messageCount"] = messageCount,
                        ["windowSeconds"] = config.WindowSeconds,
                        ["threshold"] = threshold,
                        ["messageId"] = messageId,
                        ["channelId"] = channelId
                    },
                    ShouldAutoAction = config.AutoAction != AutoAction.None,
                    RecommendedAction = config.AutoAction
                };
            }
            else if (messageCount >= threshold * 1.5)
            {
                _logger.LogWarning("High spam detected for user {UserId} in guild {GuildId}: {Count} messages in {Window}s (threshold: {Threshold})",
                    userId, guildId, messageCount, config.WindowSeconds, threshold);

                result = new DetectionResultDto
                {
                    RuleType = RuleType.Spam,
                    Severity = Severity.High,
                    Description = $"Message flood detected: {messageCount} messages in {config.WindowSeconds} seconds (threshold: {threshold})",
                    Evidence = new Dictionary<string, object>
                    {
                        ["messageCount"] = messageCount,
                        ["windowSeconds"] = config.WindowSeconds,
                        ["threshold"] = threshold,
                        ["messageId"] = messageId,
                        ["channelId"] = channelId
                    },
                    ShouldAutoAction = config.AutoAction != AutoAction.None,
                    RecommendedAction = config.AutoAction
                };
            }
            else if (messageCount >= threshold)
            {
                _logger.LogInformation("Medium spam detected for user {UserId} in guild {GuildId}: {Count} messages in {Window}s (threshold: {Threshold})",
                    userId, guildId, messageCount, config.WindowSeconds, threshold);

                result = new DetectionResultDto
                {
                    RuleType = RuleType.Spam,
                    Severity = Severity.Medium,
                    Description = $"Message flood detected: {messageCount} messages in {config.WindowSeconds} seconds (threshold: {threshold})",
                    Evidence = new Dictionary<string, object>
                    {
                        ["messageCount"] = messageCount,
                        ["windowSeconds"] = config.WindowSeconds,
                        ["threshold"] = threshold,
                        ["messageId"] = messageId,
                        ["channelId"] = channelId
                    },
                    ShouldAutoAction = config.AutoAction != AutoAction.None,
                    RecommendedAction = config.AutoAction
                };
            }

            // Duplicate message check (only if not already flagged for flood)
            if (result == null && duplicateCount >= 3)
            {
                _logger.LogInformation("Duplicate messages detected for user {UserId} in guild {GuildId}: {Count} identical messages in {Window}s",
                    userId, guildId, duplicateCount, config.WindowSeconds);

                result = new DetectionResultDto
                {
                    RuleType = RuleType.Spam,
                    Severity = Severity.Medium,
                    Description = $"Duplicate message spam: {duplicateCount} identical messages in {config.WindowSeconds} seconds",
                    Evidence = new Dictionary<string, object>
                    {
                        ["duplicateCount"] = duplicateCount,
                        ["windowSeconds"] = config.WindowSeconds,
                        ["contentHash"] = contentHash,
                        ["messageId"] = messageId,
                        ["channelId"] = channelId
                    },
                    ShouldAutoAction = config.AutoAction != AutoAction.None,
                    RecommendedAction = config.AutoAction
                };
            }

            // @everyone/@here abuse check (only if not already flagged)
            if (result == null && hasEveryoneOrHere && messageCount >= 2)
            {
                _logger.LogWarning("@everyone/@here abuse detected for user {UserId} in guild {GuildId}: used in {Count} messages",
                    userId, guildId, messageCount);

                result = new DetectionResultDto
                {
                    RuleType = RuleType.Spam,
                    Severity = Severity.High,
                    Description = $"@everyone/@here abuse detected: used in {messageCount} messages in {config.WindowSeconds} seconds",
                    Evidence = new Dictionary<string, object>
                    {
                        ["messageCount"] = messageCount,
                        ["windowSeconds"] = config.WindowSeconds,
                        ["messageId"] = messageId,
                        ["channelId"] = channelId,
                        ["mentionType"] = content.Contains("@everyone", StringComparison.OrdinalIgnoreCase) ? "everyone" : "here"
                    },
                    ShouldAutoAction = config.AutoAction != AutoAction.None,
                    RecommendedAction = config.AutoAction
                };
            }

            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public void RecordMessage(ulong guildId, ulong userId, string contentHash, DateTime timestamp)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "spam_detection",
            "record_message",
            guildId: guildId,
            userId: userId);

        try
        {
            var cacheKey = string.Format(CacheKeyPattern, guildId, userId);

            // Get or create the bounded queue for this user
            var messages = _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.SlidingExpiration = MaxRetention;
                return new BoundedTimestampQueue<MessageRecord>(_options.MaxMessagesPerUser);
            });

            if (messages == null)
            {
                messages = new BoundedTimestampQueue<MessageRecord>(_options.MaxMessagesPerUser);
                _cache.Set(cacheKey, messages, new MemoryCacheEntryOptions { SlidingExpiration = MaxRetention });
            }

            // Check if queue is at capacity and log if so (for monitoring)
            if (messages.IsAtCapacity)
            {
                _logger.LogDebug(
                    "Spam detection queue at capacity for user {UserId} in guild {GuildId}. Oldest messages will be overwritten.",
                    userId, guildId);
            }

            // Add the new message - oldest will be automatically evicted if at capacity
            messages.Enqueue(new MessageRecord(timestamp, contentHash));

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public int GetMessageCount(ulong guildId, ulong userId, TimeSpan window)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "spam_detection",
            "get_message_count",
            guildId: guildId,
            userId: userId);

        try
        {
            var cacheKey = string.Format(CacheKeyPattern, guildId, userId);

            if (!_cache.TryGetValue<BoundedTimestampQueue<MessageRecord>>(cacheKey, out var messages) || messages == null)
            {
                BotActivitySource.SetSuccess(activity);
                return 0;
            }

            var cutoff = DateTime.UtcNow - window;
            var count = messages.CountAfter(cutoff);

            BotActivitySource.SetSuccess(activity);
            return count;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public int GetDuplicateCount(ulong guildId, ulong userId, string contentHash, TimeSpan window)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "spam_detection",
            "get_duplicate_count",
            guildId: guildId,
            userId: userId);

        try
        {
            var cacheKey = string.Format(CacheKeyPattern, guildId, userId);

            if (!_cache.TryGetValue<BoundedTimestampQueue<MessageRecord>>(cacheKey, out var messages) || messages == null)
            {
                BotActivitySource.SetSuccess(activity);
                return 0;
            }

            var cutoff = DateTime.UtcNow - window;
            var count = messages.CountAfterWithPredicate(cutoff, m => m.ContentHash == contentHash);

            BotActivitySource.SetSuccess(activity);
            return count;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Normalizes message content for consistent hash calculation.
    /// Removes extra whitespace and converts to lowercase.
    /// </summary>
    private static string NormalizeContent(string content)
    {
        return string.Join(" ", content.ToLowerInvariant()
            .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Computes a SHA256 hash of the content for duplicate detection.
    /// </summary>
    private static string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    #region IMemoryReportable Implementation

    /// <inheritdoc />
    public string ServiceName => "Spam Detection Service";

    /// <inheritdoc />
    public ServiceMemoryReportDto GetMemoryReport()
    {
        // We cannot directly enumerate the cache, but we can estimate based on configuration
        // Maximum memory per user = MaxMessagesPerUser * EstimatedBytesPerMessage + queue overhead
        var maxBytesPerUser = (_options.MaxMessagesPerUser * EstimatedBytesPerMessage) + 64;

        // We don't know exactly how many users are cached, but we can report the max possible
        // based on MaxCachedGuilds as a rough estimate
        var estimatedMaxBytes = maxBytesPerUser * _options.MaxCachedGuilds;

        return new ServiceMemoryReportDto
        {
            ServiceName = ServiceName,
            EstimatedBytes = estimatedMaxBytes,
            ItemCount = _options.MaxMessagesPerUser, // Report max capacity per queue
            Details = $"Max {_options.MaxMessagesPerUser} messages/user, ~{maxBytesPerUser / 1024:N1} KB/user, bounded by IMemoryCache sliding expiration"
        };
    }

    #endregion
}
