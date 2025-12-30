#!/bin/bash
# =============================================================================
# Discord Bot Database Migration Script
# =============================================================================
# This script applies Entity Framework Core database migrations to the
# Discord Bot database. It can be run as part of deployment or standalone.
#
# Usage:
#   sudo -E ./run-migrations.sh                 # Apply pending migrations
#   ./run-migrations.sh --status                # Check migration status
#   ./run-migrations.sh --list                  # List all migrations
#   sudo -E ./run-migrations.sh --rollback <name>  # Rollback to specific migration
#
# Note: Use 'sudo -E' to preserve environment variables when running as root.
#
# =============================================================================

set -e

# Configuration (can be overridden via environment variables)
APP_DIR="${APP_DIR:-/opt/discordbot}"
BACKUP_DIR="${BACKUP_DIR:-/opt/discordbot-backups}"
SERVICE_NAME="${SERVICE_NAME:-discordbot}"
DB_FILE="${DB_FILE:-$APP_DIR/discordbot.db}"
REPO_URL="${REPO_URL:-https://github.com/cpike5/discordbot.git}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if running as root or with sudo
check_privileges() {
    if [ "$EUID" -ne 0 ]; then
        log_error "This script must be run with sudo or as root"
        exit 1
    fi
}

# Verify the application is installed
check_installation() {
    if [ ! -f "$APP_DIR/DiscordBot.Bot.dll" ]; then
        log_error "Discord Bot is not installed at $APP_DIR"
        log_info "Please deploy the application first using update-from-release.sh"
        exit 1
    fi
}

# Backup the database before migration
backup_database() {
    if [ -f "$DB_FILE" ]; then
        log_info "Backing up database..."
        mkdir -p "$BACKUP_DIR/db"
        local backup_name="discordbot.db.$(date +%Y%m%d_%H%M%S).bak"
        cp "$DB_FILE" "$BACKUP_DIR/db/$backup_name"
        log_success "Database backed up to: $BACKUP_DIR/db/$backup_name"

        # Cleanup old database backups (keep last 10)
        local backup_count
        backup_count=$(ls -1 "$BACKUP_DIR/db/"*.bak 2>/dev/null | wc -l)
        if [ "$backup_count" -gt 10 ]; then
            ls -1t "$BACKUP_DIR/db/"*.bak | tail -n +11 | xargs rm -f
            log_info "Cleaned up old database backups"
        fi
    else
        log_warn "No existing database found at $DB_FILE"
    fi
}

# Check if service is running
is_service_running() {
    systemctl is-active --quiet "$SERVICE_NAME"
}

# Stop service if running
stop_service_if_needed() {
    if is_service_running; then
        log_info "Stopping $SERVICE_NAME service..."
        systemctl stop "$SERVICE_NAME"
        SERVICE_WAS_RUNNING=true
    else
        SERVICE_WAS_RUNNING=false
    fi
}

# Restart service if it was running
restart_service_if_needed() {
    if [ "$SERVICE_WAS_RUNNING" = true ]; then
        log_info "Restarting $SERVICE_NAME service..."
        systemctl start "$SERVICE_NAME"

        sleep 3

        if is_service_running; then
            log_success "Service restarted successfully"
        else
            log_error "Service failed to restart. Check logs with: journalctl -u $SERVICE_NAME -n 50"
            exit 1
        fi
    fi
}

# Get the current deployed version
get_current_version() {
    if [ -f "$APP_DIR/version.txt" ]; then
        cat "$APP_DIR/version.txt"
    else
        # Default to latest if no version file
        echo ""
    fi
}

# Get the latest release tag from GitHub
get_latest_release() {
    local latest
    latest=$(curl -s "https://api.github.com/repos/cpike5/discordbot/releases/latest" | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/')

    if [ -z "$latest" ]; then
        # Fallback to git ls-remote if API fails
        latest=$(git ls-remote --tags --sort=-v:refname "$REPO_URL" | head -n1 | sed 's/.*refs\/tags\///' | sed 's/\^{}//')
    fi

    echo "$latest"
}

# Apply pending migrations
apply_migrations() {
    check_privileges
    check_installation

    log_info "Database file: $DB_FILE"
    log_info "Checking for pending migrations..."

    # Determine which version to use for migrations
    local target_version
    target_version=$(get_current_version)

    if [ -z "$target_version" ]; then
        target_version=$(get_latest_release)
        log_info "No version.txt found, using latest release: $target_version"
    else
        log_info "Using deployed version: $target_version"
    fi

    # Stop service to avoid database locks
    stop_service_if_needed

    # Backup database before migration
    backup_database

    log_info "Applying database migrations..."

    # Check for EF bundle first (preferred method)
    if [ -f "$APP_DIR/efbundle" ]; then
        log_info "Using EF bundle..."
        cd "$APP_DIR"
        ./efbundle --connection "Data Source=$DB_FILE"
        log_success "Database migrations applied successfully"
    else
        # Clone repo and run migrations using dotnet ef
        local temp_dir="/tmp/discordbot-migrate-$$"
        mkdir -p "$temp_dir"

        log_info "Cloning repository for migration (version: $target_version)..."

        if [ -n "$target_version" ]; then
            git clone --depth 1 --branch "$target_version" "$REPO_URL" "$temp_dir/discordbot" 2>&1
        else
            git clone --depth 1 "$REPO_URL" "$temp_dir/discordbot" 2>&1
        fi

        if [ ! -d "$temp_dir/discordbot" ]; then
            log_error "Failed to clone repository"
            rm -rf "$temp_dir"
            restart_service_if_needed
            exit 1
        fi

        cd "$temp_dir/discordbot"

        # Check if dotnet-ef is available, install if needed
        log_info "Checking for dotnet-ef tool..."
        if ! dotnet ef --version &> /dev/null; then
            log_info "Installing dotnet-ef tool..."
            dotnet tool install --global dotnet-ef 2>&1 || true

            # Add .dotnet/tools to PATH for this session if not already there
            export PATH="$PATH:$HOME/.dotnet/tools"
        fi

        # Verify dotnet-ef is now available
        if ! dotnet ef --version &> /dev/null; then
            log_error "Failed to install or locate dotnet-ef tool"
            rm -rf "$temp_dir"
            restart_service_if_needed
            exit 1
        fi

        log_info "Using dotnet-ef version: $(dotnet ef --version)"

        # Build project first (required for migrations)
        log_info "Building project for migrations..."
        dotnet build src/DiscordBot.Bot -c Release 2>&1

        # Run migrations
        log_info "Running EF Core migrations..."
        dotnet ef database update \
            --project src/DiscordBot.Infrastructure \
            --startup-project src/DiscordBot.Bot \
            --connection "Data Source=$DB_FILE" 2>&1

        log_success "Database migrations applied successfully"

        # Cleanup
        log_info "Cleaning up temporary files..."
        rm -rf "$temp_dir"
    fi

    # Restart service if it was running
    restart_service_if_needed

    log_success "================================================"
    log_success "Migration complete!"
    log_success "================================================"
}

# Check migration status
check_status() {
    check_installation

    log_info "Checking migration status..."

    cd "$APP_DIR"

    if [ -f "$APP_DIR/efbundle" ]; then
        ./efbundle --connection "Data Source=$DB_FILE" -- --list
    else
        log_info "Database file: $DB_FILE"
        if [ -f "$DB_FILE" ]; then
            local db_size
            db_size=$(du -h "$DB_FILE" | cut -f1)
            log_info "Database size: $db_size"
            log_success "Database exists and is accessible"
        else
            log_warn "Database file does not exist (will be created on first run)"
        fi

        # Try to list applied migrations using sqlite3 if available
        if command -v sqlite3 &> /dev/null && [ -f "$DB_FILE" ]; then
            log_info "Applied migrations:"
            sqlite3 "$DB_FILE" "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;" 2>/dev/null || \
                log_warn "Could not read migrations table (may not exist yet)"
        fi
    fi
}

# List all migrations
list_migrations() {
    check_installation

    log_info "Listing migrations..."

    if [ -f "$DB_FILE" ] && command -v sqlite3 &> /dev/null; then
        echo ""
        echo "Applied Migrations"
        echo "=================="
        sqlite3 "$DB_FILE" "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;" 2>/dev/null || \
            log_warn "No migrations table found (database may not be initialized)"
        echo ""
    else
        log_warn "Cannot list migrations. SQLite3 not available or database doesn't exist."
        log_info "Install sqlite3 with: apt install sqlite3"
    fi
}

# Rollback to a specific migration
rollback_migration() {
    local target_migration="$1"

    if [ -z "$target_migration" ]; then
        log_error "Please specify a migration name to rollback to"
        log_info "Use --list to see available migrations"
        log_info "Use '0' to rollback all migrations (empty database)"
        exit 1
    fi

    check_privileges
    check_installation

    log_warn "Rolling back to migration: $target_migration"
    log_warn "This operation may result in data loss!"

    read -p "Are you sure you want to continue? (yes/no): " confirm
    if [ "$confirm" != "yes" ]; then
        log_info "Rollback cancelled"
        exit 0
    fi

    stop_service_if_needed
    backup_database

    log_info "Rolling back database..."

    # Check for EF bundle first
    if [ -f "$APP_DIR/efbundle" ]; then
        cd "$APP_DIR"
        ./efbundle "$target_migration" --connection "Data Source=$DB_FILE"
    else
        # Clone repo and run rollback using dotnet ef
        local target_version
        target_version=$(get_current_version)

        if [ -z "$target_version" ]; then
            target_version=$(get_latest_release)
        fi

        local temp_dir="/tmp/discordbot-migrate-$$"
        mkdir -p "$temp_dir"

        log_info "Cloning repository for rollback (version: $target_version)..."

        if [ -n "$target_version" ]; then
            git clone --depth 1 --branch "$target_version" "$REPO_URL" "$temp_dir/discordbot" 2>&1
        else
            git clone --depth 1 "$REPO_URL" "$temp_dir/discordbot" 2>&1
        fi

        if [ ! -d "$temp_dir/discordbot" ]; then
            log_error "Failed to clone repository"
            rm -rf "$temp_dir"
            restart_service_if_needed
            exit 1
        fi

        cd "$temp_dir/discordbot"

        # Check if dotnet-ef is available, install if needed
        log_info "Checking for dotnet-ef tool..."
        if ! dotnet ef --version &> /dev/null; then
            log_info "Installing dotnet-ef tool..."
            dotnet tool install --global dotnet-ef 2>&1 || true

            # Add .dotnet/tools to PATH for this session if not already there
            export PATH="$PATH:$HOME/.dotnet/tools"
        fi

        # Verify dotnet-ef is now available
        if ! dotnet ef --version &> /dev/null; then
            log_error "Failed to install or locate dotnet-ef tool"
            rm -rf "$temp_dir"
            restart_service_if_needed
            exit 1
        fi

        # Build and run rollback
        log_info "Building project..."
        dotnet build src/DiscordBot.Bot -c Release 2>&1

        log_info "Running rollback to: $target_migration"
        dotnet ef database update "$target_migration" \
            --project src/DiscordBot.Infrastructure \
            --startup-project src/DiscordBot.Bot \
            --connection "Data Source=$DB_FILE" 2>&1

        # Cleanup
        log_info "Cleaning up temporary files..."
        rm -rf "$temp_dir"
    fi

    log_success "Rollback complete"

    restart_service_if_needed
}

# Show usage
show_usage() {
    echo "Discord Bot Database Migration Script"
    echo ""
    echo "Usage:"
    echo "  sudo -E $0                      Apply pending migrations"
    echo "  $0 --status                     Check migration status"
    echo "  $0 --list                       List all applied migrations"
    echo "  sudo -E $0 --rollback <name>    Rollback to specific migration"
    echo "  $0 --help                       Show this help message"
    echo ""
    echo "Environment Variables:"
    echo "  APP_DIR        Application directory (default: /opt/discordbot)"
    echo "  BACKUP_DIR     Backup directory (default: /opt/discordbot-backups)"
    echo "  SERVICE_NAME   Systemd service name (default: discordbot)"
    echo "  DB_FILE        Database file path (default: \$APP_DIR/discordbot.db)"
    echo "  REPO_URL       Git repository URL (default: https://github.com/cpike5/discordbot.git)"
    echo ""
    echo "Examples:"
    echo "  sudo -E $0                  # Apply all pending migrations"
    echo "  $0 --status                 # Check current migration status"
    echo "  $0 --list                   # List applied migrations"
    echo "  sudo -E $0 --rollback 20241201_InitialCreate"
    echo ""
    echo "  # Use custom database location (note: use sudo -E to preserve env vars):"
    echo "  DB_FILE=/var/data/bot.db sudo -E $0"
    echo ""
    echo "Notes:"
    echo "  - Use 'sudo -E' to preserve environment variables when running as root"
    echo "  - Database is automatically backed up before applying migrations"
    echo "  - The service will be stopped during migration to avoid locks"
    echo "  - If no EF bundle is deployed, the script clones the repo and runs dotnet ef"
    echo "  - Rollback operations require 'yes' confirmation"
}

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --status|-s)
            check_status
            exit 0
            ;;
        --list|-l)
            list_migrations
            exit 0
            ;;
        --rollback|-r)
            shift
            rollback_migration "$1"
            exit 0
            ;;
        --help|-h)
            show_usage
            exit 0
            ;;
        *)
            log_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Default action: apply migrations
apply_migrations
