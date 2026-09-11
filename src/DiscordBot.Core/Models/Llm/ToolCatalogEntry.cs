using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Models.Llm;

/// <summary>
/// What the portal needs to know about one agent tool: where to file it, what to call it, and
/// whether it is on by default. The engine never reads this - a tool's model-facing name, schema
/// and description live in its <c>LlmToolDefinition</c>. This is the human-facing side.
/// </summary>
/// <param name="Name">The model-facing tool name, e.g. <c>search_commands</c>.</param>
/// <param name="Category">Group heading in the settings checklist and the metrics table.</param>
/// <param name="DisplayName">Short human-readable label.</param>
/// <param name="Description">One line explaining what the tool does, for an admin choosing whether to allow it.</param>
/// <param name="Scopes">Which assistants advertise the tool.</param>
/// <param name="EnabledByDefault">
/// Whether the tool is in the house default set - what a guild gets when it has selected nothing.
/// An opt-in tool is one an admin must tick deliberately.
/// </param>
public sealed record ToolCatalogEntry(
    string Name,
    string Category,
    string DisplayName,
    string Description,
    ToolScopes Scopes,
    bool EnabledByDefault = true);
