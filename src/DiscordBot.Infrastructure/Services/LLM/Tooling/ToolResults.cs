using System.Text.Json;
using System.Text.Json.Nodes;

namespace DiscordBot.Infrastructure.Services.LLM.Tooling;

/// <summary>
/// The result shapes every assistant tool returns, as the JSON text the framework hands the model.
/// </summary>
/// <remarks>
/// Expected failures are results the model reads, not exceptions: a top-level <c>error</c> string,
/// or a <c>found</c>/<c>success</c> flag that is false. The framework's
/// <c>AgentService.FailureCodeOf</c> reads exactly those, so a tool that reports through these
/// helpers has its failures counted on the tool span and metric by construction.
/// </remarks>
public static class ToolResults
{
    /// <summary>A successful result: <paramref name="payload"/> serialized compactly.</summary>
    public static string Json(object payload) =>
        JsonSerializer.Serialize(payload, ToolJson.Compact);

    /// <summary>An expected failure the model should explain or recover from.</summary>
    public static string Error(string message) =>
        Json(new { error = message });

    /// <summary>A lookup that found nothing.</summary>
    public static string NotFound(string message) =>
        Json(new { found = false, error = message });

    /// <summary>
    /// A result cut down to fit, with a note telling the model so. A non-object payload is wrapped
    /// as <c>{ "result": … }</c>.
    /// </summary>
    public static string Truncated(object payload, string note)
    {
        var json = JsonSerializer.SerializeToNode(payload, ToolJson.Compact);
        var wrapper = json as JsonObject ?? new JsonObject { ["result"] = json };
        wrapper["truncated"] = true;
        wrapper["truncation_note"] = note;
        return wrapper.ToJsonString(ToolJson.Compact);
    }

    /// <summary>
    /// The refusal for a caller who may not do <paramref name="action"/>. For a tool that only
    /// sometimes changes data; a tool that always does sets <c>RequiresEdit</c> and lets the loop
    /// refuse it.
    /// </summary>
    public static string Forbidden(string action) =>
        Json(new
        {
            forbidden = true,
            message = $"The user isn't allowed to {action}. Don't retry — tell them plainly, and "
                + "mention that a server administrator can do it for them."
        });
}
