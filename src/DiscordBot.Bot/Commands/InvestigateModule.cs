using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Slash command module for investigating users and compiling comprehensive moderation profiles.
/// Provides a single command that aggregates all moderation data for a user: cases, notes, tags, flags, and watchlist status.
/// </summary>
[RequireGuildActive]
[RequireModerationEnabled]
[RequireModerator]
public class InvestigateModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IInvestigationService _investigationService;
    private readonly DiscordSocketClient _client;
    private readonly ILogger<InvestigateModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvestigateModule"/> class.
    /// </summary>
    public InvestigateModule(
        IInvestigationService investigationService,
        DiscordSocketClient client,
        ILogger<InvestigateModule> logger)
    {
        _investigationService = investigationService;
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Investigates a user and compiles a comprehensive moderation report.
    /// Includes account info, moderation cases, notes, tags, flagged events, and watchlist status.
    /// </summary>
    [SlashCommand("investigate", "Get a comprehensive report on a user")]
    public async Task InvestigateAsync(
        [Summary("user", "The user to investigate")] IUser user)
    {
        _logger.LogInformation(
            "Investigate command executed by {ModeratorUsername} (ID: {ModeratorId}) for user {TargetUsername} (ID: {TargetId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            user.Username,
            user.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        // Defer response as investigation may take time
        await DeferAsync(ephemeral: true);

        try
        {
            // Get full profile from service
            var profile = await _investigationService.InvestigateUserAsync(Context.Guild.Id, user.Id);

            _logger.LogDebug(
                "Investigation complete for user {TargetId}: {CaseCount} cases, {NoteCount} notes, {TagCount} tags, {FlagCount} flags, watchlist: {OnWatchlist}",
                user.Id,
                profile.Cases.Count,
                profile.Notes.Count,
                profile.Tags.Count,
                profile.FlaggedEvents.Count,
                profile.IsOnWatchlist);

            // Build comprehensive embed
            var embed = new EmbedBuilder()
                .WithTitle($"🔍 Investigation Report: {profile.Username}")
                .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .WithColor(DetermineProfileColor(profile))
                .WithCurrentTimestamp()
                .WithFooter("Visible to moderators only");

            // User info section
            var accountAge = DateTime.UtcNow - profile.AccountCreatedAt;
            var accountTimestamp = new DateTimeOffset(profile.AccountCreatedAt).ToUnixTimeSeconds();

            var userInfoValue = $"**User ID:** {profile.UserId}\n";
            userInfoValue += $"**Account Created:** <t:{accountTimestamp}:D> ({FormatAge(accountAge)} old)\n";

            if (profile.JoinedGuildAt.HasValue)
            {
                var joinTimestamp = new DateTimeOffset(profile.JoinedGuildAt.Value).ToUnixTimeSeconds();
                var joinAge = DateTime.UtcNow - profile.JoinedGuildAt.Value;
                userInfoValue += $"**Joined Server:** <t:{joinTimestamp}:D> ({FormatAge(joinAge)} ago)";
            }
            else
            {
                userInfoValue += "**Joined Server:** Not in server";
            }

            embed.AddField("👤 User Information", userInfoValue, inline: false);

            // Watchlist status
            if (profile.IsOnWatchlist && profile.WatchlistEntry != null)
            {
                var watchTimestamp = new DateTimeOffset(profile.WatchlistEntry.AddedAt).ToUnixTimeSeconds();
                var watchValue = $"⚠️ **On Watchlist** — Added <t:{watchTimestamp}:R> by {profile.WatchlistEntry.AddedByUsername}";

                if (!string.IsNullOrWhiteSpace(profile.WatchlistEntry.Reason))
                {
                    var reasonPreview = profile.WatchlistEntry.Reason.Length > 100
                        ? profile.WatchlistEntry.Reason[..97] + "..."
                        : profile.WatchlistEntry.Reason;
                    watchValue += $"\n> {reasonPreview}";
                }

                embed.AddField("🚨 Watchlist Status", watchValue, inline: false);
            }

            // Case summary
            var totalCases = profile.Cases.Count;
            var warnCount = profile.Cases.Count(c => c.Type == CaseType.Warn);
            var muteCount = profile.Cases.Count(c => c.Type == CaseType.Mute);
            var kickCount = profile.Cases.Count(c => c.Type == CaseType.Kick);
            var banCount = profile.Cases.Count(c => c.Type == CaseType.Ban);

            var caseSummary = totalCases == 0
                ? "No moderation cases"
                : $"**Total:** {totalCases} | Warns: {warnCount} | Mutes: {muteCount} | Kicks: {kickCount} | Bans: {banCount}";

            embed.AddField("📋 Moderation Cases", caseSummary, inline: false);

            // Recent cases (last 5)
            if (totalCases > 0)
            {
                var recentCases = profile.Cases
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(5)
                    .ToList();

                var recentCasesValue = string.Join("\n", recentCases.Select(c =>
                {
                    var timestamp = new DateTimeOffset(c.CreatedAt).ToUnixTimeSeconds();
                    var reason = string.IsNullOrWhiteSpace(c.Reason) ? "No reason" : c.Reason;
                    var reasonPreview = reason.Length > 50 ? reason[..47] + "..." : reason;
                    return $"• **{c.Type}** (<t:{timestamp}:d>) — {reasonPreview}";
                }));

                embed.AddField("📝 Recent Cases", recentCasesValue, inline: false);
            }

            // Tags
            if (profile.Tags.Count > 0)
            {
                var positiveTagsValue = string.Join(", ", profile.Tags
                    .Where(t => t.TagCategory == TagCategory.Positive)
                    .Select(t => $"`{t.TagName}`"));

                var negativeTagsValue = string.Join(", ", profile.Tags
                    .Where(t => t.TagCategory == TagCategory.Negative)
                    .Select(t => $"`{t.TagName}`"));

                var neutralTagsValue = string.Join(", ", profile.Tags
                    .Where(t => t.TagCategory == TagCategory.Neutral)
                    .Select(t => $"`{t.TagName}`"));

                var tagsValue = "";
                if (!string.IsNullOrEmpty(positiveTagsValue))
                    tagsValue += $"✅ {positiveTagsValue}\n";
                if (!string.IsNullOrEmpty(negativeTagsValue))
                    tagsValue += $"⚠️ {negativeTagsValue}\n";
                if (!string.IsNullOrEmpty(neutralTagsValue))
                    tagsValue += $"ℹ️ {neutralTagsValue}";

                embed.AddField($"🏷️ Tags ({profile.Tags.Count})", tagsValue.Trim(), inline: false);
            }
            else
            {
                embed.AddField("🏷️ Tags", "No tags applied", inline: false);
            }

            // Notes
            embed.AddField("📝 Moderator Notes", $"{profile.Notes.Count} note{(profile.Notes.Count != 1 ? "s" : "")}", inline: true);

            // Flagged events
            var pendingFlags = profile.FlaggedEvents.Count(f => f.Status == FlaggedEventStatus.Pending);
            var flagValue = profile.FlaggedEvents.Count == 0
                ? "No flagged events"
                : $"{profile.FlaggedEvents.Count} total ({pendingFlags} pending review)";

            embed.AddField("🚩 Auto-Mod Flags", flagValue, inline: true);

            await FollowupAsync(embed: embed.Build(), ephemeral: true);

            _logger.LogDebug("Investigate command response sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to investigate user {TargetId}", user.Id);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to investigate user: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await FollowupAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Determines the embed color based on the user's moderation profile severity.
    /// </summary>
    private static Color DetermineProfileColor(Core.DTOs.UserModerationProfileDto profile)
    {
        // On watchlist = Orange
        if (profile.IsOnWatchlist)
        {
            return Color.Orange;
        }

        // Has bans = Red
        if (profile.Cases.Any(c => c.Type == CaseType.Ban))
        {
            return Color.Red;
        }

        // Has negative tags = Dark Orange
        if (profile.Tags.Any(t => t.TagCategory == TagCategory.Negative))
        {
            return new Color(255, 140, 0); // Dark orange
        }

        // Has multiple cases = Yellow
        if (profile.Cases.Count >= 3)
        {
            return Color.Gold;
        }

        // Has any cases = Light Orange
        if (profile.Cases.Count > 0)
        {
            return Color.LightOrange;
        }

        // Clean record = Green
        return Color.Green;
    }

    /// <summary>
    /// Formats a TimeSpan into a human-readable age string.
    /// </summary>
    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalDays >= 365)
        {
            var years = (int)(age.TotalDays / 365);
            return $"{years} year{(years != 1 ? "s" : "")}";
        }
        else if (age.TotalDays >= 30)
        {
            var months = (int)(age.TotalDays / 30);
            return $"{months} month{(months != 1 ? "s" : "")}";
        }
        else if (age.TotalDays >= 1)
        {
            var days = (int)age.TotalDays;
            return $"{days} day{(days != 1 ? "s" : "")}";
        }
        else if (age.TotalHours >= 1)
        {
            var hours = (int)age.TotalHours;
            return $"{hours} hour{(hours != 1 ? "s" : "")}";
        }
        else
        {
            var minutes = (int)age.TotalMinutes;
            return $"{minutes} minute{(minutes != 1 ? "s" : "")}";
        }
    }
}
