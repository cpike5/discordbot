using System.Text.Json;
using cpike.Agent;
using DiscordBot.Infrastructure.Services.LLM.Tooling;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>get_feature_documentation</c> — documentation access, served to both the guild and DM assistants.
/// </summary>
public sealed class GetFeatureDocumentationTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "get_feature_documentation",
        "Retrieves comprehensive documentation for a bot feature including ALL related commands, configuration options, usage instructions, and examples. This is the BEST tool for 'how do I use X' questions - use it FIRST before search_commands. Feature names: soundboard, rat-watch, tts, vox, reminder, member-directory, moderation, welcome, scheduled-messages, consent, privacy, commands, settings, audio, performance, audit.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "feature_name": {
                        "type": "string",
                        "description": "The name of the feature to get documentation for. Use lowercase with hyphens (e.g., 'soundboard', 'rat-watch', 'tts-support', 'reminder-system', 'member-directory')."
                    }
                },
                "required": ["feature_name"]
            }
            """).RootElement.Clone());

    private readonly DocumentationLibrary _docs;
    private readonly DiscordRunContext _run;

    /// <summary>Creates the tool.</summary>
    public GetFeatureDocumentationTool(DocumentationLibrary docs, DiscordRunContext run)
    {
        _docs = docs ?? throw new ArgumentNullException(nameof(docs));
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
        return await _docs.GetFeatureDocumentationAsync(input, DocumentationLibrary.EffectiveGuildId(_run), cancellationToken);
    }
}
