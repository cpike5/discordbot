# Audio Dependencies

This document describes the external dependencies required for audio features (soundboard, voice channel playback).

## Overview

The bot's audio features require external tools for audio processing. These are not bundled with the application and must be installed separately on the host system.

## Audio Pipeline

Understanding the audio pipeline helps diagnose issues:

```
Sound File → FFmpeg → PCM Stream → Discord Voice Server
   (.mp3)      ↓         ↓              ↓
             Transcode  Encrypt     UDP Stream
             48kHz      (libsodium)
             16-bit
             Stereo
```

**The flow:**
1. **Sound file** (mp3, wav, ogg) is read from disk
2. **FFmpeg** transcodes it to Opus-ready PCM (48kHz, 16-bit, stereo)
3. **Discord.NET** encrypts the PCM bytes using **libsodium**
4. Encrypted audio is sent over UDP to the Discord Voice Server

**Key insight:** Unlike text messages which are fire-and-forget, audio requires maintaining a continuous session per guild. The PCM stream must persist for the duration of the voice connection.

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
| **libsodium** | Voice encryption | Must be in build output directory on Windows; apt package on Linux |
| **libopus** | Opus audio encoding | Must be in build output directory on Windows; apt package on Linux |

## Windows Setup (Complete Guide)

### Step 1: Install FFmpeg

**Option A: Using winget (Recommended)**
```powershell
winget install ffmpeg
```

**Option B: Manual Installation**
1. Download FFmpeg from [ffmpeg.org/download.html](https://ffmpeg.org/download.html) (choose "Windows builds from gyan.dev")
2. Download the "essentials" build (e.g., `ffmpeg-release-essentials.zip`)
3. Extract to `C:\ffmpeg`
4. Add `C:\ffmpeg\bin` to your system PATH:
   - Press Win+X → System → Advanced system settings → Environment Variables
   - Under "System variables", select `Path` → Edit → New → `C:\ffmpeg\bin`
   - Click OK to save

**Verify installation:**
```powershell
ffmpeg -version
ffprobe -version
```

### Step 2: Install Native Libraries (libsodium and opus)

The native DLLs must be in your build output directory (e.g., `bin/Debug/net8.0/`):
- `libsodium.dll`
- `opus.dll`

**Option A: Using NuGet packages (Recommended)**

Add these packages to your project:
```xml
<PackageReference Include="libsodium" Version="1.0.19" />
```

The `Discord.Net.WebSocket` package typically includes opus support, but if needed:
```xml
<PackageReference Include="Concentus" Version="2.2.0" />
```

**Option B: Manual Download**

1. **libsodium**: Download from [libsodium releases](https://download.libsodium.org/libsodium/releases/)
   - Get `libsodium-*-msvc.zip`
   - Extract and copy `libsodium.dll` from `x64/Release/v143/dynamic/` to your output directory

2. **opus**: Download from [opus-tools](https://opus-codec.org/downloads/)
   - Or get a pre-built Windows binary from various sources
   - Copy `opus.dll` to your output directory

**Automatic copy on build** (add to your `.csproj`):
```xml
<ItemGroup>
  <None Include="path\to\libsodium.dll" CopyToOutputDirectory="PreserveNewest" />
  <None Include="path\to\opus.dll" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

### Step 3: Verify Setup

Run the bot and try joining a voice channel. If you see `DllNotFoundException`, the native libraries are not in the correct location.

## Linux Setup (Complete Guide)

### Debian / Ubuntu

```bash
# Update package list
sudo apt update

# Install all audio dependencies
sudo apt install -y ffmpeg libsodium23 libopus0

# Verify installation
ffmpeg -version
ldconfig -p | grep -E "libsodium|libopus"
```

### Ubuntu 22.04+ / Debian 12+

The package names may differ slightly:
```bash
sudo apt install -y ffmpeg libsodium23 libopus0
```

### RHEL / Fedora / Rocky Linux / AlmaLinux

```bash
# Enable EPEL repository (if needed)
sudo dnf install -y epel-release

# Install dependencies
sudo dnf install -y ffmpeg libsodium opus

# Verify
ffmpeg -version
```

### Arch Linux

```bash
sudo pacman -S ffmpeg libsodium opus
```

### Alpine Linux (Docker base images)

```bash
apk add --no-cache ffmpeg libsodium opus
```

### Verify Native Library Installation

```bash
# Check if libraries are found
ldconfig -p | grep libsodium
ldconfig -p | grep opus

# Expected output like:
# libsodium.so.23 (libc6,x86-64) => /lib/x86_64-linux-gnu/libsodium.so.23
# libopus.so.0 (libc6,x86-64) => /lib/x86_64-linux-gnu/libopus.so.0
```

### Creating Symlinks for .NET

**Important:** Ubuntu/Debian packages install versioned libraries (e.g., `libopus.so.0`, `libsodium.so.23`), but .NET's P/Invoke looks for unversioned names (`libopus.so`, `libsodium.so`). You must create symlinks in your application directory for .NET to find them.

```bash
# Find where the libraries are installed
dpkg -L libopus0 | grep "\.so"
dpkg -L libsodium23 | grep "\.so"

# Create symlinks in your bot's application directory
# Replace /opt/discordbot with your actual deployment path
sudo ln -s /usr/lib/x86_64-linux-gnu/libopus.so.0 /opt/discordbot/libopus.so
sudo ln -s /usr/lib/x86_64-linux-gnu/libsodium.so.23 /opt/discordbot/libsodium.so
```

**Note:** Creating the symlinks in `/usr/lib/x86_64-linux-gnu/` alone is not sufficient. .NET searches the application directory first, so the symlinks must be placed there.

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

**Symptoms:** Bot joins channel but no audio plays, or you see errors like:
```
Unable to load shared library 'opus' or one of its dependencies
```

**Possible causes:**
1. Missing libsodium or libopus packages
2. Missing symlinks for .NET P/Invoke (most common on Linux)
3. FFmpeg not configured correctly
4. Bot lacks voice permissions in Discord

**Linux fix:**
```bash
# Install the packages
sudo apt install libsodium23 libopus0

# Create symlinks in your application directory (required for .NET)
# Replace /opt/discordbot with your actual deployment path
sudo ln -s /usr/lib/x86_64-linux-gnu/libopus.so.0 /opt/discordbot/libopus.so
sudo ln -s /usr/lib/x86_64-linux-gnu/libsodium.so.23 /opt/discordbot/libsodium.so

# Restart your bot service
sudo systemctl restart discordbot
```

**Why symlinks are needed:** Ubuntu/Debian packages install versioned libraries (e.g., `libopus.so.0`), but .NET looks for unversioned names (`libopus.so`). The symlinks must be in your application directory, not just in the system library path.

### Consecutive Playback Failures

**Symptoms:** First sound plays correctly, but subsequent sounds are silent or cut off.

**Root cause:** Discord.NET's `CreatePCMStream()` doesn't work properly when called multiple times on the same `IAudioClient` after disposing previous streams. This is a known quirk of the Discord.NET audio pipeline.

**What happens:**
1. First sound plays correctly with a fresh PCM stream
2. Second sound has the first 1-2 seconds cut off
3. All subsequent sounds produce complete silence

**Solution:** Create ONE persistent PCM stream per voice connection and reuse it for all playback operations:

```csharp
// AudioService maintains cached PCM streams per guild
private readonly ConcurrentDictionary<ulong, AudioOutStream> _pcmStreams = new();

public AudioOutStream? GetOrCreatePcmStream(ulong guildId)
{
    // Return existing stream if we have one
    if (_pcmStreams.TryGetValue(guildId, out var existingStream))
    {
        return existingStream;
    }

    // Get audio client
    if (!_connections.TryGetValue(guildId, out var connection))
    {
        return null;
    }

    // Create new PCM stream and cache it
    var pcmStream = connection.AudioClient.CreatePCMStream(AudioApplication.Voice);
    _pcmStreams[guildId] = pcmStream;
    return pcmStream;
}
```

**Important:** Clean up the cached PCM stream when the bot leaves the voice channel:

```csharp
// In disconnect logic
if (_pcmStreams.TryRemove(guildId, out var pcmStream))
{
    await pcmStream.FlushAsync();
    pcmStream.Dispose();
}
```

### Audio Cut-Off at Start

**Symptoms:** The first 100-200ms of audio is missing when playback begins.

**Cause:** Discord's UDP connection takes a few milliseconds to "wake up" the user's client, causing the initial audio to be lost.

**Solution:** Send a brief silence frame before actual audio:

```csharp
// 48kHz * 2 channels * 2 bytes (16-bit) = 192,000 bytes per second
// 200ms = 38,400 bytes of silence
byte[] silence = new byte[38400];
await discordStream.WriteAsync(silence, 0, silence.Length);
// Now send actual audio
```

### File Permission Errors

**Symptoms:** Cannot upload or play sounds; permission denied errors.

**Solutions:**
1. Check directory ownership
2. Ensure write permissions on sounds folder
3. For Docker, verify volume mount permissions

## Environment-Specific Setup

### Development (Windows)

1. Follow the [Windows Setup guide](#windows-setup-complete-guide) above
2. Ensure FFmpeg is in PATH or configure `Soundboard:FfmpegPath` in `appsettings.Development.json`
3. Sounds are stored in `./sounds` relative to the project directory
4. Native DLLs are automatically copied to output on build (if using NuGet packages)

### Development (Linux/macOS)

1. Follow the [Linux Setup guide](#linux-setup-complete-guide) above
2. FFmpeg is typically in PATH after installation; no additional config needed
3. Sounds are stored in `./sounds` relative to the project directory

### Production (Linux Server)

1. Install via package manager: `sudo apt install ffmpeg libsodium23 libopus0`
2. Create symlinks for .NET to find the native libraries:
   ```bash
   # Replace /opt/discordbot with your actual deployment path
   sudo ln -s /usr/lib/x86_64-linux-gnu/libopus.so.0 /opt/discordbot/libopus.so
   sudo ln -s /usr/lib/x86_64-linux-gnu/libsodium.so.23 /opt/discordbot/libsodium.so
   ```
3. Create sounds directory with proper permissions:
   ```bash
   sudo mkdir -p /var/discordbot/sounds
   sudo chown -R www-data:www-data /var/discordbot/sounds
   ```
4. Configure `Soundboard:BasePath` to the production sounds directory
5. Consider using systemd for service management

### CI/CD

For build pipelines that need audio processing:

```yaml
# GitHub Actions example
- name: Install audio dependencies
  run: |
    sudo apt-get update
    sudo apt-get install -y ffmpeg libsodium23 libopus0
```

### Production Checklist

- [ ] FFmpeg installed and accessible (via PATH or explicit config)
- [ ] libsodium available (system package or DLL in output)
- [ ] libopus available (system package or DLL in output)
- [ ] **Linux only:** Symlinks created in application directory (`libopus.so`, `libsodium.so`)
- [ ] Sounds directory exists with correct permissions
- [ ] `Soundboard:BasePath` configured for production path
- [ ] Storage limits configured appropriately for server capacity

## See Also

- [Soundboard Feature](soundboard.md) - Soundboard commands and admin UI
- [Discord.NET Audio Documentation](https://docs.discord.net/faq/voice/sending-voice.html) - Official voice guide
- [FFmpeg Documentation](https://ffmpeg.org/documentation.html) - FFmpeg reference
