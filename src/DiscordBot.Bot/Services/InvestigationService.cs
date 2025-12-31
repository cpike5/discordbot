using Discord;
using Discord.WebSocket;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service implementation for compiling comprehensive user moderation profiles and investigation reports.
/// Aggregates data from all moderation-related services into a unified profile.
/// </summary>
public class InvestigationService : IInvestigationService
{
    private readonly IModerationService _moderationService;
    private readonly IModNoteService _modNoteService;
    private readonly IModTagService _modTagService;
    private readonly IWatchlistService _watchlistService;
    private readonly IFlaggedEventService _flaggedEventService;
    private readonly DiscordSocketClient _client;
    private readonly ILogger<InvestigationService> _logger;

    public InvestigationService(
        IModerationService moderationService,
        IModNoteService modNoteService,
        IModTagService modTagService,
        IWatchlistService watchlistService,
        IFlaggedEventService flaggedEventService,
        DiscordSocketClient client,
        ILogger<InvestigationService> logger)
    {
        _moderationService = moderationService;
        _modNoteService = modNoteService;
        _modTagService = modTagService;
        _watchlistService = watchlistService;
        _flaggedEventService = flaggedEventService;
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UserModerationProfileDto> InvestigateUserAsync(ulong guildId, ulong userId, CancellationToken ct = default)
    {
        _logger.LogInformation("Investigating user {UserId} in guild {GuildId}", userId, guildId);

        // Fetch user info from Discord
        var username = await GetUsernameAsync(userId);
        var accountCreatedAt = GetAccountCreationDate(userId);
        var joinedGuildAt = await GetGuildJoinDateAsync(guildId, userId);

        // Fetch all moderation data in parallel for better performance
        var casesTask = _moderationService.GetUserCasesAsync(guildId, userId, 1, int.MaxValue, ct);
        var notesTask = _modNoteService.GetNotesAsync(guildId, userId, ct);
        var tagsTask = _modTagService.GetUserTagsAsync(guildId, userId, ct);
        var watchlistEntryTask = _watchlistService.GetEntryAsync(guildId, userId, ct);
        var flaggedEventsTask = _flaggedEventService.GetUserEventsAsync(guildId, userId, ct);

        await Task.WhenAll(casesTask, notesTask, tagsTask, watchlistEntryTask, flaggedEventsTask);

        var (cases, _) = await casesTask;
        var notes = await notesTask;
        var tags = await tagsTask;
        var watchlistEntry = await watchlistEntryTask;
        var flaggedEvents = await flaggedEventsTask;

        var profile = new UserModerationProfileDto
        {
            UserId = userId,
            Username = username,
            GuildId = guildId,
            AccountCreatedAt = accountCreatedAt,
            JoinedGuildAt = joinedGuildAt,
            Cases = cases.ToList(),
            Notes = notes.ToList(),
            Tags = tags.ToList(),
            FlaggedEvents = flaggedEvents.ToList(),
            IsOnWatchlist = watchlistEntry != null,
            WatchlistEntry = watchlistEntry
        };

        _logger.LogInformation("Investigation completed for user {UserId} in guild {GuildId}: {CaseCount} cases, {NoteCount} notes, {TagCount} tags, {EventCount} flagged events, watchlist: {IsOnWatchlist}",
            userId, guildId, profile.Cases.Count, profile.Notes.Count, profile.Tags.Count, profile.FlaggedEvents.Count, profile.IsOnWatchlist);

        return profile;
    }

    /// <summary>
    /// Resolves a Discord user ID to username.
    /// </summary>
    private async Task<string> GetUsernameAsync(ulong userId)
    {
        try
        {
            var user = await _client.Rest.GetUserAsync(userId);
            return user?.Username ?? $"Unknown#{userId}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve username for user {UserId}", userId);
            return $"Unknown#{userId}";
        }
    }

    /// <summary>
    /// Extracts account creation date from Discord snowflake ID.
    /// </summary>
    private DateTime GetAccountCreationDate(ulong userId)
    {
        try
        {
            return SnowflakeUtils.FromSnowflake(userId).UtcDateTime;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract account creation date from user ID {UserId}", userId);
            return DateTime.MinValue;
        }
    }

    /// <summary>
    /// Gets the date when a user joined a specific guild.
    /// </summary>
    private async Task<DateTime?> GetGuildJoinDateAsync(ulong guildId, ulong userId)
    {
        try
        {
            var guild = _client.GetGuild(guildId);
            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found when retrieving join date for user {UserId}", guildId, userId);
                return null;
            }

            // Try to get from cache first
            var user = guild.GetUser(userId);
            if (user?.JoinedAt != null)
            {
                return user.JoinedAt.Value.UtcDateTime;
            }

            // If not in cache, try downloading members (if privileged intents enabled)
            await guild.DownloadUsersAsync();
            user = guild.GetUser(userId);
            if (user?.JoinedAt != null)
            {
                return user.JoinedAt.Value.UtcDateTime;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve guild join date for user {UserId} in guild {GuildId}",
                userId, guildId);
            return null;
        }
    }
}
