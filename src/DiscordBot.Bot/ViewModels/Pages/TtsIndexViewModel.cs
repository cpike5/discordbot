using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for a single TTS message in the history list.
/// </summary>
public record TtsMessageViewModel
{
    /// <summary>
    /// Gets the unique identifier for this TTS message.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the Discord user ID who sent the message.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    /// Gets the username at the time the message was sent.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Gets the message text content.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the voice used for TTS.
    /// </summary>
    public string Voice { get; init; } = string.Empty;

    /// <summary>
    /// Gets the duration in seconds.
    /// </summary>
    public double DurationSeconds { get; init; }

    /// <summary>
    /// Gets the duration formatted for display (e.g., "0:02", "1:30").
    /// </summary>
    public string DurationFormatted { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when this message was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets the created timestamp in ISO format for client-side rendering.
    /// </summary>
    public string CreatedAtUtcIso => DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc).ToString("o");

    /// <summary>
    /// Creates a TtsMessageViewModel from a TtsMessageDto.
    /// </summary>
    public static TtsMessageViewModel FromDto(TtsMessageDto dto)
    {
        return new TtsMessageViewModel
        {
            Id = dto.Id,
            UserId = dto.UserId,
            Username = dto.Username,
            Message = dto.Message,
            Voice = dto.Voice,
            DurationSeconds = dto.DurationSeconds,
            DurationFormatted = FormatDuration(dto.DurationSeconds),
            CreatedAt = dto.CreatedAt
        };
    }

    /// <summary>
    /// Formats a duration in seconds to a human-readable string (e.g., "0:02", "1:30").
    /// </summary>
    private static string FormatDuration(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);

        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";

        return $"{ts.Minutes}:{ts.Seconds:D2}";
    }
}

/// <summary>
/// View model for TTS statistics.
/// </summary>
public record TtsStatsViewModel
{
    /// <summary>
    /// Gets the number of TTS messages sent today.
    /// </summary>
    public int MessagesToday { get; init; }

    /// <summary>
    /// Gets the total playback duration in seconds.
    /// </summary>
    public double TotalPlaybackSeconds { get; init; }

    /// <summary>
    /// Gets the total playback formatted as hours (e.g., "2.3h").
    /// </summary>
    public string TotalPlaybackFormatted { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of unique users who sent TTS messages.
    /// </summary>
    public int UniqueUsers { get; init; }

    /// <summary>
    /// Gets the most used voice identifier.
    /// </summary>
    public string? MostUsedVoice { get; init; }

    /// <summary>
    /// Creates a TtsStatsViewModel from a TtsStatsDto.
    /// </summary>
    public static TtsStatsViewModel FromDto(TtsStatsDto dto)
    {
        var totalHours = dto.TotalPlaybackSeconds / 3600.0;

        return new TtsStatsViewModel
        {
            MessagesToday = dto.MessagesToday,
            TotalPlaybackSeconds = dto.TotalPlaybackSeconds,
            TotalPlaybackFormatted = $"{totalHours:F1}h",
            UniqueUsers = dto.UniqueUsers,
            MostUsedVoice = dto.MostUsedVoice
        };
    }
}

/// <summary>
/// View model for the TTS management page.
/// </summary>
public record TtsIndexViewModel
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
    /// Gets the TTS statistics.
    /// </summary>
    public TtsStatsViewModel Stats { get; init; } = new();

    /// <summary>
    /// Gets the list of recent TTS messages.
    /// </summary>
    public List<TtsMessageViewModel> RecentMessages { get; init; } = new();

    /// <summary>
    /// Gets the guild TTS settings.
    /// </summary>
    public GuildTtsSettings Settings { get; init; } = new();

    /// <summary>
    /// Gets the maximum message length.
    /// </summary>
    public int MaxMessageLength => Settings.MaxMessageLength;

    /// <summary>
    /// Creates a TtsIndexViewModel from service data.
    /// </summary>
    public static TtsIndexViewModel Create(
        ulong guildId,
        string guildName,
        string? guildIconUrl,
        TtsStatsDto stats,
        IEnumerable<TtsMessageDto> recentMessages,
        GuildTtsSettings settings)
    {
        return new TtsIndexViewModel
        {
            GuildId = guildId,
            GuildName = guildName,
            GuildIconUrl = guildIconUrl,
            Stats = TtsStatsViewModel.FromDto(stats),
            RecentMessages = recentMessages.Select(TtsMessageViewModel.FromDto).ToList(),
            Settings = settings
        };
    }
}
