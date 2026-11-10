using System.Text.Json;
using cpike.Agent;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services.LLM.Tooling;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>search_notes</c> — keyword search over the caller's saved notes.
/// </summary>
public sealed class SearchNotesTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "search_notes",
        "Searches the user's saved notes by keyword. Matches against both note content and tags. Use this to recall previously saved information about the user.",
        ToolInput.ObjectSchema(
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
            "query"));

    private readonly IDmAssistantNoteRepository _notes;
    private readonly DiscordRunContext _run;

    /// <summary>Creates the tool.</summary>
    public SearchNotesTool(IDmAssistantNoteRepository notes, DiscordRunContext run)
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
        var query = ToolInput.GetString(input, "query");
        if (query is null)
        {
            return ToolResults.Error("Missing required parameter: query");
        }

        var limit = ToolInput.GetInt(input, "limit", fallback: 10, min: 1, max: 50);

        var notes = await _notes.SearchAsync(query, _run.UserId, limit, cancellationToken);

        return ToolResults.Json(new
        {
            results = notes.Select(NotePayloads.Describe).ToList(),
            total_results = notes.Count
        });
    }
}
