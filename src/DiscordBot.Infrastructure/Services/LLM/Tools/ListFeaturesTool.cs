using System.Text.Json;
using cpike.Agent;
using DiscordBot.Infrastructure.Services.LLM.Tooling;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>list_features</c> — documentation access, served to both the guild and DM assistants.
/// </summary>
public sealed class ListFeaturesTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "list_features",
        "Lists all available bot features with brief descriptions and availability status. Use this when users ask what the bot can do or want an overview of capabilities.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {},
                "required": []
            }
            """).RootElement.Clone());

    private readonly DocumentationLibrary _docs;

    /// <summary>Creates the tool.</summary>
    public ListFeaturesTool(DocumentationLibrary docs)
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
        return await _docs.ListFeaturesAsync();
    }
}
