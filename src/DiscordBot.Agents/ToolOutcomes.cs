using System.Text.Json;

namespace DiscordBot.Agents;

/// <summary>
/// The values <see cref="AgentsActivitySource.ToolTags.Outcome"/> takes, and the convention for
/// telling an expected tool failure from a successful one.
/// </summary>
/// <remarks>
/// House style is for a tool to report an expected failure - a user who does not exist, a document
/// that is not there - as a <em>successful</em> result carrying an explanatory payload, because
/// that is what the model needs to read. That makes expected failures invisible to a span that only
/// looks at <c>ToolExecutionResult.Success</c>, and expected failures are the majority of what is
/// worth seeing in a trace. <see cref="Classify"/> is the convention that makes them countable:
/// a top-level <c>error</c> string, or a top-level <c>success</c>/<c>available</c>/<c>found</c>
/// flag that is false. A tool that reports through <see cref="ToolResults"/> follows it by
/// construction, which is the point of those helpers; one that hand-rolls its result has to follow
/// it deliberately, or its failures are invisible here.
/// </remarks>
public static class ToolOutcomes
{
    /// <summary>The tool ran and reported a result that reads as a success.</summary>
    public const string Ok = "ok";

    /// <summary>The tool ran and reported an expected failure in its result payload.</summary>
    public const string FailedResult = "failed_result";

    /// <summary>The tool exceeded its per-execution deadline and was abandoned.</summary>
    public const string Timeout = "timeout";

    /// <summary>The call was refused as an identical repeat and the tool was never entered.</summary>
    public const string RepeatedCall = "repeated_call";

    /// <summary>The tool threw, or the registry turned the call into an error result.</summary>
    public const string Error = "error";

    /// <summary>No registered provider owns the tool, or this scope is not allowed it.</summary>
    public const string UnknownTool = "unknown_tool";

    /// <summary>Top-level result properties whose <c>false</c> value marks an expected failure.</summary>
    private static readonly string[] FailureFlags = { "success", "available", "found" };

    /// <summary>
    /// Classifies a tool result payload as <see cref="Ok"/> or <see cref="FailedResult"/>.
    /// </summary>
    /// <param name="result">The JSON the tool returned, as it enters conversation history.</param>
    /// <returns><see cref="FailedResult"/> when the payload reports an expected failure, else <see cref="Ok"/>.</returns>
    public static string Classify(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object)
        {
            return Ok;
        }

        if (result.TryGetProperty("error", out var error)
            && error.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(error.GetString()))
        {
            return FailedResult;
        }

        foreach (var flag in FailureFlags)
        {
            if (result.TryGetProperty(flag, out var value) && value.ValueKind == JsonValueKind.False)
            {
                return FailedResult;
            }
        }

        return Ok;
    }
}
