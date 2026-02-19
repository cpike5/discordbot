# Docker Deployment Guide

**Last Updated:** 2026-02-18
**Applies to:** v1.0.x+

---

## Table of Contents

- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Docker Compose Setup](#docker-compose-setup)
- [Configuration Reference](#configuration-reference)
- [Volume Mounts](#volume-mounts)
- [Database Options](#database-options)
- [Audio Support](#audio-support)
- [Updating](#updating)
- [Troubleshooting](#troubleshooting)
- [Related Documentation](#related-documentation)

---

## Prerequisites

- **Docker Engine** 24.0+ with Docker Compose v2
- **Discord Bot Token** — see [Discord Bot Setup](discord-bot-setup.md)
- **Discord OAuth Credentials** — required for the admin UI

Verify your Docker installation:

```bash
docker --version        # Docker Engine 24.0+
docker compose version  # Docker Compose v2.x
```

## Quick Start

The fastest way to get running with SQLite (no external database needed):

```bash
# Clone the repository
git clone https://github.com/cpike5/discordbot.git
cd discordbot

# Configure environment
cp .env.example .env
# Edit .env and set Discord__Token, Discord__OAuth__ClientId, Discord__OAuth__ClientSecret

# Start the bot
docker compose up -d
```

The admin UI will be available at `http://localhost:5000`.

### Using a pre-built image

```bash
docker run -d \
  --name discordbot \
  -p 5000:5000 \
  -v bot-data:/app/data \
  --env-file .env \
  ghcr.io/cpike5/discordbot:latest
```

## Docker Compose Setup

The `docker-compose.yml` uses **profiles** for optional services. Only the bot starts by default.

### Default — Bot + SQLite

```bash
docker compose up -d
```

This starts the bot with SQLite storage. The database file is persisted in the `bot-data` volume at `/app/data/discordbot.db`.

### With PostgreSQL

```bash
docker compose --profile postgres up -d
```

Enable PostgreSQL by uncommenting and configuring these settings in `.env`:

```env
ConnectionStrings__DefaultConnection=Host=postgres;Database=discordbot;Username=discordbot;Password=changeme
POSTGRES_DB=discordbot
POSTGRES_USER=discordbot
POSTGRES_PASSWORD=changeme
```

The bot waits for PostgreSQL to be healthy before starting.

### With Seq Logging

```bash
docker compose --profile seq up -d
```

Enable Seq log ingestion by uncommenting in `.env`:

```env
Serilog__WriteTo__2__Name=Seq
Serilog__WriteTo__2__Args__serverUrl=http://seq
```

Seq UI is available at `http://localhost:5341`.

### Full Stack — All Services

```bash
docker compose --profile postgres --profile seq up -d
```

## Configuration Reference

Configure the bot by editing `.env`. See `.env.example` for all available settings.

### Required Settings

| Variable | Description |
|----------|-------------|
| `Discord__Token` | Bot token from Discord Developer Portal |
| `Discord__OAuth__ClientId` | OAuth2 client ID for admin UI login |
| `Discord__OAuth__ClientSecret` | OAuth2 client secret |

### Optional Settings

| Variable | Default | Description |
|----------|---------|-------------|
| `Discord__TestGuildId` | *(none)* | Guild ID for instant command registration |
| `Identity__DefaultAdmin__Email` | `admin@example.com` | Default admin email (first run) |
| `Identity__DefaultAdmin__Password` | `ChangeThisPassword123!` | Default admin password (change immediately!) |
| `ConnectionStrings__DefaultConnection` | SQLite at `/app/data/discordbot.db` | Database connection string |
| `Anthropic__ApiKey` | *(none)* | API key for AI assistant feature |
| `AzureSpeech__SubscriptionKey` | *(none)* | Azure Speech Services key for TTS |
| `AzureSpeech__Region` | *(none)* | Azure region (e.g., `eastus`) |

### PostgreSQL Profile Settings

| Variable | Default | Description |
|----------|---------|-------------|
| `POSTGRES_DB` | `discordbot` | Database name |
| `POSTGRES_USER` | `discordbot` | Database user |
| `POSTGRES_PASSWORD` | `changeme` | Database password (**change this!**) |

### Seq Profile Settings

| Variable | Default | Description |
|----------|---------|-------------|
| `Serilog__WriteTo__2__Name` | *(none)* | Set to `Seq` to enable |
| `Serilog__WriteTo__2__Args__serverUrl` | *(none)* | Set to `http://seq` (compose service name) |

## Volume Mounts

| Volume | Container Path | Purpose |
|--------|---------------|---------|
| `bot-data` | `/app/data` | SQLite database and application data |
| `postgres-data` | `/var/lib/postgresql/data` | PostgreSQL data (postgres profile only) |
| `seq-data` | `/data` | Seq log storage (seq profile only) |

### Custom Sound Clips

Mount your sound clips directory for VOX/soundboard features:

```yaml
# Already configured in docker-compose.yml:
volumes:
  - ./sounds:/app/sounds:ro
```

Place `.wav` files in a `sounds/` directory next to `docker-compose.yml`. The mount is read-only (`:ro`). See [soundboard.md](soundboard.md) and [vox-system-spec.md](vox-system-spec.md) for clip organization.

## Database Options

### SQLite (Default)

No configuration needed. The database is created automatically at `/app/data/discordbot.db` and persisted via the `bot-data` volume.

### PostgreSQL

1. Start with the postgres profile: `docker compose --profile postgres up -d`
2. Set the connection string in `.env`:
   ```env
   ConnectionStrings__DefaultConnection=Host=postgres;Database=discordbot;Username=discordbot;Password=changeme
   ```
3. EF Core migrations run automatically on startup

**Switching from SQLite to PostgreSQL** requires migrating data manually — there is no built-in migration tool between providers.

## Audio Support

The Docker image includes all audio dependencies pre-installed:

- **FFmpeg** — audio transcoding
- **libsodium** — encryption for Discord voice
- **libopus** — Opus audio codec

No additional configuration is needed for audio features (soundboard, VOX, TTS). See [audio-dependencies.md](audio-dependencies.md) for details.

## Updating

### From source (docker compose build)

```bash
git pull origin main
docker compose build
docker compose up -d
```

### From GHCR (pre-built images)

```bash
docker compose pull
docker compose up -d
```

### Version pinning

Pin to a specific version in `docker-compose.yml`:

```yaml
services:
  bot:
    image: ghcr.io/cpike5/discordbot:1.0.0
```

EF Core migrations run automatically on startup, so database schema updates are handled during container restart.

## Troubleshooting

### Bot won't start

```bash
# Check container logs
docker compose logs bot

# Check container status
docker compose ps
```

### Port 5000 already in use

Change the host port mapping in `docker-compose.yml`:

```yaml
ports:
  - "8080:5000"  # Access admin UI at http://localhost:8080
```

### Database locked (SQLite)

Only one bot instance can use a SQLite database at a time. If you see "database is locked" errors, ensure no other container or process is accessing the same volume.

### PostgreSQL connection refused

Ensure the postgres profile is active and the container is healthy:

```bash
docker compose --profile postgres ps
docker compose --profile postgres logs postgres
```

Verify the connection string in `.env` uses the service name `postgres` (not `localhost`).

### Audio not working

Audio dependencies are included in the image. If audio features aren't working:

1. Check that the bot has joined a voice channel
2. Verify sound files are mounted correctly: `docker compose exec bot ls /app/sounds/`
3. Check logs for audio-related errors: `docker compose logs bot | grep -i audio`

### Permission denied errors

The container runs as non-root user `appuser`. Ensure mounted directories are readable:

```bash
# Fix sounds directory permissions
chmod -R o+r ./sounds/
```

### Container health check failing

The bot has a 60-second startup grace period. If it's still unhealthy after that:

```bash
docker compose exec bot wget -q --spider http://localhost:5000/api/health/live
```

## Related Documentation

- [Linux VPS Deployment](linux-deployment.md) — Systemd-based deployment (non-Docker)
- [Discord Bot Setup](discord-bot-setup.md) — Obtaining bot tokens and OAuth credentials
- [Audio Dependencies](audio-dependencies.md) — Audio library details
- [Environment Configuration](environment-configuration.md) — Full environment settings reference
- [Soundboard](soundboard.md) — Soundboard feature and clip management
- [VOX System](vox-system-spec.md) — VOX clip library architecture
