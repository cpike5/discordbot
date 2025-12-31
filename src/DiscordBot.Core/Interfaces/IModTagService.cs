using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for managing moderation tags and their application to users.
/// </summary>
public interface IModTagService
{
    /// <summary>
    /// Creates a new mod tag for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="dto">The tag creation data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created mod tag DTO.</returns>
    Task<ModTagDto> CreateTagAsync(ulong guildId, ModTagCreateDto dto, CancellationToken ct = default);

    /// <summary>
    /// Deletes a mod tag by name.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="tagName">The tag name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the tag was deleted, false if not found.</returns>
    Task<bool> DeleteTagAsync(ulong guildId, string tagName, CancellationToken ct = default);

    /// <summary>
    /// Gets all mod tags for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of mod tags for the guild.</returns>
    Task<IEnumerable<ModTagDto>> GetGuildTagsAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Gets a mod tag by name in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The tag name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The mod tag DTO, or null if not found.</returns>
    Task<ModTagDto?> GetTagByNameAsync(ulong guildId, string name, CancellationToken ct = default);

    /// <summary>
    /// Applies a tag to a user.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="tagName">The tag name.</param>
    /// <param name="appliedById">The moderator applying the tag.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The user mod tag association DTO, or null if the tag doesn't exist.</returns>
    Task<UserModTagDto?> ApplyTagAsync(ulong guildId, ulong userId, string tagName, ulong appliedById, CancellationToken ct = default);

    /// <summary>
    /// Removes a tag from a user.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="tagName">The tag name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the tag was removed, false if not found.</returns>
    Task<bool> RemoveTagAsync(ulong guildId, ulong userId, string tagName, CancellationToken ct = default);

    /// <summary>
    /// Gets all tags applied to a user.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of user mod tag associations.</returns>
    Task<IEnumerable<UserModTagDto>> GetUserTagsAsync(ulong guildId, ulong userId, CancellationToken ct = default);

    /// <summary>
    /// Imports pre-built tag templates into a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="templateNames">The template names to import.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of tags imported.</returns>
    Task<int> ImportTemplateTagsAsync(ulong guildId, IEnumerable<string> templateNames, CancellationToken ct = default);
}
