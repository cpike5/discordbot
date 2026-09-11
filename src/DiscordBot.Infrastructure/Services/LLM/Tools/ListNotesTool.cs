using System.Text.Json;
using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>list_notes</c> — browses the caller's saved notes, most recently updated first.
/// </summary>
public sealed class ListNotesTool : IAgentTool
{
    private static readonly LlmToolDefinition ToolDefinition = new()
    {
        Name = "list_notes",
        Description = "Lists the user's saved notes, optionally filtered by tag. Returns notes sorted by most recently updated. Use this to browse all saved notes or notes in a specific category.",
        InputSchema = ToolInput.ObjectSchema(
            new
            {
                tag = ToolInput.Schema("string", "Optional tag to filter notes by category."),
                limit = ToolInput.Schema(
                    "integer",
                    "Maximum number of notes to return. Default is 20.",
                    @default: 20,
                    minimum: 1,
                    maximum: 50)
            })
    };

    private readonly IDmAssistantNoteRepository _notes;

    /// <summary>Creates the tool.</summary>
    public ListNotesTool(IDmAssistantNoteRepository notes)
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
        var tag = ToolInput.GetString(input, "tag");
        var limit = ToolInput.GetInt(input, "limit", fallback: 20, min: 1, max: 50);

        var notes = await _notes.ListAsync(context.UserId, tag, limit, cancellationToken);

        return ToolResults.Json(new
        {
            notes = notes.Select(NotePayloads.Describe).ToList(),
            total_count = notes.Count,
            filter_tag = tag
        });
    }
}
