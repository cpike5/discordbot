using DiscordBot.Core.Models;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Provides access to voice capability information with caching support.
/// </summary>
public interface IVoiceCapabilityProvider
{
    /// <summary>
    /// Gets the capabilities for a specific voice.
    /// </summary>
    /// <param name="voiceName">Voice short name (e.g., "en-US-JennyNeural").</param>
    /// <returns>Voice capabilities if known, null if voice is unknown.</returns>
    VoiceCapabilities? GetCapabilities(string voiceName);

    /// <summary>
    /// Checks if a voice supports a specific speaking style.
    /// </summary>
    /// <param name="voiceName">Voice short name (e.g., "en-US-JennyNeural").</param>
    /// <param name="style">Style name (e.g., "cheerful", "angry").</param>
    /// <returns>True if voice supports the style, false otherwise or if voice is unknown.</returns>
    bool IsStyleSupported(string voiceName, string style);

    /// <summary>
    /// Gets all supported speaking styles for a voice.
    /// </summary>
    /// <param name="voiceName">Voice short name (e.g., "en-US-JennyNeural").</param>
    /// <returns>List of supported styles, or empty list if voice is unknown.</returns>
    IReadOnlyList<string> GetSupportedStyles(string voiceName);

    /// <summary>
    /// Gets all known voice capabilities.
    /// </summary>
    /// <returns>Enumerable of all registered voice capabilities.</returns>
    IEnumerable<VoiceCapabilities> GetAllKnownVoices();
}
