namespace DiscordBot.Core.Entities;

/// <summary>
/// Per-guild configuration settings for TTS (Text-to-Speech) features.
/// </summary>
public class GuildTtsSettings
{
    /// <summary>
    /// Gets or sets the Discord guild snowflake ID (serves as primary key).
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets whether TTS is enabled for this guild.
    /// </summary>
    public bool TtsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the default voice identifier for TTS messages.
    /// </summary>
    public string DefaultVoice { get; set; } = "en-US-JennyNeural";

    /// <summary>
    /// Gets or sets the default speech speed multiplier (1.0 = normal speed).
    /// </summary>
    public double DefaultSpeed { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the default pitch adjustment (1.0 = normal pitch).
    /// </summary>
    public double DefaultPitch { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the default volume level (0.0 to 1.0).
    /// </summary>
    public double DefaultVolume { get; set; } = 0.8;

    /// <summary>
    /// Gets or sets the maximum character limit for TTS messages.
    /// </summary>
    public int MaxMessageLength { get; set; } = 500;

    /// <summary>
    /// Gets or sets the maximum number of TTS messages per user per minute.
    /// </summary>
    public int RateLimitPerMinute { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether to automatically play TTS when sending from the UI.
    /// </summary>
    public bool AutoPlayOnSend { get; set; }

    /// <summary>
    /// Gets or sets whether to announce member join/leave events via TTS.
    /// </summary>
    public bool AnnounceJoinsLeaves { get; set; }

    /// <summary>
    /// Gets or sets whether SSML markup is enabled for TTS in this guild.
    /// </summary>
    public bool SsmlEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether to enforce strict SSML validation.
    /// When true, invalid SSML will be rejected rather than falling back to plain text.
    /// </summary>
    public bool StrictSsmlValidation { get; set; }

    /// <summary>
    /// Gets or sets the maximum SSML complexity score allowed.
    /// Higher values allow more nested elements and prosody modifications.
    /// </summary>
    public int MaxSsmlComplexity { get; set; } = 50;

    /// <summary>
    /// Gets or sets the default SSML style for voices that support styles (e.g., "cheerful", "sad").
    /// </summary>
    public string? DefaultStyle { get; set; }

    /// <summary>
    /// Gets or sets the default style intensity (0.01 to 2.0, where 1.0 is normal).
    /// </summary>
    public double DefaultStyleDegree { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the timestamp when these settings were created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when these settings were last updated (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild these settings belong to.
    /// </summary>
    public Guild? Guild { get; set; }
}
