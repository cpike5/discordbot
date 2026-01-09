namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the soundboard and audio features.
/// </summary>
public class SoundboardOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "Soundboard";

    /// <summary>
    /// Gets or sets the base path for sound file storage.
    /// Default is "./sounds". Use forward slashes for cross-platform compatibility;
    /// paths are normalized at runtime using Path.Combine.
    /// </summary>
    /// <remarks>
    /// Per-guild subfolders are created under this path: {BasePath}/{guildId}/
    /// </remarks>
    public string BasePath { get; set; } = "./sounds";

    /// <summary>
    /// Gets or sets the path to the FFmpeg executable.
    /// Default is null, which means FFmpeg is expected to be in the system PATH.
    /// </summary>
    /// <remarks>
    /// Windows: Usually "C:/ffmpeg/bin/ffmpeg.exe" or just "ffmpeg" if in PATH.
    /// Linux: Usually "/usr/bin/ffmpeg" or just "ffmpeg" if in PATH.
    /// Docker: Typically "ffmpeg" as it's installed in PATH.
    /// </remarks>
    public string? FfmpegPath { get; set; }

    /// <summary>
    /// Gets or sets the path to the FFprobe executable (for audio metadata extraction).
    /// Default is null, which means FFprobe is expected to be in the system PATH.
    /// </summary>
    /// <remarks>
    /// FFprobe is typically installed alongside FFmpeg and is used to extract
    /// audio duration and format information.
    /// </remarks>
    public string? FfprobePath { get; set; }

    /// <summary>
    /// Gets or sets the default maximum duration for sounds in seconds.
    /// Default is 30 seconds.
    /// </summary>
    public int DefaultMaxDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the default maximum file size for sounds in bytes.
    /// Default is 10MB (10,485,760 bytes).
    /// </summary>
    public long DefaultMaxFileSizeBytes { get; set; } = 10_485_760;

    /// <summary>
    /// Gets or sets the default maximum number of sounds per guild.
    /// Default is 100.
    /// </summary>
    public int DefaultMaxSoundsPerGuild { get; set; } = 100;

    /// <summary>
    /// Gets or sets the default total storage limit per guild in bytes.
    /// Default is 500MB (524,288,000 bytes).
    /// </summary>
    public long DefaultMaxStorageBytes { get; set; } = 524_288_000;

    /// <summary>
    /// Gets or sets the default auto-leave timeout in minutes.
    /// 0 means the bot will stay in the voice channel indefinitely.
    /// Default is 0 (stay indefinitely).
    /// </summary>
    public int DefaultAutoLeaveTimeoutMinutes { get; set; } = 0;

    /// <summary>
    /// Gets or sets the supported audio file formats.
    /// Default is mp3, wav, and ogg.
    /// </summary>
    public string[] SupportedFormats { get; set; } = ["mp3", "wav", "ogg"];
}
