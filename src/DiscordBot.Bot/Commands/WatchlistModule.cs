using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Components;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Slash commands for managing the moderator watchlist.
/// The watchlist tracks users that moderators want to monitor more closely.
/// </summary>
[RequireGuildActive]
[RequireModerationEnabled]
[RequireModerator]
[Group("watchlist", "Watchlist management commands")]
public class WatchlistModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IWatchlistService _watchlistService;
    private readonly IInteractionStateService _stateService;
    private readonly ILogger<WatchlistModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WatchlistModule"/> class.
    /// </summary>
    public WatchlistModule(
        IWatchlistService watchlistService,
        IInteractionStateService stateService,
        ILogger<WatchlistModule> logger)
    {
        _watchlistService = watchlistService;
        _stateService = stateService;
        _logger = logger;
    }

    /// <summary>
    /// Adds a user to the moderator watchlist.
    /// </summary>
    [SlashCommand("add", "Add a user to the watchlist")]
    public async Task AddAsync(
        [Summary("user", "The user to watch")] IUser user,
        [Summary("reason", "Reason for watching")] string? reason = null)
    {
        _logger.LogInformation(
            "Watchlist add command executed by {ModeratorUsername} (ID: {ModeratorId}) adding user {TargetUsername} (ID: {TargetId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            user.Username,
            user.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        try
        {
            // Prevent adding bots to watchlist
            if (user.IsBot)
            {
                var botEmbed = new EmbedBuilder()
                    .WithTitle("❌ Cannot Watch Bots")
                    .WithDescription("Bots cannot be added to the watchlist. Please select a human user.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: botEmbed, ephemeral: true);
                _logger.LogDebug("Watchlist add failed: target is a bot");
                return;
            }

            // Check if user is already on watchlist
            var isAlreadyWatched = await _watchlistService.IsOnWatchlistAsync(Context.Guild.Id, user.Id);
            if (isAlreadyWatched)
            {
                var alreadyEmbed = new EmbedBuilder()
                    .WithTitle("⚠️ Already on Watchlist")
                    .WithDescription($"<@{user.Id}> is already on the watchlist.")
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: alreadyEmbed, ephemeral: true);
                _logger.LogDebug("Watchlist add skipped: user {TargetId} already on watchlist", user.Id);
                return;
            }

            // Add via service
            var entry = await _watchlistService.AddToWatchlistAsync(
                Context.Guild.Id,
                user.Id,
                reason,
                Context.User.Id);

            _logger.LogInformation(
                "User {TargetId} added to watchlist by {ModeratorId} with ID {EntryId}",
                user.Id,
                Context.User.Id,
                entry.Id);

            // Build confirmation embed
            var embed = new EmbedBuilder()
                .WithTitle("✅ Added to Watchlist")
                .WithDescription($"<@{user.Id}> has been added to the watchlist.")
                .AddField("Added by", $"<@{Context.User.Id}>", inline: true)
                .WithColor(Color.Green)
                .WithCurrentTimestamp()
                .Build();

            if (!string.IsNullOrWhiteSpace(reason))
            {
                embed = embed.ToEmbedBuilder()
                    .AddField("Reason", reason, inline: false)
                    .Build();
            }

            await RespondAsync(embed: embed, ephemeral: true);

            _logger.LogDebug("Watchlist add command response sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add user {TargetId} to watchlist", user.Id);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to add to watchlist: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Removes a user from the moderator watchlist.
    /// </summary>
    [SlashCommand("remove", "Remove a user from the watchlist")]
    public async Task RemoveAsync(
        [Summary("user", "The user to remove")] IUser user)
    {
        _logger.LogInformation(
            "Watchlist remove command executed by {ModeratorUsername} (ID: {ModeratorId}) removing user {TargetUsername} (ID: {TargetId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            user.Username,
            user.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        try
        {
            // Remove via service
            var removed = await _watchlistService.RemoveFromWatchlistAsync(Context.Guild.Id, user.Id);

            if (!removed)
            {
                var notFoundEmbed = new EmbedBuilder()
                    .WithTitle("❌ Not on Watchlist")
                    .WithDescription($"<@{user.Id}> is not on the watchlist.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: notFoundEmbed, ephemeral: true);
                _logger.LogDebug("Watchlist remove failed: user {TargetId} not on watchlist", user.Id);
                return;
            }

            _logger.LogInformation(
                "User {TargetId} removed from watchlist by {ModeratorId}",
                user.Id,
                Context.User.Id);

            // Build confirmation embed
            var embed = new EmbedBuilder()
                .WithTitle("✅ Removed from Watchlist")
                .WithDescription($"<@{user.Id}> has been removed from the watchlist.")
                .WithColor(Color.Green)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed, ephemeral: true);

            _logger.LogDebug("Watchlist remove command response sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove user {TargetId} from watchlist", user.Id);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to remove from watchlist: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Views the guild's watchlist with pagination.
    /// </summary>
    [SlashCommand("list", "View the guild watchlist")]
    public async Task ListAsync(
        [Summary("page", "Page number")] int page = 1)
    {
        _logger.LogInformation(
            "Watchlist list command executed by {ModeratorUsername} (ID: {ModeratorId}) for page {Page} in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            page,
            Context.Guild.Name,
            Context.Guild.Id);

        try
        {
            // Validate page number
            if (page < 1)
            {
                page = 1;
            }

            const int pageSize = 10;

            // Get paginated watchlist
            var (entries, totalCount) = await _watchlistService.GetWatchlistAsync(
                Context.Guild.Id,
                page,
                pageSize);

            var entriesList = entries.ToList();

            _logger.LogDebug(
                "Retrieved {EntryCount} watchlist entries (page {Page}), total count: {TotalCount}",
                entriesList.Count,
                page,
                totalCount);

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            // Build embed
            var embed = new EmbedBuilder()
                .WithTitle("👁️ Watchlist")
                .WithColor(Color.Blue)
                .WithCurrentTimestamp();

            if (totalCount == 0)
            {
                embed.WithDescription("The watchlist is empty.");
            }
            else
            {
                embed.WithDescription($"Page {page} of {totalPages} — Total: {totalCount} user{(totalCount != 1 ? "s" : "")}");

                // Add each entry as a field
                foreach (var entry in entriesList)
                {
                    var timestamp = new DateTimeOffset(entry.AddedAt).ToUnixTimeSeconds();
                    var fieldValue = $"Added by {entry.AddedByUsername} <t:{timestamp}:R>";

                    if (!string.IsNullOrWhiteSpace(entry.Reason))
                    {
                        var reasonPreview = entry.Reason.Length > 50 ? entry.Reason[..47] + "..." : entry.Reason;
                        fieldValue += $"\n> {reasonPreview}";
                    }

                    embed.AddField(
                        $"<@{entry.UserId}>",
                        fieldValue,
                        inline: false);
                }

                // Add pagination footer
                if (totalPages > 1)
                {
                    embed.WithFooter($"Use /watchlist list page:{page + 1} for next page");
                }
            }

            await RespondAsync(embed: embed.Build(), ephemeral: true);

            _logger.LogDebug("Watchlist list command response sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list watchlist for guild {GuildId}", Context.Guild.Id);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to retrieve watchlist: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }
}
