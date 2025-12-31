using System.Collections.Concurrent;
using Discord.WebSocket;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for detecting raid patterns (coordinated attacks on a server).
/// Thread-safe singleton service with sliding window join tracking.
/// </summary>
public class RaidDetectionService : IRaidDetectionService
{
    private readonly IGuildModerationConfigService _configService;
    private readonly DiscordSocketClient _discordClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RaidDetectionService> _logger;

    // Cache key pattern: raids:{guildId}
    private const string CacheKeyPattern = "raids:{0}";

    // Cache key for stored verification levels: raidlockdown:{guildId}
    private const string LockdownStateKeyPattern = "raidlockdown:{0}";

    // Maximum retention for old entries (15 minutes)
    private static readonly TimeSpan MaxRetention = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Represents a join record for raid tracking.
    /// </summary>
    private record JoinRecord(DateTime Timestamp, ulong UserId, DateTime AccountCreated);

    public RaidDetectionService(
        IGuildModerationConfigService configService,
        DiscordSocketClient discordClient,
        IMemoryCache cache,
        ILogger<RaidDetectionService> logger)
    {
        _configService = configService;
        _discordClient = discordClient;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DetectionResultDto?> AnalyzeJoinAsync(
        ulong guildId,
        ulong userId,
        DateTime accountCreated,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Analyzing join for user {UserId} in guild {GuildId} for raid patterns", userId, guildId);

        // Get raid protection configuration for guild
        var config = await _configService.GetRaidProtectionConfigAsync(guildId, ct);

        // If raid protection is disabled, return null
        if (!config.Enabled)
        {
            _logger.LogTrace("Raid protection is disabled for guild {GuildId}", guildId);
            return null;
        }

        // Record the join
        RecordJoin(guildId, userId, DateTime.UtcNow);

        var window = TimeSpan.FromSeconds(config.WindowSeconds);
        var joinCount = GetRecentJoinCount(guildId, window);
        var threshold = config.MaxJoinsPerWindow;

        // Check account age
        var accountAge = DateTime.UtcNow - accountCreated;
        var isNewAccount = config.MinAccountAgeHours > 0 &&
                          accountAge < TimeSpan.FromHours(config.MinAccountAgeHours);

        // Determine severity and create detection result
        DetectionResultDto? result = null;

        // Mass join detection with tiered severity
        if (joinCount >= threshold * 2)
        {
            // Critical: 2x threshold
            _logger.LogCritical("Critical raid detected in guild {GuildId}: {Count} joins in {Window}s (threshold: {Threshold}), new account ratio: {Ratio:P0}",
                guildId, joinCount, config.WindowSeconds, threshold, GetNewAccountRatio(guildId, window, config.MinAccountAgeHours));

            result = new DetectionResultDto
            {
                RuleType = RuleType.Raid,
                Severity = Severity.Critical,
                Description = $"Critical raid detected: {joinCount} joins in {config.WindowSeconds} seconds (threshold: {threshold})",
                Evidence = new Dictionary<string, object>
                {
                    ["joinCount"] = joinCount,
                    ["windowSeconds"] = config.WindowSeconds,
                    ["threshold"] = threshold,
                    ["newAccountCount"] = GetNewAccountCount(guildId, window, config.MinAccountAgeHours),
                    ["userId"] = userId,
                    ["accountAge"] = accountAge.ToString()
                },
                ShouldAutoAction = config.AutoAction != RaidAutoAction.None && config.AutoAction != RaidAutoAction.AlertOnly,
                RecommendedAction = AutoAction.Kick // For raids, recommend kicking suspicious joins
            };
        }
        else if (joinCount >= threshold)
        {
            // High: 1x threshold
            _logger.LogWarning("Raid detected in guild {GuildId}: {Count} joins in {Window}s (threshold: {Threshold})",
                guildId, joinCount, config.WindowSeconds, threshold);

            result = new DetectionResultDto
            {
                RuleType = RuleType.Raid,
                Severity = Severity.High,
                Description = $"Raid detected: {joinCount} joins in {config.WindowSeconds} seconds (threshold: {threshold})",
                Evidence = new Dictionary<string, object>
                {
                    ["joinCount"] = joinCount,
                    ["windowSeconds"] = config.WindowSeconds,
                    ["threshold"] = threshold,
                    ["newAccountCount"] = GetNewAccountCount(guildId, window, config.MinAccountAgeHours),
                    ["userId"] = userId,
                    ["accountAge"] = accountAge.ToString()
                },
                ShouldAutoAction = config.AutoAction != RaidAutoAction.None && config.AutoAction != RaidAutoAction.AlertOnly,
                RecommendedAction = AutoAction.Kick // For raids, recommend kicking suspicious joins
            };
        }
        else if (isNewAccount && joinCount >= threshold * 0.5)
        {
            // Medium: New account during elevated join activity
            _logger.LogInformation("New account join during elevated activity in guild {GuildId}: user {UserId}, account age {AccountAge}, recent joins: {JoinCount}",
                guildId, userId, accountAge, joinCount);

            result = new DetectionResultDto
            {
                RuleType = RuleType.Raid,
                Severity = Severity.Medium,
                Description = $"New account join during elevated activity: account age {accountAge.TotalHours:F1} hours, {joinCount} recent joins",
                Evidence = new Dictionary<string, object>
                {
                    ["joinCount"] = joinCount,
                    ["windowSeconds"] = config.WindowSeconds,
                    ["accountAge"] = accountAge.ToString(),
                    ["minAccountAgeHours"] = config.MinAccountAgeHours,
                    ["userId"] = userId
                },
                ShouldAutoAction = false, // Don't auto-action on medium severity
                RecommendedAction = AutoAction.None // Monitor only, no action
            };
        }

        return result;
    }

    /// <inheritdoc />
    public void RecordJoin(ulong guildId, ulong userId, DateTime joinTime)
    {
        var cacheKey = string.Format(CacheKeyPattern, guildId);

        // Get or create the join list for this guild
        var joins = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SlidingExpiration = MaxRetention;
            return new ConcurrentBag<JoinRecord>();
        });

        if (joins == null)
        {
            joins = new ConcurrentBag<JoinRecord>();
            _cache.Set(cacheKey, joins, MaxRetention);
        }

        // Add the new join (we don't have account creation time here, will be set to min value)
        joins.Add(new JoinRecord(joinTime, userId, DateTime.MinValue));

        // Clean up old entries to prevent unbounded growth
        CleanupOldEntries(joins, joinTime);
    }

    /// <inheritdoc />
    public int GetRecentJoinCount(ulong guildId, TimeSpan window)
    {
        var cacheKey = string.Format(CacheKeyPattern, guildId);

        if (!_cache.TryGetValue<ConcurrentBag<JoinRecord>>(cacheKey, out var joins) || joins == null)
        {
            return 0;
        }

        var cutoff = DateTime.UtcNow - window;
        return joins.Count(j => j.Timestamp >= cutoff);
    }

    /// <inheritdoc />
    public async Task TriggerLockdownAsync(ulong guildId, CancellationToken ct = default)
    {
        _logger.LogWarning("Triggering raid lockdown for guild {GuildId}", guildId);

        var guild = _discordClient.GetGuild(guildId);
        if (guild == null)
        {
            _logger.LogError("Cannot trigger lockdown: guild {GuildId} not found in client cache", guildId);
            return;
        }

        try
        {
            // Store the current verification level
            var currentLevel = guild.VerificationLevel;
            var lockdownStateKey = string.Format(LockdownStateKeyPattern, guildId);
            _cache.Set(lockdownStateKey, currentLevel, TimeSpan.FromHours(24));

            _logger.LogInformation("Stored previous verification level {Level} for guild {GuildId}", currentLevel, guildId);

            // Set verification level to High
            await guild.ModifyAsync(props =>
            {
                props.VerificationLevel = Discord.VerificationLevel.High;
            });

            _logger.LogInformation("Set verification level to High for guild {GuildId}", guildId);

            // Optionally disable invites (if AutoAction allows)
            var config = await _configService.GetRaidProtectionConfigAsync(guildId, ct);
            if (config.AutoAction == RaidAutoAction.LockInvites || config.AutoAction == RaidAutoAction.LockServer)
            {
                var invites = await guild.GetInvitesAsync();
                foreach (var invite in invites)
                {
                    try
                    {
                        await invite.DeleteAsync();
                        _logger.LogDebug("Deleted invite {InviteCode} for guild {GuildId}", invite.Code, guildId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete invite {InviteCode} for guild {GuildId}", invite.Code, guildId);
                    }
                }

                _logger.LogInformation("Disabled {Count} active invites for guild {GuildId}", invites.Count, guildId);
            }

            _logger.LogWarning("Raid lockdown activated for guild {GuildId}", guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger raid lockdown for guild {GuildId}", guildId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task LiftLockdownAsync(ulong guildId, CancellationToken ct = default)
    {
        _logger.LogInformation("Lifting raid lockdown for guild {GuildId}", guildId);

        var guild = _discordClient.GetGuild(guildId);
        if (guild == null)
        {
            _logger.LogError("Cannot lift lockdown: guild {GuildId} not found in client cache", guildId);
            return;
        }

        try
        {
            // Restore the previous verification level
            var lockdownStateKey = string.Format(LockdownStateKeyPattern, guildId);
            if (_cache.TryGetValue<Discord.VerificationLevel>(lockdownStateKey, out var previousLevel))
            {
                await guild.ModifyAsync(props =>
                {
                    props.VerificationLevel = previousLevel;
                });

                _logger.LogInformation("Restored verification level to {Level} for guild {GuildId}", previousLevel, guildId);

                // Remove the stored state
                _cache.Remove(lockdownStateKey);
            }
            else
            {
                _logger.LogWarning("No previous verification level found for guild {GuildId}, setting to Low", guildId);
                await guild.ModifyAsync(props =>
                {
                    props.VerificationLevel = Discord.VerificationLevel.Low;
                });
            }

            _logger.LogInformation("Raid lockdown lifted for guild {GuildId}", guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to lift raid lockdown for guild {GuildId}", guildId);
            throw;
        }
    }

    /// <summary>
    /// Gets the count of new accounts (below min age) in the time window.
    /// </summary>
    private int GetNewAccountCount(ulong guildId, TimeSpan window, int minAccountAgeHours)
    {
        if (minAccountAgeHours == 0)
            return 0;

        var cacheKey = string.Format(CacheKeyPattern, guildId);

        if (!_cache.TryGetValue<ConcurrentBag<JoinRecord>>(cacheKey, out var joins) || joins == null)
        {
            return 0;
        }

        var cutoff = DateTime.UtcNow - window;
        var minAccountAge = TimeSpan.FromHours(minAccountAgeHours);

        return joins.Count(j => j.Timestamp >= cutoff &&
                               j.AccountCreated != DateTime.MinValue &&
                               (DateTime.UtcNow - j.AccountCreated) < minAccountAge);
    }

    /// <summary>
    /// Gets the ratio of new accounts to total joins in the time window.
    /// </summary>
    private double GetNewAccountRatio(ulong guildId, TimeSpan window, int minAccountAgeHours)
    {
        var totalJoins = GetRecentJoinCount(guildId, window);
        if (totalJoins == 0)
            return 0;

        var newAccounts = GetNewAccountCount(guildId, window, minAccountAgeHours);
        return (double)newAccounts / totalJoins;
    }

    /// <summary>
    /// Removes entries older than the maximum retention period.
    /// This prevents unbounded memory growth.
    /// </summary>
    private static void CleanupOldEntries(ConcurrentBag<JoinRecord> joins, DateTime currentTime)
    {
        var cutoff = currentTime - MaxRetention;

        // Only cleanup if we have a lot of entries
        if (joins.Count > 100)
        {
            var validJoins = joins.Where(j => j.Timestamp >= cutoff).ToList();
            joins.Clear();
            foreach (var join in validJoins)
            {
                joins.Add(join);
            }
        }
    }
}
