using DiscordBot.Agents.Contracts;

namespace DiscordBot.Agents;

/// <summary>
/// Parses one skill markdown file: a small front-matter block, then the instructions.
/// </summary>
/// <remarks>
/// <para>
/// The format is deliberately the smallest thing that works, and is hand-rolled rather than
/// pulling a YAML parser into a leaf library for four keys:
/// </para>
/// <code>
/// ---
/// key: moderation
/// summary: Look up moderation cases, warnings, and a user's moderation history for a server.
/// tools: get_moderation_cases, get_user_mod_history
/// ---
/// **Reading cases.** `get_moderation_cases` takes a guild and an optional status …
/// </code>
/// <para>
/// <c>summary</c> is the only required key. It is the entire basis for the model's decision to
/// load the skill, so a file without one is not a skill with a missing field — it is a skill
/// nothing will ever load, and <see cref="TryParse"/> rejects it rather than shipping it invisible.
/// </para>
/// </remarks>
public static class SkillFile
{
    private const string Fence = "---";

    /// <summary>
    /// Parses <paramref name="content"/> into a skill.
    /// </summary>
    /// <param name="content">The file's text.</param>
    /// <param name="defaultKey">
    /// The key to use when the front matter omits one — normally the file name without its
    /// extension, so a one-skill-per-file directory needs no <c>key</c> at all.
    /// </param>
    /// <param name="source">Where it came from, carried for logging.</param>
    /// <param name="skill">The parsed skill.</param>
    /// <param name="error">Why parsing failed, for the caller to log.</param>
    /// <returns>True when <paramref name="content"/> is a usable skill.</returns>
    public static bool TryParse(
        string? content,
        string defaultKey,
        string? source,
        out AgentSkill? skill,
        out string? error)
    {
        skill = null;
        error = null;

        if (string.IsNullOrWhiteSpace(content))
        {
            error = "the file is empty";
            return false;
        }

        if (!TrySplitFrontMatter(content, out var frontMatter, out var body))
        {
            error = "there is no `---` front-matter block at the top of the file";
            return false;
        }

        var fields = ParseFields(frontMatter);

        if (!fields.TryGetValue("summary", out var summary) || string.IsNullOrWhiteSpace(summary))
        {
            // A file that merely opens with a horizontal rule parses as a fenced block full of
            // prose. Saying "summary is missing" would send its author looking in the wrong place.
            error = fields.Count == 0
                ? "the `---` block at the top of the file holds no `name: value` fields, so it is "
                    + "probably a horizontal rule rather than front matter"
                : "`summary` is missing, and it is the only thing the model reads when deciding "
                    + "whether to load the skill";
            return false;
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            error = "the file has front matter but no instructions under it";
            return false;
        }

        var key = fields.TryGetValue("key", out var declaredKey) && !string.IsNullOrWhiteSpace(declaredKey)
            ? declaredKey.Trim()
            : defaultKey;

        if (string.IsNullOrWhiteSpace(key))
        {
            error = "no `key`, and none could be taken from the file name";
            return false;
        }

        skill = new AgentSkill
        {
            Key = key,
            Summary = summary.Trim(),
            Tools = fields.TryGetValue("tools", out var tools) ? ParseToolList(tools) : Array.Empty<string>(),
            Instructions = body.Trim(),
            Source = source
        };

        return true;
    }

    /// <summary>
    /// Splits a leading <c>---</c> block off the front of the file.
    /// </summary>
    private static bool TrySplitFrontMatter(string content, out string frontMatter, out string body)
    {
        frontMatter = string.Empty;
        body = string.Empty;

        var text = content.TrimStart('﻿', ' ', '\t', '\r', '\n');

        if (!text.StartsWith(Fence, StringComparison.Ordinal))
        {
            return false;
        }

        var afterOpening = text.IndexOf('\n');
        if (afterOpening < 0)
        {
            return false;
        }

        var rest = text[(afterOpening + 1)..];

        // The closing fence is a line of its own; finding it by line keeps a `---` rule inside the
        // instructions from ending the block early.
        var lines = rest.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd('\r').Trim() != Fence)
            {
                continue;
            }

            frontMatter = string.Join('\n', lines.Take(i));
            body = string.Join('\n', lines.Skip(i + 1));
            return true;
        }

        return false;
    }

    /// <summary>
    /// The <c>name: value</c> lines of a front-matter block. Later duplicates win, blank lines and
    /// <c>#</c> comments are skipped, and a line without a colon is ignored rather than fatal.
    /// </summary>
    private static Dictionary<string, string> ParseFields(string frontMatter)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in frontMatter.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var name = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            if (name.Length > 0)
            {
                fields[name] = Unquote(value);
            }
        }

        return fields;
    }

    /// <summary>Strips one layer of matching quotes, which a summary containing a colon needs.</summary>
    private static string Unquote(string value)
    {
        if (value.Length >= 2
            && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }

    /// <summary>
    /// Tool names from a comma-separated list, tolerating the <c>[a, b]</c> form a YAML habit
    /// produces. Order is preserved: it is the author's, and nothing downstream re-sorts it.
    /// </summary>
    private static IReadOnlyList<string> ParseToolList(string value)
    {
        var trimmed = value.Trim();

        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
        {
            trimmed = trimmed[1..^1];
        }

        return trimmed
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name => name.Trim('"', '\'').Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
