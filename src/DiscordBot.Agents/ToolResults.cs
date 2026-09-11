using System.Text.Json;
using System.Text.Json.Nodes;
using DiscordBot.Agents.Contracts;

namespace DiscordBot.Agents;

/// <summary>
/// The result shapes a tool returns, so they are consistent across tools rather than per-author.
/// </summary>
/// <remarks>
/// <para>
/// The important one is <see cref="Error"/>. House style reports an <em>expected</em> failure — a
/// note that does not exist, a parameter the model left out — as a <em>successful</em> result
/// carrying an explanation, because that is what the model needs to read and because
/// <c>ToolExecutionResult.CreateError</c> prepends <c>Error: </c> on the wire, which reads as a
/// malfunction to retry around. The cost of that convention is that such failures are invisible to
/// anything looking at <c>ToolExecutionResult.Success</c>, which is why
/// <see cref="ToolOutcomes.Classify"/> exists — and every shape here is built to satisfy it, so a
/// tool that reports through these helpers is countable in the traces without its author having to
/// know that.
/// </para>
/// <para>
/// <see cref="Failed"/> is the other side: a genuine malfunction, flagged as an error, counted as
/// one.
/// </para>
/// </remarks>
public static class ToolResults
{
    /// <summary>
    /// A successful result carrying <paramref name="payload"/>, serialized with
    /// <see cref="ToolJson.Compact"/>.
    /// </summary>
    /// <param name="payload">Usually an anonymous object with <c>snake_case</c> members.</param>
    public static ToolExecutionResult Json(object payload) =>
        ToolExecutionResult.CreateSuccess(ToolJson.Element(payload));

    /// <summary>
    /// An expected failure: <c>{"error": message}</c>, reported as a successful result so the model
    /// reads the explanation, and classified as <see cref="ToolOutcomes.FailedResult"/> in the trace.
    /// </summary>
    /// <param name="message">
    /// What went wrong and what the model should do about it, in the model's terms —
    /// "Missing required parameter: content", not "ArgumentNullException".
    /// </param>
    public static ToolExecutionResult Error(string message) =>
        Json(new { error = message });

    /// <summary>
    /// The specific expected failure of looking something up and not finding it:
    /// <c>{"found": false, "error": message}</c>.
    /// </summary>
    /// <param name="message">What was looked for, e.g. "Note 41 was not found."</param>
    public static ToolExecutionResult NotFound(string message) =>
        Json(new { found = false, error = message });

    /// <summary>
    /// A successful result the tool shortened itself, marked <c>truncated</c> so the model knows the
    /// answer is partial and can narrow its query rather than treating it as complete.
    /// </summary>
    /// <remarks>
    /// This is for a tool that knows it clipped its own output — it has 400 rows and returned 50.
    /// The loop's own cap on oversized results (<c>ToolResultLimiter</c>) is a separate, blunter
    /// backstop that a tool cannot see.
    /// </remarks>
    /// <param name="payload">The partial result.</param>
    /// <param name="note">Why it is partial and how to get the rest, e.g. "Showing 50 of 400 notes; narrow with `tag`."</param>
    public static ToolExecutionResult Truncated(object payload, string note)
    {
        var json = JsonSerializer.SerializeToNode(payload, ToolJson.Compact);

        var wrapper = json as JsonObject ?? new JsonObject { ["result"] = json };
        wrapper["truncated"] = true;
        wrapper["truncation_note"] = note;

        return ToolExecutionResult.CreateSuccess(
            JsonSerializer.SerializeToElement(wrapper, ToolJson.Compact));
    }

    /// <summary>
    /// The engine's default refusal when a tool declares an <see cref="Abstractions.IAgentTool.Mutation"/>
    /// and <see cref="ToolContext.CanMutate"/> is false.
    /// </summary>
    /// <remarks>
    /// A successful result carrying a directive, not an error, for the same reason as
    /// <see cref="Error"/>. A host with a voice of its own passes its own wording to
    /// <see cref="AgentToolProvider"/> instead of using this.
    /// </remarks>
    /// <param name="action">What the caller tried to do, phrased to follow "isn't allowed to".</param>
    public static ToolExecutionResult Forbidden(string action) =>
        Json(new
        {
            forbidden = true,
            message = $"The caller isn't allowed to {action}. Don't retry — say so plainly."
        });

    /// <summary>
    /// A genuine malfunction: flagged as a failed <see cref="ToolExecutionResult"/>, which reaches
    /// the model prefixed with <c>Error: </c> and counts as <see cref="ToolOutcomes.Error"/>.
    /// </summary>
    /// <remarks>
    /// Rare on purpose. If the model could do something useful with the news, it is an
    /// <see cref="Error"/> instead.
    /// </remarks>
    /// <param name="message">What broke.</param>
    public static ToolExecutionResult Failed(string message) =>
        ToolExecutionResult.CreateError(message);
}
