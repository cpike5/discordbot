namespace DiscordBot.Core.DTOs.Soundboard;

/// <summary>
/// Represents the manifest for a soundboard export archive.
/// Contains metadata about the export and all included sounds.
/// </summary>
public class SoundboardExportManifestDto
{
    /// <summary>
    /// Gets or sets the timestamp when this export was created (ISO8601 UTC format).
    /// </summary>
    public string ExportedAt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord guild ID this export is from.
    /// </summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord guild name this export is from.
    /// </summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of sounds included in this export.
    /// </summary>
    public int TotalSounds { get; set; }

    /// <summary>
    /// Gets or sets the total size of all sound files in bytes.
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the collection of sounds included in this export.
    /// </summary>
    public List<ExportedSoundDto> Sounds { get; set; } = new();
}
