namespace DiscordBot.Core.Enums;

/// <summary>
/// Represents audio filter presets that can be applied to soundboard playback.
/// </summary>
public enum AudioFilter
{
    /// <summary>
    /// No audio filter applied.
    /// </summary>
    None = 0,

    /// <summary>
    /// Adds a reverb/echo effect to the audio.
    /// </summary>
    Reverb = 1,

    /// <summary>
    /// Boosts bass frequencies for deeper sound.
    /// </summary>
    BassBoost = 2,

    /// <summary>
    /// Boosts treble frequencies for brighter sound.
    /// </summary>
    TrebleBoost = 3,

    /// <summary>
    /// Raises the pitch of the audio.
    /// </summary>
    PitchUp = 4,

    /// <summary>
    /// Lowers the pitch of the audio.
    /// </summary>
    PitchDown = 5,

    /// <summary>
    /// Nightcore effect - higher pitch and faster tempo.
    /// </summary>
    Nightcore = 6,

    /// <summary>
    /// Slows down the audio playback.
    /// </summary>
    SlowMo = 7
}
