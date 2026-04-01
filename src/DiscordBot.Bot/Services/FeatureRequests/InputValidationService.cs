using System.Text;
using System.Text.RegularExpressions;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models.FeatureRequests;

namespace DiscordBot.Bot.Services.FeatureRequests;

/// <summary>
/// Validates and sanitizes free-text user input for feature request submissions.
/// Checks length bounds, strips HTML and control characters, normalizes Unicode,
/// and runs prompt-injection detection.
/// </summary>
public class InputValidationService : IInputValidationService
{
    private readonly PromptInjectionFilter _injectionFilter;

    public InputValidationService(PromptInjectionFilter injectionFilter)
    {
        _injectionFilter = injectionFilter;
    }

    /// <inheritdoc/>
    public ValidationResult Validate(string input, int minLength, int maxLength)
    {
        var text = input.Trim();

        if (text.Length < minLength)
            return ValidationResult.Failure("TooShort");

        if (text.Length > maxLength)
            return ValidationResult.Failure("TooLong");

        // Strip HTML tags
        text = Regex.Replace(text, "<[^>]+>", string.Empty);

        // Strip control characters (preserve newline and tab)
        text = new string(text.Where(c => !char.IsControl(c) || c == '\n' || c == '\t').ToArray());

        // NFC normalize
        text = text.Normalize(NormalizationForm.FormC);

        // Truncate to maxLength after normalization
        if (text.Length > maxLength)
            text = text[..maxLength];

        if (_injectionFilter.IsInjection(text, out var pattern))
        {
            // Store only the pattern key (not the matched attacker-controlled value)
            // to avoid leaking injection payloads into rejection records.
            var patternKey = pattern == "base64-encoded-payload" ? pattern : "keyword-match";
            return ValidationResult.Failure($"PromptInjection:{patternKey}");
        }

        return ValidationResult.Success(text);
    }
}
