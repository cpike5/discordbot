using System.Text.Json;
using DiscordBot.Agents.Contracts;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <summary>
/// The house convention for a tool that writes: check <see cref="ToolContext.CanMutate"/> first,
/// and refuse with <see cref="MutationForbidden"/> rather than with an error.
/// </summary>
/// <remarks>
/// Lives here rather than in <c>DiscordBot.Agents</c> because the refusal is written in this bot's
/// voice, and what a caller may change is this bot's policy. The engine only carries the flag.
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
