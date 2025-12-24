using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for managing user consent operations for privacy compliance.
/// </summary>
public class ConsentService : IConsentService
{
    private readonly IUserConsentRepository _consentRepository;
    private readonly ILogger<ConsentService> _logger;

    private const string WebUISource = "WebUI";

    public ConsentService(
        IUserConsentRepository consentRepository,
        ILogger<ConsentService> logger)
    {
        _consentRepository = consentRepository;
        _logger = logger;
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
        _logger.LogDebug("Granting consent {ConsentType} for Discord user {DiscordUserId} via {Source}",
            type, discordUserId, WebUISource);

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
                GrantedVia = WebUISource,
                RevokedAt = null,
                RevokedVia = null
            };

            await _consentRepository.AddAsync(newConsent, cancellationToken);

            _logger.LogInformation(
                "Consent {ConsentType} granted for Discord user {DiscordUserId} via {Source}",
                type, discordUserId, WebUISource);

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
        _logger.LogDebug("Revoking consent {ConsentType} for Discord user {DiscordUserId} via {Source}",
            type, discordUserId, WebUISource);

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
            activeConsent.RevokedVia = WebUISource;

            await _consentRepository.UpdateAsync(activeConsent, cancellationToken);

            _logger.LogInformation(
                "Consent {ConsentType} revoked for Discord user {DiscordUserId} via {Source}",
                type, discordUserId, WebUISource);

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
}
