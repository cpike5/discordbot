using System.Text.RegularExpressions;

namespace DiscordBot.Bot.Blazor.Shared;

/// <summary>
/// C# port of <c>wwwroot/js/ssml-markers.js</c> (the shared marker parser/serializer for Pro TTS
/// mode's visual markup: <c>**text**</c> strong emphasis, <c>*text*</c> moderate emphasis,
/// <c>[# text #]</c> say-as cardinal, <c>[📅 text 📅]</c> say-as date, <c>[⏸️ Nms]</c> break) plus
/// the marker-insertion logic that lived inline in <c>_EmphasisToolbar.cshtml</c>'s
/// <c>applyFormatting</c>. <see cref="EmphasisToolbar"/> is the only caller in this library, but
/// the parsing/serialization half is pure text-in/text-out on purpose - <c>portal-tts.js</c> (not
/// yet ported) shares the same marker syntax against the original <c>ssml-markers.js</c>, so this
/// class exists independently of any one component and is unit-tested on its own
/// (tests/DiscordBot.Tests/Blazor/Shared/Tts/SsmlMarkersTests.cs).
/// </summary>
public static class SsmlMarkers
{
    private const string MarkerPattern = @"\*\*(.+?)\*\*(?!\*)|\*(?!\*)(.+?)\*(?!\*)|\[#\s(.+?)\s#\]|\[📅\s(.+?)\s📅\]|\[⏸️\s(\d+)ms\]";

    private static readonly Regex MarkerRegex = new(MarkerPattern, RegexOptions.Compiled);

    /// <summary>One parsed marker element, matching <c>parseMarkers</c>'s <c>{ type, text, attributes }</c> shape.</summary>
    public sealed record MarkerElement(string Type, string? Text, IReadOnlyDictionary<string, string> Attributes);

    /// <summary>
    /// Parses visual markers in <paramref name="text"/> into the element list the SSML builder
    /// expects (ported from <c>parseMarkers</c>).
    /// </summary>
    public static IReadOnlyList<MarkerElement> ParseMarkers(string text)
    {
        var elements = new List<MarkerElement>();
        var cursor = 0;

        foreach (Match match in MarkerRegex.Matches(text))
        {
            if (match.Index > cursor)
            {
                elements.Add(new MarkerElement("text", text[cursor..match.Index], EmptyAttributes));
            }

            if (match.Groups[1].Success)
            {
                elements.Add(new MarkerElement("emphasis", match.Groups[1].Value, new Dictionary<string, string> { ["level"] = "strong" }));
            }
            else if (match.Groups[2].Success)
            {
                elements.Add(new MarkerElement("emphasis", match.Groups[2].Value, new Dictionary<string, string> { ["level"] = "moderate" }));
            }
            else if (match.Groups[3].Success)
            {
                elements.Add(new MarkerElement("say-as", match.Groups[3].Value, new Dictionary<string, string> { ["interpret-as"] = "cardinal" }));
            }
            else if (match.Groups[4].Success)
            {
                elements.Add(new MarkerElement("say-as", match.Groups[4].Value, new Dictionary<string, string> { ["interpret-as"] = "date" }));
            }
            else if (match.Groups[5].Success)
            {
                elements.Add(new MarkerElement("break", null, new Dictionary<string, string> { ["duration"] = match.Groups[5].Value + "ms" }));
            }

            cursor = match.Index + match.Length;
        }

        if (cursor < text.Length)
        {
            elements.Add(new MarkerElement("text", text[cursor..], EmptyAttributes));
        }

        return elements;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyAttributes = new Dictionary<string, string>();

    /// <summary>
    /// Strips all visual markers from <paramref name="text"/>, returning plain text: emphasis/say-as
    /// markers are replaced with their inner text, break markers are removed entirely (ported from
    /// <c>stripMarkers</c>).
    /// </summary>
    public static string StripMarkers(string text) => MarkerRegex.Replace(text, match =>
    {
        for (var g = 1; g <= 4; g++)
        {
            if (match.Groups[g].Success)
            {
                return match.Groups[g].Value;
            }
        }

        return string.Empty;
    });

    /// <summary>Wraps <paramref name="selectedText"/> in the emphasis marker for
    /// <paramref name="level"/> (<c>"strong"</c> → <c>**text**</c>, anything else → <c>*text*</c>),
    /// ported from <c>_EmphasisToolbar.cshtml</c>'s <c>applyFormatting</c> <c>'emphasis'</c> case.</summary>
    public static string WrapEmphasis(string selectedText, string level)
    {
        var wrap = level == "strong" ? "**" : "*";
        return wrap + selectedText + wrap;
    }

    /// <summary>Builds a pause/break marker (<c>[⏸️ Nms]</c>), ported from <c>applyFormatting</c>'s
    /// <c>'pause'</c> case.</summary>
    public static string PauseMarker(int durationMs) => $"[⏸️ {durationMs}ms]";

    /// <summary>Wraps <paramref name="selectedText"/> in a say-as marker - <c>[📅 text 📅]</c> for
    /// <c>interpretAs == "date"</c>, otherwise <c>[# text #]</c> - ported from
    /// <c>applyFormatting</c>'s <c>'say-as'</c> case.</summary>
    public static string WrapSayAs(string selectedText, string interpretAs)
        => interpretAs == "date" ? $"[📅 {selectedText} 📅]" : $"[# {selectedText} #]";
}
