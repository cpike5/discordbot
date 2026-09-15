using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Bot.Blazor.Shared;

/// <summary>
/// C# port of <c>_SsmlPreview.cshtml</c>'s inline <c>ssmlPreview_highlightSyntax</c> (see
/// docs/plans/blazor-port-inventory.md Part 3 §2.2: "highlighting is portable to C#"). Splits SSML
/// on tag boundaries and wraps tags/attribute-names/attribute-values in the same
/// <c>ssml-tag</c>/<c>ssml-attribute</c>/<c>ssml-value</c>/<c>ssml-text</c> classes the source
/// partial's CSS already styles. Every literal piece of input is HTML-encoded before it is ever
/// concatenated into the output — the <c>&lt;span&gt;</c> wrapper markup is the only unescaped
/// content <see cref="Highlight"/> emits, so the result is safe to render as a
/// <see cref="Microsoft.AspNetCore.Components.MarkupString"/>.
/// </summary>
public static class SsmlSyntaxHighlighter
{
    private static readonly Regex TagSplit = new("(<[^>]+>)", RegexOptions.Compiled);
    private static readonly Regex TagShape = new(@"^<(/?)(\w+)(.*?)(/?)>$", RegexOptions.Compiled);
    private static readonly Regex AttributePattern = new("""(\s+)([\w:-]+)(=)("[^"]*"|'[^']*')""", RegexOptions.Compiled);

    /// <summary>Returns highlighted HTML for <paramref name="ssml"/>, or an empty string for
    /// null/empty input (matching the source's <c>if (!ssml) return '';</c>).</summary>
    public static string Highlight(string? ssml)
    {
        if (string.IsNullOrEmpty(ssml))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var part in TagSplit.Split(ssml))
        {
            if (string.IsNullOrEmpty(part))
            {
                continue;
            }

            if (part[0] == '<')
            {
                AppendTag(sb, part);
            }
            else
            {
                sb.Append("<span class=\"ssml-text\">").Append(WebUtility.HtmlEncode(part)).Append("</span>");
            }
        }

        return sb.ToString();
    }

    private static void AppendTag(StringBuilder sb, string part)
    {
        var match = TagShape.Match(part);
        if (!match.Success)
        {
            // Fallback for a malformed tag, matching the source's catch-all branch.
            sb.Append("<span class=\"ssml-tag\">").Append(WebUtility.HtmlEncode(part)).Append("</span>");
            return;
        }

        var closingSlash = match.Groups[1].Value;
        var tagName = match.Groups[2].Value;
        var attributes = match.Groups[3].Value;
        var selfClosing = match.Groups[4].Value;

        sb.Append("<span class=\"ssml-tag\">&lt;")
          .Append(WebUtility.HtmlEncode(closingSlash))
          .Append(WebUtility.HtmlEncode(tagName))
          .Append("</span>");

        if (attributes.Length > 0)
        {
            AppendAttributes(sb, attributes);
        }

        sb.Append("<span class=\"ssml-tag\">").Append(WebUtility.HtmlEncode(selfClosing)).Append("&gt;</span>");
    }

    private static void AppendAttributes(StringBuilder sb, string attributes)
    {
        var cursor = 0;
        foreach (Match match in AttributePattern.Matches(attributes))
        {
            if (match.Index > cursor)
            {
                sb.Append(WebUtility.HtmlEncode(attributes[cursor..match.Index]));
            }

            var space = match.Groups[1].Value;
            var attrName = match.Groups[2].Value;
            var equals = match.Groups[3].Value;
            var quoted = match.Groups[4].Value;
            var quoteChar = quoted[0];
            var value = quoted[1..^1];

            sb.Append(WebUtility.HtmlEncode(space))
              .Append("<span class=\"ssml-attribute\">").Append(WebUtility.HtmlEncode(attrName)).Append("</span>")
              .Append(WebUtility.HtmlEncode(equals))
              .Append(quoteChar)
              .Append("<span class=\"ssml-value\">").Append(WebUtility.HtmlEncode(value)).Append("</span>")
              .Append(quoteChar);

            cursor = match.Index + match.Length;
        }

        if (cursor < attributes.Length)
        {
            sb.Append(WebUtility.HtmlEncode(attributes[cursor..]));
        }
    }
}
