using System.Text.Json;
using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>get_note</c> — reads one of the caller's saved notes by id.
/// </summary>
public sealed class GetNoteTool : IAgentTool
{
    private static readonly LlmToolDefinition ToolDefinition = new()
    {
        Name = "get_note",
        Description = "Retrieves a specific note by its ID. Use this when you need the full content of a particular note.",
        InputSchema = ToolInput.ObjectSchema(
            new
            {
                note_id = ToolInput.Schema("integer", "The ID of the note to retrieve.")
            },
            "note_id")
    };

    private readonly IDmAssistantNoteRepository _notes;

    /// <summary>Creates the tool.</summary>
    public GetNoteTool(IDmAssistantNoteRepository notes)
    {
        _notes = notes ?? throw new ArgumentNullException(nameof(notes));
    }

    /// <inheritdoc />
    public LlmToolDefinition Definition => ToolDefinition;

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

        var note = await _notes.GetByIdAsync(noteId.Value, context.UserId, cancellationToken);

        return note is null
            ? ToolResults.NotFound($"Note with ID {noteId.Value} not found.")
            : ToolResults.Json(NotePayloads.Describe(note));
    }
}
