namespace DiscordBot.Infrastructure.Abstractions.LLM;

/// <summary>
/// Builds the guild-scoped <see cref="IAssistantContext"/> for one question, bundling the
/// prompt template, tool registry, guild lookup, and metrics/interaction-log repositories so
/// <c>AssistantService</c> only needs this one dependency to hand the pipeline a context.
/// </summary>
public interface IGuildAssistantContextFactory
{
    /// <summary>
    /// Builds the context, resolving the guild assistant's effective model slug (see
    /// <see cref="ILlmModelResolver"/>) as part of the build - hence async.
    /// </summary>
    Task<IAssistantContext> CreateAsync(
        ulong guildId,
        ulong channelId,
        ulong userId,
        ulong messageId,
        int rateLimit,
        string question,
        CancellationToken cancellationToken = default);
}
