using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Autocomplete;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Slash command module for personal reminder commands.
/// </summary>
[RequireGuildActive]
[Group("remind", "Personal reminder commands")]
public class ReminderModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IReminderService _reminderService;
    private readonly ITimeParsingService _timeParsingService;
    private readonly ReminderOptions _options;
    private readonly ILogger<ReminderModule> _logger;

    private const int RemindersPerPage = 10;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReminderModule"/> class.
    /// </summary>
    public ReminderModule(
        IReminderService reminderService,
        ITimeParsingService timeParsingService,
        IOptions<ReminderOptions> options,
        ILogger<ReminderModule> logger)
    {
        _reminderService = reminderService;
        _timeParsingService = timeParsingService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Sets a personal reminder that will be delivered via DM.
    /// </summary>
    /// <param name="time">When to remind (e.g., "10m", "2h", "tomorrow 3pm").</param>
    /// <param name="message">What to remind about (max 500 chars).</param>
    [SlashCommand("set", "Set a personal reminder")]
    public async Task SetAsync(
        [Summary("time", "When to remind (10m, 2h, tomorrow 3pm)")] string time,
        [Summary("message", "What to remind about")] [MaxLength(500)] string message)
    {
        _logger.LogInformation(
            "Remind set command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}), Time: {Time}",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            Context.Guild.Id,
            time);

        // Check pending reminder count
        var pendingCount = await _reminderService.GetPendingCountAsync(Context.User.Id);
        if (pendingCount >= _options.MaxRemindersPerUser)
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("Too Many Reminders")
                .WithDescription($"You have reached the maximum of **{_options.MaxRemindersPerUser}** pending reminders.")
                .WithColor(Color.Red)
                .AddField("Suggestion", "Cancel some pending reminders using `/remind cancel` to make room for new ones.")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);

            _logger.LogDebug(
                "Remind set rejected for user {UserId}: max reminders reached ({Count}/{Max})",
                Context.User.Id,
                pendingCount,
                _options.MaxRemindersPerUser);
            return;
        }

        // Use UTC for time parsing
        // Note: Guild-level timezone configuration can be added in a future enhancement
        const string timezone = "UTC";

        // Parse the time expression
        var parseResult = _timeParsingService.Parse(time, timezone);
        if (!parseResult.Success)
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("Invalid Time Format")
                .WithDescription(parseResult.ErrorMessage)
                .WithColor(Color.Red)
                .AddField("Examples",
                    "• `10m` - 10 minutes from now\n" +
                    "• `2h` - 2 hours from now\n" +
                    "• `1d` - 1 day from now\n" +
                    "• `tomorrow 3pm` - Tomorrow at 3 PM\n" +
                    "• `friday 10am` - Next Friday at 10 AM\n" +
                    "• `Dec 25 noon` - December 25th at noon")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);

            _logger.LogDebug(
                "Remind set rejected for user {UserId}: time parse failed - {Error}",
                Context.User.Id,
                parseResult.ErrorMessage);
            return;
        }

        var triggerAt = parseResult.UtcTime!.Value;

        // Validate min/max advance time
        var now = DateTime.UtcNow;
        var minTime = now.AddMinutes(_options.MinAdvanceMinutes);
        var maxTime = now.AddDays(_options.MaxAdvanceDays);

        if (triggerAt < minTime)
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("Time Too Soon")
                .WithDescription($"Reminders must be at least **{_options.MinAdvanceMinutes} minute(s)** in the future.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
            return;
        }

        if (triggerAt > maxTime)
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("Time Too Far")
                .WithDescription($"Reminders cannot be more than **{_options.MaxAdvanceDays} days** in the future.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
            return;
        }

        // Create the reminder
        var reminder = await _reminderService.CreateReminderAsync(
            Context.Guild.Id,
            Context.Channel.Id,
            Context.User.Id,
            message,
            triggerAt);

        // Format the response
        var timestamp = new DateTimeOffset(triggerAt).ToUnixTimeSeconds();
        var messagePreview = message.Length > 100 ? message[..97] + "..." : message;

        var successEmbed = new EmbedBuilder()
            .WithTitle("Reminder Set")
            .WithDescription($"I'll remind you <t:{timestamp}:R> (<t:{timestamp}:F>)")
            .WithColor(Color.Green)
            .AddField("Message", messagePreview, inline: false)
            .WithFooter($"Reminder ID: {reminder.Id}")
            .WithCurrentTimestamp()
            .Build();

        await RespondAsync(embed: successEmbed, ephemeral: true);

        _logger.LogInformation(
            "Reminder created by {Username} (ID: {UserId}) for {TriggerTime} in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            triggerAt,
            Context.Guild.Name,
            Context.Guild.Id);
    }

    /// <summary>
    /// Lists the user's pending reminders.
    /// </summary>
    /// <param name="page">Page number (default: 1).</param>
    [SlashCommand("list", "View your pending reminders")]
    public async Task ListAsync(
        [Summary("page", "Page number")] int page = 1)
    {
        _logger.LogInformation(
            "Remind list command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}), Page: {Page}",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            Context.Guild.Id,
            page);

        if (page < 1)
        {
            page = 1;
        }

        var (reminders, totalCount) = await _reminderService.GetUserRemindersAsync(
            Context.User.Id,
            page,
            RemindersPerPage);

        if (totalCount == 0)
        {
            var emptyEmbed = new EmbedBuilder()
                .WithTitle("Your Reminders")
                .WithDescription("You have no pending reminders.")
                .WithColor(Color.Blue)
                .AddField("Create a Reminder", "Use `/remind set` to create a new reminder.")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: emptyEmbed, ephemeral: true);
            return;
        }

        var totalPages = (int)Math.Ceiling(totalCount / (double)RemindersPerPage);
        if (page > totalPages)
        {
            page = totalPages;
        }

        var embed = new EmbedBuilder()
            .WithTitle($"Your Reminders (Page {page}/{totalPages})")
            .WithColor(Color.Blue)
            .WithFooter($"Showing {(page - 1) * RemindersPerPage + 1}-{Math.Min(page * RemindersPerPage, totalCount)} of {totalCount} reminders")
            .WithCurrentTimestamp();

        var index = (page - 1) * RemindersPerPage + 1;
        foreach (var reminder in reminders)
        {
            var timestamp = new DateTimeOffset(reminder.TriggerAt).ToUnixTimeSeconds();
            var messagePreview = reminder.Message.Length > 80
                ? reminder.Message[..77] + "..."
                : reminder.Message;
            var shortId = reminder.Id.ToString()[..8];

            embed.AddField(
                $"{index}. {messagePreview}",
                $"⏰ <t:{timestamp}:R> | ID: `{shortId}`",
                inline: false);

            index++;
        }

        await RespondAsync(embed: embed.Build(), ephemeral: true);

        _logger.LogDebug(
            "Remind list response sent for user {UserId}: {Count} reminders, page {Page}/{TotalPages}",
            Context.User.Id,
            totalCount,
            page,
            totalPages);
    }

    /// <summary>
    /// Cancels a pending reminder.
    /// </summary>
    /// <param name="id">Reminder ID to cancel.</param>
    [SlashCommand("cancel", "Cancel a pending reminder")]
    public async Task CancelAsync(
        [Summary("id", "Reminder ID")] [Autocomplete(typeof(ReminderAutocompleteHandler))] string id)
    {
        _logger.LogInformation(
            "Remind cancel command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}), ID: {ReminderId}",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            Context.Guild.Id,
            id);

        if (!Guid.TryParse(id, out var reminderId))
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("Invalid ID")
                .WithDescription("The provided reminder ID is not valid.")
                .WithColor(Color.Red)
                .AddField("Suggestion", "Use `/remind list` to see your reminders and their IDs.")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);

            _logger.LogDebug(
                "Remind cancel rejected for user {UserId}: invalid GUID format - {Id}",
                Context.User.Id,
                id);
            return;
        }

        var reminder = await _reminderService.CancelReminderAsync(reminderId, Context.User.Id);

        if (reminder == null)
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("Reminder Not Found")
                .WithDescription("Could not find a pending reminder with that ID.")
                .WithColor(Color.Red)
                .AddField("Suggestion", "Use `/remind list` to see your pending reminders.")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);

            _logger.LogDebug(
                "Remind cancel failed for user {UserId}: reminder {ReminderId} not found or not owned",
                Context.User.Id,
                reminderId);
            return;
        }

        var timestamp = new DateTimeOffset(reminder.TriggerAt).ToUnixTimeSeconds();

        var successEmbed = new EmbedBuilder()
            .WithTitle("Reminder Cancelled")
            .WithDescription("Your reminder has been cancelled.")
            .WithColor(Color.Green)
            .AddField("Message", reminder.Message, inline: false)
            .AddField("Was Scheduled For", $"<t:{timestamp}:F>", inline: false)
            .WithCurrentTimestamp()
            .Build();

        await RespondAsync(embed: successEmbed, ephemeral: true);

        _logger.LogInformation(
            "Reminder cancelled by {Username} (ID: {UserId}): Reminder ID {ReminderId}",
            Context.User.Username,
            Context.User.Id,
            reminderId);
    }
}
