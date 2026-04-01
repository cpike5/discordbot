using System.Text.RegularExpressions;
using DiscordBot.Core.Configuration;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services.FeatureRequests;

/// <summary>
/// Detects common prompt-injection patterns in user-submitted text.
/// Checks against a configurable list of regex patterns and also performs
/// an entropy check to catch base64-encoded payloads.
/// </summary>
public class PromptInjectionFilter
{
    private readonly Regex[] _patterns;

    public PromptInjectionFilter(IOptions<FeatureRequestsOptions> options)
    {
        _patterns = options.Value.InjectionPatterns
            .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled))
            .ToArray();
    }

    /// <summary>
    /// Returns true if the text contains a known injection pattern or a suspicious
    /// base64-encoded payload.
    /// </summary>
    /// <param name="text">The user-submitted text to inspect.</param>
    /// <param name="matchedPattern">The pattern that was matched, or empty string if none.</param>
    public bool IsInjection(string text, out string matchedPattern)
    {
        matchedPattern = string.Empty;

        foreach (var pattern in _patterns)
        {
            var match = pattern.Match(text);
            if (match.Success)
            {
                matchedPattern = match.Value;
                return true;
            }
        }

        // Entropy check: look for long base64-like tokens (≥50 contiguous base64 chars)
        if (Regex.IsMatch(text, @"[A-Za-z0-9+/]{50,}={0,2}"))
        {
            matchedPattern = "base64-encoded-payload";
            return true;
        }

        return false;
    }
}
