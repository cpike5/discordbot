namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Builds the guild-scoped <see cref="IAssistantContext"/> for one question, bundling the
/// prompt template, tool registry, guild lookup, and metrics/interaction-log repositories so
/// <c>AssistantService</c> only needs this one dependency to hand the pipeline a context.
/// </summary>
public interface IGuildAssistantContextFactory
{
    IAssistantContext Create(
        ulong guildId,
        ulong channelId,
        ulong userId,
        ulong messageId,
        int rateLimit,
        string question);
}
