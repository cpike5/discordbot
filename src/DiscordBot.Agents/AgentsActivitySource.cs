using System.Diagnostics;

namespace DiscordBot.Agents;

/// <summary>
/// The agent engine's own <see cref="System.Diagnostics.ActivitySource"/>.
/// </summary>
/// <remarks>
/// The engine is a leaf library and cannot reach the host's tracing types, so it owns its source
/// and the host subscribes to it by name. Registered in the bot's OpenTelemetry setup alongside
/// the other sources.
/// </remarks>
public static class AgentsActivitySource
{
    /// <summary>The source name the host subscribes to.</summary>
    public const string SourceName = "DiscordBot.Agents";

    /// <summary>The shared source instance for engine spans.</summary>
    public static readonly ActivitySource Instance = new(SourceName);

    /// <summary>Span name prefix for one tool execution.</summary>
    public const string ToolActivityPrefix = "agent.tool ";

    /// <summary>
    /// Starts the span covering one tool execution, named <c>agent.tool {toolName}</c>.
    /// </summary>
    /// <param name="toolName">The model-facing tool name.</param>
    /// <param name="toolCallId">The model's id for this call, for correlating with the transcript.</param>
    /// <param name="providerName">The provider that owns the tool, when the registry can name it.</param>
    /// <returns>The started activity, or null when nothing is listening.</returns>
    public static Activity? StartToolActivity(string toolName, string? toolCallId = null, string? providerName = null)
    {
        var activity = Instance.StartActivity(ToolActivityPrefix + toolName, ActivityKind.Internal);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag(ToolTags.Name, toolName);

        if (!string.IsNullOrWhiteSpace(toolCallId))
        {
            activity.SetTag(ToolTags.CallId, toolCallId);
        }

        if (!string.IsNullOrWhiteSpace(providerName))
        {
            activity.SetTag(ToolTags.Provider, providerName);
        }

        return activity;
    }

    /// <summary>
    /// Records how a tool execution ended.
    /// </summary>
    /// <remarks>
    /// The status is set to <see cref="ActivityStatusCode.Error"/> only for the outcomes that are a
    /// malfunction - a thrown exception or a tool the registry does not know. An expected failure
    /// (<see cref="ToolOutcomes.FailedResult"/>), a timeout and a refused repeat are normal traffic
    /// the model is expected to handle, so they stay Unset and are counted from
    /// <see cref="ToolTags.Outcome"/> instead of inflating the error rate.
    /// </remarks>
    /// <param name="activity">The tool activity, or null.</param>
    /// <param name="outcome">One of the <see cref="ToolOutcomes"/> values.</param>
    /// <param name="resultChars">Characters of the result that entered conversation history.</param>
    /// <param name="failureCode">Optional detail, e.g. the exception type or the timeout in ms.</param>
    public static void SetToolOutcome(Activity? activity, string outcome, int resultChars, string? failureCode = null)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag(ToolTags.Outcome, outcome);
        activity.SetTag(ToolTags.ResultChars, resultChars);

        if (!string.IsNullOrWhiteSpace(failureCode))
        {
            activity.SetTag(ToolTags.FailureCode, failureCode);
        }

        activity.SetStatus(
            outcome is ToolOutcomes.Error or ToolOutcomes.UnknownTool
                ? ActivityStatusCode.Error
                : ActivityStatusCode.Unset);
    }

    /// <summary>Tag names used on a tool span.</summary>
    public static class ToolTags
    {
        /// <summary>The model-facing tool name. Follows the OpenTelemetry gen-ai convention.</summary>
        public const string Name = "gen_ai.tool.name";

        /// <summary>The model's id for this call. Follows the OpenTelemetry gen-ai convention.</summary>
        public const string CallId = "gen_ai.tool.call.id";

        /// <summary>The provider that owns the tool.</summary>
        public const string Provider = "bot.tool.provider";

        /// <summary>Characters of the result that entered conversation history.</summary>
        public const string ResultChars = "bot.tool.result_chars";

        /// <summary>One of the <see cref="ToolOutcomes"/> values.</summary>
        public const string Outcome = "bot.tool.outcome";

        /// <summary>Optional detail about a non-<see cref="ToolOutcomes.Ok"/> outcome.</summary>
        public const string FailureCode = "bot.tool.failure_code";
    }
}
