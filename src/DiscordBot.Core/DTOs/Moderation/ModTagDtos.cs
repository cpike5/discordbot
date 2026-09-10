using System.Text.Json.Serialization;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for creating a new mod tag.
/// </summary>
public class ModTagCreateDto
{
    /// <summary>
    /// Gets or sets the Discord guild snowflake ID where this tag will be created.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the name of the tag.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the color of the tag (hex format, e.g., "#FF5733").
    /// </summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category of the tag.
    /// </summary>
    public TagCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the optional description of the tag.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Data transfer object for mod tag information.
/// </summary>
public class ModTagDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this tag.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild snowflake ID where this tag exists.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the name of the tag.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the color of the tag (hex format).
    /// </summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category of the tag.
    /// </summary>
    public TagCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the optional description of the tag.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether this tag was created from a template.
    /// </summary>
    public bool IsFromTemplate { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this tag was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the number of users currently assigned this tag.
    /// </summary>
    public int UserCount { get; set; }
}

/// <summary>
/// Data transfer object for user mod tag association.
/// </summary>
public class UserModTagDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this association.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild snowflake ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the tagged user.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the tagged user (resolved from Discord).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tag ID.
    /// </summary>
    public Guid TagId { get; set; }

    /// <summary>
    /// Gets or sets the name of the tag applied to the user.
    /// </summary>
    public string TagName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the color of the tag in hex format (e.g., "#FF5733").
    /// </summary>
    public string TagColor { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category/sentiment of the tag.
    /// </summary>
    public TagCategory TagCategory { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of who applied this tag.
    /// </summary>
    public ulong AppliedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the username of who applied the tag (resolved from Discord).
    /// </summary>
    public string AppliedByUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this tag was applied (UTC).
    /// </summary>
    public DateTime AppliedAt { get; set; }
}

/// <summary>
/// DTO for applying a tag to a user.
/// </summary>
public class ApplyTagDto
{
    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the moderator applying the tag.
    /// </summary>
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public ulong AppliedById { get; set; }
}
