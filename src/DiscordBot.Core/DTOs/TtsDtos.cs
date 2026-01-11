namespace DiscordBot.Core.DTOs;

/// <summary>
/// DTO for TTS message statistics for a guild.
/// </summary>
public class TtsStatsDto
{
    /// <summary>
    /// Gets or sets the guild ID these stats are for.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the number of TTS messages sent today.
    /// </summary>
    public int MessagesToday { get; set; }

    /// <summary>
    /// Gets or sets the total number of TTS messages all time.
    /// </summary>
    public int TotalMessages { get; set; }

    /// <summary>
    /// Gets or sets the total playback duration in seconds for the period.
    /// </summary>
    public double TotalPlaybackSeconds { get; set; }

    /// <summary>
    /// Gets or sets the number of unique users who sent TTS messages.
    /// </summary>
    public int UniqueUsers { get; set; }

    /// <summary>
    /// Gets or sets the most used voice identifier.
    /// </summary>
    public string? MostUsedVoice { get; set; }

    /// <summary>
    /// Gets or sets the user ID who sent the most messages.
    /// </summary>
    public ulong? TopUserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the top user.
    /// </summary>
    public string? TopUsername { get; set; }

    /// <summary>
    /// Gets or sets the message count for the top user.
    /// </summary>
    public int TopUserMessageCount { get; set; }
}

/// <summary>
/// DTO for a TTS message in history views.
/// </summary>
public class TtsMessageDto
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the Discord user ID who sent the message.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the username at the time the message was sent.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message text content.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the voice used for TTS.
    /// </summary>
    public string Voice { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the duration in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets when the message was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
