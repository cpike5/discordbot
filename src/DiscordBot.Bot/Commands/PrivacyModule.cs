using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Privacy management commands for user data control and GDPR compliance.
/// Allows users to grant, revoke, and view their consent for data processing, and to
/// preview, export, and delete their personal data.
/// </summary>
/// <remarks>
/// Rate limits are per command: the consent commands allow 5 uses a minute, the data
/// commands 2 a minute because they are expensive or destructive.
/// </remarks>
[Group("privacy", "Manage your personal data and consent")]
[RequireGuildActive]
public class PrivacyModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IUserConsentRepository _consentRepository;
    private readonly IUserPurgeService _purgeService;
    private readonly IUserDataExportService _exportService;
    private readonly ILogger<PrivacyModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivacyModule"/> class.
    /// </summary>
    public PrivacyModule(
        IUserConsentRepository consentRepository,
        IUserPurgeService purgeService,
        IUserDataExportService exportService,
        ILogger<PrivacyModule> logger)
    {
        _consentRepository = consentRepository;
        _purgeService = purgeService;
        _exportService = exportService;
        _logger = logger;
    }

    /// <summary>
    /// Grants consent for data collection. Type is optional and defaults to MessageLogging.
    /// </summary>
    /// <param name="type">The type of consent to grant (defaults to MessageLogging).</param>
    [SlashCommand("consent-grant", "Grant consent for data collection")]
    [RateLimit(5, 60)]
    public async Task ConsentGrantAsync(
        [Summary("type", "Type of consent to grant (defaults to Message Logging)")]
        ConsentType type = ConsentType.MessageLogging)
    {
        var userId = Context.User.Id;

        _logger.LogDebug(
            "Grant consent command executed by {Username} (ID: {UserId}) for consent type {ConsentType}",
            Context.User.Username,
            userId,
            type);

        try
        {
            // Check if user already has active consent for this type
            var existingConsent = await _consentRepository.GetActiveConsentAsync(userId, type);

            if (existingConsent != null)
            {
                _logger.LogDebug(
                    "User {UserId} already has active consent for {ConsentType}, granted at {GrantedAt}",
                    userId,
                    type,
                    existingConsent.GrantedAt);

                var alreadyGrantedEmbed = new EmbedBuilder()
                    .WithTitle("ℹ️ Consent Already Active")
                    .WithDescription($"You already have active consent for **{GetConsentTypeName(type)}**.\n\n" +
                                   $"Originally granted: <t:{new DateTimeOffset(existingConsent.GrantedAt).ToUnixTimeSeconds()}:F>")
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: alreadyGrantedEmbed, ephemeral: true);
                return;
            }

            // Create new consent record
            var newConsent = new UserConsent
            {
                DiscordUserId = userId,
                ConsentType = type,
                GrantedAt = DateTime.UtcNow,
                GrantedVia = "SlashCommand",
                RevokedAt = null,
                RevokedVia = null
            };

            await _consentRepository.AddAsync(newConsent);

            _logger.LogInformation(
                "User {UserId} granted consent for {ConsentType}",
                userId,
                type);

            var successEmbed = new EmbedBuilder()
                .WithTitle("✅ Consent Granted")
                .WithDescription($"You have opted in to **{GetConsentTypeName(type).ToLower()}**. " +
                               $"{GetConsentGrantDescription(type)}\n\n" +
                               $"You can revoke consent at any time with `/privacy consent-revoke {type}`.")
                .WithColor(Color.Green)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: successEmbed, ephemeral: true);

            _logger.LogDebug("Grant consent command completed successfully for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to grant consent for user {UserId} and consent type {ConsentType}",
                userId,
                type);

            await RespondAsync(embed: EmbedHelper.Error("Error", "An error occurred while granting consent. Please try again later."), ephemeral: true);
        }
    }

    /// <summary>
    /// Revokes consent for data collection. Type is optional and defaults to MessageLogging.
    /// </summary>
    /// <param name="type">The type of consent to revoke (defaults to MessageLogging).</param>
    [SlashCommand("consent-revoke", "Revoke consent for data collection")]
    [RateLimit(5, 60)]
    public async Task ConsentRevokeAsync(
        [Summary("type", "Type of consent to revoke (defaults to Message Logging)")]
        ConsentType type = ConsentType.MessageLogging)
    {
        var userId = Context.User.Id;

        _logger.LogDebug(
            "Revoke consent command executed by {Username} (ID: {UserId}) for consent type {ConsentType}",
            Context.User.Username,
            userId,
            type);

        try
        {
            // Find active consent to revoke
            var activeConsent = await _consentRepository.GetActiveConsentAsync(userId, type);

            if (activeConsent == null)
            {
                _logger.LogDebug(
                    "User {UserId} attempted to revoke consent for {ConsentType} but no active consent exists",
                    userId,
                    type);

                var noConsentEmbed = new EmbedBuilder()
                    .WithTitle("ℹ️ No Active Consent")
                    .WithDescription($"You don't have active consent for **{GetConsentTypeName(type)}** to revoke.\n\n" +
                                   "Use `/privacy consent-grant` to grant consent.")
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: noConsentEmbed, ephemeral: true);
                return;
            }

            // Revoke the consent
            activeConsent.RevokedAt = DateTime.UtcNow;
            activeConsent.RevokedVia = "SlashCommand";

            await _consentRepository.UpdateAsync(activeConsent);

            _logger.LogInformation(
                "User {UserId} revoked consent for {ConsentType}",
                userId,
                type);

            var successEmbed = new EmbedBuilder()
                .WithTitle("✅ Consent Revoked")
                .WithDescription($"You have opted out of **{GetConsentTypeName(type).ToLower()}**. " +
                               $"{GetConsentRevokeDescription(type)}\n\n" +
                               "**Note:** Previously logged data is retained per our data retention policy.\n" +
                               "Use `/privacy export-data` to download it or `/privacy delete-data` to remove it.")
                .WithColor(Color.Green)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: successEmbed, ephemeral: true);

            _logger.LogDebug("Revoke consent command completed successfully for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to revoke consent for user {UserId} and consent type {ConsentType}",
                userId,
                type);

            await RespondAsync(embed: EmbedHelper.Error("Error", "An error occurred while revoking consent. Please try again later."), ephemeral: true);
        }
    }

    /// <summary>
    /// Shows the user's current consent status for all consent types.
    /// </summary>
    [SlashCommand("status", "View your current consent status")]
    [RateLimit(5, 60)]
    public async Task StatusAsync()
    {
        var userId = Context.User.Id;

        _logger.LogDebug(
            "Consent status command executed by {Username} (ID: {UserId})",
            Context.User.Username,
            userId);

        try
        {
            // Get all consents for the user
            var userConsents = await _consentRepository.GetUserConsentsAsync(userId);
            var consentsList = userConsents.ToList();

            _logger.LogDebug(
                "Retrieved {ConsentCount} consent records for user {UserId}",
                consentsList.Count,
                userId);

            // Build status embed
            var embedBuilder = new EmbedBuilder()
                .WithTitle("📋 Your Consent Status")
                .WithColor(Color.Blue)
                .WithCurrentTimestamp();

            // Check status for each known consent type
            var hasAnyConsent = false;
            foreach (ConsentType consentType in Enum.GetValues(typeof(ConsentType)))
            {
                var activeConsent = consentsList.FirstOrDefault(c => c.ConsentType == consentType && c.IsActive);
                var consentTypeName = GetConsentTypeName(consentType);

                if (activeConsent != null)
                {
                    var grantedTimestamp = new DateTimeOffset(activeConsent.GrantedAt).ToUnixTimeSeconds();
                    embedBuilder.AddField(
                        consentTypeName,
                        $"✅ Granted (since <t:{grantedTimestamp}:D>)",
                        inline: false);
                    hasAnyConsent = true;
                }
                else
                {
                    embedBuilder.AddField(
                        consentTypeName,
                        "❌ Not granted",
                        inline: false);
                }
            }

            if (!hasAnyConsent)
            {
                embedBuilder.WithDescription(
                    "You have not granted consent for any data processing activities.\n\n" +
                    "Use `/privacy consent-grant` to opt in to message logging.");
            }
            else
            {
                embedBuilder.WithDescription(
                    "Use `/privacy consent-grant` or `/privacy consent-revoke` to manage your preferences.");
            }

            await RespondAsync(embed: embedBuilder.Build(), ephemeral: true);

            _logger.LogDebug("Consent status command completed successfully for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve consent status for user {UserId}",
                userId);

            await RespondAsync(embed: EmbedHelper.Error("Error", "An error occurred while retrieving your consent status. Please try again later."), ephemeral: true);
        }
    }

    /// <summary>
    /// Shows a preview of data that would be deleted.
    /// </summary>
    [RateLimit(2, 60)] // Lower rate limit for expensive and destructive operations
    [SlashCommand("preview-delete", "Preview what data would be deleted")]
    public async Task PreviewDeleteAsync()
    {
        var userId = Context.User.Id;

        _logger.LogDebug(
            "Preview delete command executed by {Username} (ID: {UserId})",
            Context.User.Username, userId);

        try
        {
            // Check if user can be purged
            var (canPurge, reason) = await _purgeService.CanPurgeUserAsync(userId);
            if (!canPurge)
            {
                var blockedEmbed = new EmbedBuilder()
                    .WithTitle("⚠️ Cannot Delete Data")
                    .WithDescription(reason ?? "Your data cannot be deleted at this time.")
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: blockedEmbed, ephemeral: true);
                return;
            }

            // Get preview
            var preview = await _purgeService.PreviewPurgeAsync(userId);

            var totalRecords = preview.DeletedCounts.Values.Sum();

            var previewEmbed = new EmbedBuilder()
                .WithTitle("📋 Data Deletion Preview")
                .WithDescription($"The following data would be **permanently deleted** if you proceed:\n\n" +
                               $"**Total Records:** {totalRecords}")
                .WithColor(Color.Blue)
                .WithCurrentTimestamp();

            // Add fields for each data category with records
            foreach (var (category, count) in preview.DeletedCounts.Where(kvp => kvp.Value > 0))
            {
                previewEmbed.AddField(GetFriendlyName(category), $"{count} records", inline: true);
            }

            if (totalRecords == 0)
            {
                previewEmbed.WithDescription("You have no data stored in our system.");
            }
            else
            {
                previewEmbed.AddField("⚠️ Warning",
                    "Use `/privacy delete-data` to permanently delete this data. This action **cannot be undone**.",
                    inline: false);
            }

            await RespondAsync(embed: previewEmbed.Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preview delete for user {UserId}", userId);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription("An error occurred while previewing your data. Please try again later.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Exports all user data as a downloadable ZIP file.
    /// </summary>
    [RateLimit(2, 60)] // Lower rate limit for expensive and destructive operations
    [SlashCommand("export-data", "Export all your data as a downloadable file")]
    public async Task ExportDataAsync()
    {
        var userId = Context.User.Id;

        _logger.LogDebug(
            "Export data command executed by {Username} (ID: {UserId})",
            Context.User.Username, userId);

        try
        {
            // Defer since export may take time
            await DeferAsync(ephemeral: true);

            _logger.LogInformation("User {UserId} initiated data export", userId);

            // Execute export
            var result = await _exportService.ExportUserDataAsync(userId);

            if (result.Success)
            {
                var totalRecords = result.ExportedCounts.Values.Sum();
                var expiresAtLocal = result.ExpiresAt!.Value;

                var successEmbed = new EmbedBuilder()
                    .WithTitle("📦 Data Export Ready")
                    .WithDescription($"Your data has been exported and is ready for download.\n\n" +
                                   $"**Records Exported:** {totalRecords}\n" +
                                   $"**Export ID:** `{result.ExportId.ToString()![..8]}...`\n" +
                                   $"**Expires:** <t:{new DateTimeOffset(expiresAtLocal).ToUnixTimeSeconds()}:R>\n\n" +
                                   $"[Download Your Data]({result.DownloadUrl})\n\n" +
                                   "⚠️ This link will expire in 7 days. Download your data before then.")
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp()
                    .Build();

                await FollowupAsync(embed: successEmbed, ephemeral: true);

                _logger.LogInformation(
                    "User {UserId} data export completed. {RecordCount} records exported",
                    userId, totalRecords);
            }
            else
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Export Failed")
                    .WithDescription($"An error occurred while exporting your data.\n\n" +
                                   $"**Error:** {result.ErrorMessage ?? "Unknown error"}\n\n" +
                                   "Please try again later or contact support.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await FollowupAsync(embed: errorEmbed, ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export data for user {UserId}", userId);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription("An error occurred while exporting your data. Please try again later.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            try
            {
                await FollowupAsync(embed: errorEmbed, ephemeral: true);
            }
            catch
            {
                await RespondAsync(embed: errorEmbed, ephemeral: true);
            }
        }
    }

    /// <summary>
    /// Deletes all user data from the system (GDPR right to be forgotten).
    /// Requires explicit confirmation.
    /// </summary>
    [RateLimit(2, 60)] // Lower rate limit for expensive and destructive operations
    [SlashCommand("delete-data", "Permanently delete all your data from the system")]
    public async Task DeleteDataAsync(
        [Summary("confirm", "Type 'DELETE' to confirm permanent data deletion")]
        string? confirm = null)
    {
        var userId = Context.User.Id;

        _logger.LogDebug(
            "Delete data command executed by {Username} (ID: {UserId})",
            Context.User.Username, userId);

        // Require explicit confirmation
        if (confirm != "DELETE")
        {
            var confirmEmbed = new EmbedBuilder()
                .WithTitle("⚠️ Confirmation Required")
                .WithDescription("This will **permanently delete** all your data from our system.\n\n" +
                               "**This action cannot be undone.**\n\n" +
                               "To proceed, run this command again with:\n" +
                               "`/privacy delete-data confirm:DELETE`\n\n" +
                               "Use `/privacy preview-delete` to see what would be deleted first.")
                .WithColor(Color.Orange)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: confirmEmbed, ephemeral: true);
            return;
        }

        try
        {
            // Check if user can be purged
            var (canPurge, reason) = await _purgeService.CanPurgeUserAsync(userId);
            if (!canPurge)
            {
                var blockedEmbed = new EmbedBuilder()
                    .WithTitle("⚠️ Cannot Delete Data")
                    .WithDescription(reason ?? "Your data cannot be deleted at this time.")
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: blockedEmbed, ephemeral: true);
                return;
            }

            // Defer response since this may take a while
            await DeferAsync(ephemeral: true);

            _logger.LogInformation(
                "User {UserId} initiated data deletion",
                userId);

            // Execute purge
            var result = await _purgeService.PurgeUserDataAsync(
                userId,
                PurgeInitiator.User,
                userId.ToString());

            if (result.Success)
            {
                var totalDeleted = result.DeletedCounts.Values.Sum();

                var successEmbed = new EmbedBuilder()
                    .WithTitle("✅ Data Deleted")
                    .WithDescription($"Your data has been permanently deleted from our system.\n\n" +
                                   $"**Records Deleted:** {totalDeleted}\n" +
                                   $"**Reference ID:** `{result.AuditLogCorrelationId[..8]}...`\n\n" +
                                   "Thank you for using our service.")
                    .WithColor(Color.Green)
                    .WithCurrentTimestamp()
                    .Build();

                await FollowupAsync(embed: successEmbed, ephemeral: true);

                _logger.LogInformation(
                    "User {UserId} data deletion completed. {RecordCount} records deleted",
                    userId, totalDeleted);
            }
            else
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Deletion Failed")
                    .WithDescription($"An error occurred while deleting your data.\n\n" +
                                   $"**Error:** {result.ErrorMessage ?? "Unknown error"}\n\n" +
                                   "Please try again later or contact support.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await FollowupAsync(embed: errorEmbed, ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete data for user {UserId}", userId);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription("An error occurred while deleting your data. Please try again later.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            // Try to respond or follow up depending on state
            try
            {
                await FollowupAsync(embed: errorEmbed, ephemeral: true);
            }
            catch
            {
                await RespondAsync(embed: errorEmbed, ephemeral: true);
            }
        }
    }

    /// <summary>
    /// Gets a user-friendly name for a data category.
    /// </summary>
    private static string GetFriendlyName(string category)
    {
        return category switch
        {
            "MessageLogs" => "Message Logs",
            "CommandLogs" => "Command Logs",
            "RatVotes" => "Rat Watch Votes",
            "RatRecords_Anonymized" => "Rat Watch Records (anonymized)",
            "RatWatches_Anonymized" => "Rat Watches (anonymized)",
            "Reminders" => "Reminders",
            "ModNotes" => "Moderation Notes",
            "UserModTags" => "Moderation Tags",
            "Watchlists" => "Watchlist Entries",
            "SoundPlayLogs" => "Soundboard History",
            "TtsMessages" => "TTS Messages",
            "GuildMembers" => "Guild Memberships",
            "UserConsents" => "Consent Records",
            "Users" => "User Profile",
            "ApplicationUser" => "Admin Account",
            "UserGuildAccess" => "Guild Access Permissions",
            "UserDiscordGuilds" => "Discord Guild Links",
            "DiscordOAuthTokens" => "OAuth Tokens",
            _ => category
        };
    }

    /// <summary>
    /// Gets a user-friendly name for a consent type.
    /// </summary>
    /// <param name="type">The consent type.</param>
    /// <returns>A user-friendly display name.</returns>
    private static string GetConsentTypeName(ConsentType type)
    {
        return type switch
        {
            ConsentType.MessageLogging => "Message Logging",
            ConsentType.AssistantUsage => "AI Assistant Usage",
            _ => type.ToString()
        };
    }

    /// <summary>
    /// Gets a user-friendly description for a consent type when granting.
    /// </summary>
    /// <param name="type">The consent type.</param>
    /// <returns>A user-friendly description.</returns>
    private static string GetConsentGrantDescription(ConsentType type)
    {
        return type switch
        {
            ConsentType.MessageLogging =>
                "Your messages in DMs with this bot and in mutual servers may now be logged.",
            ConsentType.AssistantUsage =>
                "You can now mention the bot to ask questions. When you @-mention the bot, the text of your message is sent to OpenRouter and on to the model provider serving the configured model. " +
                "Your question and the bot's response are also stored in this bot's own database for quality, troubleshooting, and usage metrics.",
            _ => "Your consent has been recorded."
        };
    }

    /// <summary>
    /// Gets a user-friendly description for a consent type when revoking.
    /// </summary>
    /// <param name="type">The consent type.</param>
    /// <returns>A user-friendly description.</returns>
    private static string GetConsentRevokeDescription(ConsentType type)
    {
        return type switch
        {
            ConsentType.MessageLogging =>
                "Your messages will no longer be logged.",
            ConsentType.AssistantUsage =>
                "You will no longer be able to use the AI assistant feature by mentioning the bot, and no further messages of yours will be sent to OpenRouter.",
            _ => "Your consent has been revoked."
        };
    }
}
