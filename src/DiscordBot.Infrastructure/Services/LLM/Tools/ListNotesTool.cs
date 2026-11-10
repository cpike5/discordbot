using System.Text.Json;
using cpike.Agent;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services.LLM.Tooling;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>list_notes</c> — browses the caller's saved notes, most recently updated first.
/// </summary>
public sealed class ListNotesTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "list_notes",
        "Lists the user's saved notes, optionally filtered by tag. Returns notes sorted by most recently updated. Use this to browse all saved notes or notes in a specific category.",
        ToolInput.ObjectSchema(
            new
            {
                tag = ToolInput.Schema("string", "Optional tag to filter notes by category."),
                limit = ToolInput.Schema(
                    "integer",
                    "Maximum number of notes to return. Default is 20.",
                    @default: 20,
                    minimum: 1,
                    maximum: 50)
            }));

    private readonly IDmAssistantNoteRepository _notes;
    private readonly DiscordRunContext _run;

    /// <summary>Creates the tool.</summary>
    public ListNotesTool(IDmAssistantNoteRepository notes, DiscordRunContext run)
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
        var tag = ToolInput.GetString(input, "tag");
        var limit = ToolInput.GetInt(input, "limit", fallback: 20, min: 1, max: 50);

        var notes = await _notes.ListAsync(_run.UserId, tag, limit, cancellationToken);

        return ToolResults.Json(new
        {
            notes = notes.Select(NotePayloads.Describe).ToList(),
            total_count = notes.Count,
            filter_tag = tag
        });
    }
}
