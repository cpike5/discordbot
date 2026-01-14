using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Privacy management commands for user data control and GDPR compliance.
/// Allows users to view, export, and delete their personal data.
/// </summary>
[Group("privacy", "Manage your personal data")]
[RequireGuildActive]
[RateLimit(2, 60)] // Lower rate limit for destructive operations
public class PrivacyModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IUserPurgeService _purgeService;
    private readonly IUserDataExportService _exportService;
    private readonly ILogger<PrivacyModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivacyModule"/> class.
    /// </summary>
    public PrivacyModule(
        IUserPurgeService purgeService,
        IUserDataExportService exportService,
        ILogger<PrivacyModule> logger)
    {
        _purgeService = purgeService;
        _exportService = exportService;
        _logger = logger;
    }

    /// <summary>
    /// Shows a preview of data that would be deleted.
    /// </summary>
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
}
