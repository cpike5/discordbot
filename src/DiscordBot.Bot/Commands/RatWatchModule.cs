using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Components;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Slash command and context menu module for Rat Watch accountability feature.
/// </summary>
[RequireGuildActive]
[RequireRatWatchEnabled]
public class RatWatchModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IRatWatchService _ratWatchService;
    private readonly IRatWatchStatusService _ratWatchStatusService;
    private readonly IDashboardUpdateService _dashboardUpdateService;
    private readonly DiscordSocketClient _client;
    private readonly ILogger<RatWatchModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RatWatchModule"/> class.
    /// </summary>
    public RatWatchModule(
        IRatWatchService ratWatchService,
        IRatWatchStatusService ratWatchStatusService,
        IDashboardUpdateService dashboardUpdateService,
        DiscordSocketClient client,
        ILogger<RatWatchModule> logger)
    {
        _ratWatchService = ratWatchService;
        _ratWatchStatusService = ratWatchStatusService;
        _dashboardUpdateService = dashboardUpdateService;
        _client = client;
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

                var botEmbed = new EmbedBuilder()
                    .WithTitle("❌ Cannot Watch Bots")
                    .WithDescription("Bots cannot be targeted with Rat Watch. Please select a message from a human user.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: botEmbed, ephemeral: true);
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
            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Invalid Time Format")
                .WithDescription("Could not parse the time you provided. Use formats like:\n• `10m` - 10 minutes from now\n• `2h` - 2 hours from now\n• `1h30m` - 1 hour 30 minutes\n• `10pm` - 10 PM today\n• `22:00` - 10 PM today (24-hour)")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
            _logger.LogDebug("Failed to parse time input: {TimeInput}", modal.Time);
            return;
        }

        // Validate time is in the future
        if (scheduledTime.Value <= DateTime.UtcNow)
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Invalid Time")
                .WithDescription("The scheduled time must be in the future.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
            _logger.LogDebug("Scheduled time {ScheduledTime} is in the past", scheduledTime.Value);
            return;
        }

        // Validate time is within MaxAdvanceHours
        var maxAdvanceTime = DateTime.UtcNow.AddHours(settings.MaxAdvanceHours);
        if (scheduledTime.Value > maxAdvanceTime)
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Time Too Far Ahead")
                .WithDescription($"Rat Watches can only be scheduled up to {settings.MaxAdvanceHours} hours in advance.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
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

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to create Rat Watch: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Slash command for the accused to clear themselves from all pending watches.
    /// </summary>
    [SlashCommand("rat-clear", "Clear yourself from active Rat Watches")]
    public async Task RatClearAsync()
    {
        _logger.LogInformation(
            "Rat clear command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        try
        {
            // Get all pending watches for this user in this guild
            var (watches, _) = await _ratWatchService.GetByGuildAsync(Context.Guild.Id, 1, 1000);

            var pendingWatches = watches
                .Where(w => w.AccusedUserId == Context.User.Id && w.Status == Core.Enums.RatWatchStatus.Pending)
                .ToList();

            if (pendingWatches.Count == 0)
            {
                var noWatchesEmbed = new EmbedBuilder()
                    .WithTitle("✅ No Active Watches")
                    .WithDescription("You have no active Rat Watches to clear.")
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: noWatchesEmbed, ephemeral: true);
                _logger.LogDebug("User {UserId} has no pending Rat Watches", Context.User.Id);
                return;
            }

            // Clear each one using service.ClearWatchAsync
            var clearedCount = 0;
            foreach (var watch in pendingWatches)
            {
                var success = await _ratWatchService.ClearWatchAsync(watch.Id, Context.User.Id);
                if (success)
                {
                    clearedCount++;
                }
            }

            _logger.LogInformation(
                "User {UserId} cleared {ClearedCount} of {TotalCount} pending Rat Watches",
                Context.User.Id,
                clearedCount,
                pendingWatches.Count);

            var embed = new EmbedBuilder()
                .WithTitle("✅ Watches Cleared")
                .WithDescription($"Successfully cleared {clearedCount} active Rat Watch{(clearedCount != 1 ? "es" : "")}.")
                .WithColor(Color.Green)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed, ephemeral: true);

            // Notify that watches were cleared - may need to update bot status
            if (clearedCount > 0)
            {
                _ratWatchStatusService.RequestStatusUpdate();
            }

            _logger.LogDebug("Rat clear command response sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear Rat Watches for user {UserId}", Context.User.Id);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to clear Rat Watches: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Slash command to view a user's rat record statistics.
    /// </summary>
    [SlashCommand("rat-stats", "View a user's rat record")]
    public async Task RatStatsAsync(
        [Summary("user", "The user to check (defaults to yourself)")] IUser? user = null)
    {
        var targetUser = user ?? Context.User;

        _logger.LogInformation(
            "Rat stats command executed by {Username} (ID: {UserId}) for user {TargetUsername} (ID: {TargetUserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            targetUser.Username,
            targetUser.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        try
        {
            // Get stats from service
            var stats = await _ratWatchService.GetUserStatsAsync(Context.Guild.Id, targetUser.Id);

            // Build embed
            var embed = new EmbedBuilder()
                .WithTitle($"🐀 Rat Record: {stats.Username}")
                .WithColor(stats.TotalGuiltyCount == 0 ? Color.Green : Color.Orange)
                .AddField("Total Guilty Verdicts", stats.TotalGuiltyCount.ToString(), inline: true);

            if (stats.TotalGuiltyCount == 0)
            {
                embed.WithDescription("This user has a clean record!");
            }
            else
            {
                // Show recent records (up to 5)
                var recentRecords = stats.RecentRecords.Take(5).ToList();

                if (recentRecords.Count > 0)
                {
                    embed.AddField("\u200B", "**Recent Records:**", inline: false);

                    foreach (var record in recentRecords)
                    {
                        var timestamp = new DateTimeOffset(record.RecordedAt).ToUnixTimeSeconds();
                        var voteTally = $"{record.GuiltyVotes} Rat, {record.NotGuiltyVotes} Not Rat";
                        var recordLink = !string.IsNullOrWhiteSpace(record.OriginalMessageLink)
                            ? $" - [Jump to message]({record.OriginalMessageLink})"
                            : "";

                        embed.AddField(
                            $"<t:{timestamp}:D>",
                            $"<t:{timestamp}:R> — {voteTally}{recordLink}",
                            inline: false);
                    }
                }
            }

            embed.WithCurrentTimestamp();

            await RespondAsync(embed: embed.Build());

            _logger.LogDebug("Rat stats command response sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Rat stats for user {UserId}", targetUser.Id);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to retrieve Rat stats: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Slash command to configure Rat Watch settings for this server.
    /// </summary>
    [SlashCommand("rat-settings", "View or configure Rat Watch settings")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task RatSettingsAsync(
        [Summary("timezone", "Set the timezone for parsing times like '10pm'")]
        [Choice("Eastern Time", "Eastern Standard Time")]
        [Choice("Central Time", "Central Standard Time")]
        [Choice("Mountain Time", "Mountain Standard Time")]
        [Choice("Pacific Time", "Pacific Standard Time")]
        [Choice("UTC", "UTC")]
        string? timezone = null)
    {
        _logger.LogInformation(
            "Rat settings command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        try
        {
            var settings = await _ratWatchService.GetGuildSettingsAsync(Context.Guild.Id);

            if (!string.IsNullOrWhiteSpace(timezone))
            {
                // Validate timezone
                try
                {
                    TimeZoneInfo.FindSystemTimeZoneById(timezone);
                }
                catch
                {
                    await RespondAsync($"Invalid timezone: `{timezone}`", ephemeral: true);
                    return;
                }

                // Update settings
                settings = await _ratWatchService.UpdateGuildSettingsAsync(
                    Context.Guild.Id,
                    s => s.Timezone = timezone);

                _logger.LogInformation(
                    "Rat Watch timezone updated to {Timezone} for guild {GuildId}",
                    timezone,
                    Context.Guild.Id);
            }

            var embed = new EmbedBuilder()
                .WithTitle("🐀 Rat Watch Settings")
                .WithColor(Color.Blue)
                .AddField("Timezone", settings.Timezone, inline: true)
                .AddField("Max Advance Hours", $"{settings.MaxAdvanceHours}h", inline: true)
                .AddField("Voting Duration", $"{settings.VotingDurationMinutes} min", inline: true)
                .AddField("Feature Enabled", settings.IsEnabled ? "Yes" : "No", inline: true)
                .WithFooter(timezone != null ? "Settings updated!" : "Use /rat-settings timezone:<value> to change")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed, ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get/update Rat Watch settings for guild {GuildId}", Context.Guild.Id);
            await RespondAsync($"Failed to update settings: {ex.Message}", ephemeral: true);
        }
    }

    /// <summary>
    /// Slash command to view the rat leaderboard.
    /// </summary>
    [SlashCommand("rat-leaderboard", "View the top rats in this server")]
    public async Task RatLeaderboardAsync()
    {
        _logger.LogInformation(
            "Rat leaderboard command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        try
        {
            // Get leaderboard from service (top 10)
            var leaderboard = await _ratWatchService.GetLeaderboardAsync(Context.Guild.Id, 10);

            // Build embed
            var embed = new EmbedBuilder()
                .WithTitle("🏆 Rat Leaderboard")
                .WithColor(Color.Gold)
                .WithCurrentTimestamp();

            if (leaderboard.Count == 0)
            {
                embed.WithDescription("No rats have been caught yet!");
            }
            else
            {
                var description = string.Join("\n", leaderboard.Select(entry =>
                {
                    var medal = entry.Rank switch
                    {
                        1 => "🥇",
                        2 => "🥈",
                        3 => "🥉",
                        _ => $"**{entry.Rank}.**"
                    };

                    return $"{medal} {entry.Username} — {entry.GuiltyCount} incident{(entry.GuiltyCount != 1 ? "s" : "")}";
                }));

                embed.WithDescription(description);
            }

            await RespondAsync(embed: embed.Build());

            _logger.LogDebug("Rat leaderboard command response sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Rat leaderboard for guild {GuildId}", Context.Guild.Id);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to retrieve leaderboard: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
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
