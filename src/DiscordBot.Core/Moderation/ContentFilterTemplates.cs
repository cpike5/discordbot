namespace DiscordBot.Core.Moderation;

/// <summary>
/// Represents a content filter template with predefined patterns.
/// </summary>
/// <param name="Name">The template name.</param>
/// <param name="Description">A description of what the template filters.</param>
/// <param name="Patterns">The regex patterns or literal strings to match.</param>
/// <param name="IsRegex">Whether the patterns are regex or literal word matches.</param>
public record ContentFilterTemplate(string Name, string Description, IReadOnlyList<string> Patterns, bool IsRegex);

/// <summary>
/// Provides predefined content filter templates for common moderation scenarios.
/// </summary>
public static class ContentFilterTemplates
{
    /// <summary>
    /// Gets the collection of available content filter templates.
    /// </summary>
    public static IReadOnlyDictionary<string, ContentFilterTemplate> Templates { get; } = new Dictionary<string, ContentFilterTemplate>
    {
        ["profanity"] = new ContentFilterTemplate(
            Name: "profanity",
            Description: "Common profanity and offensive language patterns",
            Patterns: new[]
            {
                @"\b(f+u+c+k|s+h+i+t|b+i+t+c+h|a+s+s+h+o+l+e|c+u+n+t|d+a+m+n|h+e+l+l)\b",
                @"\b(n+i+g+g+a|n+i+g+g+e+r|f+a+g+g+o+t|r+e+t+a+r+d)\b",
                @"\b(w+h+o+r+e|s+l+u+t|p+u+s+s+y|d+i+c+k|c+o+c+k)\b"
            },
            IsRegex: true
        ),

        ["links"] = new ContentFilterTemplate(
            Name: "links",
            Description: "Matches URL patterns (HTTP/HTTPS links)",
            Patterns: new[]
            {
                @"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)"
            },
            IsRegex: true
        ),

        ["invites"] = new ContentFilterTemplate(
            Name: "invites",
            Description: "Discord invite links (discord.gg and discordapp.com/invite)",
            Patterns: new[]
            {
                @"discord\.gg\/[a-zA-Z0-9]+",
                @"discordapp\.com\/invite\/[a-zA-Z0-9]+",
                @"discord\.com\/invite\/[a-zA-Z0-9]+"
            },
            IsRegex: true
        ),

        ["spam_phrases"] = new ContentFilterTemplate(
            Name: "spam_phrases",
            Description: "Common spam phrases and scam messages",
            Patterns: new[]
            {
                @"\bfree\s+nitro\b",
                @"\b(click|visit|go\s+to).*(win|prize|gift|reward)\b",
                @"\b(make|earn)\s+\$?\d+\s+(dollars?|per|a)\s+(day|hour|week)\b",
                @"\bget\s+(rich|paid)\s+quick\b",
                @"\bsteam\s+gift\b",
                @"\b(buy|selling)\s+accounts?\b"
            },
            IsRegex: true
        )
    };
}
