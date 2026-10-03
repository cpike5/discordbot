using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Services.Tts;
using DiscordBot.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Custom TTS presets for the admin Text-to-Speech page. The preset bar on that page used to post
/// to the member portal endpoint, which refuses everyone while the guild's member portal is off,
/// so saving a preset failed for the admin who is looking at the page. These are the same presets
/// (same service, same limits, same JSON) behind the admin policies, keyed to the signed-in
/// admin's linked Discord account.
/// </summary>
[ApiController]
[Route("api/guilds/{guildId}/tts/presets/custom")]
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class GuildTtsPresetsController : ControllerBase
{
    private readonly CustomTtsPresetService _customPresets;

    public GuildTtsPresetsController(CustomTtsPresetService customPresets)
    {
        _customPresets = customPresets;
    }

    /// <summary>The signed-in admin's custom presets.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCustomPresets(ulong guildId, CancellationToken cancellationToken)
    {
        if (!User.TryGetDiscordUserId(out var userId))
            return NeedsDiscordLink();

        var presets = await _customPresets.ListAsync(userId, cancellationToken);
        return Ok(presets.Select(CustomTtsPresetService.ToResponse));
    }

    /// <summary>Saves a custom preset for the signed-in admin.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCustomPreset(
        ulong guildId,
        [FromBody] PortalTtsPresetsController.CreateCustomPresetRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetDiscordUserId(out var userId))
            return NeedsDiscordLink();

        var result = await _customPresets.CreateAsync(userId, request.ToInput(), cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new ApiErrorDto
            {
                Message = result.Message!,
                Detail = result.Detail,
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = result.ErrorCode
            });
        }

        return StatusCode(StatusCodes.Status201Created, CustomTtsPresetService.ToResponse(result.Preset!));
    }

    /// <summary>Deletes one of the signed-in admin's custom presets.</summary>
    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCustomPreset(ulong guildId, int id, CancellationToken cancellationToken)
    {
        if (!User.TryGetDiscordUserId(out var userId))
            return NeedsDiscordLink();

        return await _customPresets.DeleteAsync(userId, id, cancellationToken) ? NoContent() : NotFound();
    }

    // Presets belong to a Discord account. An admin who signed in with a password and never linked
    // Discord has none to save under, and the page should say so rather than fail without a reason.
    private IActionResult NeedsDiscordLink() => BadRequest(new ApiErrorDto
    {
        Message = "Link your Discord account to save presets",
        Detail = "Custom presets are saved to your Discord account. Link it from your profile, then try again.",
        StatusCode = StatusCodes.Status400BadRequest,
        TraceId = HttpContext.GetCorrelationId(),
        ErrorCode = "discord_link_required"
    });
}
