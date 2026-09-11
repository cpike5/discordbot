namespace DiscordBot.Core.DTOs.Llm.Reporting;

/// <summary>
/// One row of the per-tool usage table on a guild's assistant metrics page.
/// </summary>
/// <remarks>
/// Counted from <c>AssistantInteractionLog.ToolNames</c> rather than from spans, so it works
/// whether or not a trace backend is deployed. Durations are not available from the log - those
/// come from the <c>agent.tool</c> spans - so this carries counts and recency only.
/// </remarks>
/// <param name="ToolName">The model-facing tool name as it was logged.</param>
/// <param name="Calls">How many times the tool was called in the window.</param>
/// <param name="Interactions">How many interactions called it at least once.</param>
/// <param name="FailedInteractions">How many of those interactions failed overall.</param>
/// <param name="LastUsed">When it was last called, in UTC.</param>
public sealed record AssistantToolUsage(
    string ToolName,
    int Calls,
    int Interactions,
    int FailedInteractions,
    DateTime LastUsed);
