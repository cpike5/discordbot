using System.Text.Json;
using cpike.Agent;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>delete_note</c> — removes one of the caller's saved notes.
/// </summary>
public sealed class DeleteNoteTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "delete_note",
        "Deletes a specific note by its ID. Use this when the user wants to remove previously saved information. This action cannot be undone.",
        ToolInput.ObjectSchema(
            new
            {
                note_id = ToolInput.Schema("integer", "The ID of the note to delete.")
            },
            "note_id"),
        requiresEdit: true);

    private readonly IDmAssistantNoteRepository _notes;
    private readonly DiscordRunContext _run;
    private readonly ILogger<DeleteNoteTool> _logger;

    /// <summary>Creates the tool.</summary>
    public DeleteNoteTool(IDmAssistantNoteRepository notes, DiscordRunContext run, ILogger<DeleteNoteTool> logger)
    {
        _notes = notes ?? throw new ArgumentNullException(nameof(notes));
        _run = run ?? throw new ArgumentNullException(nameof(run));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public AgentToolDefinition Definition => ToolDefinition;

    /// <inheritdoc />
    public async Task<string> InvokeAsync(
        IReadOnlyDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken)
    {
        var input = ToolInput.ToElement(arguments);
        var noteId = ToolInput.GetLong(input, "note_id");
        if (noteId is null)
        {
            return ToolResults.Error("Missing required parameter: note_id");
        }

        var deleted = await _notes.DeleteAsync(noteId.Value, _run.UserId, cancellationToken);

        if (!deleted)
        {
            return ToolResults.NotFound($"Note with ID {noteId.Value} not found or already deleted.");
        }

        _logger.LogDebug("Deleted note {NoteId} for user {UserId}", noteId.Value, _run.UserId);

        return ToolResults.Json(new
        {
            success = true,
            message = $"Note {noteId.Value} deleted successfully."
        });
    }
}
