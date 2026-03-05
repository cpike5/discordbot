# Docker Deployment Guide

**Last Updated:** 2026-02-19
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
DATABASE_PROVIDER=PostgreSql
CONNECTION_STRING=Host=postgres;Database=discordbot;Username=discordbot;Password=changeme
POSTGRES_DB=discordbot
POSTGRES_USER=discordbot
POSTGRES_PASSWORD=changeme
```

The bot waits for PostgreSQL to be healthy before starting. The `DATABASE_PROVIDER` key explicitly selects the provider; omit it to use connection string auto-detection (`Host=`/`Server=` prefix selects PostgreSQL, file-path `Data Source` selects SQLite).

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
| `DATABASE_PROVIDER` | *(auto-detect)* | Database provider: `Sqlite`, `PostgreSql`, or omit for auto-detection |
| `CONNECTION_STRING` | SQLite at `/app/data/discordbot.db` | Database connection string (overrides default) |
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

The bot supports SQLite (default) and PostgreSQL. Provider selection uses the `DATABASE_PROVIDER` config key or is auto-detected from the connection string format.

| Provider | Connection String Pattern | `DATABASE_PROVIDER` Value |
|----------|--------------------------|--------------------------|
| SQLite | `Data Source=...` (file path) | `Sqlite` |
| PostgreSQL | `Host=...` or `Server=...` | `PostgreSql` |

### SQLite (Default)

No configuration needed. The database is created automatically at `/app/data/discordbot.db` and persisted via the `bot-data` volume.

```env
# .env — SQLite is the default; no DATABASE_PROVIDER needed
CONNECTION_STRING=Data Source=/app/data/discordbot.db
```

### PostgreSQL

1. Start with the postgres profile:

   ```bash
   docker compose --profile postgres up -d
   ```

2. Configure `.env`:

   ```env
   DATABASE_PROVIDER=PostgreSql
   CONNECTION_STRING=Host=postgres;Database=discordbot;Username=discordbot;Password=changeme
   POSTGRES_DB=discordbot
   POSTGRES_USER=discordbot
   POSTGRES_PASSWORD=changeme
   ```

3. EF Core migrations run automatically on startup using the PostgreSQL migration set.

### Migrating Data from SQLite to PostgreSQL

The `migrate-data` CLI command copies data between providers:

```bash
# Run against the bot binary with both connection strings
dotnet run --project src/DiscordBot.Bot -- migrate-data \
  --source "Data Source=/app/data/discordbot.db" \
  --target "Host=localhost;Database=discordbot;Username=discordbot;Password=changeme"
```

**Recommended workflow:**

1. Stop the running bot container: `docker compose stop bot`
2. Run `migrate-data` (see above)
3. Update `.env` to set `DATABASE_PROVIDER=PostgreSql` and the new `CONNECTION_STRING`
4. Restart: `docker compose --profile postgres up -d`

## Base Image

The Docker image uses **Ubuntu Noble (`8.0-noble`)** rather than the default Debian Bookworm or Alpine base images. This is required because `libdave.so` — the native library for Discord's [DAVE E2EE voice protocol](https://github.com/discord/libdave) — is compiled against glibc and requires GLIBC 2.38 / GLIBCXX 3.4.32:

- **Debian Bookworm** (default `8.0` tag) ships older glibc/glibcxx versions that don't meet the requirement.
- **Alpine** uses musl libc instead of glibc, so the prebuilt libdave binary cannot load at all.
- **Ubuntu Noble** (24.04) provides both GLIBC 2.38+ and GLIBCXX 3.4.32+.

If Discord publishes a musl-compatible or statically-linked libdave in the future, Alpine could be reconsidered for smaller image sizes.

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

### PostgreSQL authentication failure

If you see `password authentication failed`, confirm that `POSTGRES_USER`, `POSTGRES_PASSWORD`, and the credentials in `CONNECTION_STRING` all match. Changes to `POSTGRES_PASSWORD` only take effect on first container creation — delete the `postgres-data` volume to reset:

```bash
docker compose --profile postgres down -v
docker compose --profile postgres up -d
```

### Npgsql timestamp errors

If you see errors like `Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp with time zone'`, the Npgsql legacy timestamp switch is not applied. This is configured automatically by the application via `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` at startup. Ensure you are running the current version of the bot image.

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
