namespace DiscordBot.Core.Enums;

/// <summary>
/// Represents the type of audio feature used for playback.
/// Used in the audio moderation log to classify playback events.
/// </summary>
public enum AudioFeatureType
{
    /// <summary>
    /// A soundboard sound effect was played.
    /// </summary>
    Soundboard,

    /// <summary>
    /// A text-to-speech message was played.
    /// </summary>
    Tts,

    /// <summary>
    /// A VOX/FVOX/HGRUNT clip announcement was played.
    /// </summary>
    Vox
}
