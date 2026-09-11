using System.Text.Json;

namespace DiscordBot.Agents;

/// <summary>
/// Caps the size of a tool result before it enters conversation history.
/// </summary>
/// <remarks>
/// A tool result is not paid for once: it is appended to the history and re-sent on every
/// subsequent iteration of the agentic loop, so a single 80 KB read costs its tokens again on
/// every following turn. Individual tools should return aggregates rather than dumps, but this is
/// the backstop that makes the next careless tool safe.
/// <para>
/// The replacement envelope carries an explicit instruction not to re-call the tool. Without it a
/// model reading a fragment tends to call the same tool again hoping for the rest — which is
/// exactly the loop <see cref="AgentContext.DuplicateToolCallLimit"/> refuses.
/// </para>
/// </remarks>
public static class ToolResultLimiter
{
    /// <summary>
    /// The instruction attached to a truncated result. Public so callers and tests can assert on
    /// the exact prompt surface rather than a substring of it.
    /// </summary>
    public const string TruncationMessage =
        "This result was truncated. Narrow your query or request a specific section rather than " +
        "re-calling this tool.";

    /// <summary>
    /// Returns <paramref name="content"/> unchanged when its serialized form is within
    /// <paramref name="maxChars"/>, and a truncation envelope when it is not.
    /// </summary>
    /// <param name="content">The tool result as it would enter conversation history.</param>
    /// <param name="maxChars">The ceiling in characters of serialized JSON; 0 or less disables the cap.</param>
    /// <returns>The original element, or the truncation envelope describing it.</returns>
    public static JsonElement Cap(JsonElement content, int maxChars)
    {
        if (maxChars <= 0 || content.ValueKind == JsonValueKind.Undefined)
        {
            return content;
        }

        // The common case allocates nothing beyond the raw text the serializer would produce anyway.
        var raw = content.GetRawText();
        if (raw.Length <= maxChars)
        {
            return content;
        }

        var shown = raw[..SafeCut(raw, maxChars)];

        return JsonSerializer.SerializeToElement(new
        {
            truncated = true,
            shown_chars = shown.Length,
            total_chars = raw.Length,
            content = shown,
            message = TruncationMessage,
        });
    }

    /// <summary>
    /// The cut length, moved back one character when <paramref name="length"/> would split a
    /// surrogate pair and leave an unpaired half in the envelope.
    /// </summary>
    private static int SafeCut(string raw, int length) =>
        char.IsHighSurrogate(raw[length - 1]) ? length - 1 : length;
}
