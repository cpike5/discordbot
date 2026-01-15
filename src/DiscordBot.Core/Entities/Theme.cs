namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a UI theme definition with color palette stored as JSON.
/// </summary>
public class Theme
{
    /// <summary>
    /// Unique identifier for this theme (primary key).
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Unique key for the theme (e.g., "discord-dark", "purple-dusk").
    /// Used for programmatic theme identification.
    /// </summary>
    public string ThemeKey { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name for the theme (e.g., "Discord Dark").
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the theme.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// JSON object containing the theme's color palette definitions.
    /// Keys map to CSS variable names (e.g., bgPrimary, textPrimary).
    /// </summary>
    public string ColorDefinition { get; set; } = "{}";

    /// <summary>
    /// Indicates whether this theme is available for selection.
    /// Inactive themes are hidden from users.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp when the theme was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property for users who have selected this theme.
    /// </summary>
    public ICollection<ApplicationUser> PreferringUsers { get; set; } = new List<ApplicationUser>();
}
