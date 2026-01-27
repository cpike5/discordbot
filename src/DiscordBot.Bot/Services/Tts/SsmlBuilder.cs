using System.Security;
using System.Text;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.Tts;

/// <summary>
/// Builder implementation for constructing SSML markup programmatically.
/// Provides a fluent API for creating complex SSML documents with validation.
/// </summary>
public class SsmlBuilder : ISsmlBuilder
{
    private readonly ILogger<SsmlBuilder> _logger;
    private readonly StringBuilder _content;
    private readonly Stack<string> _elementStack;
    private string _language;
    private string? _pendingVoiceRole;

    // Constants for element names
    private const string ElementVoice = "voice";
    private const string ElementProsody = "prosody";
    private const string ElementStyle = "mstts:express-as";

    public SsmlBuilder(ILogger<SsmlBuilder> logger)
    {
        _logger = logger;
        _content = new StringBuilder();
        _elementStack = new Stack<string>();
        _language = "en-US";
    }

    /// <inheritdoc/>
    public ISsmlBuilder BeginDocument(string language = "en-US")
    {
        _logger.LogDebug("Beginning SSML document with language {Language}", language);

        _language = language;
        _content.Clear();
        _elementStack.Clear();

        _content.Append($"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xmlns:mstts=\"https://www.w3.org/2001/mstts\" xml:lang=\"{EscapeXml(language)}\">");

        return this;
    }

    /// <inheritdoc/>
    public ISsmlBuilder WithVoice(string voiceName)
    {
        if (string.IsNullOrWhiteSpace(voiceName))
            throw new ArgumentException("Voice name cannot be null or whitespace.", nameof(voiceName));

        _logger.LogDebug("Adding voice element: {VoiceName}", voiceName);

        // Build voice tag with optional role attribute
        var roleAttr = string.IsNullOrWhiteSpace(_pendingVoiceRole)
            ? ""
            : $" role=\"{EscapeXml(_pendingVoiceRole)}\"";

        _content.Append($"<voice name=\"{EscapeXml(voiceName)}\"{roleAttr}>");
        _elementStack.Push(ElementVoice);

        // Clear pending role after use
        _pendingVoiceRole = null;

        return this;
    }

    /// <inheritdoc/>
    public ISsmlBuilder WithProsody(
        double? rate = null,
        double? pitch = null,
        double? volume = null,
        string? contour = null)
    {
        _logger.LogDebug(
            "Adding prosody element: Rate={Rate}, Pitch={Pitch}, Volume={Volume}, Contour={Contour}",
            rate, pitch, volume, contour);

        // Validate Azure Speech Service ranges
        if (rate.HasValue && (rate.Value < 0.5 || rate.Value > 2.0))
            throw new ArgumentOutOfRangeException(nameof(rate), "Rate must be between 0.5 and 2.0.");

        if (pitch.HasValue && (pitch.Value < 0.5 || pitch.Value > 1.5))
            throw new ArgumentOutOfRangeException(nameof(pitch), "Pitch must be between 0.5 and 1.5.");

        if (volume.HasValue && (volume.Value < 0 || volume.Value > 100))
            throw new ArgumentOutOfRangeException(nameof(volume), "Volume must be between 0 and 100.");

        var attributes = new List<string>();

        if (rate.HasValue && Math.Abs(rate.Value - 1.0) > 0.001)
        {
            var rateValue = rate.Value >= 1.0
                ? $"+{(int)((rate.Value - 1.0) * 100)}%"
                : $"-{(int)((1.0 - rate.Value) * 100)}%";
            attributes.Add($"rate=\"{rateValue}\"");
        }

        if (pitch.HasValue && Math.Abs(pitch.Value - 1.0) > 0.001)
        {
            var pitchValue = pitch.Value >= 1.0
                ? $"+{(int)((pitch.Value - 1.0) * 100)}%"
                : $"-{(int)((1.0 - pitch.Value) * 100)}%";
            attributes.Add($"pitch=\"{pitchValue}\"");
        }

        if (volume.HasValue)
        {
            // Volume can be 0-100 or keywords like "default", "silent", "x-soft", "soft", "medium", "loud", "x-loud"
            attributes.Add($"volume=\"{volume.Value:F0}\"");
        }

        if (!string.IsNullOrWhiteSpace(contour))
        {
            attributes.Add($"contour=\"{EscapeXml(contour)}\"");
        }

        if (attributes.Count > 0)
        {
            _content.Append($"<prosody {string.Join(" ", attributes)}>");
            _elementStack.Push(ElementProsody);
        }
        else
        {
            _logger.LogDebug("WithProsody called with no attributes; no prosody element was added");
        }

        return this;
    }

    /// <inheritdoc/>
    public ISsmlBuilder WithStyle(string style, double degree = 1.0)
    {
        if (string.IsNullOrWhiteSpace(style))
            throw new ArgumentException("Style cannot be null or whitespace.", nameof(style));

        if (degree < 0.01 || degree > 2.0)
            throw new ArgumentException("Style degree must be between 0.01 and 2.0.", nameof(degree));

        _logger.LogDebug("Adding style element: Style={Style}, Degree={Degree}", style, degree);

        _content.Append($"<mstts:express-as style=\"{EscapeXml(style)}\" styledegree=\"{degree:F2}\">");
        _elementStack.Push(ElementStyle);

        return this;
    }

    /// <inheritdoc/>
    public ISsmlBuilder WithRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be null or whitespace.", nameof(role));

        _logger.LogDebug("Setting pending role attribute for next voice element: {Role}", role);

        // Store the role to be added when the next voice element is created
        // This ensures the role attribute is properly included in the opening <voice> tag
        _pendingVoiceRole = role;

        return this;
    }

    /// <inheritdoc/>
    public ISsmlBuilder AddText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return this;

        _logger.LogDebug("Adding text content: {TextLength} characters", text.Length);

        _content.Append(EscapeXml(text));

        return this;
    }

    /// <inheritdoc/>
    public ISsmlBuilder AddBreak(string duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
            throw new ArgumentException("Break duration cannot be null or whitespace.", nameof(duration));

        _logger.LogDebug("Adding break element: {Duration}", duration);

        _content.Append($"<break time=\"{EscapeXml(duration)}\"/>");

        return this;
    }

    /// <inheritdoc/>
    public ISsmlBuilder AddEmphasis(string text, string level = "moderate")
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Emphasis text cannot be null or whitespace.", nameof(text));

        if (string.IsNullOrWhiteSpace(level))
            level = "moderate";

        _logger.LogDebug("Adding emphasis element: Text={TextLength} characters, Level={Level}", text.Length, level);

        _content.Append($"<emphasis level=\"{EscapeXml(level)}\">{EscapeXml(text)}</emphasis>");

        return this;
    }

    /// <inheritdoc/>
    public ISsmlBuilder AddSayAs(string text, string interpretAs, string? format = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Say-as text cannot be null or whitespace.", nameof(text));

        if (string.IsNullOrWhiteSpace(interpretAs))
            throw new ArgumentException("Interpret-as type cannot be null or whitespace.", nameof(interpretAs));

        _logger.LogDebug(
            "Adding say-as element: Text={TextLength} characters, InterpretAs={InterpretAs}, Format={Format}",
            text.Length, interpretAs, format);

        var formatAttr = string.IsNullOrWhiteSpace(format) ? "" : $" format=\"{EscapeXml(format)}\"";
        _content.Append($"<say-as interpret-as=\"{EscapeXml(interpretAs)}\"{formatAttr}>{EscapeXml(text)}</say-as>");

        return this;
    }

    /// <inheritdoc/>
    public ISsmlBuilder AddPhoneme(string text, string alphabet, string ph)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Phoneme text cannot be null or whitespace.", nameof(text));

        if (string.IsNullOrWhiteSpace(alphabet))
            throw new ArgumentException("Phonetic alphabet cannot be null or whitespace.", nameof(alphabet));

        if (string.IsNullOrWhiteSpace(ph))
            throw new ArgumentException("Phonetic spelling cannot be null or whitespace.", nameof(ph));

        _logger.LogDebug(
            "Adding phoneme element: Text={Text}, Alphabet={Alphabet}, Phoneme={Phoneme}",
            text, alphabet, ph);

        _content.Append($"<phoneme alphabet=\"{EscapeXml(alphabet)}\" ph=\"{EscapeXml(ph)}\">{EscapeXml(text)}</phoneme>");

        return this;
    }

    /// <inheritdoc/>
    public ISsmlBuilder AddSubstitution(string alias, string text)
    {
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Substitution alias cannot be null or whitespace.", nameof(alias));

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Substitution text cannot be null or whitespace.", nameof(text));

        _logger.LogDebug("Adding substitution element: Alias={Alias}, Text={Text}", alias, text);

        _content.Append($"<sub alias=\"{EscapeXml(alias)}\">{EscapeXml(text)}</sub>");

        return this;
    }

    /// <inheritdoc/>
    public ISsmlBuilder EndVoice()
    {
        _logger.LogDebug("Ending voice element");

        EnsureElementOpen(ElementVoice, "Cannot end voice element: no voice element is open.");

        // Close all nested elements within voice first
        while (_elementStack.Count > 0 && _elementStack.Peek() != ElementVoice)
        {
            var element = _elementStack.Pop();
            _content.Append(GetClosingTag(element));
            _logger.LogDebug("Auto-closing nested element {Element} before ending voice", element);
        }

        // Pop and close voice
        _elementStack.Pop();
        _content.Append("</voice>");

        return this;
    }

    /// <inheritdoc/>
    public ISsmlBuilder EndProsody()
    {
        _logger.LogDebug("Ending prosody element");

        EnsureElementOpen(ElementProsody, "Cannot end prosody element: no prosody element is open.");

        _elementStack.Pop();
        _content.Append("</prosody>");

        return this;
    }

    /// <inheritdoc/>
    public ISsmlBuilder EndStyle()
    {
        _logger.LogDebug("Ending style element");

        EnsureElementOpen(ElementStyle, "Cannot end style element: no style element is open.");

        _elementStack.Pop();
        _content.Append("</mstts:express-as>");

        return this;
    }

    /// <inheritdoc/>
    public string Build()
    {
        _logger.LogDebug("Building SSML document");

        // Close any remaining open elements
        while (_elementStack.Count > 0)
        {
            var element = _elementStack.Pop();
            _content.Append(GetClosingTag(element));
            _logger.LogWarning("Auto-closing unclosed element {Element} during build", element);
        }

        // Close speak tag
        _content.Append("</speak>");

        var result = _content.ToString();

        _logger.LogDebug("Built SSML document: {Length} characters", result.Length);

        return result;
    }

    /// <inheritdoc/>
    public ISsmlBuilder Reset()
    {
        _logger.LogDebug("Resetting builder");

        _content.Clear();
        _elementStack.Clear();
        _language = "en-US";
        _pendingVoiceRole = null;

        return this;
    }

    /// <summary>
    /// Escapes XML special characters for safe inclusion in SSML.
    /// </summary>
    private static string EscapeXml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return SecurityElement.Escape(text) ?? text;
    }

    /// <summary>
    /// Ensures that a specific element type is currently open.
    /// </summary>
    private void EnsureElementOpen(string elementName, string errorMessage)
    {
        if (_elementStack.Count == 0 || _elementStack.Peek() != elementName)
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    /// <summary>
    /// Gets the closing tag for a given element name.
    /// </summary>
    private static string GetClosingTag(string elementName)
    {
        return elementName switch
        {
            ElementVoice => "</voice>",
            ElementProsody => "</prosody>",
            ElementStyle => "</mstts:express-as>",
            _ => $"</{elementName}>"
        };
    }
}
