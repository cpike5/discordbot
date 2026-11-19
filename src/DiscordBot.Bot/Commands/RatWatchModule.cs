using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Bot.Helpers;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Slash commands for the Rat Watch accountability feature: <c>/rat clear</c>, <c>/rat stats</c>
/// and <c>/rat leaderboard</c>.
/// </summary>
/// <remarks>
/// The "Rat Watch" message context menu and its modal live in <see cref="RatWatchContextMenuModule"/>,
/// because a context menu cannot be declared inside a <c>[Group]</c>. That module follows this
/// module's enable toggle.
/// </remarks>
[Group("rat", "Rat Watch accountability commands")]
[RequireGuildActive]
[RequireRatWatchEnabled]
public class RatWatchModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IRatWatchService _ratWatchService;
    private readonly IRatWatchStatusService _ratWatchStatusService;
    private readonly ILogger<RatWatchModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RatWatchModule"/> class.
    /// </summary>
    public RatWatchModule(
        IRatWatchService ratWatchService,
        IRatWatchStatusService ratWatchStatusService,
        ILogger<RatWatchModule> logger)
    {
        _ratWatchService = ratWatchService;
        _ratWatchStatusService = ratWatchStatusService;
        _logger = logger;
    }

    /// <summary>
    /// Slash command for the accused to clear themselves from all pending watches.
    /// </summary>
    [SlashCommand("clear", "Clear yourself from active Rat Watches")]
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
                await RespondAsync(embed: EmbedHelper.EmptyState("No Active Watches", "You have no active Rat Watches to clear."), ephemeral: true);
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

            await RespondAsync(embed: EmbedHelper.Success("Watches Cleared", $"Successfully cleared {clearedCount} active Rat Watch{(clearedCount != 1 ? "es" : "")}."), ephemeral: true);

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

            await RespondAsync(embed: EmbedHelper.Error("Error", $"Failed to clear Rat Watches: {ex.Message}"), ephemeral: true);
        }
    }

    /// <summary>
    /// Slash command to view a user's rat record statistics.
    /// </summary>
    [SlashCommand("stats", "View a user's rat record")]
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

            await RespondAsync(embed: EmbedHelper.Error("Error", $"Failed to retrieve Rat stats: {ex.Message}"), ephemeral: true);
        }
    }

    /// <summary>
    /// Slash command to view the rat leaderboard.
    /// </summary>
    [SlashCommand("leaderboard", "View the top rats in this server")]
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

            await RespondAsync(embed: EmbedHelper.Error("Error", $"Failed to retrieve leaderboard: {ex.Message}"), ephemeral: true);
        }
    }
}
