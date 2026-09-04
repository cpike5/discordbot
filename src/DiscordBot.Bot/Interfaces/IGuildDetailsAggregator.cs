using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// Aggregates the many independent data sources shown on the Guild Details page
/// (guild record, recent commands, welcome/scheduled-message/rat-watch/reminder/member/audio/
/// assistant widgets) into a single call, so the page model itself stays a thin
/// presentation layer. Extracted from <c>DiscordBot.Bot.Pages.Guilds.DetailsModel</c>.
/// </summary>
public interface IGuildDetailsAggregator
{
    /// <summary>
    /// Builds the full set of data needed to render the Guild Details page for one guild.
    /// Returns <c>null</c> when the guild does not exist.
    /// </summary>
    /// <param name="guildId">The guild to load.</param>
    /// <param name="recentCommandsLimit">How many recent command log entries to include.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<GuildDetailsAggregateDto?> BuildAsync(ulong guildId, int recentCommandsLimit, CancellationToken cancellationToken);
}

/// <summary>
/// Aggregate view of everything the Guild Details page displays, produced by
/// <see cref="IGuildDetailsAggregator"/>.
/// </summary>
public sealed record GuildDetailsAggregateDto
{
    public required GuildDto Guild { get; init; }
    public required IReadOnlyList<CommandLogDto> RecentCommandLogs { get; init; }

    public bool WelcomeEnabled { get; init; }

    public int ScheduledMessagesTotal { get; init; }
    public int ScheduledMessagesActive { get; init; }
    public int ScheduledMessagesPaused { get; init; }
    public DateTime? NextScheduledExecution { get; init; }
    public string? NextScheduledMessageTitle { get; init; }

    public bool RatWatchEnabled { get; init; }
    public int RatWatchTotal { get; init; }
    public int RatWatchPending { get; init; }
    public int RatWatchCompleted { get; init; }
    public IReadOnlyList<RatLeaderboardEntryDto> TopRatLeaderboard { get; init; } = Array.Empty<RatLeaderboardEntryDto>();

    public int RemindersTotal { get; init; }
    public int RemindersPending { get; init; }
    public int RemindersDeliveredToday { get; init; }
    public int RemindersFailed { get; init; }
    public IReadOnlyList<UpcomingReminderDto> UpcomingReminders { get; init; } = Array.Empty<UpcomingReminderDto>();

    public int MembersTotalCount { get; init; }
    public int MembersActiveToday { get; init; }
    public IReadOnlyList<GuildMemberDto> NewestMembers { get; init; } = Array.Empty<GuildMemberDto>();

    public bool AudioEnabled { get; init; }
    public int TotalSoundCount { get; init; }
    public IReadOnlyList<(string Name, int PlayCount)> TopSounds { get; init; } = Array.Empty<(string, int)>();
    public string? MostUsedTtsVoice { get; init; }

    public bool AssistantGloballyEnabled { get; init; }
    public bool AssistantLocallyEnabled { get; init; }
    public int AssistantChannelCount { get; init; }
    public int AssistantRateLimit { get; init; }
    public bool AssistantIsRateLimitOverride { get; init; }
    public int AssistantRateLimitWindowMinutes { get; init; }
}
