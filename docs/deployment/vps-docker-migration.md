# VPS Manual Install to Docker Migration

Migrate from a manual VPS install to Docker using the GHCR pre-built image with PostgreSQL.

## Prerequisites

- Docker Engine 24+ and Docker Compose v2 installed on the VPS
- Existing bot data at `/var/lib/discordbot/`
- Existing config at `/etc/discordbot/discordbot.env`

## Current Layout

| Resource | VPS Path |
|---|---|
| Bot binaries | `/opt/discordbot` |
| SQLite DB | `/var/lib/discordbot/discordbot.db` |
| Soundboard clips | `/var/lib/discordbot/sounds/{guildId}/*.mp3` |
| VOX clips | `/var/lib/discordbot/sounds/vox/`, `fvox/`, `hgrunt/` |
| Assistant prompt | `/var/lib/discordbot/assistant/system-prompt.md` |
| Assistant docs | `/var/lib/discordbot/assistant/docs/` |
| Env config | `/etc/discordbot/discordbot.env` |

## Phase 1: Prepare

```bash
# Back up SQLite database
cp /var/lib/discordbot/discordbot.db /var/lib/discordbot/discordbot.db.backup

# Create Docker directory and new data dirs
mkdir -p /opt/discordbot-docker
mkdir -p /var/lib/discordbot/{postgres,cache,logs,exports,backups,data}

# Copy the SQLite DB into the data subdir (bind mount target)
cp /var/lib/discordbot/discordbot.db /var/lib/discordbot/data/discordbot.db

# Copy compose and env files
cp docker-compose.production.yml /opt/discordbot-docker/docker-compose.yml
cp .env.production.example /opt/discordbot-docker/.env

# Edit .env with real secrets
nano /opt/discordbot-docker/.env
```

## Phase 2: Start PostgreSQL

Start only PostgreSQL to prepare for data migration. The bot continues running on the old install.

```bash
cd /opt/discordbot-docker
docker compose --profile postgres up -d postgres
```

Wait for healthy:
```bash
docker compose ps
```

## Phase 3: Migrate Data (SQLite to PostgreSQL)

Run the built-in `migrate-data` command using a one-off container:

```bash
docker run --rm \
  --network discordbot-docker_default \
  -v /var/lib/discordbot/data:/app/data:ro \
  ghcr.io/cpike5/discordbot:latest \
  dotnet DiscordBot.Bot.dll migrate-data \
    --source "Data Source=/app/data/discordbot.db" \
    --target "Host=postgres;Database=discordbot;Username=discordbot;Password=YOUR_PASSWORD"
```

This will:
- Verify the SQLite database has no pending migrations
- Auto-apply all PostgreSQL schema migrations
- Copy all tables in dependency order (batches of 1,000 rows)
- Reset PostgreSQL sequences for integer PK tables
- Run in a single transaction (all-or-nothing)

### Verify Migration

Spot-check row counts:

```bash
docker exec -it discordbot-postgres psql -U discordbot -d discordbot \
  -c "SELECT 'Guilds', COUNT(*) FROM \"Guilds\"
      UNION ALL SELECT 'Users', COUNT(*) FROM \"Users\"
      UNION ALL SELECT 'MessageLogs', COUNT(*) FROM \"MessageLogs\";"
```

## Phase 4: Stop Old Bot, Start Docker Bot

```bash
# Stop the manual install
sudo systemctl stop discordbot

# Start the full Docker stack
cd /opt/discordbot-docker
docker compose --profile postgres up -d
```

## Phase 5: Verify

```bash
# Health check
curl http://localhost:5000/health

# Tail logs
docker compose logs -f bot
```

### Verification Checklist

- [ ] `curl http://localhost:5000/health` returns healthy
- [ ] Discord bot comes online and responds to commands
- [ ] Soundboard clips play correctly
- [ ] VOX clips play correctly
- [ ] Admin UI loads and Discord OAuth login works
- [ ] Elastic APM shows traces (if configured)
- [ ] Loki receives logs (if configured)
- [ ] New data writes to PostgreSQL (run a command, check the logs table)
- [ ] Sound uploads work (if the feature is used)

### Troubleshooting

**Bot can't reach Elasticsearch/APM on the host:**
```bash
docker exec discordbot curl http://host.docker.internal:9200
```
If this fails, the `extra_hosts` directive isn't working. Fall back to `network_mode: host` in the compose file.

**Permission errors on sound files:**
The container runs as `appuser` (non-root). Ensure the bind-mounted directories are readable:
```bash
chmod -R a+r /var/lib/discordbot/sounds
chmod -R a+rw /var/lib/discordbot/sounds  # If uploads are used
```

**Discord OAuth redirect fails:**
Ensure the Discord Developer Portal has your domain/IP as a redirect URI:
`http://<your-domain>:5000/signin-discord`

## Phase 6: Cleanup

```bash
# Disable old systemd service
sudo systemctl disable discordbot

# After 1-2 weeks of stable operation, remove backups
rm /var/lib/discordbot/discordbot.db.backup
rm -rf /var/lib/discordbot/data  # SQLite bind mount no longer needed
```

Remove the `/app/data` volume mount from `docker-compose.yml` once SQLite is no longer needed.

## PostgreSQL Backup

All data lives at `/var/lib/discordbot/postgres/` on the host. Set up a daily `pg_dump` cron job:

```bash
# /etc/cron.d/discordbot-backup
0 3 * * * root docker exec discordbot-postgres \
  pg_dump -U discordbot -d discordbot --format=custom \
  > /var/lib/discordbot/backups/discordbot-$(date +\%Y\%m\%d).dump 2>&1
```

## Final Directory Layout

```
/var/lib/discordbot/
├── sounds/              # Soundboard + VOX (existing)
│   ├── {guildId}/
│   ├── vox/
│   ├── fvox/
│   └── hgrunt/
├── assistant/           # AI assistant docs (existing)
│   ├── system-prompt.md
│   └── docs/
├── postgres/            # PostgreSQL data
├── cache/               # Audio PCM cache
├── logs/                # Serilog file output
├── exports/             # User data export ZIPs
└── backups/             # pg_dump output
```
