using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Core.Extensions;

/// <summary>
/// The one place an enum member becomes words a person reads (UX plan C-2). An enum name such as
/// <c>ClearedEarly</c> never goes into user-facing text, an export or a log line meant for admins;
/// <c>status.DisplayName()</c> does.
/// <para>
/// A member is named in this order: <c>[Display(Name = "...")]</c> on the member, then its own
/// name split into words ("DirectMessage" becomes "Direct message"). So a plain one-word member
/// needs nothing, a multi-word member reads acceptably without help, and only a member whose
/// words differ from its name (a rename for "server", "Cleared early") carries an attribute. The
/// attribute also takes <c>Description</c> for a one-line explanation (<see cref="Description"/>).
/// </para>
/// </summary>
public static class EnumDisplayExtensions
{
    private static readonly ConcurrentDictionary<(Type Type, string Member), (string Name, string? Description)> Cache = new();

    /// <summary>The member in plain words, in sentence case: "Cleared early", "Moderation cases".</summary>
    public static string DisplayName(this Enum value) => Resolve(value).Name;

    /// <summary>Like <see cref="DisplayName(Enum)"/>, for a nullable value; null gives <paramref name="empty"/>.</summary>
    public static string DisplayName<T>(this T? value, string empty = "—") where T : struct, Enum =>
        value is null ? empty : value.Value.DisplayName();

    /// <summary>
    /// The words for a member given by its name, for the DTOs that carry an enum as a string
    /// (<c>ActionName = "PermissionChanged"</c>). A name that is not a member of <typeparamref name="T"/>
    /// is split into words instead, so it never reaches a screen run together.
    /// </summary>
    public static string DisplayNameFor<T>(string? member) where T : struct, Enum =>
        Enum.TryParse<T>(member, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
            ? parsed.DisplayName()
            : Humanize(member);

    /// <summary>The member as it reads inside a sentence: "cleared early", "moderation cases".</summary>
    public static string DisplayNameLower(this Enum value) => LowerFirstWord(value.DisplayName());

    /// <summary>The member's one-line description from <c>[Display(Description = "...")]</c>, or an empty string.</summary>
    public static string Description(this Enum value) => Resolve(value).Description ?? string.Empty;

    /// <summary>
    /// A PascalCase or snake_case name as words: "ModerationCases" becomes "Moderation cases". Used
    /// for names that are not enum members (a table key) and as the fallback for members.
    /// </summary>
    public static string Humanize(string? pascal)
    {
        if (string.IsNullOrEmpty(pascal)) return string.Empty;
        var spaced = Regex.Replace(pascal.Replace('_', ' '), "(?<=[a-z0-9])(?=[A-Z])", " ").Trim();
        if (spaced.Length == 0) return string.Empty;
        var sb = new StringBuilder(spaced.ToLowerInvariant());
        sb[0] = char.ToUpperInvariant(sb[0]);
        return sb.ToString();
    }

    private static (string Name, string? Description) Resolve(Enum value)
    {
        var type = value.GetType();
        var member = value.ToString();
        return Cache.GetOrAdd((type, member), static key =>
        {
            // A combined [Flags] value ("A, B") has no single field: name each part.
            if (key.Member.Contains(','))
            {
                var parts = key.Member.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => ResolveField(key.Type, part).Name.ToLowerInvariant());
                return (Humanize(string.Join(", ", parts)), null);
            }

            return ResolveField(key.Type, key.Member);
        });
    }

    private static (string Name, string? Description) ResolveField(Type type, string member)
    {
        var display = type.GetField(member, BindingFlags.Public | BindingFlags.Static)?.GetCustomAttribute<DisplayAttribute>();
        var name = display?.GetName();
        return (string.IsNullOrWhiteSpace(name) ? Humanize(member) : name, display?.GetDescription());
    }

    // Lower-case the first word only ("DM assistant" stays "DM assistant": an acronym keeps its case).
    private static string LowerFirstWord(string text)
    {
        if (text.Length == 0) return text;
        var firstSpace = text.IndexOf(' ');
        var first = firstSpace < 0 ? text : text[..firstSpace];
        if (first.Length > 1 && first.All(char.IsUpper)) return text;
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
