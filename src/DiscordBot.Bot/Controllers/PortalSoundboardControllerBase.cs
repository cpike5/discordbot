using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Shared base for the split Portal Soundboard controllers (sounds, playback,
/// favorites, categories): the bot-level audio-enabled check they all use.
/// </summary>
public abstract class PortalSoundboardControllerBase : ControllerBase
{
    private readonly ISettingsService _settingsService;

    protected PortalSoundboardControllerBase(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// Checks if audio features are globally enabled at the bot level.
    /// </summary>
    /// <returns>True if audio is globally enabled, false otherwise.</returns>
    protected async Task<bool> IsAudioGloballyEnabledAsync()
    {
        return await _settingsService.GetSettingValueAsync<bool?>("Features:AudioEnabled") ?? true;
    }
}
