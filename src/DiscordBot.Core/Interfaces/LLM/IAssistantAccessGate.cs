namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Bundles the guild assistant's enable/allow/consent checks (global setting, per-guild
/// setting, channel allow-list, user consent, rate limit lookup) behind one dependency,
/// so <c>AssistantService</c> doesn't need direct references to <c>ISettingsService</c>,
/// <c>IAssistantGuildSettingsService</c>, and <c>IConsentService</c> individually.
/// </summary>
public interface IAssistantAccessGate
{
    Task<bool> IsEnabledForGuildAsync(ulong guildId, CancellationToken cancellationToken = default);

    Task<bool> IsChannelAllowedAsync(ulong guildId, ulong channelId, CancellationToken cancellationToken = default);

    Task<bool> HasConsentAsync(ulong userId, CancellationToken cancellationToken = default);

    Task<int> GetRateLimitAsync(ulong guildId, CancellationToken cancellationToken = default);
}
