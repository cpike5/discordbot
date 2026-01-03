using Discord.WebSocket;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service implementation for managing moderator notes about users.
/// Handles creation, retrieval, and deletion of mod notes.
/// </summary>
public class ModNoteService : IModNoteService
{
    private readonly IModNoteRepository _noteRepository;
    private readonly DiscordSocketClient _client;
    private readonly ILogger<ModNoteService> _logger;

    public ModNoteService(
        IModNoteRepository noteRepository,
        DiscordSocketClient client,
        ILogger<ModNoteService> logger)
    {
        _noteRepository = noteRepository;
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ModNoteDto> AddNoteAsync(ulong guildId, ulong targetUserId, string content, ulong authorId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "mod_note",
            "add_note",
            guildId: guildId,
            userId: targetUserId);

        try
        {
            _logger.LogInformation("Adding mod note for user {TargetUserId} in guild {GuildId} by moderator {AuthorId}",
                targetUserId, guildId, authorId);

            var note = new ModNote
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                TargetUserId = targetUserId,
                AuthorUserId = authorId,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };

            await _noteRepository.AddAsync(note, ct);

            _logger.LogInformation("Mod note {NoteId} created successfully for user {TargetUserId} in guild {GuildId}",
                note.Id, targetUserId, guildId);

            var result = await MapToDtoAsync(note, ct);
            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ModNoteDto>> GetNotesAsync(ulong guildId, ulong targetUserId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "mod_note",
            "get_notes",
            guildId: guildId,
            userId: targetUserId);

        try
        {
            _logger.LogDebug("Retrieving mod notes for user {TargetUserId} in guild {GuildId}", targetUserId, guildId);

            var notes = await _noteRepository.GetByUserAsync(guildId, targetUserId, ct);
            var notesList = notes.ToList();

            var dtos = new List<ModNoteDto>();
            foreach (var note in notesList)
            {
                dtos.Add(await MapToDtoAsync(note, ct));
            }

            _logger.LogDebug("Retrieved {Count} mod notes for user {TargetUserId} in guild {GuildId}",
                dtos.Count, targetUserId, guildId);

            BotActivitySource.SetRecordsReturned(activity, dtos.Count);
            BotActivitySource.SetSuccess(activity);
            return dtos;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteNoteAsync(Guid noteId, ulong deletedByUserId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "mod_note",
            "delete_note",
            userId: deletedByUserId);

        try
        {
            _logger.LogInformation("Deleting mod note {NoteId} by moderator {DeletedByUserId}", noteId, deletedByUserId);

            var note = await _noteRepository.GetByIdAsync(noteId, ct);
            if (note == null)
            {
                _logger.LogWarning("Mod note {NoteId} not found", noteId);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            await _noteRepository.DeleteAsync(note, ct);

            _logger.LogInformation("Mod note {NoteId} deleted successfully by moderator {DeletedByUserId}",
                noteId, deletedByUserId);

            BotActivitySource.SetSuccess(activity);
            return true;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ModNoteDto?> GetNoteAsync(Guid noteId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "mod_note",
            "get_note");

        try
        {
            _logger.LogDebug("Retrieving mod note {NoteId}", noteId);

            var note = await _noteRepository.GetByIdAsync(noteId, ct);
            if (note == null)
            {
                _logger.LogWarning("Mod note {NoteId} not found", noteId);
                BotActivitySource.SetSuccess(activity);
                return null;
            }

            var result = await MapToDtoAsync(note, ct);
            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Maps a ModNote entity to a DTO with resolved usernames.
    /// </summary>
    private async Task<ModNoteDto> MapToDtoAsync(ModNote note, CancellationToken ct = default)
    {
        var targetUsername = await GetUsernameAsync(note.TargetUserId);
        var authorUsername = await GetUsernameAsync(note.AuthorUserId);

        return new ModNoteDto
        {
            Id = note.Id,
            GuildId = note.GuildId,
            TargetUserId = note.TargetUserId,
            TargetUsername = targetUsername,
            AuthorUserId = note.AuthorUserId,
            AuthorUsername = authorUsername,
            Content = note.Content,
            CreatedAt = note.CreatedAt
        };
    }

    /// <summary>
    /// Resolves a Discord user ID to username.
    /// </summary>
    private async Task<string> GetUsernameAsync(ulong userId)
    {
        try
        {
            var user = await _client.Rest.GetUserAsync(userId);
            return user?.Username ?? $"Unknown#{userId}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve username for user {UserId}", userId);
            return $"Unknown#{userId}";
        }
    }
}
