namespace DiscordBot.Core.DTOs;

/// <summary>
/// DTO representing a theme for API responses.
/// </summary>
public record ThemeDto
{
    /// <summary>
    /// Unique identifier for this theme.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Unique key for the theme (e.g., "discord-dark").
    /// </summary>
    public string ThemeKey { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Optional description of the theme.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// JSON object containing color palette definitions.
    /// </summary>
    public string ColorDefinition { get; init; } = "{}";

    /// <summary>
    /// Whether this theme is available for selection.
    /// </summary>
    public bool IsActive { get; init; }
}

/// <summary>
/// DTO for setting a user's theme preference.
/// </summary>
public record SetUserThemeDto
{
    /// <summary>
    /// The ID of the theme to set as the user's preference.
    /// Null clears the preference (uses default).
    /// </summary>
    public int? ThemeId { get; init; }
}

/// <summary>
/// DTO for setting the system default theme.
/// </summary>
public record SetDefaultThemeDto
{
    /// <summary>
    /// The ID of the theme to set as the system default.
    /// </summary>
    public int ThemeId { get; init; }
}

/// <summary>
/// DTO representing the current effective theme for a user.
/// </summary>
public record CurrentThemeDto
{
    /// <summary>
    /// The theme data.
    /// </summary>
    public ThemeDto Theme { get; init; } = null!;

    /// <summary>
    /// Source of the theme (User, Admin, System).
    /// </summary>
    public ThemeSource Source { get; init; }
}

/// <summary>
/// Indicates the source of a user's current theme.
/// </summary>
public enum ThemeSource
{
    /// <summary>
    /// User explicitly selected this theme.
    /// </summary>
    User,

    /// <summary>
    /// Theme is the admin-configured default (not implemented yet).
    /// </summary>
    Admin,

    /// <summary>
    /// Theme is the system-wide default.
    /// </summary>
    System
}
