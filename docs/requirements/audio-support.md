# Requirements: Audio Support & Soundboard

## Executive Summary

Add voice channel audio support to the Discord bot, enabling the bot to join voice channels and play audio. The primary feature for this release is a per-guild soundboard system where users can trigger pre-defined sound clips. This lays the foundation for future audio features including TTS and external audio playback.

**Target Release:** v0.7.0

---

## Problem Statement

Users want to play sound clips (memes, reactions, sound effects) in Discord voice channels. Currently the bot has no voice channel capabilities.

## Primary Purpose

Enable the bot to join voice channels and play audio, with a soundboard feature for triggering pre-defined sound clips.

## Target Users

- **Server members**: Play sounds via `/play` command while in voice channels
- **Server admins**: Manage soundboard (upload, delete, rename sounds) via admin UI
- **Bot administrators**: Configure global audio settings

---

## Core Features (MVP - v0.7.0)

### 1. Voice Channel Management

#### Join Voice Channel
- `/join` - Bot joins the user's current voice channel
- `/join <channel>` - Bot joins a specified voice channel
- Auto-join: When a user runs `/play` and the bot isn't in their channel, auto-join first
- One voice channel per guild at a time (Discord API limitation)

#### Leave Voice Channel
- `/leave` - Bot leaves the current voice channel
- Configurable auto-leave timeout when idle (default: stay indefinitely)
  - Setting of `0` = stay indefinitely
  - Any positive value = leave after N minutes of no audio playback

#### Permissions
- Configurable role restrictions per command (default: everyone)
- Empty allowed-roles list = everyone can use the command
- Settings apply to: `/join`, `/leave`, `/play`, `/sounds`, `/stop`

### 2. Soundboard Feature

#### Playing Sounds
- `/play <sound_name>` - Play a sound from the guild's soundboard
  - Autocomplete support for sound names
  - If bot not in user's voice channel, auto-join first
  - User must be in a voice channel to use
- `/sounds` - List all available sounds for the guild
- `/stop` - Stop current playback and clear the queue (Admin only by default)

#### Queue Behavior
- Sounds queue up (don't interrupt current playback)
- Configurable per-guild: enable/disable queue (default: enabled)
- When queue disabled: new `/play` replaces current sound

#### Sound Discovery
- Autocomplete on `/play` command shows matching sound names
- `/sounds` command lists all available sounds

### 3. Sound Management (Admin UI)

#### Soundboard Page (`/Guilds/{guildId}/Soundboard`)
- List all sounds with: name, duration, file size, upload date, play count
- Upload new sounds (file upload)
- Register sounds from server folder (discovery)
- Rename sounds (display name)
- Delete sounds (removes from database and file system)
- Preview/play sounds in browser

#### File Storage
- Sound files stored on local file system
- Configurable base path (default: `./sounds/`)
- Per-guild subfolders organized by guild ID:
  ```
  /sounds/
    /123456789012345678/    # guild ID
      airhorn.mp3
      bruh.mp3
    /987654321098765432/
      victory.ogg
  ```
- UI can upload files (saved to guild folder) or discover existing files in folder

#### Supported Formats
- MP3, WAV, OGG

#### Limits (Configurable per-guild)
- Maximum sound duration: 30 seconds (default)
- Maximum file size: TBD (generous default)
- Maximum sounds per guild: TBD (generous default)

### 4. Guild Settings

#### Quick Settings (Guild Settings Page)
- Audio enabled: Yes/No
- Auto-leave timeout
- Queue enabled: Yes/No

#### Dedicated Audio Settings Page (`/Guilds/{guildId}/AudioSettings`)
- All quick settings plus:
- Sound duration limit
- File size limit
- Max sounds per guild
- Role restrictions per command
- Sound folder path (read-only display)

---

## Data Model

### Sound Entity
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| GuildId | ulong | Discord guild ID |
| Name | string | Display name (for autocomplete/listing) |
| FileName | string | Actual filename on disk |
| FileSizeBytes | long | File size in bytes |
| DurationSeconds | double | Audio duration |
| UploadedById | ulong? | Discord user ID who uploaded (null if discovered) |
| UploadedAt | DateTime | When the sound was added |
| PlayCount | int | Number of times played |

### GuildAudioSettings Entity
| Field | Type | Description |
|-------|------|-------------|
| GuildId | ulong | Primary key, Discord guild ID |
| AudioEnabled | bool | Whether audio features are enabled |
| AutoLeaveTimeoutMinutes | int | 0 = stay indefinitely |
| QueueEnabled | bool | Whether sounds queue or replace |
| MaxDurationSeconds | int | Max sound duration |
| MaxFileSizeBytes | long | Max upload file size |
| MaxSoundsPerGuild | int | Sound count limit |
| MaxStorageBytes | long | Total storage limit per guild |

### CommandRoleRestriction Entity (or embed in settings)
| Field | Type | Description |
|-------|------|-------------|
| GuildId | ulong | Discord guild ID |
| CommandName | string | e.g., "play", "join", "leave" |
| AllowedRoleIds | List<ulong> | Empty = everyone allowed |

---

## Configuration Options

### SoundboardOptions (appsettings)
| Setting | Default | Description |
|---------|---------|-------------|
| BasePath | `./sounds` | Root folder for sound storage |
| DefaultMaxDurationSeconds | 30 | Default max sound duration |
| DefaultMaxFileSizeBytes | 10485760 (10MB) | Default max file size |
| DefaultMaxSoundsPerGuild | 100 | Default sound count limit |
| DefaultMaxStorageBytes | 524288000 (500MB) | Default total storage limit per guild |
| DefaultAutoLeaveTimeoutMinutes | 0 | Default idle timeout (0 = stay) |
| SupportedFormats | `["mp3", "wav", "ogg"]` | Allowed audio formats |

---

## Commands Summary

| Command | Description | Default Permission |
|---------|-------------|-------------------|
| `/join` | Join user's voice channel | Everyone |
| `/join <channel>` | Join specified voice channel | Everyone |
| `/leave` | Leave current voice channel | Everyone |
| `/play <sound>` | Play a sound (autocomplete) | Everyone |
| `/sounds` | List available sounds | Everyone |
| `/stop` | Stop playback and clear queue | Admin |

---

## Admin UI Pages

| Page | Route | Purpose |
|------|-------|---------|
| Soundboard | `/Guilds/{guildId}/Soundboard` | Sound CRUD, upload, discovery |
| Audio Settings | `/Guilds/{guildId}/AudioSettings` | Full audio configuration |
| Guild Settings | `/Guilds/Edit/{guildId}` | Quick audio settings section |

---

## Error Handling

| Scenario | Response |
|----------|----------|
| User not in voice channel | "You need to be in a voice channel to use this command." |
| Sound not found | "Sound '{name}' not found. Use `/sounds` to see available sounds." |
| Bot lacks channel permissions | "I don't have permission to join that voice channel." |
| Queue disabled, sound playing | Replace current sound (no error) |
| Sound file missing from disk | "Sound file not found. It may have been deleted." |
| Upload exceeds size limit | "File too large. Maximum size is {limit}." |
| Sound exceeds duration limit | "Sound too long. Maximum duration is {limit} seconds." |
| Guild sound limit reached | "Sound limit reached ({limit}). Delete some sounds first." |
| Guild storage limit reached | "Storage limit reached ({used} of {limit}). Delete some sounds first." |

---

## Future Features (Not in v0.7.0)

### TTS (Text to Speech)
- `/tts <message>` - Convert text to speech and play in voice channel
- Configurable voice/language
- Rate limiting to prevent spam

### Audio Playback from External Sources
- YouTube, Spotify, SoundCloud integration
- `/play <url>` - Play audio from URL
- Playlist support
- Volume control

### Soundboard Enhancements
- Categories/tags for organizing sounds
- User favorites
- Sound aliases
- Per-user upload permissions (beyond admin-only)

---

## Open Questions

1. **File size default** - What's a reasonable default max file size? 10MB suggested.
2. **Sound count default** - What's a reasonable default max sounds per guild? 100 suggested.
3. **FFmpeg dependency** - Audio processing likely requires FFmpeg. Document installation requirement?

---

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Per-guild soundboards (not global) | Each server has its own community and sound preferences |
| File system storage (not database blobs) | Easier manual management, bulk imports, disk efficiency |
| Queue by default (not interrupt) | Users want to spam sounds without cutting each other off |
| Admin-only sound management | Prevent abuse; can expand to role-based later |
| Everyone can play sounds | Small friend groups; role restrictions available for larger servers |
| Configurable auto-leave timeout | Avoid annoying join/leave notifications |
| Sound name defaults to filename | Reduce friction; easy rename available |

---

## Technical Considerations

### Discord.NET Audio
- Requires `Discord.Net.WebSocket` voice support
- Uses `IAudioClient` for voice connections
- One audio client per guild
- May require FFmpeg for audio format conversion
- Consider using `libopus` for Opus encoding

### Dependencies
- FFmpeg (likely required for audio processing)
- NAudio or similar for audio metadata extraction (duration)

---

## Recommended Next Steps

1. Create GitHub milestone for v0.7.0
2. Break down into epics/features:
   - Voice channel infrastructure (join/leave/audio client management)
   - Soundboard core (entity, repository, service)
   - Discord commands (slash commands, autocomplete)
   - Admin UI (soundboard page, audio settings)
   - Guild settings integration
3. Research Discord.NET audio implementation patterns
4. Determine FFmpeg installation/bundling strategy
