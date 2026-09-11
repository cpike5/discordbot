namespace DiscordBot.Bot.Blazor.Shared;

/// <summary>
/// A user-saved custom TTS preset, as rendered by <see cref="PresetBar"/>'s
/// <c>CustomPresets</c> parameter. Mirrors the shape <c>PortalTtsPresetsController</c>'s
/// <c>GetCustomPresets</c>/<c>CreateCustomPresetRequest</c> exchange (the entity is
/// <c>DiscordBot.Core.Entities.UserTtsPreset</c>) without depending on either — per the Tier 5
/// contract, persistence (the fetch/POST/DELETE the original inline script did against
/// <c>/api/portal/tts/{guildId}/presets/custom</c>) is the hosting page's job, not the
/// component's; this record is only what <see cref="PresetBar"/> needs to render a custom
/// preset button and hand it back on <c>OnDeleteCustom</c>.
/// </summary>
/// <param name="Id">The preset's database id.</param>
/// <param name="Name">User-defined display name (max 50 characters server-side).</param>
/// <param name="VoiceName">Azure TTS voice name (e.g. "en-US-JennyNeural").</param>
/// <param name="Style">Optional speaking style.</param>
/// <param name="Speed">Speech rate multiplier (0.5-2.0).</param>
/// <param name="Pitch">Pitch adjustment multiplier (0.5-2.0).</param>
public sealed record CustomTtsPreset(int Id, string Name, string VoiceName, string? Style, decimal Speed, decimal Pitch);
