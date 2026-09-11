using System.Text.Json;
using DiscordBot.Agents.Contracts;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <summary>
/// The house refusal a tool returns when <see cref="ToolContext.CanMutate"/> is false.
/// </summary>
/// <remarks>
/// <para>
/// Lives here rather than in <c>DiscordBot.Agents</c> because the refusal is written in this bot's
/// voice, and what a caller may change is this bot's policy. The engine only carries the flag.
/// </para>
/// <para>
/// A tool authored as <c>IAgentTool</c> does not call this itself: it declares
/// <c>IAgentTool.Mutation</c>, and the surface providers hand this method to
/// <c>AgentToolProvider</c> as their refusal, which applies it before the tool is entered. The
/// hand-written <c>IToolProvider</c>s still check <c>CanMutate</c> and call this at the top of each
/// write.
/// </para>
/// </remarks>
public static class ToolPermissions
{
    /// <summary>
    /// The refusal a mutating tool returns when the caller may not write.
    /// </summary>
    /// <remarks>
    /// A successful result carrying a directive, not an error — the same reasoning as the loop's
    /// duplicate refusal. Flagging it would inflate tool-failure metrics and prepend
    /// <c>Error: </c> on the wire, which reads to the model as a malfunction it should retry
    /// around rather than a decision it should relay.
    /// </remarks>
    /// <param name="action">What the caller tried to do, phrased to follow "isn't allowed to".</param>
    public static ToolExecutionResult MutationForbidden(string action) =>
        ToolExecutionResult.CreateSuccess(JsonSerializer.SerializeToElement(new
        {
            forbidden = true,
            message = $"The user isn't allowed to {action}. Don't retry — tell them plainly, and "
                + "mention that a server administrator can do it for them."
        }));
}
