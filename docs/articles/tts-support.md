# Text-to-Speech (TTS) Support

This document describes the Text-to-Speech (TTS) feature, which allows Discord users to convert text to speech and play it in voice channels, and enables administrators to manage TTS settings and send TTS messages from the admin UI.

## Overview

The TTS feature provides:
- Voice channel TTS playback via slash commands
- Azure Cognitive Services Speech integration for high-quality neural voices
- Per-guild TTS configuration with voice, speed, pitch, and volume settings
- Admin UI for sending TTS messages and managing settings
- TTS message history and analytics
- Rate limiting to prevent spam
- Real-time playback status via SignalR

## Prerequisites

Before using TTS features, ensure the following are configured:

### Azure Speech Service

1. **Azure Subscription** - Active Azure account with Speech service resource
2. **Azure Speech SDK** - Automatically included via NuGet package
3. **User Secrets Configuration** - Subscription key must be configured (never commit to source control)

See [Azure Speech Configuration](#azure-speech-configuration) section for setup instructions.

### Audio Dependencies

TTS uses the same audio infrastructure as the Soundboard feature:
- **FFmpeg** - for audio transcoding
- **libsodium** - for voice encryption
- **libopus** - for Opus audio encoding

See [Audio Dependencies](audio-dependencies.md) for detailed installation instructions.

## Slash Commands

### TtsModule

| Command | Description | Preconditions |
|---------|-------------|---------------|
| `/tts <message> [voice]` | Convert text to speech and play in voice channel | `RequireGuildActive`, `RequireTtsEnabled`, `RequireVoiceChannel`, `RateLimit(5, 10)` |

**Parameters:**
- `message` (required) - The text to speak (max 500 characters by default)
- `voice` (optional) - Voice identifier to use for synthesis (autocomplete supported)

**Autocomplete:** The `/tts` command supports autocomplete for voice names, filtering available voices as the user types.

**Example usage:**
```
/tts message:"Hello, this is a test of the TTS system"
/tts message:"Welcome to the server!" voice:en-US-GuyNeural
```

**Rate Limiting:**
- Default: 5 messages per 10 seconds (configured via `RateLimit` attribute)
- Per-guild: Configurable via `GuildTtsSettings.RateLimitPerMinute`

**Behavior:**
1. Verifies user is in a voice channel
2. Auto-joins the user's voice channel if bot is not connected
3. Synthesizes speech using Azure Cognitive Services
4. Plays audio in the voice channel
5. Logs message to TTS history

## Admin UI

### Text-to-Speech Management Page

**URL:** `/Guilds/TextToSpeech/{guildId}`

**Authorization:** RequireAdmin policy

**Features:**
- Send TTS messages from web interface with custom text input
- Preview TTS audio in browser before sending to channel
- Configure voice, speed, pitch, and volume settings
- View recent TTS messages with replay capability
- Real-time playback status (via SignalR)
- Voice channel control panel (join/leave/status)
- TTS message history with user information and timestamps

**Settings available:**
- Enable/disable TTS for the guild
- Default voice selection (from available Azure voices)
- Default speed (0.5x to 2.0x)
- Default pitch (0.5 to 1.5)
- Default volume (0.0 to 1.0)
- Maximum message length (characters)
- Rate limit (messages per user per minute)
- Auto-play on send option
- Join/leave announcements (optional)

## TTS Member Portal

Guild members can access a web-based TTS portal to send text-to-speech messages without using Discord slash commands or admin privileges.

**Portal URL:** `/Portal/TTS/{guildId}`

**Authorization:** Discord OAuth required (guild membership verified)

### Features

- **Authentication**: Discord OAuth with automatic guild membership verification
- **Landing Page**: Unauthenticated users see guild info and "Sign in with Discord" button
- **Voice Channel Selection**: Join/leave voice channels via dropdown selector
- **Message Input**: Large textarea with real-time character counter (respects guild max length)
- **Voice Customization**:
  - Voice selector dropdown (categorized by locale: English US, English UK, Spanish, etc.)
  - Speed slider (0.5-2.0x)
  - Pitch slider (0.5-2.0x)
- **Now Playing**: Shows currently playing TTS message via unified `_VoiceChannelPanel` component (real-time SignalR updates)
- **Rate Limiting**: Enforced per-user based on `GuildTtsSettings.RateLimitPerMinute`
- **Real-time Status**: Connection and playback status via SignalR (replaced polling)
- **Mobile Responsive**: Sidebar stacks on mobile devices, touch-friendly controls

### Access Control

**Unauthenticated Users**
- See landing page with guild name and icon
- "Sign in with Discord" button redirects to OAuth flow
- Return URL preserved to redirect back to portal after authentication

**Authenticated Non-Members**
- See "Access Denied" message
- Explanation that user must be a member of the guild
- Link to Discord server invite (if applicable)

**Guild Members**
- Full portal access with all features enabled
- Can send TTS messages to voice channels
- Subject to same rate limits as slash command users

### Configuration

The portal respects all guild TTS settings from `GuildTtsSettings`:
- `TtsEnabled` - Portal returns 404 if TTS is disabled for the guild
- `MaxMessageLength` - Character limit enforced in textarea
- `RateLimitPerMinute` - Rate limiting enforced per user (429 response on exceed)
- `DefaultVoice` - Initial voice selection in dropdown
- `DefaultSpeed` - Initial speed slider value
- `DefaultPitch` - Initial pitch slider value

### User Experience

**Sending a TTS Message:**
1. Select a voice channel from the dropdown (bot joins automatically)
2. Type message in textarea (character counter updates in real-time)
3. Optionally adjust voice, speed, and pitch settings
4. Click "Send to Channel" button
5. Button shows loading state during synthesis
6. Textarea clears on success, toast notification appears
7. Voice/speed/pitch settings remain for next message

**Rate Limiting:**
- After reaching rate limit, user receives 429 error with clear message
- Toast notification shows remaining wait time
- Send button remains functional (server enforces limit)

**Connection Status:**
- Green indicator when connected to voice channel
- Gray indicator when not connected
- Send button disabled when not connected
- Status updates in real-time via SignalR

**Now Playing Panel** (unified component):

Now Playing is provided by the `_VoiceChannelPanel` component with `ShowNowPlaying = true` and `ShowProgress = false`:
- Appears when TTS audio is playing
- Shows "Playing..." text indicator (no progress bar, since TTS has no known duration)
- Stop button to interrupt current playback
- Hides automatically when playback completes
- Real-time updates via SignalR (no polling overhead)

See [Unified Now Playing](unified-now-playing.md) for architecture details.

### Technical Implementation

**Frontend:**
- Razor Page with two states: landing page (unauthenticated) and full portal (authenticated)
- Vanilla JavaScript with AJAX for form submission (no page reloads)
- Real-time SignalR updates for connection and playback state (replaced 3-second polling)
- Toast notification system for success/error feedback
- Character counter with real-time updates and warning colors

**Backend API:**
- `GET /api/portal/tts/{guildId}/status` - Connection and playback status
- `POST /api/portal/tts/{guildId}/send` - Synthesize and play TTS message
- `POST /api/portal/tts/{guildId}/channel` - Join voice channel
- `DELETE /api/portal/tts/{guildId}/channel` - Leave voice channel
- `POST /api/portal/tts/{guildId}/stop` - Stop current playback

**Security:**
- All API endpoints require `[Authorize(Policy = "PortalGuildMember")]`
- Guild membership verified via Discord API on each request
- Rate limiting enforced server-side (user-level key: `tts:{guildId}:{userId}`)
- XSS prevention: User-submitted text rendered as `textContent` (not `innerHTML`)
- Input validation: Message length, voice name, speed/pitch ranges

**Logging:**
- All TTS messages sent via portal logged to `TtsMessages` table
- Includes: GuildId, UserId, Username, Message, Voice, DurationSeconds, CreatedAt
- Same logging mechanism as slash command TTS

### Differences from Admin UI

| Feature | Admin UI | Member Portal |
|---------|----------|---------------|
| **Authorization** | Requires Admin role | Requires guild membership only |
| **URL Pattern** | `/Guilds/TextToSpeech/{guildId}` | `/Portal/TTS/{guildId}` |
| **Message History** | Yes (view all messages) | No (send-only) |
| **Preview Audio** | Yes (browser playback) | No |
| **Settings Management** | Yes (edit guild settings) | No (read-only) |
| **Landing Page** | N/A (requires login) | Yes (for unauthenticated users) |
| **Rate Limiting** | Admin bypass (optional) | Always enforced |
| **Analytics** | Yes (usage stats) | No |

### Use Cases

**Community Servers:**
- Allow members to send announcements via TTS without moderator privileges
- Useful for events, game nights, or community gatherings

**Friend Servers:**
- Casual TTS access for all members without command syntax
- Web-based interface more accessible than slash commands

**Music/Entertainment Bots:**
- Provide TTS as a member-accessible feature alongside soundboard
- Integrated voice channel controls for seamless audio experience

**Accessibility:**
- Web interface may be easier than Discord commands for some users
- Keyboard navigation and screen reader support

### Future Enhancements (Out of Scope)

Potential future additions to the portal:
- **Preview Button**: Client-side audio preview before sending to channel
- **Message History**: Show user's own TTS message history with replay
- **Voice Presets**: Save favorite voice/speed/pitch combinations per user
- **Queue Management**: View queued messages when multiple users send simultaneously
- **Message Templates**: Save frequently used phrases for quick access
- **SSML Support**: Advanced users can write custom SSML for prosody control

## Azure Speech Configuration

### User Secrets Setup

Configure Azure Speech credentials via User Secrets (never commit to appsettings.json):

```bash
cd src/DiscordBot.Bot
dotnet user-secrets set "AzureSpeech:SubscriptionKey" "your-azure-speech-subscription-key"
dotnet user-secrets set "AzureSpeech:Region" "eastus"  # Optional, defaults to eastus
```

### Azure Portal Setup

1. Go to https://portal.azure.com
2. Create a Speech service resource (or use existing)
3. Go to **Keys and Endpoint** in the resource
4. Copy **Key 1** or **Key 2** to user secrets as `SubscriptionKey`
5. Note the **Region** (e.g., "eastus", "westus2") and set as `Region` if different from default

### Available Regions

Common Azure Speech regions:
- `eastus` - East US (default)
- `westus` - West US
- `westus2` - West US 2
- `eastus2` - East US 2
- `westeurope` - West Europe
- `southeastasia` - Southeast Asia

See [Azure Speech regions documentation](https://learn.microsoft.com/azure/ai-services/speech-service/regions) for the full list.

## Configuration

### AzureSpeechOptions

Configure in `appsettings.json` under the `AzureSpeech` section:

```json
{
  "AzureSpeech": {
    "SubscriptionKey": null,
    "Region": "eastus",
    "DefaultVoice": "en-US-JennyNeural",
    "MaxTextLength": 500,
    "DefaultSpeed": 1.0,
    "DefaultPitch": 1.0,
    "DefaultVolume": 0.8
  }
}
```

| Option | Default | Description |
|--------|---------|-------------|
| `SubscriptionKey` | `null` | Azure Speech subscription key (configure via user secrets) |
| `Region` | `"eastus"` | Azure region for Speech service |
| `DefaultVoice` | `"en-US-JennyNeural"` | Default voice identifier |
| `MaxTextLength` | `500` | Maximum text length for synthesis (characters) |
| `DefaultSpeed` | `1.0` | Default speech rate multiplier (0.5 to 2.0) |
| `DefaultPitch` | `1.0` | Default pitch adjustment (0.5 to 1.5) |
| `DefaultVolume` | `0.8` | Default volume level (0.0 to 1.0) |

**Note:** The `SubscriptionKey` should NEVER be stored in `appsettings.json`. Always use user secrets or environment variables for production.

## Database Entities

### TtsMessage

Represents a TTS message that was played in a voice channel.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | Guid | Primary key |
| `GuildId` | ulong | Discord guild ID |
| `UserId` | ulong | User who sent the message |
| `Username` | string | Username at time of sending |
| `Message` | string | Text content |
| `Voice` | string | Voice used for synthesis |
| `DurationSeconds` | double | Audio duration |
| `CreatedAt` | DateTime | When sent (UTC) |

### GuildTtsSettings

Per-guild TTS configuration.

| Property | Type | Description |
|----------|------|-------------|
| `GuildId` | ulong | Primary key (Discord guild ID) |
| `TtsEnabled` | bool | Whether TTS is enabled |
| `DefaultVoice` | string | Default voice identifier |
| `DefaultSpeed` | double | Default speech rate (1.0 = normal) |
| `DefaultPitch` | double | Default pitch (1.0 = normal) |
| `DefaultVolume` | double | Default volume (0.0 to 1.0) |
| `MaxMessageLength` | int | Character limit per message |
| `RateLimitPerMinute` | int | Max messages per user per minute |
| `AutoPlayOnSend` | bool | Auto-play when sending from UI |
| `AnnounceJoinsLeaves` | bool | TTS announce member events |
| `CreatedAt` | DateTime | Settings created (UTC) |
| `UpdatedAt` | DateTime | Last updated (UTC) |

## Services

### Core Services

| Service | Purpose |
|---------|---------|
| `ITtsService` | Azure Speech synthesis and voice management |
| `ITtsSettingsService` | Guild TTS configuration and rate limiting |
| `ITtsHistoryService` | TTS message logging and history retrieval |
| `ITtsMessageRepository` | TTS message data access |
| `IGuildTtsSettingsRepository` | TTS settings data access |
| `IAudioService` | Voice channel connection management (shared with Soundboard) |

### ITtsService Interface

```csharp
public interface ITtsService
{
    Task<Stream> SynthesizeSpeechAsync(string text, TtsOptions? options = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<VoiceInfo>> GetAvailableVoicesAsync(string? locale = "en-US", CancellationToken cancellationToken = default);
    bool IsConfigured { get; }
}
```

**Methods:**
- `SynthesizeSpeechAsync` - Converts text to PCM audio stream (48kHz, 16-bit, stereo)
- `GetAvailableVoicesAsync` - Retrieves available voices for a locale
- `IsConfigured` - Checks if Azure Speech is configured

### SignalR Notifications

TTS integrates with the audio notification system (shared with Soundboard):

| Event | Description |
|-------|-------------|
| `AudioConnected` | Bot connected to voice channel |
| `AudioDisconnected` | Bot disconnected from voice channel |
| `PlaybackStarted` | TTS playback started |
| `PlaybackProgress` | Playback progress update |
| `PlaybackFinished` | TTS playback finished |

## Preconditions

### RequireTtsEnabled

Ensures TTS features are enabled for the guild. Commands will fail with a user-friendly message if disabled.

```csharp
[RequireTtsEnabled]
public async Task TtsAsync(string message, string? voice = null) { ... }
```

**Error message when disabled:**
> Text-to-speech is disabled for this server.

### RequireVoiceChannel

Ensures the user is in a voice channel before executing the command (shared precondition with audio features).

```csharp
[RequireVoiceChannel]
public async Task TtsAsync(...) { ... }
```

## Azure Neural Voices

Azure Cognitive Services provides high-quality neural voices in multiple languages and styles.

### Popular English Voices

| Voice ID | Language | Gender | Description |
|----------|----------|--------|-------------|
| `en-US-JennyNeural` | US English | Female | Friendly, natural (default) |
| `en-US-GuyNeural` | US English | Male | Clear, professional |
| `en-US-AriaNeural` | US English | Female | Expressive, conversational |
| `en-US-DavisNeural` | US English | Male | Warm, authoritative |
| `en-US-JaneNeural` | US English | Female | Calm, informative |
| `en-GB-SoniaNeural` | British English | Female | Clear, professional |
| `en-GB-RyanNeural` | British English | Male | Authoritative |
| `en-AU-NatashaNeural` | Australian English | Female | Friendly |
| `en-CA-ClaraNeural` | Canadian English | Female | Clear |

### Voice Discovery

Get all available voices via the admin UI or by calling:
```csharp
var voices = await _ttsService.GetAvailableVoicesAsync("en-US");
```

See [Azure Speech voice gallery](https://learn.microsoft.com/azure/ai-services/speech-service/language-support) for the complete list.

## TTS Playback Flow

1. User runs `/tts message:"Hello world"` or admin sends from web UI
2. System checks:
   - TTS is enabled for guild (`RequireTtsEnabled`)
   - User is in voice channel (`RequireVoiceChannel`)
   - Message length is within limit
   - User is not rate-limited
3. Bot verifies Azure Speech is configured
4. Bot auto-joins user's voice channel (if not already connected)
5. Text is synthesized via Azure Speech SDK using SSML for voice control
6. Azure returns audio as WAV format
7. FFmpeg transcodes to PCM (48kHz, 16-bit, stereo)
8. Audio is encrypted via libsodium
9. Opus-encoded audio streams to Discord voice server
10. Message is logged to `TtsMessage` history
11. Success response sent to user

## Rate Limiting

TTS implements two layers of rate limiting:

### Command-Level Rate Limit

`[RateLimit(5, 10)]` attribute on TtsModule limits users to 5 command invocations per 10 seconds.

### Guild-Level Rate Limit

`GuildTtsSettings.RateLimitPerMinute` (default: 5) limits users to N TTS messages per minute per guild.

**When rate limited:**
> You're sending TTS messages too quickly. Please wait a moment before trying again.

## Troubleshooting

### "TTS Not Available" Error

**Symptom:** User runs `/tts` and gets "Text-to-speech is not configured on this server."

**Solutions:**
1. Verify Azure Speech subscription key is configured in user secrets
2. Check `SubscriptionKey` is not null or empty
3. Verify `Region` matches your Azure resource
4. Review logs for Azure Speech SDK errors

### "Text-to-speech is disabled for this server" Error

**Symptom:** Command fails with disabled message.

**Solutions:**
1. Go to `/Guilds/TextToSpeech/{guildId}` admin page
2. Enable TTS toggle in settings
3. Save settings

### No audio plays after synthesis

**Symptom:** Command succeeds but no audio is heard.

**Solutions:**
1. Check FFmpeg is installed: `ffmpeg -version`
2. Verify libsodium and libopus are available
3. Check bot has voice permissions in the channel
4. Review logs for audio-related errors
5. Try `/leave` and then `/tts` again to reset connection

### Audio cuts off at the beginning

This is a Discord quirk. The bot sends a brief silence frame before audio to "wake up" the UDP connection. The first few milliseconds may be cut off.

### Rate limit errors

**Symptom:** User gets rate limited frequently.

**Solutions:**
1. Adjust `RateLimitPerMinute` in guild TTS settings
2. Increase cooldown period in `[RateLimit]` attribute
3. Educate users on rate limits

### Azure Speech API errors

**Symptom:** Synthesis fails with Azure-related errors.

**Solutions:**
1. Verify subscription key is correct and active
2. Check Azure Speech resource is not paused or deleted
3. Verify region matches the resource location
4. Check Azure subscription has available quota
5. Review Azure Speech service status page

## Security Considerations

### Subscription Key Protection

- **NEVER** commit `SubscriptionKey` to source control
- Use user secrets for local development
- Use environment variables or Azure Key Vault for production
- Rotate keys periodically via Azure Portal

### Input Validation

- Message length is enforced at command and guild level
- SSML injection is mitigated via Azure Speech SDK escaping
- Rate limiting prevents spam and API abuse

### Voice Channel Permissions

- Bot must have `Connect` and `Speak` permissions in voice channel
- Users must be in a voice channel to use `/tts`
- Admin UI respects guild access authorization

## Cost Management

Azure Cognitive Services Speech is a paid service. Monitor usage to control costs:

### Pricing (as of 2025)

- **Neural Voices:** ~$16 per 1 million characters
- **Free Tier:** 0.5 million characters free per month

### Cost Optimization Tips

1. Set reasonable `MaxMessageLength` per guild (default 500)
2. Implement aggressive rate limiting for high-traffic servers
3. Monitor `TtsMessage` table for usage analytics
4. Consider disabling TTS for inactive guilds
5. Use Azure Cost Management alerts

### Usage Monitoring

Query TTS usage via database:
```sql
SELECT
    GuildId,
    COUNT(*) AS MessageCount,
    SUM(LENGTH(Message)) AS TotalCharacters,
    SUM(DurationSeconds) AS TotalDurationSeconds
FROM TtsMessages
WHERE CreatedAt >= DATE('now', '-30 days')
GROUP BY GuildId
ORDER BY TotalCharacters DESC;
```

## See Also

- [Audio Dependencies](audio-dependencies.md) - FFmpeg, libsodium, libopus setup
- [Soundboard](soundboard.md) - Related audio playback feature
- [Unified Now Playing](unified-now-playing.md) - Shared Now Playing component architecture
- [SignalR Real-Time Updates](signalr-realtime.md) - Dashboard real-time notifications
- [Authorization Policies](authorization-policies.md) - Admin UI access control
- [Azure Speech Service Documentation](https://learn.microsoft.com/azure/ai-services/speech-service/) - Official Azure docs
