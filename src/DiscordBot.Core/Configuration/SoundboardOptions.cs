namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the soundboard feature.
/// </summary>
public class SoundboardOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "Soundboard";

    /// <summary>
    /// Gets or sets the base path for sound file storage, relative to the application root.
    /// Default is "sounds".
    /// </summary>
    public string BasePath { get; set; } = "sounds";

    /// <summary>
    /// Gets or sets the default maximum duration for sound files in seconds.
    /// Default is 30 seconds.
    /// </summary>
    public int DefaultMaxDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the default maximum file size for uploads in bytes.
    /// Default is 5,242,880 bytes (5 MB).
    /// </summary>
    public long DefaultMaxFileSizeBytes { get; set; } = 5_242_880;

    /// <summary>
    /// Gets or sets the default maximum number of sounds allowed per guild.
    /// Default is 50.
    /// </summary>
    public int DefaultMaxSoundsPerGuild { get; set; } = 50;

    /// <summary>
    /// Gets or sets the default total storage limit per guild in bytes.
    /// Default is 104,857,600 bytes (100 MB).
    /// </summary>
    public long DefaultMaxStorageBytes { get; set; } = 104_857_600;

    /// <summary>
    /// Gets or sets the default timeout in minutes before the bot automatically leaves a voice channel due to inactivity.
    /// Default is 5 minutes.
    /// </summary>
    public int DefaultAutoLeaveTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the list of supported audio file formats (including the dot).
    /// Default is [".mp3", ".wav", ".ogg", ".m4a"].
    /// </summary>
    public List<string> SupportedFormats { get; set; } = new() { ".mp3", ".wav", ".ogg", ".m4a" };
}
