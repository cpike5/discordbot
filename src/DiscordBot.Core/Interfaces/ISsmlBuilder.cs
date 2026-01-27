namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Builder interface for constructing SSML markup programmatically.
/// Provides a fluent API for creating complex SSML documents.
/// </summary>
public interface ISsmlBuilder
{
    /// <summary>
    /// Starts a new SSML document.
    /// </summary>
    /// <param name="language">The language code (e.g., "en-US", "fr-FR"). Default is "en-US".</param>
    /// <returns>This builder instance for method chaining.</returns>
    ISsmlBuilder BeginDocument(string language = "en-US");

    /// <summary>
    /// Adds a voice section with the specified voice.
    /// </summary>
    /// <param name="voiceName">The voice name (e.g., "en-US-JennyNeural", "fr-FR-DeniseNeural").</param>
    /// <returns>This builder instance for method chaining.</returns>
    ISsmlBuilder WithVoice(string voiceName);

    /// <summary>
    /// Adds prosody controls (rate, pitch, volume, contour).
    /// </summary>
    /// <param name="rate">Speech rate (0.5 to 2.0, or as percentage/relative value).</param>
    /// <param name="pitch">Pitch adjustment (0.5 to 1.5, or as Hz/relative value).</param>
    /// <param name="volume">Volume level (0 to 100, or as dB value).</param>
    /// <param name="contour">Pitch contour specification for intonation control.</param>
    /// <returns>This builder instance for method chaining.</returns>
    ISsmlBuilder WithProsody(
        double? rate = null,
        double? pitch = null,
        double? volume = null,
        string? contour = null);

    /// <summary>
    /// Adds speaking style (requires style-capable voice like Jenny, Aria, Guy, Davis).
    /// </summary>
    /// <param name="style">Style name (cheerful, sad, angry, excited, friendly, terrified, shouting, unfriendly, whispering, hopeful).</param>
    /// <param name="degree">Style intensity (0.01-2.0, default 1.0).</param>
    /// <returns>This builder instance for method chaining.</returns>
    ISsmlBuilder WithStyle(string style, double degree = 1.0);

    /// <summary>
    /// Adds role/personality (e.g., "YoungAdultFemale", "OlderAdultMale").
    /// </summary>
    /// <param name="role">The role/personality identifier.</param>
    /// <returns>This builder instance for method chaining.</returns>
    ISsmlBuilder WithRole(string role);

    /// <summary>
    /// Adds plain text content (automatically escaped).
    /// </summary>
    /// <param name="text">The text to add to the document.</param>
    /// <returns>This builder instance for method chaining.</returns>
    ISsmlBuilder AddText(string text);

    /// <summary>
    /// Adds a break/pause.
    /// </summary>
    /// <param name="duration">Duration (e.g., "500ms", "1s", "weak", "medium", "strong").</param>
    /// <returns>This builder instance for method chaining.</returns>
    ISsmlBuilder AddBreak(string duration);

    /// <summary>
    /// Adds emphasis to text.
    /// </summary>
    /// <param name="text">Text to emphasize.</param>
    /// <param name="level">Emphasis level (reduced, none, moderate, strong). Default is "moderate".</param>
    /// <returns>This builder instance for method chaining.</returns>
    ISsmlBuilder AddEmphasis(string text, string level = "moderate");

    /// <summary>
    /// Adds say-as interpretation.
    /// </summary>
    /// <param name="text">Text to interpret.</param>
    /// <param name="interpretAs">Interpretation type (date, time, telephone, cardinal, ordinal, currency, etc.).</param>
    /// <param name="format">Optional format specifier (e.g., "dmy" for dates, "12hour" for time).</param>
    /// <returns>This builder instance for method chaining.</returns>
    ISsmlBuilder AddSayAs(string text, string interpretAs, string? format = null);

    /// <summary>
    /// Adds phoneme pronunciation.
    /// </summary>
    /// <param name="text">Text to pronounce.</param>
    /// <param name="alphabet">Phonetic alphabet (ipa or x-sampa).</param>
    /// <param name="ph">Phonetic spelling.</param>
    /// <returns>This builder instance for method chaining.</returns>
    ISsmlBuilder AddPhoneme(string text, string alphabet, string ph);

    /// <summary>
    /// Adds word substitution.
    /// </summary>
    /// <param name="alias">Text to speak.</param>
    /// <param name="text">Text to display.</param>
    /// <returns>This builder instance for method chaining.</returns>
    ISsmlBuilder AddSubstitution(string alias, string text);

    /// <summary>
    /// Closes the current voice section.
    /// </summary>
    /// <returns>This builder instance for method chaining.</returns>
    ISsmlBuilder EndVoice();

    /// <summary>
    /// Closes the current prosody section.
    /// </summary>
    /// <returns>This builder instance for method chaining.</returns>
    ISsmlBuilder EndProsody();

    /// <summary>
    /// Closes the current style section.
    /// </summary>
    /// <returns>This builder instance for method chaining.</returns>
    ISsmlBuilder EndStyle();

    /// <summary>
    /// Builds the final SSML string.
    /// </summary>
    /// <returns>Valid SSML markup.</returns>
    string Build();

    /// <summary>
    /// Resets the builder to start a new document.
    /// </summary>
    /// <returns>This builder instance for method chaining.</returns>
    ISsmlBuilder Reset();
}
