using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Moderation;

/// <summary>
/// Provides built-in moderator tag templates that guilds can import.
/// </summary>
public static class ModTagTemplates
{
    /// <summary>
    /// Represents a template for creating a moderator tag.
    /// </summary>
    public class TagTemplate
    {
        /// <summary>
        /// Name of the tag.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Hex color code for the tag (e.g., "#FF5733").
        /// </summary>
        public required string Color { get; init; }

        /// <summary>
        /// Description of what this tag means.
        /// </summary>
        public required string Description { get; init; }

        /// <summary>
        /// Category of this tag.
        /// </summary>
        public required TagCategory Category { get; init; }
    }

    /// <summary>
    /// Negative tags for marking problematic users.
    /// </summary>
    public static readonly IReadOnlyList<TagTemplate> NegativeTags = new List<TagTemplate>
    {
        new TagTemplate
        {
            Name = "Spammer",
            Color = "#E74C3C",
            Description = "User has been flagged for spam behavior",
            Category = TagCategory.Negative
        },
        new TagTemplate
        {
            Name = "Troll",
            Color = "#9B59B6",
            Description = "User engages in trolling or disruptive behavior",
            Category = TagCategory.Negative
        },
        new TagTemplate
        {
            Name = "Repeat Offender",
            Color = "#C0392B",
            Description = "User has multiple moderation actions on record",
            Category = TagCategory.Negative
        },
        new TagTemplate
        {
            Name = "Under Review",
            Color = "#F39C12",
            Description = "User is being monitored for suspicious activity",
            Category = TagCategory.Negative
        },
        new TagTemplate
        {
            Name = "Warned",
            Color = "#E67E22",
            Description = "User has received a formal warning",
            Category = TagCategory.Negative
        }
    };

    /// <summary>
    /// Positive tags for marking trusted or helpful users.
    /// </summary>
    public static readonly IReadOnlyList<TagTemplate> PositiveTags = new List<TagTemplate>
    {
        new TagTemplate
        {
            Name = "Trusted",
            Color = "#27AE60",
            Description = "User is trusted and has a good standing",
            Category = TagCategory.Positive
        },
        new TagTemplate
        {
            Name = "VIP",
            Color = "#F1C40F",
            Description = "User has VIP status in the community",
            Category = TagCategory.Positive
        },
        new TagTemplate
        {
            Name = "Verified",
            Color = "#3498DB",
            Description = "User has been verified by moderators",
            Category = TagCategory.Positive
        },
        new TagTemplate
        {
            Name = "Helper",
            Color = "#1ABC9C",
            Description = "User actively helps other community members",
            Category = TagCategory.Positive
        }
    };

    /// <summary>
    /// All available tag templates.
    /// </summary>
    public static readonly IReadOnlyList<TagTemplate> AllTemplates =
        NegativeTags.Concat(PositiveTags).ToList();

    /// <summary>
    /// Gets a tag template by name (case-insensitive).
    /// </summary>
    /// <param name="name">The template name to find.</param>
    /// <returns>The tag template, or null if not found.</returns>
    public static TagTemplate? GetByName(string name)
    {
        return AllTemplates.FirstOrDefault(t =>
            t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all tag templates for a specific category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>Tag templates matching the specified category.</returns>
    public static IEnumerable<TagTemplate> GetByCategory(TagCategory category)
    {
        return AllTemplates.Where(t => t.Category == category);
    }
}
