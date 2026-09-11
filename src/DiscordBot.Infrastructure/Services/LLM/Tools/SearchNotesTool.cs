using System.Text.Json;
using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>search_notes</c> — keyword search over the caller's saved notes.
/// </summary>
public sealed class SearchNotesTool : IAgentTool
{
    private static readonly LlmToolDefinition ToolDefinition = new()
    {
        Name = "search_notes",
        Description = "Searches the user's saved notes by keyword. Matches against both note content and tags. Use this to recall previously saved information about the user.",
        InputSchema = ToolInput.ObjectSchema(
            new
            {
                query = ToolInput.Schema("string", "Search term to find in note content or tags."),
                limit = ToolInput.Schema(
                    "integer",
                    "Maximum number of results to return. Default is 10.",
                    @default: 10,
                    minimum: 1,
                    maximum: 50)
            },
            "query")
    };

    private readonly IDmAssistantNoteRepository _notes;

    /// <summary>Creates the tool.</summary>
    public SearchNotesTool(IDmAssistantNoteRepository notes)
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
        var query = ToolInput.GetString(input, "query");
        if (query is null)
        {
            return ToolResults.Error("Missing required parameter: query");
        }

        var limit = ToolInput.GetInt(input, "limit", fallback: 10, min: 1, max: 50);

        var notes = await _notes.SearchAsync(query, context.UserId, limit, cancellationToken);

        return ToolResults.Json(new
        {
            results = notes.Select(NotePayloads.Describe).ToList(),
            total_results = notes.Count
        });
    }
}
