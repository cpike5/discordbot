namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the VOX clip library system.
/// </summary>
public class VoxOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "Vox";

    /// <summary>
    /// Gets or sets the base path for VOX audio files.
    /// </summary>
    public string BasePath { get; set; } = "./sounds";

    /// <summary>
    /// Gets or sets the default word gap in milliseconds.
    /// </summary>
    public int DefaultWordGapMs { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum number of words allowed in a VOX message.
    /// </summary>
    public int MaxMessageWords { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum character length allowed in a VOX message.
    /// </summary>
    public int MaxMessageLength { get; set; } = 500;
}
