using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.Tts;

/// <summary>
/// Validator implementation for SSML markup.
/// Validates syntax, structure, and Azure Speech Service-specific constraints.
/// </summary>
public partial class SsmlValidator : ISsmlValidator
{
    private readonly ILogger<SsmlValidator> _logger;
    private readonly IVoiceCapabilityProvider _voiceCapabilityProvider;
    private readonly int _maxNestingDepth;
    private readonly int _maxDocumentLength;

    // Constants for element names
    private const string ElementSpeak = "speak";
    private const string ElementVoice = "voice";
    private const string ElementProsody = "prosody";
    private const string ElementStyle = "mstts:express-as";
    private const string ElementBreak = "break";
    private const string ElementEmphasis = "emphasis";
    private const string ElementSayAs = "say-as";
    private const string ElementPhoneme = "phoneme";
    private const string ElementSub = "sub";

    // Azure Speech Service limits
    private const double MinRate = 0.5;
    private const double MaxRate = 2.0;
    private const double MinPitch = 0.5;
    private const double MaxPitch = 1.5;
    private const double MinVolume = 0.0;
    private const double MaxVolume = 100.0;

    // Regex patterns for validation
    [GeneratedRegex(@"<script[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ScriptTagRegex();

    [GeneratedRegex(@"javascript:", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex JavaScriptProtocolRegex();

    [GeneratedRegex(@"on\w+\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EventHandlerRegex();

    [GeneratedRegex(@"^\d+m?s$|^(x-weak|weak|medium|strong|x-strong)$", RegexOptions.Compiled)]
    private static partial Regex BreakDurationRegex();

    [GeneratedRegex(@"^[+-]?\d+%?$|^(x-slow|slow|medium|fast|x-fast|default)$", RegexOptions.Compiled)]
    private static partial Regex RateValueRegex();

    [GeneratedRegex(@"^[+-]?\d+%?$|^(x-low|low|medium|high|x-high|default)$", RegexOptions.Compiled)]
    private static partial Regex PitchValueRegex();

    public SsmlValidator(
        ILogger<SsmlValidator> logger,
        IVoiceCapabilityProvider voiceCapabilityProvider,
        int maxNestingDepth = 3,
        int maxDocumentLength = 5000)
    {
        _logger = logger;
        _voiceCapabilityProvider = voiceCapabilityProvider;
        _maxNestingDepth = maxNestingDepth;
        _maxDocumentLength = maxDocumentLength;
    }

    /// <inheritdoc/>
    public SsmlValidationResult Validate(string ssml)
    {
        if (string.IsNullOrWhiteSpace(ssml))
        {
            _logger.LogDebug("Validation failed: SSML is null or whitespace");
            return new SsmlValidationResult
            {
                IsValid = false,
                Errors = new[] { "SSML cannot be null or whitespace." }
            };
        }

        var errors = new List<string>();
        var warnings = new List<string>();
        var detectedVoices = new List<string>();
        double? estimatedDuration = null;
        var plainTextLength = 0;

        try
        {
            // Check document length
            if (ssml.Length > _maxDocumentLength)
            {
                errors.Add($"SSML document exceeds maximum length of {_maxDocumentLength} characters.");
            }

            // Check for script injection attempts
            if (ScriptTagRegex().IsMatch(ssml))
            {
                errors.Add("SSML contains forbidden <script> tags.");
            }

            if (JavaScriptProtocolRegex().IsMatch(ssml))
            {
                errors.Add("SSML contains forbidden javascript: protocol.");
            }

            if (EventHandlerRegex().IsMatch(ssml))
            {
                errors.Add("SSML contains forbidden event handlers (onclick, onload, etc.).");
            }

            // Parse XML
            var xmlDoc = new XmlDocument();
            try
            {
                xmlDoc.LoadXml(ssml);
            }
            catch (XmlException ex)
            {
                errors.Add($"SSML is not well-formed XML: {ex.Message}");
                _logger.LogDebug(ex, "XML parsing failed for SSML validation");

                return new SsmlValidationResult
                {
                    IsValid = false,
                    Errors = errors
                };
            }

            // Check root element
            if (xmlDoc.DocumentElement?.LocalName != ElementSpeak)
            {
                errors.Add($"Root element must be <{ElementSpeak}>, found: {xmlDoc.DocumentElement?.LocalName ?? "none"}");
            }

            // Validate document structure
            ValidateNode(xmlDoc.DocumentElement!, errors, warnings, detectedVoices, ref plainTextLength, 0);

            // Estimate duration (rough approximation: ~150 words per minute, ~5 characters per word)
            if (plainTextLength > 0)
            {
                var estimatedWords = plainTextLength / 5.0;
                estimatedDuration = (estimatedWords / 150.0) * 60.0; // Convert to seconds
            }

            _logger.LogDebug(
                "SSML validation completed: IsValid={IsValid}, Errors={ErrorCount}, Warnings={WarningCount}, Voices={VoiceCount}",
                errors.Count == 0, errors.Count, warnings.Count, detectedVoices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during SSML validation");
            errors.Add($"Unexpected validation error: {ex.Message}");
        }

        return new SsmlValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            DetectedVoices = detectedVoices.Distinct().ToList(),
            EstimatedDurationSeconds = estimatedDuration,
            PlainTextLength = plainTextLength
        };
    }

    /// <inheritdoc/>
    public string Sanitize(string ssml)
    {
        if (string.IsNullOrWhiteSpace(ssml))
        {
            _logger.LogDebug("Sanitize called with null/whitespace SSML");
            return string.Empty;
        }

        _logger.LogDebug("Sanitizing SSML: {Length} characters", ssml.Length);

        var sanitized = ssml;

        // Remove script injection attempts
        sanitized = ScriptTagRegex().Replace(sanitized, string.Empty);
        sanitized = JavaScriptProtocolRegex().Replace(sanitized, string.Empty);
        sanitized = EventHandlerRegex().Replace(sanitized, string.Empty);

        // Remove invalid control characters (except tab, newline, carriage return)
        sanitized = new string(sanitized.Where(c =>
            c == '\t' || c == '\n' || c == '\r' || !char.IsControl(c)).ToArray());

        // Trim excessive whitespace
        sanitized = Regex.Replace(sanitized, @"\s+", " ");
        sanitized = sanitized.Trim();

        // Try to fix XML structure
        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(sanitized);

            // If it parses successfully, return formatted output
            using var stringWriter = new StringWriter();
            using var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings
            {
                Indent = false,
                OmitXmlDeclaration = true
            });

            xmlDoc.Save(xmlWriter);
            sanitized = stringWriter.ToString();

            _logger.LogDebug("SSML sanitization successful");
        }
        catch (XmlException ex)
        {
            _logger.LogWarning(ex, "Could not parse SSML as XML during sanitization, returning cleaned text");

            // If XML parsing fails, try basic fixes
            sanitized = EscapeUnescapedCharacters(sanitized);
        }

        return sanitized;
    }

    /// <inheritdoc/>
    public bool IsStyleSupported(string voiceName, string style)
    {
        if (string.IsNullOrWhiteSpace(voiceName) || string.IsNullOrWhiteSpace(style))
        {
            _logger.LogDebug("IsStyleSupported called with null/whitespace parameters");
            return false;
        }

        var isSupported = _voiceCapabilityProvider.IsStyleSupported(voiceName, style);

        _logger.LogDebug(
            "Style support check: Voice={VoiceName}, Style={Style}, Supported={Supported}",
            voiceName, style, isSupported);

        return isSupported;
    }

    /// <inheritdoc/>
    public string ExtractPlainText(string ssml)
    {
        if (string.IsNullOrWhiteSpace(ssml))
        {
            _logger.LogDebug("ExtractPlainText called with null/whitespace SSML");
            return string.Empty;
        }

        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(ssml);

            var plainText = ExtractTextFromNode(xmlDoc.DocumentElement!);

            // Normalize whitespace
            plainText = Regex.Replace(plainText, @"\s+", " ").Trim();

            _logger.LogDebug("Extracted plain text: {Length} characters", plainText.Length);

            return plainText;
        }
        catch (XmlException ex)
        {
            _logger.LogWarning(ex, "Could not parse SSML for text extraction, attempting text-only extraction");

            // Fallback: strip all XML tags
            return Regex.Replace(ssml, @"<[^>]+>", " ").Trim();
        }
    }

    /// <summary>
    /// Recursively validates an XML node and its children.
    /// </summary>
    private void ValidateNode(
        XmlNode node,
        List<string> errors,
        List<string> warnings,
        List<string> detectedVoices,
        ref int plainTextLength,
        int depth)
    {
        if (node == null)
            return;

        // Check nesting depth
        if (depth > _maxNestingDepth)
        {
            errors.Add($"Element nesting depth exceeds maximum of {_maxNestingDepth} levels.");
            return; // Don't validate deeper
        }

        // Validate element-specific rules
        switch (node.LocalName)
        {
            case ElementVoice:
                ValidateVoiceElement(node, errors, warnings, detectedVoices);
                break;

            case ElementProsody:
                ValidateProsodyElement(node, errors, warnings);
                break;

            case ElementStyle:
                ValidateStyleElement(node, errors, warnings);
                break;

            case ElementBreak:
                ValidateBreakElement(node, errors, warnings);
                break;

            case ElementEmphasis:
                ValidateEmphasisElement(node, errors, warnings);
                break;

            case ElementSayAs:
                ValidateSayAsElement(node, errors, warnings);
                break;

            case ElementPhoneme:
                ValidatePhonemeElement(node, errors, warnings);
                break;

            case ElementSub:
                ValidateSubElement(node, errors, warnings);
                break;
        }

        // Count plain text length
        if (node.NodeType == XmlNodeType.Text)
        {
            plainTextLength += node.Value?.Length ?? 0;
        }

        // Recursively validate child nodes
        foreach (XmlNode child in node.ChildNodes)
        {
            ValidateNode(child, errors, warnings, detectedVoices, ref plainTextLength, depth + 1);
        }
    }

    /// <summary>
    /// Validates a voice element.
    /// </summary>
    private void ValidateVoiceElement(XmlNode node, List<string> errors, List<string> warnings, List<string> detectedVoices)
    {
        var nameAttr = node.Attributes?["name"];
        if (nameAttr == null || string.IsNullOrWhiteSpace(nameAttr.Value))
        {
            errors.Add("Voice element must have a 'name' attribute.");
            return;
        }

        var voiceName = nameAttr.Value;
        detectedVoices.Add(voiceName);

        // Voices not in known voices are considered valid but without style support
        var capabilities = _voiceCapabilityProvider.GetCapabilities(voiceName);
        if (capabilities == null)
        {
            warnings.Add($"Voice '{voiceName}' is not in the known voices list. It may be valid but style support cannot be verified.");
        }
    }

    /// <summary>
    /// Validates a prosody element.
    /// </summary>
    private void ValidateProsodyElement(XmlNode node, List<string> errors, List<string> warnings)
    {
        var attributes = node.Attributes;
        if (attributes == null)
            return;

        // Validate rate
        var rateAttr = attributes["rate"];
        if (rateAttr != null && !string.IsNullOrWhiteSpace(rateAttr.Value))
        {
            if (!RateValueRegex().IsMatch(rateAttr.Value))
            {
                errors.Add($"Invalid prosody rate value: '{rateAttr.Value}'");
            }
            else if (TryParsePercentage(rateAttr.Value, out var rate))
            {
                if (rate < MinRate || rate > MaxRate)
                {
                    errors.Add($"Prosody rate must be between {MinRate} and {MaxRate}, got {rate}");
                }
            }
        }

        // Validate pitch
        var pitchAttr = attributes["pitch"];
        if (pitchAttr != null && !string.IsNullOrWhiteSpace(pitchAttr.Value))
        {
            if (!PitchValueRegex().IsMatch(pitchAttr.Value))
            {
                errors.Add($"Invalid prosody pitch value: '{pitchAttr.Value}'");
            }
            else if (TryParsePercentage(pitchAttr.Value, out var pitch))
            {
                if (pitch < MinPitch || pitch > MaxPitch)
                {
                    errors.Add($"Prosody pitch must be between {MinPitch} and {MaxPitch}, got {pitch}");
                }
            }
        }

        // Validate volume
        var volumeAttr = attributes["volume"];
        if (volumeAttr != null && !string.IsNullOrWhiteSpace(volumeAttr.Value))
        {
            if (double.TryParse(volumeAttr.Value, out var volume))
            {
                if (volume < MinVolume || volume > MaxVolume)
                {
                    errors.Add($"Prosody volume must be between {MinVolume} and {MaxVolume}, got {volume}");
                }
            }
        }
    }

    /// <summary>
    /// Validates a style element.
    /// </summary>
    private void ValidateStyleElement(XmlNode node, List<string> errors, List<string> warnings)
    {
        var styleAttr = node.Attributes?["style"];
        if (styleAttr == null || string.IsNullOrWhiteSpace(styleAttr.Value))
        {
            errors.Add("Style element must have a 'style' attribute.");
            return;
        }

        // Check if style is used with a voice
        var parentVoice = FindParentVoice(node);
        if (parentVoice == null)
        {
            warnings.Add("Style element should be nested within a voice element.");
        }
        else
        {
            var voiceName = parentVoice.Attributes?["name"]?.Value;
            if (!string.IsNullOrWhiteSpace(voiceName))
            {
                var style = styleAttr.Value;
                if (!IsStyleSupported(voiceName, style))
                {
                    warnings.Add($"Style '{style}' may not be supported by voice '{voiceName}'.");
                }
            }
        }

        // Validate style degree
        var degreeAttr = node.Attributes?["styledegree"];
        if (degreeAttr != null && !string.IsNullOrWhiteSpace(degreeAttr.Value))
        {
            if (double.TryParse(degreeAttr.Value, out var degree))
            {
                if (degree < 0.01 || degree > 2.0)
                {
                    errors.Add($"Style degree must be between 0.01 and 2.0, got {degree}");
                }
            }
        }
    }

    /// <summary>
    /// Validates a break element.
    /// </summary>
    private void ValidateBreakElement(XmlNode node, List<string> errors, List<string> warnings)
    {
        var timeAttr = node.Attributes?["time"];
        var strengthAttr = node.Attributes?["strength"];

        if (timeAttr == null && strengthAttr == null)
        {
            warnings.Add("Break element should have either 'time' or 'strength' attribute.");
            return;
        }

        if (timeAttr != null && !string.IsNullOrWhiteSpace(timeAttr.Value))
        {
            if (!BreakDurationRegex().IsMatch(timeAttr.Value))
            {
                errors.Add($"Invalid break duration: '{timeAttr.Value}'. Must be like '500ms', '1s', or 'weak', 'medium', 'strong'.");
            }
        }
    }

    /// <summary>
    /// Validates an emphasis element.
    /// </summary>
    private void ValidateEmphasisElement(XmlNode node, List<string> errors, List<string> warnings)
    {
        var levelAttr = node.Attributes?["level"];
        if (levelAttr != null && !string.IsNullOrWhiteSpace(levelAttr.Value))
        {
            var validLevels = new[] { "strong", "moderate", "reduced", "none" };
            if (!validLevels.Contains(levelAttr.Value, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"Invalid emphasis level: '{levelAttr.Value}'. Must be 'strong', 'moderate', 'reduced', or 'none'.");
            }
        }
    }

    /// <summary>
    /// Validates a say-as element.
    /// </summary>
    private void ValidateSayAsElement(XmlNode node, List<string> errors, List<string> warnings)
    {
        var interpretAsAttr = node.Attributes?["interpret-as"];
        if (interpretAsAttr == null || string.IsNullOrWhiteSpace(interpretAsAttr.Value))
        {
            errors.Add("Say-as element must have an 'interpret-as' attribute.");
        }
    }

    /// <summary>
    /// Validates a phoneme element.
    /// </summary>
    private void ValidatePhonemeElement(XmlNode node, List<string> errors, List<string> warnings)
    {
        var alphabetAttr = node.Attributes?["alphabet"];
        var phAttr = node.Attributes?["ph"];

        if (alphabetAttr == null || string.IsNullOrWhiteSpace(alphabetAttr.Value))
        {
            errors.Add("Phoneme element must have an 'alphabet' attribute.");
        }

        if (phAttr == null || string.IsNullOrWhiteSpace(phAttr.Value))
        {
            errors.Add("Phoneme element must have a 'ph' attribute.");
        }
    }

    /// <summary>
    /// Validates a substitution element.
    /// </summary>
    private void ValidateSubElement(XmlNode node, List<string> errors, List<string> warnings)
    {
        var aliasAttr = node.Attributes?["alias"];
        if (aliasAttr == null || string.IsNullOrWhiteSpace(aliasAttr.Value))
        {
            errors.Add("Substitution element must have an 'alias' attribute.");
        }
    }

    /// <summary>
    /// Finds the parent voice element of a given node.
    /// </summary>
    private static XmlNode? FindParentVoice(XmlNode node)
    {
        var current = node.ParentNode;
        while (current != null)
        {
            if (current.LocalName == ElementVoice)
            {
                return current;
            }
            current = current.ParentNode;
        }
        return null;
    }

    /// <summary>
    /// Recursively extracts plain text from an XML node.
    /// </summary>
    private static string ExtractTextFromNode(XmlNode node)
    {
        var sb = new StringBuilder();

        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType == XmlNodeType.Text)
            {
                sb.Append(child.Value);
            }
            else if (child.HasChildNodes)
            {
                sb.Append(ExtractTextFromNode(child));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Tries to parse a percentage value (e.g., "+50%", "-25%") to a multiplier.
    /// </summary>
    private static bool TryParsePercentage(string value, out double result)
    {
        result = 0;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        // Remove % sign if present
        value = value.TrimEnd('%');

        // Parse numeric value
        if (!double.TryParse(value, out var numericValue))
            return false;

        // Convert percentage to multiplier (e.g., "+50%" -> 1.5, "-25%" -> 0.75)
        result = 1.0 + (numericValue / 100.0);
        return true;
    }

    /// <summary>
    /// Escapes unescaped XML special characters.
    /// </summary>
    private static string EscapeUnescapedCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // This is a basic implementation - a more sophisticated version would
        // detect already-escaped entities and not double-escape them
        return SecurityElement.Escape(text) ?? text;
    }
}
