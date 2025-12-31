using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the Rat Watch management page.
/// </summary>
public record RatWatchIndexViewModel
{
    /// <summary>
    /// Gets the guild's Discord snowflake ID.
    /// </summary>
    public ulong GuildId { get; init; }

    /// <summary>
    /// Gets the guild name.
    /// </summary>
    public string GuildName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the guild icon URL.
    /// </summary>
    public string? GuildIconUrl { get; init; }

    /// <summary>
    /// Gets whether Rat Watch is enabled for this guild.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Gets the configured timezone for the guild.
    /// </summary>
    public string Timezone { get; init; } = "Eastern Standard Time";

    /// <summary>
    /// Gets the maximum hours in advance a watch can be scheduled.
    /// </summary>
    public int MaxAdvanceHours { get; init; }

    /// <summary>
    /// Gets the voting duration in minutes.
    /// </summary>
    public int VotingDurationMinutes { get; init; }

    /// <summary>
    /// Gets the list of watches for this guild.
    /// </summary>
    public IReadOnlyList<RatWatchItemViewModel> Watches { get; init; } = Array.Empty<RatWatchItemViewModel>();

    /// <summary>
    /// Gets the leaderboard entries.
    /// </summary>
    public IReadOnlyList<RatLeaderboardEntryViewModel> Leaderboard { get; init; } = Array.Empty<RatLeaderboardEntryViewModel>();

    /// <summary>
    /// Gets the total number of watches in the guild.
    /// </summary>
    public int TotalWatches { get; init; }

    /// <summary>
    /// Gets the current page number (1-based).
    /// </summary>
    public int CurrentPage { get; init; } = 1;

    /// <summary>
    /// Gets the page size.
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalWatches / PageSize);

    /// <summary>
    /// Gets whether there are more pages.
    /// </summary>
    public bool HasNextPage => CurrentPage < TotalPages;

    /// <summary>
    /// Gets whether there are previous pages.
    /// </summary>
    public bool HasPreviousPage => CurrentPage > 1;

    /// <summary>
    /// Gets the count of pending watches.
    /// </summary>
    public int PendingCount => Watches.Count(w => w.Status == RatWatchStatus.Pending);

    /// <summary>
    /// Gets the count of watches currently in voting.
    /// </summary>
    public int VotingCount => Watches.Count(w => w.Status == RatWatchStatus.Voting);

    /// <summary>
    /// Gets the count of completed watches (guilty or not guilty verdicts).
    /// </summary>
    public int CompletedCount => Watches.Count(w => w.Status == RatWatchStatus.Guilty || w.Status == RatWatchStatus.NotGuilty);

    /// <summary>
    /// Gets the analytics summary for the guild.
    /// </summary>
    public RatWatchAnalyticsSummaryDto? AnalyticsSummary { get; init; }

    /// <summary>
    /// Creates a RatWatchIndexViewModel from service data.
    /// </summary>
    public static RatWatchIndexViewModel Create(
        ulong guildId,
        string guildName,
        string? guildIconUrl,
        GuildRatWatchSettings settings,
        IEnumerable<RatWatchDto> watches,
        int totalWatches,
        IEnumerable<RatLeaderboardEntryDto> leaderboard,
        int page,
        int pageSize,
        RatWatchAnalyticsSummaryDto? analyticsSummary = null)
    {
        var votingDurationMinutes = settings.VotingDurationMinutes;
        return new RatWatchIndexViewModel
        {
            GuildId = guildId,
            GuildName = guildName,
            GuildIconUrl = guildIconUrl,
            IsEnabled = settings.IsEnabled,
            Timezone = settings.Timezone,
            MaxAdvanceHours = settings.MaxAdvanceHours,
            VotingDurationMinutes = votingDurationMinutes,
            Watches = watches.Select(w => RatWatchItemViewModel.FromDto(w, votingDurationMinutes)).ToList(),
            TotalWatches = totalWatches,
            Leaderboard = leaderboard.Select(RatLeaderboardEntryViewModel.FromDto).ToList(),
            CurrentPage = page,
            PageSize = pageSize,
            AnalyticsSummary = analyticsSummary
        };
    }
}

/// <summary>
/// View model for a single Rat Watch item.
/// </summary>
public record RatWatchItemViewModel
{
    /// <summary>
    /// Gets the unique identifier for this watch.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the accused user ID.
    /// </summary>
    public ulong AccusedUserId { get; init; }

    /// <summary>
    /// Gets the accused username.
    /// </summary>
    public string AccusedUsername { get; init; } = string.Empty;

    /// <summary>
    /// Gets the initiator user ID.
    /// </summary>
    public ulong InitiatorUserId { get; init; }

    /// <summary>
    /// Gets the initiator username.
    /// </summary>
    public string InitiatorUsername { get; init; } = string.Empty;

    /// <summary>
    /// Gets the custom message for the watch.
    /// </summary>
    public string? CustomMessage { get; init; }

    /// <summary>
    /// Gets the scheduled time (UTC).
    /// </summary>
    public DateTime ScheduledAt { get; init; }

    /// <summary>
    /// Gets the scheduled time in ISO format for client-side rendering.
    /// </summary>
    public string ScheduledAtUtcIso => DateTime.SpecifyKind(ScheduledAt, DateTimeKind.Utc).ToString("o");

    /// <summary>
    /// Gets the creation time (UTC).
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets the creation time in ISO format for client-side rendering.
    /// </summary>
    public string CreatedAtUtcIso => DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc).ToString("o");

    /// <summary>
    /// Gets the current status of the watch.
    /// </summary>
    public RatWatchStatus Status { get; init; }

    /// <summary>
    /// Gets the status display text.
    /// </summary>
    public string StatusText => Status switch
    {
        RatWatchStatus.Pending => "Pending",
        RatWatchStatus.Voting => "Voting",
        RatWatchStatus.Guilty => "Guilty",
        RatWatchStatus.NotGuilty => "Not Guilty",
        RatWatchStatus.ClearedEarly => "Cleared",
        RatWatchStatus.Expired => "Expired",
        RatWatchStatus.Cancelled => "Cancelled",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets the status badge variant.
    /// </summary>
    public string StatusBadgeVariant => Status switch
    {
        RatWatchStatus.Pending => "Warning",
        RatWatchStatus.Voting => "Info",
        RatWatchStatus.Guilty => "Error",
        RatWatchStatus.NotGuilty => "Success",
        RatWatchStatus.ClearedEarly => "Secondary",
        RatWatchStatus.Expired => "Secondary",
        RatWatchStatus.Cancelled => "Error",
        _ => "Secondary"
    };

    /// <summary>
    /// Gets whether this watch can be cancelled.
    /// </summary>
    public bool CanCancel => Status == RatWatchStatus.Pending || Status == RatWatchStatus.Voting;

    /// <summary>
    /// Gets whether voting can be ended early for this watch.
    /// Only watches in Voting status can have voting ended early.
    /// </summary>
    public bool CanEndVote => Status == RatWatchStatus.Voting;

    /// <summary>
    /// Gets the time when voting started (UTC).
    /// Null if voting has not started yet.
    /// </summary>
    public DateTime? VotingStartedAt { get; init; }

    /// <summary>
    /// Gets the voting duration in minutes (from guild settings).
    /// </summary>
    public int VotingDurationMinutes { get; init; }

    /// <summary>
    /// Gets the computed time when voting ends (UTC).
    /// Null if not currently in voting status.
    /// </summary>
    public DateTime? VoteEndsAt => Status == RatWatchStatus.Voting && VotingStartedAt.HasValue
        ? VotingStartedAt.Value.AddMinutes(VotingDurationMinutes)
        : null;

    /// <summary>
    /// Gets the vote end time in ISO format for client-side rendering.
    /// Null if not in voting status.
    /// </summary>
    public string? VoteEndsAtUtcIso => VoteEndsAt.HasValue
        ? DateTime.SpecifyKind(VoteEndsAt.Value, DateTimeKind.Utc).ToString("o")
        : null;

    /// <summary>
    /// Gets the number of guilty votes.
    /// </summary>
    public int GuiltyVotes { get; init; }

    /// <summary>
    /// Gets the number of not guilty votes.
    /// </summary>
    public int NotGuiltyVotes { get; init; }

    /// <summary>
    /// Gets the total number of votes.
    /// </summary>
    public int TotalVotes => GuiltyVotes + NotGuiltyVotes;

    /// <summary>
    /// Creates a RatWatchItemViewModel from a DTO.
    /// </summary>
    /// <param name="dto">The Rat Watch DTO.</param>
    /// <param name="votingDurationMinutes">The voting duration from guild settings.</param>
    public static RatWatchItemViewModel FromDto(RatWatchDto dto, int votingDurationMinutes)
    {
        return new RatWatchItemViewModel
        {
            Id = dto.Id,
            AccusedUserId = dto.AccusedUserId,
            AccusedUsername = dto.AccusedUsername,
            InitiatorUserId = dto.InitiatorUserId,
            InitiatorUsername = dto.InitiatorUsername,
            CustomMessage = dto.CustomMessage,
            ScheduledAt = dto.ScheduledAt,
            CreatedAt = dto.CreatedAt,
            Status = dto.Status,
            VotingStartedAt = dto.VotingStartedAt,
            VotingDurationMinutes = votingDurationMinutes,
            GuiltyVotes = dto.GuiltyVotes,
            NotGuiltyVotes = dto.NotGuiltyVotes
        };
    }
}

/// <summary>
/// View model for a leaderboard entry.
/// </summary>
public record RatLeaderboardEntryViewModel
{
    /// <summary>
    /// Gets the rank on the leaderboard.
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    /// Gets the user ID.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    /// Gets the username.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Gets the guilty count.
    /// </summary>
    public int GuiltyCount { get; init; }

    /// <summary>
    /// Creates a RatLeaderboardEntryViewModel from a DTO.
    /// </summary>
    public static RatLeaderboardEntryViewModel FromDto(RatLeaderboardEntryDto dto)
    {
        return new RatLeaderboardEntryViewModel
        {
            Rank = dto.Rank,
            UserId = dto.UserId,
            Username = dto.Username,
            GuiltyCount = dto.GuiltyCount
        };
    }
}
