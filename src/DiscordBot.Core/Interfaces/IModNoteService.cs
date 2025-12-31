using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for managing moderator notes about users.
/// </summary>
public interface IModNoteService
{
    /// <summary>
    /// Adds a new mod note for a user.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="targetUserId">The target user ID.</param>
    /// <param name="content">The note content.</param>
    /// <param name="authorId">The moderator creating the note.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created mod note DTO.</returns>
    Task<ModNoteDto> AddNoteAsync(ulong guildId, ulong targetUserId, string content, ulong authorId, CancellationToken ct = default);

    /// <summary>
    /// Gets all mod notes for a specific user in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="targetUserId">The target user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of mod notes for the user.</returns>
    Task<IEnumerable<ModNoteDto>> GetNotesAsync(ulong guildId, ulong targetUserId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a mod note.
    /// </summary>
    /// <param name="noteId">The note ID.</param>
    /// <param name="deletedByUserId">The moderator deleting the note.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the note was deleted, false if not found.</returns>
    Task<bool> DeleteNoteAsync(Guid noteId, ulong deletedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Gets a specific mod note by ID.
    /// </summary>
    /// <param name="noteId">The note ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The mod note DTO, or null if not found.</returns>
    Task<ModNoteDto?> GetNoteAsync(Guid noteId, CancellationToken ct = default);
}
