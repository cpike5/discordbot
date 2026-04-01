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

### SoundPlayLog

Records each time a sound is played for analytics and usage tracking.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | long | Primary key (auto-incrementing) |
| `SoundId` | Guid | Foreign key to Sound entity |
| `GuildId` | ulong | Discord guild ID where sound was played |
| `UserId` | ulong | Discord user ID who played the sound |
| `PlayedAt` | DateTime | Timestamp of play event (UTC) |
| `Sound` | Sound? | Navigation property to Sound entity |

**Notes:**
- Play logs are automatically created when a sound is played
- Old logs are periodically cleaned up by `SoundPlayLogRetentionService` based on retention policy
- Provides data for analytics, usage statistics, and audit trails
- Uses long (Int64) ID to support high-volume logging scenarios

## Services

### Core Services

| Service | Purpose |
|---------|---------|
| `IAudioService` | Voice channel connection management |
| `IPlaybackService` | Audio playback with FFmpeg transcoding |
| `ISoundService` | Sound CRUD operations and validation |
| `ISoundFileService` | File storage and audio metadata |
| `IGuildAudioSettingsService` | Guild audio configuration |

#### ISoundService Methods

**CRUD Operations:**
- `GetByIdAsync(id, guildId)` - Retrieve sound by ID with guild validation
- `GetAllByGuildAsync(guildId)` - Get all sounds for a guild (ordered by name)
- `GetByNameAsync(name, guildId)` - Case-insensitive sound lookup
- `CreateSoundAsync(sound)` - Create new sound (validates unique name)
- `DeleteSoundAsync(id, guildId)` - Delete sound with guild validation

**Validation:**
- `ValidateStorageLimitAsync(guildId, additionalBytes)` - Check storage space available
- `ValidateSoundCountLimitAsync(guildId)` - Check if guild can add more sounds

**Statistics:**
- `GetStorageUsedAsync(guildId)` - Total bytes used by guild's sounds
- `GetSoundCountAsync(guildId)` - Total sound count for guild

**Usage Tracking:**
- `IncrementPlayCountAsync(soundId)` - Increment play counter and update LastPlayedAt
- `LogPlayAsync(soundId, guildId, userId)` - Create play log entry for analytics

#### ISoundFileService Methods

Handles physical file I/O and audio metadata extraction:
- `GetSoundFilePath(guildId, fileName)` - Build full file system path
- `SoundFileExists(guildId, fileName)` - Check if file exists on disk
- `SaveSoundFileAsync(guildId, fileName, stream)` - Write audio file to disk
- `DeleteSoundFileAsync(guildId, fileName)` - Remove file from disk
- `GetAudioDurationAsync(filePath)` - Use FFprobe to extract duration
- `DiscoverSoundsAsync(guildId)` - Scan directory for existing audio files

#### IGuildAudioSettingsService Methods

Per-guild configuration management:
- `GetSettingsAsync(guildId)` - Retrieve guild audio settings
- `UpdateSettingsAsync(guildId, settings)` - Update audio configuration
- `ResetToDefaultsAsync(guildId)` - Restore default settings

### Background Services

| Service | Purpose |
|---------|---------|
| `VoiceAutoLeaveService` | Auto-disconnects bot when alone in channel |
| `SoundPlayLogRetentionService` | Automatically cleans up old play logs for storage efficiency |

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

## API Endpoints

### Admin Sounds API

**Base URL:** `/api/guilds/{guildId}/sounds`

**Authorization:** RequireViewer policy (all endpoints)

#### Download Sound

Downloads a sound file from the guild's soundboard.

**Endpoint:** `GET /api/guilds/{guildId}/sounds/{soundId}/download`

**Path Parameters:**
- `guildId` (ulong) - The guild's Discord snowflake ID
- `soundId` (Guid) - The sound's unique identifier

**Response:**
- **200 OK** - Returns the sound file with appropriate content type (audio/mpeg, audio/wav, audio/ogg, audio/mp4)
- **404 Not Found** - Sound not found or doesn't belong to guild, or file missing from storage

**Example:**
```bash
curl -H "Authorization: Bearer {token}" \
  "https://localhost:5001/api/guilds/123456789012345678/sounds/a1b2c3d4-e5f6-7890-a1b2-c3d4e5f6a7b8/download"
```

**Response Headers:**
```
Content-Type: audio/mpeg
Content-Disposition: attachment; filename="airhorn.mp3"
Content-Length: 234567
```

### Portal Soundboard API

**Base URL:** `/api/portal/soundboard/{guildId}`

**Authorization:** PortalGuildMember policy (all endpoints)

#### Get Guild Sounds

Retrieves all sounds for a guild with play counts.

**Endpoint:** `GET /api/portal/soundboard/{guildId}/sounds`

**Path Parameters:**
- `guildId` (ulong) - The guild's Discord snowflake ID

**Query Parameters:**
- None

**Response: 200 OK**
```json
[
  {
    "id": "a1b2c3d4-e5f6-7890-a1b2-c3d4e5f6a7b8",
    "name": "airhorn",
    "duration": 2.5,
    "fileSizeBytes": 234567,
    "playCount": 42,
    "uploadedAt": "2024-12-08T15:30:00Z"
  }
]
```

**Response Fields:**
| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid | Sound unique identifier |
| `name` | string | Display name |
| `duration` | double | Audio duration in seconds |
| `fileSizeBytes` | long | File size in bytes |
| `playCount` | int | Number of times played |
| `uploadedAt` | DateTime | Upload timestamp (UTC) |

**Error Responses:**
- **400 Bad Request** - Audio features globally disabled

#### Upload Sound

Uploads a new sound file to the guild's soundboard.

**Endpoint:** `POST /api/portal/soundboard/{guildId}/sounds`

**Path Parameters:**
- `guildId` (ulong) - The guild's Discord snowflake ID

**Request Body (multipart/form-data):**
```
file: <audio file>
name: <optional display name, defaults to filename>
```

**Response: 200 OK**
```json
{
  "id": "a1b2c3d4-e5f6-7890-a1b2-c3d4e5f6a7b8",
  "name": "airhorn",
  "duration": 2.5,
  "fileSizeBytes": 234567,
  "playCount": 0,
  "uploadedAt": "2024-12-08T15:30:00Z"
}
```

**Error Responses:**
- **400 Bad Request** - Invalid file, exceeds limits, unsupported format, or storage limit exceeded
- **409 Conflict** - Sound with same name already exists in guild

**Validation:**
- Supported formats: mp3, wav, ogg, m4a
- File size must not exceed guild's max file size limit
- Duration must not exceed guild's max duration limit
- Total guild storage must not exceed storage limit
- Sound count must not exceed guild's max sounds limit

#### Play Sound from Portal

Plays a sound in the bot's current voice channel.

**Endpoint:** `POST /api/portal/soundboard/{guildId}/play/{soundId}`

**Path Parameters:**
- `guildId` (ulong) - The guild's Discord snowflake ID
- `soundId` (Guid) - The sound's unique identifier

**Request Body:**
```json
{
  "filter": "None"
}
```

**Request Fields:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `filter` | string | No | Audio filter to apply (None, Reverb, BassBoost, TrebleBoost, PitchUp, PitchDown, Nightcore, SlowMo). Default: None |

**Response: 200 OK**
```json
{
  "status": "playing",
  "message": "Now playing: airhorn"
}
```

**Error Responses:**
- **400 Bad Request** - Audio disabled, bot not in voice channel, or sound not found
- **404 Not Found** - Sound file missing

#### List Voice Channels

Gets all voice channels in the guild.

**Endpoint:** `GET /api/portal/soundboard/{guildId}/channels`

**Path Parameters:**
- `guildId` (ulong) - The guild's Discord snowflake ID

**Response: 200 OK**
```json
[
  {
    "id": "987654321098765432",
    "name": "General Voice",
    "userCount": 3
  },
  {
    "id": "987654321098765433",
    "name": "Gaming",
    "userCount": 0
  }
]
```

**Response Fields:**
| Field | Type | Description |
|-------|------|-------------|
| `id` | ulong | Channel Discord snowflake ID |
| `name` | string | Channel name |
| `userCount` | int | Number of users in channel (excluding bots) |

#### Join Voice Channel

Joins a specific voice channel.

**Endpoint:** `POST /api/portal/soundboard/{guildId}/channel`

**Path Parameters:**
- `guildId` (ulong) - The guild's Discord snowflake ID

**Request Body:**
```json
{
  "channelId": "987654321098765432"
}
```

**Request Fields:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `channelId` | ulong | Yes | Discord channel ID to join |

**Response: 200 OK**
```json
{
  "status": "connected",
  "channelId": "987654321098765432",
  "channelName": "General Voice"
}
```

**Error Responses:**
- **400 Bad Request** - Bot cannot access channel or invalid channel ID
- **404 Not Found** - Channel not found

#### Leave Voice Channel

Disconnects the bot from the current voice channel.

**Endpoint:** `DELETE /api/portal/soundboard/{guildId}/channel`

**Path Parameters:**
- `guildId` (ulong) - The guild's Discord snowflake ID

**Response: 200 OK**
```json
{
  "status": "disconnected",
  "message": "Left voice channel"
}
```

**Error Responses:**
- **400 Bad Request** - Bot not connected to a voice channel

#### Stop Playback

Stops the currently playing sound and clears the queue.

**Endpoint:** `POST /api/portal/soundboard/{guildId}/stop`

**Path Parameters:**
- `guildId` (ulong) - The guild's Discord snowflake ID

**Response: 200 OK**
```json
{
  "status": "stopped",
  "message": "Playback stopped"
}
```

**Error Responses:**
- **400 Bad Request** - Bot not playing audio

#### Get Playback Status

Gets the current playback status and queue information.

**Endpoint:** `GET /api/portal/soundboard/{guildId}/status`

**Path Parameters:**
- `guildId` (ulong) - The guild's Discord snowflake ID

**Response: 200 OK**
```json
{
  "isPlaying": true,
  "currentSoundId": "a1b2c3d4-e5f6-7890-a1b2-c3d4e5f6a7b8",
  "currentSoundName": "airhorn",
  "position": 1.5,
  "duration": 2.5,
  "queueLength": 2,
  "isConnected": true,
  "currentChannelId": "987654321098765432"
}
```

**Response Fields:**
| Field | Type | Description |
|-------|------|-------------|
| `isPlaying` | bool | Whether audio is currently playing |
| `currentSoundId` | Guid? | ID of currently playing sound |
| `currentSoundName` | string? | Name of currently playing sound |
| `position` | double | Current playback position in seconds |
| `duration` | double | Total duration of current sound in seconds |
| `queueLength` | int | Number of sounds in queue |
| `isConnected` | bool | Whether bot is connected to voice channel |
| `currentChannelId` | ulong? | ID of current voice channel |

## UI Pages

### Admin Soundboard Management Page

**URL:** `/Guilds/Soundboard/{guildId}`

**Authorization:** RequireAdmin policy

**Features:**

1. **Sound Library Viewer**
   - Table showing all uploaded sounds with metadata
   - Columns: Name, File Size, Duration, Play Count, Uploaded By, Upload Date
   - Sortable by any column
   - Search/filter functionality

2. **Upload Sound**
   - Drag-and-drop or file picker for audio files
   - Validates format, size, and duration before upload
   - Custom display name (optional, defaults to filename)
   - Real-time validation feedback

3. **Manage Sounds**
   - Delete individual sounds
   - Bulk delete operations
   - Rename sounds (updates display name only)
   - View sound details (ID, file path, upload info)

4. **Discover Sounds**
   - Scan file system for audio files in the guild's sound directory
   - Import discovered files as new sounds
   - Batch import with progress indication

5. **Voice Channel Controls**
   - Connect to a voice channel for testing playback
   - List available voice channels
   - View current connection status
   - Test playback of sounds in real-time
   - Monitor playback progress with status updates

6. **Storage Statistics**
   - Total storage used vs. configured limit
   - Per-sound file size
   - Guild-level usage summary
   - Visual storage usage indicator

### Portal Soundboard Page

**URL:** `/Portal/Soundboard/{guildId}`

**Authorization:** AllowAnonymous (landing page), PortalGuildMember (authenticated users)

**Features:**

1. **Landing Page (Unauthenticated)**
   - Description of soundboard functionality
   - Login prompt for guild members
   - Information about audio features

2. **Soundboard Control Panel (Authenticated)**
   - **Sound Browser**
     - List of all available sounds
     - Sort by name, play count, upload date
     - Search by sound name
     - Audio preview for each sound
     - Display of sound metadata (duration, file size, play count)

   - **Voice Channel Navigation**
     - List of available voice channels with member counts
     - Current connection status indicator
     - One-click channel switching
     - Auto-join option for user's current voice channel

   - **Playback Controls**
     - Play button for each sound
     - Filter selection dropdown
     - Stop/pause controls
     - Queue display (if queue mode enabled)
     - Now Playing with progress bar via unified `_VoiceChannelPanel` component

   - **Real-Time Status Updates** (via SignalR)
     - Connection status updates
     - Now Playing with progress bar (unified `_VoiceChannelPanel` component)
     - Queue updates
     - Error notifications

   - **Settings Display**
     - Show active audio settings (queue mode, silent mode, timeout)
     - Display guild storage usage
     - Show sound count limits
     - Display max duration and file size limits

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

## Sound Play Logging and Analytics

The soundboard feature includes a comprehensive play logging system for tracking sound usage.

### Play Logging

Every time a sound is played, a `SoundPlayLog` entry is created with:
- Sound ID being played
- Guild ID where it was played
- User ID who played it
- Timestamp (UTC)

**Logging occurs automatically during playback:**
1. User initiates playback via slash command or portal
2. Sound playback starts successfully
3. `LogPlayAsync()` is called to record the event
4. Log entry is saved asynchronously (non-blocking)

**Play Log Retention:**

The `SoundPlayLogRetentionService` background service automatically cleans up old play logs:
- Runs on a configurable schedule
- Removes logs older than the configured retention period
- Prevents database bloat from continuous logging
- Configurable via `SoundPlayLogRetentionOptions`

**Configuration:**
```json
{
  "SoundPlayLogRetention": {
    "RetentionDays": 90,
    "CheckIntervalMinutes": 60
  }
}
```

| Option | Default | Description |
|--------|---------|-------------|
| `RetentionDays` | 90 | Keep play logs for this many days |
| `CheckIntervalMinutes` | 60 | How often to check for expired logs |

### Analytics Use Cases

Play log data supports:
- **Usage Statistics**: See which sounds are most popular
- **User Behavior**: Track who plays which sounds and when
- **Time Series Analysis**: Identify peak usage times
- **Guild Trends**: Monitor soundboard engagement over time
- **Audit Trail**: Maintain historical record of sound usage
- **Capacity Planning**: Identify when sounds should be rotated or removed

### Querying Play Data

Access play logs through:
- `ISoundService.LogPlayAsync()` - Record a new play event
- Direct database queries via `SoundPlayLogRepository`
- Analytics dashboards and reports

**Example: Most Played Sounds**
```csharp
var logs = await _soundPlayLogRepository.GetByGuildIdAsync(guildId, cancellationToken);
var mostPlayed = logs
    .GroupBy(l => l.SoundId)
    .OrderByDescending(g => g.Count())
    .Take(10)
    .Select(g => new { SoundId = g.Key, PlayCount = g.Count() });
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
- [Unified Now Playing](unified-now-playing.md) - Shared Now Playing component architecture
