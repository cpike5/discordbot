#!/bin/bash
# SQLite → PostgreSQL migration using EF Core's MigrateDataCommand.
# Run on VPS as root (or with sudo). Requires: docker, sqlite3.
set -euo pipefail

DB="/var/lib/discordbot/data/discordbot.db"
COMPOSE_DIR="/opt/discordbot-docker"
PG_CONTAINER="discordbot-postgres"
PG_USER="discordbot"
PG_DB="discordbot"
PG_PASS="P8Z8I0ZN1LU7pVBxKYICqvMC"
NETWORK="discordbot-docker_default"
IMAGE="ghcr.io/cpike5/discordbot:latest"

echo "=== Step 1: Fresh copy of original SQLite DB ==="
sudo cp /var/lib/discordbot/discordbot.db "$DB"

echo ""
echo "=== Step 2: Patch pending migration in SQLite ==="
sudo sqlite3 "$DB" "INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) SELECT '20260219205009_AddIsEnabledToGuildModerationConfig', ProductVersion FROM __EFMigrationsHistory LIMIT 1;"
sudo sqlite3 "$DB" "ALTER TABLE GuildModerationConfigs ADD COLUMN IsEnabled INTEGER NOT NULL DEFAULT 1;" 2>/dev/null || true

echo ""
echo "=== Step 3: Delete expendable data (recreated on startup) ==="
sudo sqlite3 "$DB" "DELETE FROM PerformanceAlertConfigs; DELETE FROM PerformanceIncidents; DELETE FROM MetricSnapshots; DELETE FROM MemberActivitySnapshots; DELETE FROM ChannelActivitySnapshots; DELETE FROM GuildMetricsSnapshots; DELETE FROM ConnectionEvents; DELETE FROM UserActivityEvents; DELETE FROM AssistantUsageMetrics; DELETE FROM Themes; DELETE FROM CommandModuleConfigurations;"

echo ""
echo "=== Step 4: Reset PostgreSQL schema ==="
sudo docker exec "$PG_CONTAINER" psql -U "$PG_USER" -d "$PG_DB" -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"

echo ""
echo "=== Step 5: Pull latest image ==="
cd "$COMPOSE_DIR"
sudo docker compose pull

echo ""
echo "=== Step 6: Run EF Core data migration ==="
sudo docker run --rm \
  --network "$NETWORK" \
  --env-file "$COMPOSE_DIR/.env" \
  -v /var/lib/discordbot/data:/app/data:ro \
  "$IMAGE" \
  migrate-data \
  --source "Data Source=/app/data/discordbot.db" \
  --target "Host=postgres;Database=$PG_DB;Username=$PG_USER;Password=$PG_PASS" \
  --force

echo ""
echo "=== Step 7: Restart bot ==="
sudo docker restart discordbot

echo ""
echo "=== Step 8: Verify ==="
echo "Waiting 5s for bot to start..."
sleep 5
sudo docker exec "$PG_CONTAINER" psql -U "$PG_USER" -d "$PG_DB" -c \
  "SELECT 'Users' AS table_name, COUNT(*) FROM \"Users\" UNION ALL SELECT 'Guilds', COUNT(*) FROM \"Guilds\" UNION ALL SELECT 'MessageLogs', COUNT(*) FROM \"MessageLogs\";"

echo ""
echo "Check bot logs: cd $COMPOSE_DIR && sudo docker compose logs -f bot"
