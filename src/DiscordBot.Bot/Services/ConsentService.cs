using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for managing user consent operations for privacy compliance.
/// </summary>
public class ConsentService : IConsentService
{
    private readonly IUserConsentRepository _consentRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ConsentService> _logger;
    private readonly CachingOptions _cachingOptions;

    private const string WebUISource = "WebUI";

    public ConsentService(
        IUserConsentRepository consentRepository,
        IMemoryCache cache,
        ILogger<ConsentService> logger,
        IOptions<CachingOptions> cachingOptions)
    {
        _consentRepository = consentRepository;
        _cache = cache;
        _logger = logger;
        _cachingOptions = cachingOptions.Value;
    }

    public async Task<IEnumerable<ConsentStatusDto>> GetConsentStatusAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving consent status for Discord user {DiscordUserId}", discordUserId);

        // Get all user consents
        var userConsents = await _consentRepository.GetUserConsentsAsync(discordUserId, cancellationToken);

        // Build status DTOs for all consent types
        var consentStatuses = new List<ConsentStatusDto>();

        foreach (ConsentType consentType in Enum.GetValues(typeof(ConsentType)))
        {
            var activeConsent = userConsents
                .Where(c => c.ConsentType == consentType && c.IsActive)
                .OrderByDescending(c => c.GrantedAt)
                .FirstOrDefault();

            consentStatuses.Add(new ConsentStatusDto
            {
                Type = (int)consentType,
                TypeDisplayName = GetConsentTypeDisplayName(consentType),
                Description = GetConsentTypeDescription(consentType),
                IsGranted = activeConsent != null,
                GrantedAt = activeConsent?.GrantedAt,
                GrantedVia = activeConsent?.GrantedVia
            });
        }

        _logger.LogDebug("Retrieved {Count} consent statuses for Discord user {DiscordUserId}",
            consentStatuses.Count, discordUserId);

        return consentStatuses;
    }

    public async Task<IEnumerable<ConsentHistoryEntryDto>> GetConsentHistoryAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving consent history for Discord user {DiscordUserId}", discordUserId);

        // Get all user consents (including revoked)
        var userConsents = await _consentRepository.GetUserConsentsAsync(discordUserId, cancellationToken);

        var historyEntries = new List<ConsentHistoryEntryDto>();

        foreach (var consent in userConsents.OrderByDescending(c => c.GrantedAt))
        {
            // Add "Granted" entry
            historyEntries.Add(new ConsentHistoryEntryDto
            {
                Type = (int)consent.ConsentType,
                TypeDisplayName = GetConsentTypeDisplayName(consent.ConsentType),
                Action = "Granted",
                Timestamp = consent.GrantedAt,
                Source = consent.GrantedVia ?? "Unknown"
            });

            // Add "Revoked" entry if consent was revoked
            if (consent.RevokedAt.HasValue)
            {
                historyEntries.Add(new ConsentHistoryEntryDto
                {
                    Type = (int)consent.ConsentType,
                    TypeDisplayName = GetConsentTypeDisplayName(consent.ConsentType),
                    Action = "Revoked",
                    Timestamp = consent.RevokedAt.Value,
                    Source = consent.RevokedVia ?? "Unknown"
                });
            }
        }

        // Sort by timestamp descending (most recent first)
        var sortedHistory = historyEntries.OrderByDescending(h => h.Timestamp).ToList();

        _logger.LogDebug("Retrieved {Count} consent history entries for Discord user {DiscordUserId}",
            sortedHistory.Count, discordUserId);

        return sortedHistory;
    }

    public async Task<ConsentUpdateResult> GrantConsentAsync(
        ulong discordUserId,
        ConsentType type,
        CancellationToken cancellationToken = default)
    {
        return await GrantConsentAsync(discordUserId, type, WebUISource, cancellationToken);
    }

    /// <summary>
    /// Grants consent for a specific consent type with configurable source.
    /// </summary>
    private async Task<ConsentUpdateResult> GrantConsentAsync(
        ulong discordUserId,
        ConsentType type,
        string grantedVia,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Granting consent {ConsentType} for Discord user {DiscordUserId} via {Source}",
            type, discordUserId, grantedVia);

        // Validate consent type
        if (!Enum.IsDefined(typeof(ConsentType), type))
        {
            _logger.LogWarning("Invalid consent type {ConsentType} for Discord user {DiscordUserId}",
                type, discordUserId);
            return ConsentUpdateResult.Failure(
                ConsentUpdateResult.InvalidConsentType,
                "Invalid consent type.");
        }

        try
        {
            // Check if user already has active consent
            var activeConsent = await _consentRepository.GetActiveConsentAsync(
                discordUserId, type, cancellationToken);

            if (activeConsent != null)
            {
                _logger.LogDebug("Discord user {DiscordUserId} already has active consent {ConsentType}",
                    discordUserId, type);
                return ConsentUpdateResult.Failure(
                    ConsentUpdateResult.AlreadyGranted,
                    "Consent is already granted for this type.");
            }

            // Create new consent record
            var newConsent = new UserConsent
            {
                DiscordUserId = discordUserId,
                ConsentType = type,
                GrantedAt = DateTime.UtcNow,
                GrantedVia = grantedVia,
                RevokedAt = null,
                RevokedVia = null
            };

            await _consentRepository.AddAsync(newConsent, cancellationToken);

            // Invalidate cache
            InvalidateConsentCache(discordUserId, type);

            _logger.LogInformation(
                "Consent {ConsentType} granted for Discord user {DiscordUserId} via {Source}",
                type, discordUserId, grantedVia);

            return ConsentUpdateResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to grant consent {ConsentType} for Discord user {DiscordUserId}",
                type, discordUserId);
            return ConsentUpdateResult.Failure(
                ConsentUpdateResult.DatabaseError,
                "An error occurred while granting consent.");
        }
    }

    public async Task<ConsentUpdateResult> RevokeConsentAsync(
        ulong discordUserId,
        ConsentType type,
        CancellationToken cancellationToken = default)
    {
        return await RevokeConsentAsync(discordUserId, type, WebUISource, cancellationToken);
    }

    /// <summary>
    /// Revokes consent for a specific consent type with configurable source.
    /// </summary>
    private async Task<ConsentUpdateResult> RevokeConsentAsync(
        ulong discordUserId,
        ConsentType type,
        string revokedVia,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Revoking consent {ConsentType} for Discord user {DiscordUserId} via {Source}",
            type, discordUserId, revokedVia);

        // Validate consent type
        if (!Enum.IsDefined(typeof(ConsentType), type))
        {
            _logger.LogWarning("Invalid consent type {ConsentType} for Discord user {DiscordUserId}",
                type, discordUserId);
            return ConsentUpdateResult.Failure(
                ConsentUpdateResult.InvalidConsentType,
                "Invalid consent type.");
        }

        try
        {
            // Find active consent
            var activeConsent = await _consentRepository.GetActiveConsentAsync(
                discordUserId, type, cancellationToken);

            if (activeConsent == null)
            {
                _logger.LogDebug("No active consent {ConsentType} found for Discord user {DiscordUserId}",
                    type, discordUserId);
                return ConsentUpdateResult.Failure(
                    ConsentUpdateResult.NotGranted,
                    "No active consent found for this type.");
            }

            // Revoke the consent
            activeConsent.RevokedAt = DateTime.UtcNow;
            activeConsent.RevokedVia = revokedVia;

            await _consentRepository.UpdateAsync(activeConsent, cancellationToken);

            // Invalidate cache
            InvalidateConsentCache(discordUserId, type);

            _logger.LogInformation(
                "Consent {ConsentType} revoked for Discord user {DiscordUserId} via {Source}",
                type, discordUserId, revokedVia);

            return ConsentUpdateResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to revoke consent {ConsentType} for Discord user {DiscordUserId}",
                type, discordUserId);
            return ConsentUpdateResult.Failure(
                ConsentUpdateResult.DatabaseError,
                "An error occurred while revoking consent.");
        }
    }

    /// <summary>
    /// Gets user-friendly display name for a consent type.
    /// </summary>
    private static string GetConsentTypeDisplayName(ConsentType type)
    {
        return type switch
        {
            ConsentType.MessageLogging => "Message Logging",
            _ => type.ToString()
        };
    }

    /// <summary>
    /// Gets user-friendly description for a consent type.
    /// </summary>
    private static string GetConsentTypeDescription(ConsentType type)
    {
        return type switch
        {
            ConsentType.MessageLogging => "Allow the bot to log your messages and interactions for moderation, analytics, and troubleshooting purposes. This includes message content, timestamps, and metadata.",
            _ => "No description available."
        };
    }

    public async Task<bool> HasConsentAsync(
        ulong discordUserId,
        ConsentType consentType,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(discordUserId, consentType);

        // Try to get from cache
        if (_cache.TryGetValue<bool>(cacheKey, out var cachedValue))
        {
            _logger.LogDebug(
                "Consent check cache hit for user {DiscordUserId} and type {ConsentType}. HasConsent={HasConsent}",
                discordUserId, consentType, cachedValue);
            return cachedValue;
        }

        _logger.LogDebug(
            "Consent check cache miss for user {DiscordUserId} and type {ConsentType}, querying repository",
            discordUserId, consentType);

        // Query repository
        var hasConsent = await _consentRepository.HasActiveConsentAsync(
            discordUserId, consentType, cancellationToken);

        // Cache the result
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(_cachingOptions.ConsentCacheDurationMinutes));

        _cache.Set(cacheKey, hasConsent, cacheOptions);

        _logger.LogDebug(
            "Cached consent check result for user {DiscordUserId} and type {ConsentType}. HasConsent={HasConsent}",
            discordUserId, consentType, hasConsent);

        return hasConsent;
    }

    public async Task<IEnumerable<ConsentType>> GetActiveConsentsAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving active consent types for Discord user {DiscordUserId}", discordUserId);

        // Get all user consents
        var userConsents = await _consentRepository.GetUserConsentsAsync(discordUserId, cancellationToken);

        // Filter to active consents and get distinct consent types
        var activeConsentTypes = userConsents
            .Where(c => c.IsActive)
            .Select(c => c.ConsentType)
            .Distinct()
            .ToList();

        _logger.LogDebug(
            "Retrieved {Count} active consent types for Discord user {DiscordUserId}",
            activeConsentTypes.Count, discordUserId);

        return activeConsentTypes;
    }

    public async Task<IDictionary<ulong, bool>> HasConsentBatchAsync(
        IEnumerable<ulong> discordUserIds,
        ConsentType consentType,
        CancellationToken cancellationToken = default)
    {
        var userIdsList = discordUserIds.ToList();
        var result = new Dictionary<ulong, bool>();

        _logger.LogDebug(
            "Batch checking consent {ConsentType} for {Count} users",
            consentType, userIdsList.Count);

        // Check cache first
        var uncachedUserIds = new List<ulong>();
        foreach (var userId in userIdsList)
        {
            var cacheKey = GetCacheKey(userId, consentType);
            if (_cache.TryGetValue<bool>(cacheKey, out var cachedValue))
            {
                result[userId] = cachedValue;
            }
            else
            {
                uncachedUserIds.Add(userId);
            }
        }

        _logger.LogDebug(
            "Batch consent check: {CachedCount} found in cache, {UncachedCount} require database query",
            result.Count, uncachedUserIds.Count);

        // Query database for uncached users
        if (uncachedUserIds.Any())
        {
            var usersWithConsent = await _consentRepository.GetUsersWithActiveConsentAsync(
                uncachedUserIds, consentType, cancellationToken);

            var usersWithConsentSet = new HashSet<ulong>(usersWithConsent);

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(_cachingOptions.ConsentCacheDurationMinutes));

            // Add results to dictionary and cache
            foreach (var userId in uncachedUserIds)
            {
                var hasConsent = usersWithConsentSet.Contains(userId);
                result[userId] = hasConsent;

                // Cache the result
                var cacheKey = GetCacheKey(userId, consentType);
                _cache.Set(cacheKey, hasConsent, cacheOptions);
            }
        }

        _logger.LogDebug(
            "Batch consent check completed for {Count} users. Users with consent: {WithConsentCount}",
            userIdsList.Count, result.Count(kvp => kvp.Value));

        return result;
    }

    /// <summary>
    /// Gets the cache key for a user consent check.
    /// </summary>
    private static string GetCacheKey(ulong discordUserId, ConsentType consentType)
    {
        return $"consent:{discordUserId}:{consentType}";
    }

    /// <summary>
    /// Invalidates cached consent data for a user and consent type.
    /// </summary>
    private void InvalidateConsentCache(ulong discordUserId, ConsentType consentType)
    {
        var cacheKey = GetCacheKey(discordUserId, consentType);
        _cache.Remove(cacheKey);

        _logger.LogDebug(
            "Invalidated consent cache for user {DiscordUserId} and type {ConsentType}",
            discordUserId, consentType);
    }
}
