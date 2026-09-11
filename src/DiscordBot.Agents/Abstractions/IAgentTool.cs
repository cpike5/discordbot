using System.Text.Json;
using DiscordBot.Agents.Contracts;

namespace DiscordBot.Agents.Abstractions;

/// <summary>
/// One tool, in one file. The unit of tool authoring: a definition the model sees and a method that
/// runs when the model calls it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IToolProvider"/> still exists and is still the right shape for a group of tools that
/// share expensive state. It is the wrong shape for the common case — a single capability over a
/// single service — where it costs a name/description pair, a <c>switch</c>, a static definitions
/// class and a DI line before the first useful statement. An <see cref="IAgentTool"/> is scanned out
/// of its assembly (<see cref="AgentToolRegistration.AddAgentTools"/>) and surfaced to the registry
/// through an <see cref="AgentToolProvider"/>, so adding a tool is adding a file.
/// </para>
/// <para>
/// Write results through <see cref="ToolResults"/> and read arguments through <see cref="ToolInput"/>
/// rather than hand-rolling either: those are what make a tool's failure reporting match the house
/// convention (<see cref="ToolOutcomes.Classify"/>) by default instead of by remembering to.
/// </para>
/// </remarks>
public interface IAgentTool
{
    /// <summary>
    /// What the model sees: name, description and input schema.
    /// </summary>
    /// <remarks>
    /// Read once per run and serialized at position 0 of the request, so keep it a cheap, stable
    /// value — build it from a static field rather than composing it per call.
    /// </remarks>
    LlmToolDefinition Definition { get; }

    /// <summary>
    /// The write this tool performs, phrased to follow "isn't allowed to" — <c>"save notes"</c>,
    /// <c>"delete a scheduled message"</c>. Null (the default) declares the tool read-only.
    /// </summary>
    /// <remarks>
    /// Declaring it is the whole of the caller-access check: <see cref="AgentToolProvider"/> refuses
    /// the call before the tool is entered when <see cref="ToolContext.CanMutate"/> is false, so a
    /// tool cannot forget the guard by forgetting to write one. A tool that writes and leaves this
    /// null is the one remaining way to get it wrong, which is why it sits on the interface where a
    /// reader of the file has to answer it.
    /// </remarks>
    string? Mutation => null;

    /// <summary>
    /// Runs the tool.
    /// </summary>
    /// <param name="input">The model's arguments, as a JSON object. Read it with <see cref="ToolInput"/>.</param>
    /// <param name="context">Who is calling, and from where.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The result, built with <see cref="ToolResults"/>. An <em>expected</em> failure — a note that
    /// does not exist, an argument the model omitted — is a successful result carrying an
    /// explanation (<see cref="ToolResults.Error"/>, <see cref="ToolResults.NotFound"/>), because
    /// that is what the model needs to read. Reserve <see cref="ToolResults.Failed"/> for a
    /// malfunction. Throwing is also fine: the registry turns it into a failure result and logs it.
    /// </returns>
    Task<ToolExecutionResult> InvokeAsync(
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default);
}
