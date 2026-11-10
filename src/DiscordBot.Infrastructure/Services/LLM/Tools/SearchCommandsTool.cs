using System.Text.Json;
using cpike.Agent;
using DiscordBot.Infrastructure.Services.LLM.Tooling;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>search_commands</c> — documentation access, served to both the guild and DM assistants.
/// </summary>
public sealed class SearchCommandsTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "search_commands",
        "Searches available slash commands by keyword. Returns matching commands with their descriptions and parameters. IMPORTANT: For feature questions like 'how do I use X', prefer get_feature_documentation instead - it provides comprehensive guides. Only use search_commands when listing available commands or when you need to find a specific command name. Never call this multiple times for the same topic.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "query": {
                        "type": "string",
                        "description": "Search keyword to find commands (e.g., 'moderation', 'sound', 'remind', 'ban')."
                    },
                    "limit": {
                        "type": "integer",
                        "description": "Maximum number of results to return. Default is 10, maximum is 50.",
                        "default": 10,
                        "minimum": 1,
                        "maximum": 50
                    }
                },
                "required": ["query"]
            }
            """).RootElement.Clone());

    private readonly DocumentationLibrary _docs;

    /// <summary>Creates the tool.</summary>
    public SearchCommandsTool(DocumentationLibrary docs)
    {
        _docs = docs ?? throw new ArgumentNullException(nameof(docs));
    }

    /// <inheritdoc />
    public AgentToolDefinition Definition => ToolDefinition;

    /// <inheritdoc />
    public async Task<string> InvokeAsync(
        IReadOnlyDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken)
    {
        var input = ToolInput.ToElement(arguments);
        return await _docs.SearchCommandsAsync(input, cancellationToken);
    }
}
