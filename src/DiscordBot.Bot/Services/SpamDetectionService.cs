using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for detecting spam patterns in messages using in-memory sliding window tracking.
/// Thread-safe singleton service.
/// </summary>
public class SpamDetectionService : ISpamDetectionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SpamDetectionService> _logger;

    // Cache key pattern: spam:{guildId}:{userId}
    private const string CacheKeyPattern = "spam:{0}:{1}";

    // Maximum retention for old entries (5 minutes)
    private static readonly TimeSpan MaxRetention = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Represents a message record for spam tracking.
    /// </summary>
    private record MessageRecord(DateTime Timestamp, string ContentHash);

    public SpamDetectionService(
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        ILogger<SpamDetectionService> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
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

            // Get or create the message list for this user
            var messages = _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.SlidingExpiration = MaxRetention;
                return new ConcurrentBag<MessageRecord>();
            });

            if (messages == null)
            {
                messages = new ConcurrentBag<MessageRecord>();
                _cache.Set(cacheKey, messages, MaxRetention);
            }

            // Add the new message
            messages.Add(new MessageRecord(timestamp, contentHash));

            // Clean up old entries to prevent unbounded growth
            CleanupOldEntries(messages, timestamp);

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

            if (!_cache.TryGetValue<ConcurrentBag<MessageRecord>>(cacheKey, out var messages) || messages == null)
            {
                BotActivitySource.SetSuccess(activity);
                return 0;
            }

            var cutoff = DateTime.UtcNow - window;
            var count = messages.Count(m => m.Timestamp >= cutoff);

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

            if (!_cache.TryGetValue<ConcurrentBag<MessageRecord>>(cacheKey, out var messages) || messages == null)
            {
                BotActivitySource.SetSuccess(activity);
                return 0;
            }

            var cutoff = DateTime.UtcNow - window;
            var count = messages.Count(m => m.Timestamp >= cutoff && m.ContentHash == contentHash);

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

    /// <summary>
    /// Removes entries older than the maximum retention period.
    /// This prevents unbounded memory growth.
    /// </summary>
    private static void CleanupOldEntries(ConcurrentBag<MessageRecord> messages, DateTime currentTime)
    {
        var cutoff = currentTime - MaxRetention;

        // Only cleanup if we have a lot of entries
        if (messages.Count > 100)
        {
            var validMessages = messages.Where(m => m.Timestamp >= cutoff).ToList();
            messages.Clear();
            foreach (var msg in validMessages)
            {
                messages.Add(msg);
            }
        }
    }
}
