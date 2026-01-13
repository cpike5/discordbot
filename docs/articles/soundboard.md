# Soundboard Feature

This document describes the Soundboard feature, which allows Discord users to play audio clips in voice channels and administrators to manage the sound library.

## Overview

The Soundboard feature provides:
- Voice channel audio playback via slash commands
- Per-guild sound libraries with upload/delete management
- Admin UI for sound management and configuration
- Configurable storage limits and audio settings per guild
- Real-time playback status via SignalR

## Prerequisites

Before using audio features, ensure the following dependencies are installed:
- **FFmpeg** - for audio transcoding
- **libsodium** - for voice encryption
- **libopus** - for Opus audio encoding

See [Audio Dependencies](audio-dependencies.md) for detailed installation instructions.

## Slash Commands

### SoundboardModule

| Command | Description | Preconditions |
|---------|-------------|---------------|
| `/play <sound> [filter]` | Play a sound from the soundboard with optional audio filter | `RequireGuildActive`, `RequireAudioEnabled` |
| `/sounds` | List all available sounds for the guild | `RequireGuildActive`, `RequireAudioEnabled` |
| `/stop` | Stop playback and clear queue (Admin only) | `RequireGuildActive`, `RequireAudioEnabled`, `RequireAdmin` |

**Autocomplete:** The `/play` command supports autocomplete for sound names, filtering available sounds as the user types.

### Audio Filters

The `/play` command supports an optional `filter` parameter to apply audio effects during playback. Filters are processed in real-time by FFmpeg.

| Filter | Description | Effect |
|--------|-------------|--------|
| `None` | No filter applied | Original audio unchanged |
| `Reverb` | Adds a reverb/echo effect | Creates spacious, echoing sound |
| `BassBoost` | Boosts bass frequencies | Deeper, more powerful low-end |
| `TrebleBoost` | Boosts treble frequencies | Brighter, crisper high-end |
| `PitchUp` | Raises the pitch | Higher-pitched audio (chipmunk effect) |
| `PitchDown` | Lowers the pitch | Lower-pitched audio (deep voice effect) |
| `Nightcore` | Higher pitch + faster tempo | Upbeat, energetic remix style |
| `SlowMo` | Slows down playback | Slower, drawn-out audio |

**Example usage:**
```
/play airhorn filter:Nightcore
/play bruh filter:BassBoost
/play victory filter:Reverb
```

**Note:** If a filter fails to apply (e.g., incompatible audio format), playback automatically falls back to unfiltered audio.

### VoiceModule

| Command | Description | Preconditions |
|---------|-------------|---------------|
| `/join` | Join the user's current voice channel | `RequireGuildActive`, `RequireAudioEnabled`, `RequireVoiceChannel` |
| `/join-channel <channel>` | Join a specific voice channel | `RequireGuildActive`, `RequireAudioEnabled` |
| `/leave` | Leave the current voice channel | `RequireGuildActive`, `RequireAudioEnabled` |

## Admin UI

### Soundboard Management Page

**URL:** `/Guilds/Soundboard/{guildId}`

**Authorization:** RequireAdmin policy

**Features:**
- View all uploaded sounds with metadata (name, file size, duration, play count)
- Upload new sound files (mp3, wav, ogg, m4a)
- Delete existing sounds
- Discover sounds from the file system (for bulk imports)
- Voice channel control panel for testing playback

### Audio Settings Page

**URL:** `/Guilds/AudioSettings/{guildId}`

**Authorization:** RequireAdmin policy

**Settings available:**
- Enable/disable audio features for the guild
- Auto-leave timeout (minutes before bot leaves when idle)
- Queue mode vs replace mode for playback
- Silent playback mode (suppress confirmation messages)
- Maximum sound duration (seconds)
- Maximum file size (bytes)
- Maximum sounds per guild
- Total storage limit (bytes)

## Configuration

### SoundboardOptions

Configure in `appsettings.json` under the `Soundboard` section:

```json
{
  "Soundboard": {
    "BasePath": "./sounds",
    "FfmpegPath": null,
    "FfprobePath": null,
    "DefaultMaxDurationSeconds": 30,
    "DefaultMaxFileSizeBytes": 10485760,
    "DefaultMaxSoundsPerGuild": 100,
    "DefaultMaxStorageBytes": 524288000,
    "DefaultAutoLeaveTimeoutMinutes": 0,
    "SupportedFormats": ["mp3", "wav", "ogg"]
  }
}
```

| Option | Default | Description |
|--------|---------|-------------|
| `BasePath` | `./sounds` | Root folder for sound file storage |
| `FfmpegPath` | `null` | Path to FFmpeg. `null` = use system PATH |
| `FfprobePath` | `null` | Path to FFprobe. `null` = use system PATH |
| `DefaultMaxDurationSeconds` | 30 | Maximum sound duration |
| `DefaultMaxFileSizeBytes` | 10485760 | Maximum file size (10MB) |
| `DefaultMaxSoundsPerGuild` | 100 | Maximum sounds per guild |
| `DefaultMaxStorageBytes` | 524288000 | Storage limit per guild (500MB) |
| `DefaultAutoLeaveTimeoutMinutes` | 0 | Idle timeout (0 = indefinite) |
| `SupportedFormats` | `["mp3", "wav", "ogg"]` | Allowed file formats |

### VoiceChannelOptions

Configure voice channel behavior:

```json
{
  "VoiceChannel": {
    "AutoLeaveTimeoutSeconds": 300,
    "CheckIntervalSeconds": 30
  }
}
```

| Option | Default | Description |
|--------|---------|-------------|
| `AutoLeaveTimeoutSeconds` | 300 | Timeout when bot is alone in channel (5 min) |
| `CheckIntervalSeconds` | 30 | Interval between auto-leave checks |

## Database Entities

### Sound

Represents an audio file in the soundboard.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | Guid | Primary key |
| `GuildId` | ulong | Discord guild ID |
| `Name` | string | Display name |
| `FileName` | string | Physical filename on disk |
| `FileSizeBytes` | long | File size in bytes |
| `DurationSeconds` | double | Audio duration |
| `UploadedById` | ulong? | User who uploaded |
| `UploadedAt` | DateTime | Upload timestamp (UTC) |
| `PlayCount` | int | Number of times played |

### GuildAudioSettings

Per-guild audio configuration.

| Property | Type | Description |
|----------|------|-------------|
| `GuildId` | ulong | Primary key (Discord guild ID) |
| `AudioEnabled` | bool | Whether audio is enabled |
| `AutoLeaveTimeoutMinutes` | int | Idle timeout (0 = indefinite) |
| `QueueEnabled` | bool | Queue mode vs replace mode |
| `SilentPlayback` | bool | Suppress "Now Playing" confirmation messages |
| `MaxDurationSeconds` | int | Max sound duration |
| `MaxFileSizeBytes` | long | Max file size |
| `MaxSoundsPerGuild` | int | Max sounds |
| `MaxStorageBytes` | long | Total storage limit |

## Services

### Core Services

| Service | Purpose |
|---------|---------|
| `IAudioService` | Voice channel connection management |
| `IPlaybackService` | Audio playback with FFmpeg transcoding |
| `ISoundService` | Sound CRUD operations and validation |
| `ISoundFileService` | File storage and audio metadata |
| `IGuildAudioSettingsService` | Guild audio configuration |

### Background Services

| Service | Purpose |
|---------|---------|
| `VoiceAutoLeaveService` | Auto-disconnects bot when alone in channel |

### SignalR Notifications

The `IAudioNotifier` service broadcasts real-time audio status to dashboard clients:

| Event | Description |
|-------|-------------|
| `AudioConnected` | Bot connected to voice channel |
| `AudioDisconnected` | Bot disconnected from voice channel |
| `PlaybackStarted` | Sound started playing |
| `PlaybackProgress` | Playback progress update |
| `PlaybackFinished` | Sound finished playing |
| `QueueUpdated` | Queue changed |

## Preconditions

### RequireAudioEnabled

Ensures audio features are enabled for the guild. Commands will fail with a user-friendly message if disabled.

```csharp
[RequireAudioEnabled]
public async Task PlayAsync([Autocomplete] string sound) { ... }
```

### RequireVoiceChannel

Ensures the user is in a voice channel before executing the command.

```csharp
[RequireVoiceChannel]
public async Task JoinAsync() { ... }
```

## File System Structure

Sounds are organized by guild ID:

```
sounds/
├── 123456789012345678/    # Guild ID
│   ├── airhorn.mp3
│   └── bruh.wav
└── 987654321098765432/    # Another guild
    └── victory.ogg
```

## Playback Flow

1. User runs `/play airhorn` (optionally with `filter:Nightcore`)
2. Bot verifies user is in voice channel (or auto-joins)
3. Sound file is located on disk
4. FFmpeg transcodes to PCM (48kHz, 16-bit, stereo), applying filter if specified
5. Audio is encrypted via libsodium
6. Opus-encoded audio streams to Discord voice server
7. Play count is incremented

## Queue vs Replace Mode

- **Queue Mode** (`QueueEnabled = true`): New sounds are added to a queue and play in order
- **Replace Mode** (`QueueEnabled = false`): New sounds stop current playback and play immediately

Configure per-guild in the Audio Settings admin page.

## Silent Playback Mode

When **Silent Playback** is enabled (`SilentPlayback = true`), the `/play` command suppresses the "Now Playing" and "Sound Queued" confirmation messages. This is useful for guilds that find the confirmation messages noisy or prefer a cleaner experience.

**Behavior:**
- **Enabled**: The bot plays sounds without showing any success confirmation
- **Disabled** (default): The bot shows an ephemeral embed confirming which sound is playing

**Important:** Error messages are always shown regardless of this setting. If a sound fails to play (e.g., sound not found, permission denied, file missing), users will still see the error message.

Configure per-guild in the Audio Settings admin page under "Silent Playback".

## Troubleshooting

### Bot joins but no audio plays

1. Check FFmpeg is installed: `ffmpeg -version`
2. Verify libsodium and libopus are available
3. Check bot has voice permissions in the channel
4. Review logs for audio-related errors

### "Audio features are disabled" error

Enable audio in the Admin UI: `/Guilds/AudioSettings/{guildId}`

### Sounds cut off at the beginning

This is a Discord quirk. The bot sends a brief silence frame before audio to "wake up" the UDP connection.

### Consecutive playback failures

The bot maintains a persistent PCM stream per guild. If audio fails after the first play, there may be a stream state issue. Try `/leave` and `/join` to reset the connection.

## See Also

- [Audio Dependencies](audio-dependencies.md) - FFmpeg, libsodium, libopus setup
- [SignalR Real-Time Updates](signalr-realtime.md) - Dashboard real-time notifications
