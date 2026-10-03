using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// How a moderator tag looks and reads for each category. One place, so the tag chips on the
/// moderation profile, the tag list in moderation settings and the tags a script adds all agree.
/// </summary>
public static class ModTagStyle
{
    /// <summary>
    /// The modifier class (defined in moderation.css) that tints a tag chip for its category.
    /// </summary>
    public static string CssClass(TagCategory category) => category switch
    {
        TagCategory.Positive => "user-tag-success",
        TagCategory.Negative => "user-tag-danger",
        TagCategory.Neutral => "user-tag-info",
        _ => string.Empty
    };

    /// <summary>
    /// Plain-language category name, for selects and labels.
    /// </summary>
    public static string DisplayName(TagCategory category) => category switch
    {
        TagCategory.Positive => "Positive (green)",
        TagCategory.Negative => "Negative (red)",
        TagCategory.Neutral => "Neutral (blue)",
        _ => category.ToString()
    };

    /// <summary>
    /// The hex colour stored with a new tag of this category. The tag entity keeps a colour, but
    /// the chips are drawn from the design tokens, so this is only the stored default.
    /// </summary>
    public static string DefaultColor(TagCategory category) => category switch
    {
        TagCategory.Positive => "#27AE60",
        TagCategory.Negative => "#E74C3C",
        TagCategory.Neutral => "#3498DB",
        _ => "#95A5A6"
    };
}
