namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for the Audio pages navigation tabs component.
/// Used to switch between Soundboard and Audio Settings pages.
/// </summary>
public class AudioTabsViewModel
{
    /// <summary>
    /// The guild ID for building navigation URLs.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// The currently active tab identifier.
    /// Valid values: "soundboard", "tts", "settings"
    /// </summary>
    public string ActiveTab { get; set; } = "soundboard";

    /// <summary>
    /// Optional: Number of sounds to display as a badge on the Soundboard tab.
    /// Set to null or 0 to hide the badge.
    /// </summary>
    public int? SoundCount { get; set; }
}
