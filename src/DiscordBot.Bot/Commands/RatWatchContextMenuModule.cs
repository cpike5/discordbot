using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Components;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// The "Rat Watch" message context menu and the modal it opens.
/// </summary>
/// <remarks>
/// Context menus cannot be declared inside a <c>[Group]</c>, so this lives apart from the
/// grouped <see cref="RatWatchModule"/> (<c>/rat ...</c>). It has no configuration row of its
/// own: <c>SlashCommandRegistrationService.ResolveCompanionParentModule</c> maps it to
/// <see cref="RatWatchModule"/>, so the Commands tab's Rat Watch toggle governs both.
/// The modal ID (<c>ratwatch:create:*:*</c>) is matched here, outside any group, so it is not
/// prefixed with a group name.
/// </remarks>
[RequireGuildActive]
[RequireRatWatchEnabled]
public class RatWatchContextMenuModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IRatWatchService _ratWatchService;
    private readonly IRatWatchStatusService _ratWatchStatusService;
    private readonly IDashboardUpdateService _dashboardUpdateService;
    private readonly ILogger<RatWatchContextMenuModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RatWatchContextMenuModule"/> class.
    /// </summary>
    public RatWatchContextMenuModule(
        IRatWatchService ratWatchService,
        IRatWatchStatusService ratWatchStatusService,
        IDashboardUpdateService dashboardUpdateService,
        ILogger<RatWatchContextMenuModule> logger)
    {
        _ratWatchService = ratWatchService;
        _ratWatchStatusService = ratWatchStatusService;
        _dashboardUpdateService = dashboardUpdateService;
        _logger = logger;
    }

    /// <summary>
    /// Context menu command to create a Rat Watch on a message.
    /// Opens a modal for time and optional message.
    /// </summary>
    [MessageCommand("Rat Watch")]
    public async Task RatWatchAsync(IMessage message)
    {
        _logger.LogInformation(
            "Rat Watch context menu used by {Username} (ID: {UserId}) on message {MessageId} from user {AccusedId} in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            message.Id,
            message.Author.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        // Get the message author as the accused
        var accusedUserId = message.Author.Id;

        // Prevent targeting bots (including this bot), unless user is an admin (for testing)
        if (message.Author.IsBot)
        {
            var guildUser = Context.User as SocketGuildUser;
            var isAdmin = guildUser?.GuildPermissions.Administrator ?? false;

            if (!isAdmin)
            {
                _logger.LogDebug("Rat Watch attempted on bot {BotId} by non-admin user {UserId}", accusedUserId, Context.User.Id);

                await RespondAsync(embed: EmbedHelper.Error("Cannot Watch Bots", "Bots cannot be targeted with Rat Watch. Please select a message from a human user."), ephemeral: true);
                return;
            }

            _logger.LogInformation("Admin {UserId} targeting bot {BotId} for Rat Watch (testing mode)", Context.User.Id, accusedUserId);
        }

        // Build and show modal with custom ID: ratwatch:create:{messageId}:{accusedUserId}
        var modalId = $"ratwatch:create:{message.Id}:{accusedUserId}";

        var modal = new ModalBuilder()
            .WithTitle("Rat Watch")
            .WithCustomId(modalId)
            .AddTextInput("When to check in", "time", TextInputStyle.Short, "10m, 2h, 10pm", maxLength: 20, required: true)
            .AddTextInput("Custom message (optional)", "message", TextInputStyle.Paragraph, maxLength: 200, required: false)
            .Build();

        await RespondWithModalAsync(modal);

        _logger.LogDebug("Rat Watch modal shown to user {UserId} for accused {AccusedId}", Context.User.Id, accusedUserId);
    }

    /// <summary>
    /// Handles the modal submission for creating a Rat Watch.
    /// </summary>
    [ModalInteraction("ratwatch:create:*:*")]
    public async Task HandleCreateModalAsync(string messageIdStr, string accusedUserIdStr, RatWatchModal modal)
    {
        _logger.LogDebug(
            "Rat Watch modal submitted by {Username} (ID: {UserId}) for message {MessageId}, accused {AccusedId}",
            Context.User.Username,
            Context.User.Id,
            messageIdStr,
            accusedUserIdStr);

        // Parse messageId and accusedUserId from wildcard parts
        if (!ulong.TryParse(messageIdStr, out var messageId))
        {
            await RespondAsync("Invalid message ID.", ephemeral: true);
            _logger.LogWarning("Invalid message ID format: {MessageId}", messageIdStr);
            return;
        }

        if (!ulong.TryParse(accusedUserIdStr, out var accusedUserId))
        {
            await RespondAsync("Invalid user ID.", ephemeral: true);
            _logger.LogWarning("Invalid user ID format: {UserId}", accusedUserIdStr);
            return;
        }

        // Get guild settings for timezone
        var settings = await _ratWatchService.GetGuildSettingsAsync(Context.Guild.Id);

        // Parse time using service.ParseScheduleTime(modal.Time, settings.Timezone)
        var scheduledTime = _ratWatchService.ParseScheduleTime(modal.Time, settings.Timezone);

        if (!scheduledTime.HasValue)
        {
            await RespondAsync(embed: EmbedHelper.Error("Invalid Time Format", "Could not parse the time you provided. Use formats like:\n• `10m` - 10 minutes from now\n• `2h` - 2 hours from now\n• `1h30m` - 1 hour 30 minutes\n• `10pm` - 10 PM today\n• `22:00` - 10 PM today (24-hour)"), ephemeral: true);
            _logger.LogDebug("Failed to parse time input: {TimeInput}", modal.Time);
            return;
        }

        // Validate time is in the future
        if (scheduledTime.Value <= DateTime.UtcNow)
        {
            await RespondAsync(embed: EmbedHelper.Error("Invalid Time", "The scheduled time must be in the future."), ephemeral: true);
            _logger.LogDebug("Scheduled time {ScheduledTime} is in the past", scheduledTime.Value);
            return;
        }

        // Validate time is within MaxAdvanceHours
        var maxAdvanceTime = DateTime.UtcNow.AddHours(settings.MaxAdvanceHours);
        if (scheduledTime.Value > maxAdvanceTime)
        {
            await RespondAsync(embed: EmbedHelper.Error("Time Too Far Ahead", $"Rat Watches can only be scheduled up to {settings.MaxAdvanceHours} hours in advance."), ephemeral: true);
            _logger.LogDebug(
                "Scheduled time {ScheduledTime} exceeds max advance hours {MaxAdvanceHours}",
                scheduledTime.Value,
                settings.MaxAdvanceHours);
            return;
        }

        // Create the watch using service.CreateWatchAsync
        var createDto = new RatWatchCreateDto
        {
            GuildId = Context.Guild.Id,
            ChannelId = Context.Channel.Id,
            AccusedUserId = accusedUserId,
            InitiatorUserId = Context.User.Id,
            OriginalMessageId = messageId,
            CustomMessage = string.IsNullOrWhiteSpace(modal.Message) ? null : modal.Message.Trim(),
            ScheduledAt = scheduledTime.Value
        };

        try
        {
            var watch = await _ratWatchService.CreateWatchAsync(createDto);

            _logger.LogInformation(
                "Rat Watch {WatchId} created by {InitiatorId} for {AccusedId}, scheduled at {ScheduledTime}",
                watch.Id,
                Context.User.Id,
                accusedUserId,
                scheduledTime.Value);

            // Post confirmation message with "I'm Here!" check-in button
            var messageLink = $"https://discord.com/channels/{Context.Guild.Id}/{Context.Channel.Id}/{messageId}";
            var scheduledTimestamp = new DateTimeOffset(scheduledTime.Value).ToUnixTimeSeconds();

            var embedDescription = $"⏰ Check-in at <t:{scheduledTimestamp}:F> (<t:{scheduledTimestamp}:R>)";
            if (!string.IsNullOrWhiteSpace(watch.CustomMessage))
            {
                embedDescription += $"\n\n> {watch.CustomMessage}";
            }
            embedDescription += $"\n\n[Jump to message]({messageLink})";

            var confirmEmbed = new EmbedBuilder()
                .WithTitle("🐀 Rat Watch Created")
                .WithDescription(embedDescription)
                .WithColor(Color.Orange)
                .AddField("Accused", $"<@{accusedUserId}>", inline: true)
                .AddField("Initiated by", $"<@{Context.User.Id}>", inline: true)
                .WithCurrentTimestamp()
                .Build();

            // Button ID: ComponentIdBuilder.Build("ratwatch", "checkin", accusedUserId, watchId.ToString())
            var checkInButton = ComponentIdBuilder.Build("ratwatch", "checkin", accusedUserId, watch.Id.ToString());

            var components = new ComponentBuilder()
                .WithButton("I'm Here! ✓", checkInButton, ButtonStyle.Success)
                .Build();

            await RespondAsync(embed: confirmEmbed, components: components);

            // Notify that a new watch was created - may need to update bot status
            _ratWatchStatusService.RequestStatusUpdate();

            // Broadcast Rat Watch created event to dashboard
            await _dashboardUpdateService.BroadcastRatWatchActivityAsync(
                Context.Guild.Id,
                Context.Guild.Name,
                "RatWatchCreated",
                watch.AccusedUsername);

            _logger.LogDebug("Rat Watch confirmation message sent with check-in button");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Rat Watch for user {AccusedId}", accusedUserId);

            await RespondAsync(embed: EmbedHelper.Error("Error", $"Failed to create Rat Watch: {ex.Message}"), ephemeral: true);
        }
    }
}

/// <summary>
/// Modal for Rat Watch creation.
/// </summary>
public class RatWatchModal : IModal
{
    public string Title => "Rat Watch";

    [InputLabel("When to check in")]
    [ModalTextInput("time", TextInputStyle.Short, "10m, 2h, 10pm", maxLength: 20)]
    public string Time { get; set; } = string.Empty;

    [InputLabel("Custom message (optional)")]
    [ModalTextInput("message", TextInputStyle.Paragraph, maxLength: 200)]
    [RequiredInput(false)]
    public string? Message { get; set; }
}
