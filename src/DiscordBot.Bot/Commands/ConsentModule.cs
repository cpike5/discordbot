using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Consent management commands for user data privacy and GDPR compliance.
/// Allows users to grant, revoke, and view their consent status for data processing.
/// </summary>
[Group("consent", "Manage your data consent preferences")]
[RequireGuildActive]
[RateLimit(5, 60)]
public class ConsentModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IUserConsentRepository _consentRepository;
    private readonly ILogger<ConsentModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentModule"/> class.
    /// </summary>
    public ConsentModule(
        IUserConsentRepository consentRepository,
        ILogger<ConsentModule> logger)
    {
        _consentRepository = consentRepository;
        _logger = logger;
    }

    /// <summary>
    /// Grants consent for data collection. Type is optional and defaults to MessageLogging.
    /// </summary>
    /// <param name="type">The type of consent to grant (defaults to MessageLogging).</param>
    [SlashCommand("grant", "Grant consent for data collection")]
    public async Task GrantAsync(
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
                               $"You can revoke consent at any time with `/consent revoke {type}`.")
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

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription("An error occurred while granting consent. Please try again later.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Revokes consent for data collection. Type is optional and defaults to MessageLogging.
    /// </summary>
    /// <param name="type">The type of consent to revoke (defaults to MessageLogging).</param>
    [SlashCommand("revoke", "Revoke consent for data collection")]
    public async Task RevokeAsync(
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
                                   "Use `/consent grant` to grant consent.")
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
                               "Use `/privacy` for more information about data handling.")
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

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription("An error occurred while revoking consent. Please try again later.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Shows the user's current consent status for all consent types.
    /// </summary>
    [SlashCommand("status", "View your current consent status")]
    public async Task StatusAsync()
    {
        var userId = Context.User.Id;

        _logger.LogDebug(
            "Status command executed by {Username} (ID: {UserId})",
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
                    "Use `/consent grant` to opt in to message logging.");
            }
            else
            {
                embedBuilder.WithDescription(
                    "Use `/consent grant` or `/consent revoke` to manage your preferences.");
            }

            await RespondAsync(embed: embedBuilder.Build(), ephemeral: true);

            _logger.LogDebug("Status command completed successfully for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve consent status for user {UserId}",
                userId);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription("An error occurred while retrieving your consent status. Please try again later.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
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
                "You can now mention the bot to ask questions. Your questions and responses will be processed by Claude AI and logged for quality purposes.",
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
                "You will no longer be able to use the AI assistant feature by mentioning the bot.",
            _ => "Your consent has been revoked."
        };
    }
}
