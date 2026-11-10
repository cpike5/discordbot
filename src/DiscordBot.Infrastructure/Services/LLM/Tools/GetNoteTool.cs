using System.Text.Json;
using cpike.Agent;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services.LLM.Tooling;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>get_note</c> — reads one of the caller's saved notes by id.
/// </summary>
public sealed class GetNoteTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "get_note",
        "Retrieves a specific note by its ID. Use this when you need the full content of a particular note.",
        ToolInput.ObjectSchema(
            new
            {
                note_id = ToolInput.Schema("integer", "The ID of the note to retrieve.")
            },
            "note_id"));

    private readonly IDmAssistantNoteRepository _notes;
    private readonly DiscordRunContext _run;

    /// <summary>Creates the tool.</summary>
    public GetNoteTool(IDmAssistantNoteRepository notes, DiscordRunContext run)
    {
        _notes = notes ?? throw new ArgumentNullException(nameof(notes));
        _run = run ?? throw new ArgumentNullException(nameof(run));
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

        var note = await _notes.GetByIdAsync(noteId.Value, _run.UserId, cancellationToken);

        return note is null
            ? ToolResults.NotFound($"Note with ID {noteId.Value} not found.")
            : ToolResults.Json(NotePayloads.Describe(note));
    }
}
