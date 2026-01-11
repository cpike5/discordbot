namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a TTS (Text-to-Speech) message that was played in a voice channel.
/// Used for tracking TTS history and usage analytics.
/// </summary>
public class TtsMessage
{
    /// <summary>
    /// Gets or sets the unique identifier for this TTS message.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild snowflake ID where the message was played.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID who sent the message.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the username at the time the message was sent.
    /// Stored for display purposes since usernames can change.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text content of the TTS message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the voice identifier used for this TTS message.
    /// </summary>
    public string Voice { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the duration of the audio playback in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the message was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild this message belongs to.
    /// </summary>
    public Guild? Guild { get; set; }
}
