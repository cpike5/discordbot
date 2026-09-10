using DiscordBot.Core.Configuration;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using Microsoft.Extensions.Options;

namespace DiscordBot.Infrastructure.Services;

/// <inheritdoc cref="IAssistantAccessGate" />
public class AssistantAccessGate : IAssistantAccessGate
{
    private readonly ISettingsService _settingsService;
    private readonly IAssistantGuildSettingsService _guildSettingsService;
    private readonly IConsentService _consentService;
    private readonly AssistantOptions _options;

    public AssistantAccessGate(
        ISettingsService settingsService,
        IAssistantGuildSettingsService guildSettingsService,
        IConsentService consentService,
        IOptions<AssistantOptions> options)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _guildSettingsService = guildSettingsService ?? throw new ArgumentNullException(nameof(guildSettingsService));
        _consentService = consentService ?? throw new ArgumentNullException(nameof(consentService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Whether the assistant is enabled globally (runtime setting overrides the configured default).
    /// Internal helper for <see cref="IsEnabledForGuildAsync"/> — not part of <see cref="IAssistantAccessGate"/>
    /// since it has no external caller.
    /// </summary>
    private async Task<bool> IsGloballyEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await _settingsService.GetSettingValueAsync<bool?>("Assistant:GloballyEnabled", cancellationToken)
            ?? _options.GloballyEnabled;
    }

    /// <inheritdoc />
    public async Task<bool> IsEnabledForGuildAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        if (!await IsGloballyEnabledAsync(cancellationToken))
        {
            return false;
        }

        return await _guildSettingsService.IsEnabledAsync(guildId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> IsChannelAllowedAsync(ulong guildId, ulong channelId, CancellationToken cancellationToken = default)
    {
        return _guildSettingsService.IsChannelAllowedAsync(guildId, channelId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> HasConsentAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        if (!_options.Privacy.RequireExplicitConsent)
        {
            return Task.FromResult(true);
        }

        return _consentService.HasConsentAsync(userId, ConsentType.AssistantUsage, cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> GetRateLimitAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        return _guildSettingsService.GetRateLimitAsync(guildId, cancellationToken);
    }
}
