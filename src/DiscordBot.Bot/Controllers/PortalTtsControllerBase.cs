using DiscordBot.Bot.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Shared base for the split Portal TTS controllers (playback, synthesis, presets, history).
/// Holds the injected <see cref="ITtsSendPipeline"/>, which owns the send pipeline and the
/// per-guild playback-tracking state that used to live here directly.
/// </summary>
public abstract class PortalTtsControllerBase : ControllerBase
{
    protected readonly ITtsSendPipeline _sendPipeline;

    protected PortalTtsControllerBase(ITtsSendPipeline sendPipeline)
    {
        _sendPipeline = sendPipeline;
    }
}
