# Migration Guide: Manual SQLite to Docker + PostgreSQL

**Last Updated:** 2026-02-19
**Applies to:** v1.0.x+

---

## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Step 1: Prepare the Docker Environment](#step-1-prepare-the-docker-environment)
- [Step 2: Configure Environment Variables](#step-2-configure-environment-variables)
- [Step 3: Start PostgreSQL](#step-3-start-postgresql)
- [Step 4: Migrate Data](#step-4-migrate-data)
- [Step 5: Start the Bot](#step-5-start-the-bot)
- [Step 6: Verify the Migration](#step-6-verify-the-migration)
- [Step 7: Decommission the Old Install](#step-7-decommission-the-old-install)
- [Rollback](#rollback)
- [Troubleshooting](#troubleshooting)
- [Related Documentation](#related-documentation)

---

## Overview

This guide walks through migrating from a **manual (non-Docker) installation using SQLite** to a **Docker Compose deployment with PostgreSQL**. The process uses the built-in `migrate-data` CLI command to transfer all data between database providers.

**What changes:**

| | Before | After |
|---|--------|-------|
| **Runtime** | Manual `dotnet run` or systemd service | Docker Compose containers |
| **Database** | SQLite file on disk | PostgreSQL container with persistent volume |
| **Configuration** | User Secrets / `appsettings.json` | `.env` file |
| **Audio deps** | Manually installed FFmpeg, libsodium, opus | Pre-installed in Docker image |

**Estimated downtime:** 5-15 minutes depending on database size.

## Prerequisites

- **Docker Engine** 24.0+ with Docker Compose v2
- **Access to your existing SQLite database file** (default: `data/discordbot.db`)
- **.NET 8 SDK** installed (for running the `migrate-data` command from source)
- Your existing Discord bot token, OAuth client ID, and client secret

Verify Docker is installed:

```bash
docker --version        # Docker Engine 24.0+
docker compose version  # Docker Compose v2.x
```

## Step 1: Prepare the Docker Environment

Clone the repository (or use your existing checkout):

```bash
cd /path/to/discordbot
```

Create your `.env` file from the provided example:

```bash
cp .env.example .env
```

## Step 2: Configure Environment Variables

Edit `.env` and set your secrets. Transfer these from your existing User Secrets or `appsettings.json`:

```env
# =============================================================================
# REQUIRED — copy from your existing user secrets
# =============================================================================
Discord__Token=YOUR_BOT_TOKEN
Discord__OAuth__ClientId=YOUR_CLIENT_ID
Discord__OAuth__ClientSecret=YOUR_CLIENT_SECRET

# =============================================================================
# DATABASE — PostgreSQL configuration
# =============================================================================
DATABASE_PROVIDER=PostgreSql
CONNECTION_STRING=Host=postgres;Database=discordbot;Username=discordbot;Password=YourStrongPassword123!
POSTGRES_DB=discordbot
POSTGRES_USER=discordbot
POSTGRES_PASSWORD=YourStrongPassword123!

# =============================================================================
# OPTIONAL — carry over from your existing config if used
# =============================================================================
# Discord__TestGuildId=123456789012345678
# OpenRouter__ApiKey=YOUR_KEY
# AzureSpeech__SubscriptionKey=YOUR_KEY
# AzureSpeech__Region=eastus
```

> **Important:** The `POSTGRES_PASSWORD` and the password in `CONNECTION_STRING` must match. Use a strong password — not `changeme`.

### Finding your User Secrets

If your manual install used .NET User Secrets, you can view them:

```bash
dotnet user-secrets list --project src/DiscordBot.Bot --id 7b84433c-c2a8-46db-a8bf-58786ea4f28e
```

## Step 3: Start PostgreSQL

Start **only** the PostgreSQL container first. Do not start the bot yet — we need to migrate data before the bot writes to the new database.

```bash
docker compose --profile postgres up -d postgres
```

Wait for the health check to pass:

```bash
docker compose --profile postgres ps
```

You should see `postgres` with status **healthy**. This may take 10-30 seconds on first start.

PostgreSQL is now running and accessible on **port 5432** from the host.

## Step 4: Migrate Data

### Back up your SQLite database

Before migrating, copy your existing database file to a safe location:

```bash
cp /path/to/data/discordbot.db /path/to/data/discordbot.db.backup
```

### Stop the existing bot

Ensure the old bot process is stopped so no writes occur during migration:

```bash
# If running as a systemd service:
sudo systemctl stop discordbot

# If running manually, stop the process
```

### Run the migration command

Use the built-in `migrate-data` CLI from the project source. The command reads every table from SQLite and writes it to PostgreSQL in a single transaction.

```bash
dotnet run --project src/DiscordBot.Bot -- migrate-data \
  --source "Data Source=/path/to/data/discordbot.db" \
  --target "Host=localhost;Database=discordbot;Username=discordbot;Password=YourStrongPassword123!"
```

> **Note:** The target uses `Host=localhost` (not `Host=postgres`) because you are running from the host machine, connecting to PostgreSQL through the exposed port 5432. The `Host=postgres` hostname only works from within the Docker network.

### What the migration does

1. **Pre-flight checks** — verifies the SQLite database has no pending EF Core migrations
2. **Schema creation** — runs all PostgreSQL migrations on the target database
3. **Data transfer** — copies all tables in dependency order (parents before children), batched at 1,000 rows
4. **Sequence reset** — resets PostgreSQL auto-increment sequences to match the migrated data
5. **Transaction commit** — all changes commit atomically; any error triggers a full rollback

Expected output:

```
=== SQLite to PostgreSQL Data Migration ===

[Pre-flight] Verifying source SQLite database...
[Pre-flight] Source database is at latest migration.
[Pre-flight] Applying migrations to target PostgreSQL database...
[Pre-flight] Target database schema is up to date.

Starting data transfer...

Migrating AspNetRoles... 4 rows transferred
Migrating AspNetUsers... 12 rows transferred
Migrating Guilds... 3 rows transferred
...
Resetting PostgreSQL sequences...
Sequences reset successfully.

=== Migration complete: 15432 total rows transferred ===
```

If the migration fails, no data is written — the transaction rolls back automatically.

## Step 5: Start the Bot

Now start the full stack:

```bash
docker compose --profile postgres up -d
```

This starts both the bot and PostgreSQL. The bot container connects to PostgreSQL using the Docker service name `postgres` as the hostname (configured in your `.env` `CONNECTION_STRING`).

EF Core migrations run automatically on startup, so the schema is always current.

## Step 6: Verify the Migration

### Check container status

```bash
docker compose --profile postgres ps
```

Both `bot` and `postgres` should show as **running** and **healthy**.

### Check bot logs

```bash
docker compose logs bot --tail 100
```

Look for:
- `Database provider: PostgreSql` (confirms PostgreSQL is active)
- `Bot is ready` or similar connected message
- No migration errors

### Verify the admin UI

Open `http://localhost:5000` in your browser. Log in with Discord OAuth and confirm:

- Guild data loads correctly
- Command logs, audit logs, and other historical data are present
- Bot responds to slash commands in Discord

### Update OAuth redirect URI

If your manual install used a different port (e.g., `https://localhost:5001`), update the OAuth2 redirect URI in the [Discord Developer Portal](https://discord.com/developers/applications):

- Add: `http://localhost:5000/signin-discord` (or your production domain)
- Remove the old redirect URI if no longer needed

## Step 7: Decommission the Old Install

Once you've verified everything works:

```bash
# Disable the old systemd service (if applicable)
sudo systemctl disable discordbot
sudo systemctl stop discordbot

# Optionally remove the old service file
sudo rm /etc/systemd/system/discordbot.service
sudo systemctl daemon-reload
```

Keep the SQLite backup file for at least a few weeks in case you discover missing data.

## Rollback

If you need to revert to the manual SQLite installation:

1. Stop the Docker containers:

   ```bash
   docker compose --profile postgres down
   ```

2. Restore the SQLite database from backup (if needed):

   ```bash
   cp /path/to/data/discordbot.db.backup /path/to/data/discordbot.db
   ```

3. Restart your old bot process or systemd service:

   ```bash
   sudo systemctl start discordbot
   ```

To completely remove Docker volumes (destroys the PostgreSQL data):

```bash
docker compose --profile postgres down -v
```

## Troubleshooting

### Migration command fails: "pending migration(s)"

Your SQLite database is not at the latest schema version. Apply migrations first:

```bash
dotnet ef database update \
  --project src/DiscordBot.Infrastructure \
  --startup-project src/DiscordBot.Bot \
  --context SqliteBotDbContext
```

Then retry the migration.

### Migration command fails: "Target database already contains data"

The PostgreSQL database already has rows (e.g., from a previous partial migration). Either:

- **Reset and retry** — drop the PostgreSQL volume and start fresh:

  ```bash
  docker compose --profile postgres down -v
  docker compose --profile postgres up -d postgres
  # Wait for healthy, then re-run migrate-data
  ```

- **Force continue** — add `--force` to proceed (may cause duplicate key errors):

  ```bash
  dotnet run --project src/DiscordBot.Bot -- migrate-data \
    --source "Data Source=..." --target "Host=..." --force
  ```

### Connection refused on port 5432

Ensure the PostgreSQL container is running and healthy:

```bash
docker compose --profile postgres ps
docker compose --profile postgres logs postgres
```

Verify port 5432 isn't already in use by another PostgreSQL instance:

```bash
ss -tlnp | grep 5432
```

### Password authentication failed

The `POSTGRES_PASSWORD` in `.env` and the password in `CONNECTION_STRING` must match exactly. If you changed `POSTGRES_PASSWORD` after first creating the container, the change won't take effect — PostgreSQL only reads this on initial database creation. To reset:

```bash
docker compose --profile postgres down -v   # Removes postgres-data volume
docker compose --profile postgres up -d postgres
```

### Npgsql timestamp errors

If you see `Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp with time zone'`, ensure you're running the current version of the bot. The application sets `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` at startup.

### Bot can't connect to postgres from Docker

The `CONNECTION_STRING` in `.env` must use `Host=postgres` (the Docker Compose service name), not `Host=localhost`. Localhost from inside the bot container refers to the container itself, not the host machine.

### Sounds or audio not working

Mount your sounds directory next to `docker-compose.yml`:

```bash
# Ensure the directory exists
ls ./sounds/

# Fix permissions if needed (container runs as non-root)
chmod -R o+r ./sounds/
```

The volume is configured as `./sounds:/app/sounds:ro` in `docker-compose.yml`.

## Related Documentation

- [Docker Deployment](docker-deployment.md) — Full Docker Compose reference
- [Linux VPS Deployment](linux-deployment.md) — Systemd-based deployment (non-Docker)
- [Environment Configuration](environment-configuration.md) — All environment settings
- [Database Schema](database-schema.md) — Entity relationships and schema
- [Discord Bot Setup](discord-bot-setup.md) — Obtaining bot tokens and OAuth credentials
