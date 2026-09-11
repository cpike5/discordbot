using System.Text.Json;
using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>delete_note</c> — removes one of the caller's saved notes.
/// </summary>
public sealed class DeleteNoteTool : IAgentTool
{
    private static readonly LlmToolDefinition ToolDefinition = new()
    {
        Name = "delete_note",
        Description = "Deletes a specific note by its ID. Use this when the user wants to remove previously saved information. This action cannot be undone.",
        InputSchema = ToolInput.ObjectSchema(
            new
            {
                note_id = ToolInput.Schema("integer", "The ID of the note to delete.")
            },
            "note_id")
    };

    private readonly IDmAssistantNoteRepository _notes;
    private readonly ILogger<DeleteNoteTool> _logger;

    /// <summary>Creates the tool.</summary>
    public DeleteNoteTool(IDmAssistantNoteRepository notes, ILogger<DeleteNoteTool> logger)
    {
        _notes = notes ?? throw new ArgumentNullException(nameof(notes));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public LlmToolDefinition Definition => ToolDefinition;

    /// <inheritdoc />
    public string? Mutation => "delete notes";

    /// <inheritdoc />
    public async Task<ToolExecutionResult> InvokeAsync(
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        var noteId = ToolInput.GetLong(input, "note_id");
        if (noteId is null)
        {
            return ToolResults.Error("Missing required parameter: note_id");
        }

        var deleted = await _notes.DeleteAsync(noteId.Value, context.UserId, cancellationToken);

        if (!deleted)
        {
            return ToolResults.NotFound($"Note with ID {noteId.Value} not found or already deleted.");
        }

        _logger.LogDebug("Deleted note {NoteId} for user {UserId}", noteId.Value, context.UserId);

        return ToolResults.Json(new
        {
            success = true,
            message = $"Note {noteId.Value} deleted successfully."
        });
    }
}
