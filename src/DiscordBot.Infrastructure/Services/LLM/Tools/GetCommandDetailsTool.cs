using System.Text.Json;
using cpike.Agent;
using DiscordBot.Infrastructure.Services.LLM.Tooling;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>get_command_details</c> — documentation access, served to both the guild and DM assistants.
/// </summary>
public sealed class GetCommandDetailsTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "get_command_details",
        "Gets detailed information about a specific slash command including all parameters, their types, descriptions, default values, permission requirements, and usage examples. Use this when users ask how to use a specific command.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "command_name": {
                        "type": "string",
                        "description": "The command name without the leading slash (e.g., 'remind', 'play', 'ban', 'warn')."
                    }
                },
                "required": ["command_name"]
            }
            """).RootElement.Clone());

    private readonly DocumentationLibrary _docs;

    /// <summary>Creates the tool.</summary>
    public GetCommandDetailsTool(DocumentationLibrary docs)
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
        return await _docs.GetCommandDetailsAsync(input, cancellationToken);
    }
}
