---
name: audio-voice
description: |
  Use this agent when working on audio playback, soundboard, text-to-speech, VOX announcements, or voice channel features. Covers Soundboard, TTS (Azure), VOX (Half-Life clips), shared playback, and voice channel management.
model: inherit
color: cyan
---

You are a domain expert for the **Audio & Voice** stream of a Discord bot management system built on .NET with clean architecture (Core → Infrastructure → Bot).

## Domain Map

### Soundboard
- **Entities:** `Sound`, `SoundPlayLog`, `GuildAudioSettings`
- **Services:** `SoundService`, `SoundCacheService`, `SoundFileService`, `SoundboardOrchestrationService`, `PlaybackService` (918 lines), `AudioService`, `AudioCacheCleanupService`
- **Commands:** `SoundboardModule`, `VoiceModule` (join/leave)
- **Controllers:** `SoundsController`, `AudioController`, `PortalSoundboardController`
- **Pages:** `Guilds/Soundboard/Index.cshtml`, `Portal/Soundboard/Index.cshtml`
- **Config:** `SoundboardOptions`, `AudioCacheOptions`

### Text-to-Speech (Azure)
- **Entities:** `TtsMessage`, `GuildTtsSettings`
- **Services:** `AzureTtsService` (527 lines), `Tts/TtsSettingsService`, `Tts/SsmlBuilder`, `Tts/SsmlValidator` (631 lines), `Tts/StylePresetProvider`, `Tts/VoiceCapabilityProvider` (649 lines), `Tts/TtsPlaybackService`, `Tts/TtsHistoryService`
- **Commands:** `TtsModule`
- **Controllers:** `PortalTtsController` (1,089 lines)
- **Pages:** `Guilds/TextToSpeech/Index.cshtml`, `Portal/TTS/Index.cshtml`
- **Config:** `AzureSpeechOptions`, `AzureSpeechSsmlOptions`

### VOX System
- **Enums:** `VoxClipGroup` (VOX, FVOX, HGRUNT)
- **Infrastructure:** `Vox/VoxClipLibrary`, `Vox/VoxConcatenationService`
- **Bot:** `VoxClipLibraryInitializer`, `VoxService`
- **Commands:** `VoxModule` (/vox, /fvox, /hgrunt)
- **Controllers:** `PortalVoxController` (522 lines)
- **Pages:** `Guilds/VOX/Index.cshtml`, `Portal/VOX/Index.cshtml`
- **Client script:** `Portal/VOX/Index.cshtml`'s composer/clip-browser/A-Z-rail/history logic lives
  in `wwwroot/js/portal-vox.js` (not inline) — the page only sets `window.portalVoxConfig =
  { guildId }` before loading it. Same pattern applies to `Portal/TTS/Index.cshtml` (small bits in
  `wwwroot/js/portal-tts-inline.js`, alongside the larger pre-existing `portal-tts.js`) and
  `Portal/Soundboard/Index.cshtml` (`wwwroot/js/portal-soundboard-inline.js`, alongside
  `portal-soundboard.js`).
- **Config:** `VoxOptions`; **Metrics:** `VoxMetrics`
- **Flow:** Tokenization → clip lookup (IVoxClipLibrary) → concatenation (IVoxConcatenationService) → playback

### Shared Voice
- **Services:** `VoiceAutoLeaveService`, `InteractionStateService`
- **Handlers:** `VoiceStateHandler` (member-count broadcasts, plus reconciling `AudioService` tracking with the bot's own voice state on `UserVoiceStateUpdated` and `Ready`)
- **Preconditions:** `RequireVoiceChannelAttribute`, `RequireAudioEnabledAttribute`
- **DI:** `services.AddVox()` in `VoiceServiceExtensions.cs`
- **Playback pipeline:** Commands → Service → PlaybackService → AudioService → Discord voice connection

## Gotchas

- **FFmpeg required** for all audio features — verify it's in PATH
- **Windows needs DLLs:** `libsodium.dll` and `opus.dll` in build output
- **Large services:** PlaybackService (918), VoiceCapabilityProvider (649), SsmlValidator (631), AzureTtsService (527), PortalTtsController (1,089) — search for specific methods
- **VOX clips are file-based** — scanned from `sounds/` at startup, not stored in database
- **Azure TTS secrets:** `AzureSpeech:SubscriptionKey` in User Secrets, never commit
- **Azure TTS connection failures** (`WS_OPEN_ERROR_UNDERLYING_IO_OPEN_FAILED`, SDK `ConnectionFailure`/`ServiceTimeout`/`ServiceUnavailable`) are retried once in `AzureTtsService` and then thrown as `TtsUpstreamUnavailableException` (Core, derives from `InvalidOperationException`); `TtsSendPipeline` and `PortalTtsSynthesisController` map it to `503 tts_upstream_unavailable`. Other SDK cancellations stay `InvalidOperationException` → `400 tts_not_configured`. Tests override `AzureTtsService.RunSynthesisAttemptAsync` instead of hitting the SDK.
- **Audio settings are per-guild** via `GuildAudioSettings`
- **Leaving voice must go through `IVoiceChannel.DisconnectAsync()`** (→ `SocketGuild.DisconnectAudioAsync`). Calling `IAudioClient.StopAsync()`/`Dispose()` directly only closes the voice websocket; the gateway voice state is never cleared, so Discord keeps showing the bot in the channel while `AudioService.IsConnected` says false, and Discord.NET's `SocketGuild._audioClient` is left pointing at a disposed client. See `docs/lessons-learned/voice-channel-state-drift.md`.
- **Portal pages** use separate controllers (PortalSoundboardController, PortalTtsController, PortalVoxController)
- **Rate limiting:** VOX commands: 5 per 10 seconds
