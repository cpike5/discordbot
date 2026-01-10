using DiscordBot.Core.Enums;
using DiscordBot.Core.Models;

namespace DiscordBot.Core.Constants;

/// <summary>
/// Static definitions for audio filter presets and their FFmpeg mappings.
/// </summary>
public static class AudioFilters
{
    /// <summary>
    /// Dictionary mapping audio filter enum values to their definitions.
    /// </summary>
    public static readonly IReadOnlyDictionary<AudioFilter, AudioFilterDefinition> Definitions =
        new Dictionary<AudioFilter, AudioFilterDefinition>
        {
            [AudioFilter.None] = new AudioFilterDefinition
            {
                Name = "None",
                Description = "No audio filter applied",
                FfmpegFilter = string.Empty
            },
            [AudioFilter.Reverb] = new AudioFilterDefinition
            {
                Name = "Reverb",
                Description = "Adds a reverb/echo effect to the audio",
                FfmpegFilter = "aecho=0.8:0.9:40:0.4"
            },
            [AudioFilter.BassBoost] = new AudioFilterDefinition
            {
                Name = "Bass Boost",
                Description = "Boosts bass frequencies for deeper sound",
                FfmpegFilter = "equalizer=f=60:width_type=h:width=50:g=10"
            },
            [AudioFilter.TrebleBoost] = new AudioFilterDefinition
            {
                Name = "Treble Boost",
                Description = "Boosts treble frequencies for brighter sound",
                FfmpegFilter = "equalizer=f=3000:width_type=h:width=200:g=5"
            },
            [AudioFilter.PitchUp] = new AudioFilterDefinition
            {
                Name = "Pitch Up",
                Description = "Raises the pitch of the audio",
                FfmpegFilter = "asetrate=48000*1.25,aresample=48000"
            },
            [AudioFilter.PitchDown] = new AudioFilterDefinition
            {
                Name = "Pitch Down",
                Description = "Lowers the pitch of the audio",
                FfmpegFilter = "asetrate=48000*0.75,aresample=48000"
            },
            [AudioFilter.Nightcore] = new AudioFilterDefinition
            {
                Name = "Nightcore",
                Description = "Nightcore effect - higher pitch and faster tempo",
                FfmpegFilter = "asetrate=48000*1.25,aresample=48000,atempo=1.25"
            },
            [AudioFilter.SlowMo] = new AudioFilterDefinition
            {
                Name = "Slow Mo",
                Description = "Slows down the audio playback",
                FfmpegFilter = "atempo=0.8"
            }
        };

    /// <summary>
    /// Gets the FFmpeg filter string for a given audio filter.
    /// </summary>
    /// <param name="filter">The audio filter.</param>
    /// <returns>The FFmpeg -af argument string, or empty string if None.</returns>
    public static string GetFfmpegFilter(AudioFilter filter)
    {
        return Definitions.TryGetValue(filter, out var definition)
            ? definition.FfmpegFilter
            : string.Empty;
    }
}
