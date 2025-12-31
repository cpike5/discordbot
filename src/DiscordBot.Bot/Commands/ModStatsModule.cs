using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Bot.Utilities;
using DiscordBot.Core.Interfaces;
using System.Text;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Slash commands for viewing moderation statistics.
/// </summary>
[RequireGuildActive]
[RequireModerationEnabled]
[RequireModerator]
public class ModStatsModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IModerationService _moderationService;
    private readonly ILogger<ModStatsModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModStatsModule"/> class.
    /// </summary>
    public ModStatsModule(
        IModerationService moderationService,
        ILogger<ModStatsModule> logger)
    {
        _moderationService = moderationService;
        _logger = logger;
    }

    /// <summary>
    /// View moderation statistics for the guild or a specific moderator.
    /// </summary>
    [SlashCommand("modstats", "View moderation statistics")]
    public async Task ModStatsAsync(
        [Summary("moderator", "Specific moderator (defaults to all)")] IUser? moderator = null,
        [Summary("timeframe", "Time period")]
        [Choice("Last 24 hours", "24h")]
        [Choice("Last 7 days", "7d")]
        [Choice("Last 30 days", "30d")]
        [Choice("All time", "all")]
        string timeframe = "30d")
    {
        _logger.LogInformation(
            "Modstats command executed by {ModeratorUsername} (ID: {ModeratorId}) in guild {GuildName} (ID: {GuildId}), timeframe: {Timeframe}, target: {Target}",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            Context.Guild.Id,
            timeframe,
            moderator?.Username ?? "all");

        await DeferAsync();

        try
        {
            // Parse timeframe to start date
            DateTime? startDate = timeframe switch
            {
                "24h" => DateTime.UtcNow.AddHours(-24),
                "7d" => DateTime.UtcNow.AddDays(-7),
                "30d" => DateTime.UtcNow.AddDays(-30),
                "all" => null,
                _ => DateTime.UtcNow.AddDays(-30)
            };

            var timeframeDisplay = timeframe switch
            {
                "24h" => "Last 24 Hours",
                "7d" => "Last 7 Days",
                "30d" => "Last 30 Days",
                "all" => "All Time",
                _ => "Last 30 Days"
            };

            _logger.LogDebug("Parsed timeframe: {Timeframe} -> {StartDate}", timeframe, startDate);

            // Get stats from service
            var stats = await _moderationService.GetModeratorStatsAsync(
                Context.Guild.Id,
                moderator?.Id,
                startDate,
                DateTime.UtcNow);

            _logger.LogDebug(
                "Retrieved moderation stats: {TotalCases} total cases, {ModeratorCount} moderators",
                stats.TotalCases,
                stats.TopModerators.Count);

            // Build embed
            var embed = new EmbedBuilder()
                .WithTitle(moderator != null
                    ? $"📊 Moderation Stats: {moderator.Username}"
                    : $"📊 Moderation Stats: {Context.Guild.Name}")
                .WithColor(Color.Blue)
                .WithFooter(timeframeDisplay)
                .WithCurrentTimestamp();

            if (moderator != null)
            {
                embed.WithThumbnailUrl(moderator.GetAvatarUrl() ?? moderator.GetDefaultAvatarUrl());
            }

            // Overall stats
            var statsText = new StringBuilder();
            statsText.AppendLine($"**Total Cases:** {stats.TotalCases}");
            statsText.AppendLine($"• Warnings: {stats.WarnCount}");
            statsText.AppendLine($"• Kicks: {stats.KickCount}");
            statsText.AppendLine($"• Bans: {stats.BanCount}");
            statsText.AppendLine($"• Mutes: {stats.MuteCount}");

            embed.AddField("Statistics", statsText.ToString(), inline: false);

            // Show top moderators if guild-wide stats
            if (moderator == null && stats.TopModerators.Count > 0)
            {
                var topModsText = new StringBuilder();
                var topMods = stats.TopModerators.Take(5).ToList();

                for (int i = 0; i < topMods.Count; i++)
                {
                    var mod = topMods[i];
                    var medal = i switch
                    {
                        0 => "🥇",
                        1 => "🥈",
                        2 => "🥉",
                        _ => $"**{i + 1}.**"
                    };

                    topModsText.AppendLine($"{medal} {mod.Username} — {mod.TotalActions} action{(mod.TotalActions != 1 ? "s" : "")}");
                }

                embed.AddField("Top Moderators", topModsText.ToString(), inline: false);
            }

            // Breakdown by action type (visual representation)
            if (stats.TotalCases > 0)
            {
                var breakdownText = new StringBuilder();
                var warnPercent = (int)((stats.WarnCount / (double)stats.TotalCases) * 100);
                var kickPercent = (int)((stats.KickCount / (double)stats.TotalCases) * 100);
                var banPercent = (int)((stats.BanCount / (double)stats.TotalCases) * 100);
                var mutePercent = (int)((stats.MuteCount / (double)stats.TotalCases) * 100);

                if (warnPercent > 0) breakdownText.AppendLine($"⚠️ Warnings: {warnPercent}%");
                if (kickPercent > 0) breakdownText.AppendLine($"🥾 Kicks: {kickPercent}%");
                if (banPercent > 0) breakdownText.AppendLine($"🔨 Bans: {banPercent}%");
                if (mutePercent > 0) breakdownText.AppendLine($"🔇 Mutes: {mutePercent}%");

                embed.AddField("Action Distribution", breakdownText.ToString(), inline: false);
            }

            await FollowupAsync(embed: embed.Build());

            _logger.LogDebug("Modstats command completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve moderation statistics");

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to retrieve moderation statistics: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await FollowupAsync(embed: errorEmbed, ephemeral: true);
        }
    }
}
