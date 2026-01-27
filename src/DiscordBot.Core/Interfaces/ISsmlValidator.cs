using DiscordBot.Core.Models;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Validator for SSML markup. Checks syntax, structure, and Azure-specific constraints.
/// </summary>
public interface ISsmlValidator
{
    /// <summary>
    /// Validates SSML markup.
    /// </summary>
    /// <param name="ssml">SSML markup to validate.</param>
    /// <returns>Validation result with details about errors, warnings, and detected voices.</returns>
    SsmlValidationResult Validate(string ssml);

    /// <summary>
    /// Attempts to sanitize/fix common SSML issues.
    /// </summary>
    /// <param name="ssml">Potentially invalid SSML.</param>
    /// <returns>Sanitized SSML or original if no fixes possible.</returns>
    string Sanitize(string ssml);

    /// <summary>
    /// Checks if a voice supports a specific style.
    /// </summary>
    /// <param name="voiceName">Voice short name (e.g., "en-US-JennyNeural").</param>
    /// <param name="style">Style name (e.g., "cheerful", "angry").</param>
    /// <returns>True if voice supports the style, false otherwise.</returns>
    bool IsStyleSupported(string voiceName, string style);

    /// <summary>
    /// Extracts plain text from SSML (strips all markup).
    /// </summary>
    /// <param name="ssml">SSML markup.</param>
    /// <returns>Plain text content without any XML markup.</returns>
    string ExtractPlainText(string ssml);
}
