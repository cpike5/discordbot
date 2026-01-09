# Audio Dependencies

This document describes the external dependencies required for audio features (soundboard, voice channel playback).

## Overview

The bot's audio features require external tools for audio processing. These are not bundled with the application and must be installed separately on the host system.

## Required Dependencies

### FFmpeg

FFmpeg is required for audio format conversion and streaming to Discord voice channels.

| Platform | Installation |
|----------|--------------|
| Windows | Download from [ffmpeg.org](https://ffmpeg.org/download.html) or use `winget install ffmpeg` |
| Linux (Debian/Ubuntu) | `sudo apt install ffmpeg` |
| Linux (RHEL/Fedora) | `sudo dnf install ffmpeg` |
| macOS | `brew install ffmpeg` |
| Docker | Include in Dockerfile (see below) |

**Verify installation:**
```bash
ffmpeg -version
ffprobe -version
```

### Native Libraries

Discord.NET audio requires additional native libraries:

| Library | Purpose | Notes |
|---------|---------|-------|
| **libsodium** | Voice encryption | Usually bundled with Discord.NET on Windows; may need explicit installation on Linux |
| **libopus** | Opus audio encoding | Usually bundled with Discord.NET on Windows; may need explicit installation on Linux |

**Linux installation:**
```bash
# Debian/Ubuntu
sudo apt install libsodium23 libopus0

# RHEL/Fedora
sudo dnf install libsodium opus
```

## Configuration

Audio dependencies are configured in `appsettings.json` under the `Soundboard` section:

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

### Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `BasePath` | `./sounds` | Root folder for sound file storage |
| `FfmpegPath` | `null` | Path to FFmpeg executable. `null` means use system PATH |
| `FfprobePath` | `null` | Path to FFprobe executable. `null` means use system PATH |
| `DefaultMaxDurationSeconds` | 30 | Maximum sound duration in seconds |
| `DefaultMaxFileSizeBytes` | 10485760 | Maximum file size (10MB) |
| `DefaultMaxSoundsPerGuild` | 100 | Maximum sounds per guild |
| `DefaultMaxStorageBytes` | 524288000 | Total storage limit per guild (500MB) |
| `DefaultAutoLeaveTimeoutMinutes` | 0 | Idle timeout (0 = stay indefinitely) |
| `SupportedFormats` | `["mp3", "wav", "ogg"]` | Allowed audio file formats |

### Platform-Specific FFmpeg Paths

If FFmpeg is not in your system PATH, specify the full path:

```json
{
  "Soundboard": {
    "FfmpegPath": "C:/ffmpeg/bin/ffmpeg.exe",
    "FfprobePath": "C:/ffmpeg/bin/ffprobe.exe"
  }
}
```

**Common paths:**
- **Windows**: `C:/ffmpeg/bin/ffmpeg.exe` or `C:/Program Files/ffmpeg/bin/ffmpeg.exe`
- **Linux**: `/usr/bin/ffmpeg` (usually in PATH)
- **macOS**: `/usr/local/bin/ffmpeg` (Homebrew) or `/opt/homebrew/bin/ffmpeg` (Apple Silicon)
- **Docker**: `/usr/bin/ffmpeg` (installed in PATH)

## Docker Deployment

### Dockerfile Example

Add FFmpeg and native libraries to your Dockerfile:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

# Install audio dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    ffmpeg \
    libsodium23 \
    libopus0 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
EXPOSE 5001

# ... rest of Dockerfile
```

### Docker Compose Example

Mount the sounds directory as a volume:

```yaml
services:
  discordbot:
    build: .
    volumes:
      - ./sounds:/app/sounds
    environment:
      - Soundboard__BasePath=/app/sounds
```

## File System Considerations

### Cross-Platform Path Handling

The application uses `Path.Combine()` for all path operations, ensuring compatibility across Windows (`\`) and Linux (`/`).

**Correct configuration:**
```json
{
  "Soundboard": {
    "BasePath": "./sounds"
  }
}
```

The path will be normalized at runtime:
- Windows: `.\sounds\123456789\sound.mp3`
- Linux: `./sounds/123456789/sound.mp3`

### Directory Structure

Sounds are organized by guild ID:

```
sounds/
├── 123456789012345678/    # Guild ID
│   ├── airhorn.mp3
│   └── bruh.wav
└── 987654321098765432/    # Another guild
    └── victory.ogg
```

### Permissions (Linux/Docker)

Ensure the application has read/write access to the sounds directory:

```bash
# Create directory with appropriate permissions
mkdir -p ./sounds
chmod 755 ./sounds

# If running as non-root user in Docker
chown -R 1000:1000 ./sounds
```

## Troubleshooting

### FFmpeg Not Found

**Symptoms:** Audio commands fail with "FFmpeg not found" errors.

**Solutions:**
1. Verify FFmpeg is installed: `ffmpeg -version`
2. Add FFmpeg to system PATH
3. Configure explicit path in `appsettings.json`:
   ```json
   { "Soundboard": { "FfmpegPath": "/path/to/ffmpeg" } }
   ```

### Voice Connection Issues

**Symptoms:** Bot joins channel but no audio plays.

**Possible causes:**
1. Missing libsodium or libopus
2. FFmpeg not configured correctly
3. Bot lacks voice permissions in Discord

**Linux fix:**
```bash
sudo apt install libsodium23 libopus0
```

### File Permission Errors

**Symptoms:** Cannot upload or play sounds; permission denied errors.

**Solutions:**
1. Check directory ownership
2. Ensure write permissions on sounds folder
3. For Docker, verify volume mount permissions

## Environment-Specific Setup

### Development (Windows)

1. Install FFmpeg via winget or download from ffmpeg.org
2. Add to PATH or configure `FfmpegPath`
3. Sounds stored in `./sounds` relative to project

### Production (Linux)

1. Install via package manager: `apt install ffmpeg libsodium23 libopus0`
2. Create sounds directory with proper permissions
3. Configure volume mounts if using containers

### CI/CD

For build pipelines that need audio processing:

```yaml
# GitHub Actions example
- name: Install FFmpeg
  run: sudo apt-get update && sudo apt-get install -y ffmpeg
```

## See Also

- [Audio Support Requirements](../requirements/audio-support.md) - Full feature specification
- [Discord.NET Audio Documentation](https://docs.discord.net/faq/voice/sending-voice.html) - Official voice guide
- [FFmpeg Documentation](https://ffmpeg.org/documentation.html) - FFmpeg reference
