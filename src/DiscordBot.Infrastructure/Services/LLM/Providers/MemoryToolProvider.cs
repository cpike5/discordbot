using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services.LLM.Implementations;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM.Providers;

/// <summary>
/// Tool provider for DM assistant memory/notes management.
/// Provides tools for saving, searching, listing, and deleting personal notes.
/// </summary>
public class MemoryToolProvider : IDmToolProvider
{
    private readonly ILogger<MemoryToolProvider> _logger;
    private readonly IDmAssistantNoteRepository _noteRepository;

    /// <inheritdoc />
    public string Name => "Memory";

    /// <inheritdoc />
    public string Description => "Save and recall personal notes, preferences, and context across conversations";

    public MemoryToolProvider(
        ILogger<MemoryToolProvider> logger,
        IDmAssistantNoteRepository noteRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _noteRepository = noteRepository ?? throw new ArgumentNullException(nameof(noteRepository));
    }

    /// <inheritdoc />
    public IEnumerable<LlmToolDefinition> GetTools()
    {
        return MemoryTools.GetAllTools();
    }

    /// <inheritdoc />
    public async Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing memory tool {ToolName}", toolName);

        try
        {
            return toolName.ToLowerInvariant() switch
            {
                MemoryTools.SaveNote => await ExecuteSaveNoteAsync(input, context, cancellationToken),
                MemoryTools.SearchNotes => await ExecuteSearchNotesAsync(input, context, cancellationToken),
                MemoryTools.GetNote => await ExecuteGetNoteAsync(input, context, cancellationToken),
                MemoryTools.ListNotes => await ExecuteListNotesAsync(input, context, cancellationToken),
                MemoryTools.DeleteNote => await ExecuteDeleteNoteAsync(input, context, cancellationToken),
                _ => throw new NotSupportedException($"Tool '{toolName}' is not supported by this provider")
            };
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing memory tool {ToolName}", toolName);
            return ToolExecutionResult.CreateError($"Error executing tool: {ex.Message}");
        }
    }

    private async Task<ToolExecutionResult> ExecuteSaveNoteAsync(
        JsonElement input, ToolContext context, CancellationToken cancellationToken)
    {
        if (!input.TryGetProperty("content", out var contentElement))
        {
            return ToolExecutionResult.CreateError("Missing required parameter: content");
        }

        var content = contentElement.GetString();
        if (string.IsNullOrWhiteSpace(content))
        {
            return ToolExecutionResult.CreateError("Parameter content cannot be empty");
        }

        if (content.Length > 4096)
        {
            return ToolExecutionResult.CreateError("Note content exceeds maximum length of 4096 characters.");
        }

        string? tag = null;
        if (input.TryGetProperty("tag", out var tagElement))
        {
            tag = tagElement.GetString();
        }

        var note = new DmAssistantNote
        {
            UserId = context.UserId,
            Content = content,
            Tag = tag,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _noteRepository.AddAsync(note, cancellationToken);

        _logger.LogDebug("Saved note {NoteId} for user {UserId}", note.Id, context.UserId);

        return CreateJsonResult(new
        {
            success = true,
            note_id = note.Id,
            message = "Note saved successfully."
        });
    }

    private async Task<ToolExecutionResult> ExecuteSearchNotesAsync(
        JsonElement input, ToolContext context, CancellationToken cancellationToken)
    {
        if (!input.TryGetProperty("query", out var queryElement))
        {
            return ToolExecutionResult.CreateError("Missing required parameter: query");
        }

        var query = queryElement.GetString();
        if (string.IsNullOrWhiteSpace(query))
        {
            return ToolExecutionResult.CreateError("Parameter query cannot be empty");
        }

        var limit = 10;
        if (input.TryGetProperty("limit", out var limitElement))
        {
            limit = Math.Clamp(limitElement.GetInt32(), 1, 50);
        }

        var notes = await _noteRepository.SearchAsync(query, context.UserId, limit, cancellationToken);

        return CreateJsonResult(new
        {
            results = notes.Select(n => new
            {
                id = n.Id,
                content = n.Content,
                tag = n.Tag,
                created_at = n.CreatedAt.ToString("o"),
                updated_at = n.UpdatedAt.ToString("o")
            }).ToList(),
            total_results = notes.Count
        });
    }

    private async Task<ToolExecutionResult> ExecuteGetNoteAsync(
        JsonElement input, ToolContext context, CancellationToken cancellationToken)
    {
        if (!input.TryGetProperty("note_id", out var noteIdElement))
        {
            return ToolExecutionResult.CreateError("Missing required parameter: note_id");
        }

        var noteId = noteIdElement.GetInt64();
        var note = await _noteRepository.GetByIdAsync(noteId, context.UserId, cancellationToken);

        if (note == null)
        {
            return ToolExecutionResult.CreateError($"Note with ID {noteId} not found.");
        }

        return CreateJsonResult(new
        {
            id = note.Id,
            content = note.Content,
            tag = note.Tag,
            created_at = note.CreatedAt.ToString("o"),
            updated_at = note.UpdatedAt.ToString("o")
        });
    }

    private async Task<ToolExecutionResult> ExecuteListNotesAsync(
        JsonElement input, ToolContext context, CancellationToken cancellationToken)
    {
        string? tag = null;
        if (input.TryGetProperty("tag", out var tagElement))
        {
            tag = tagElement.GetString();
        }

        var limit = 20;
        if (input.TryGetProperty("limit", out var limitElement))
        {
            limit = Math.Clamp(limitElement.GetInt32(), 1, 50);
        }

        var notes = await _noteRepository.ListAsync(context.UserId, tag, limit, cancellationToken);

        return CreateJsonResult(new
        {
            notes = notes.Select(n => new
            {
                id = n.Id,
                content = n.Content,
                tag = n.Tag,
                created_at = n.CreatedAt.ToString("o"),
                updated_at = n.UpdatedAt.ToString("o")
            }).ToList(),
            total_count = notes.Count,
            filter_tag = tag
        });
    }

    private async Task<ToolExecutionResult> ExecuteDeleteNoteAsync(
        JsonElement input, ToolContext context, CancellationToken cancellationToken)
    {
        if (!input.TryGetProperty("note_id", out var noteIdElement))
        {
            return ToolExecutionResult.CreateError("Missing required parameter: note_id");
        }

        var noteId = noteIdElement.GetInt64();
        var deleted = await _noteRepository.DeleteAsync(noteId, context.UserId, cancellationToken);

        if (!deleted)
        {
            return ToolExecutionResult.CreateError($"Note with ID {noteId} not found or already deleted.");
        }

        _logger.LogDebug("Deleted note {NoteId} for user {UserId}", noteId, context.UserId);

        return CreateJsonResult(new
        {
            success = true,
            message = $"Note {noteId} deleted successfully."
        });
    }

    private static ToolExecutionResult CreateJsonResult(object data)
    {
        var jsonString = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
        var jsonElement = JsonDocument.Parse(jsonString).RootElement.Clone();
        return ToolExecutionResult.CreateSuccess(jsonElement);
    }
}
