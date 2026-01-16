#!/bin/bash
# =============================================================================
# Discord Bot Deployment Script - Deploy from Latest Release
# =============================================================================
# This script deploys the Discord Bot from the latest GitHub release tag,
# rather than the current main branch. This ensures production deployments
# use stable, versioned releases.
#
# Features:
#   - Automatic database migrations with rollback on failure
#   - Native audio library (libsodium, libopus) deployment for Discord voice
#   - Configuration file preservation
#   - Automatic backup and rollback on deployment failure
#   - SQLite database backup (when applicable)
#
# Configuration (edit variables in script):
#   - APP_DIR: Deployment directory (default: /opt/discordbot)
#   - DB_PATH: SQLite database path (default: /var/discordbot/discordbot.db)
#   - LIBSODIUM_PATH: Path to libsodium.so on your system
#   - LIBOPUS_PATH: Path to libopus.so on your system
#
# Usage:
#   ./update-from-release.sh              # Deploy latest release
#   ./update-from-release.sh v0.3.6       # Deploy specific version
#   ./update-from-release.sh --dev        # Deploy current dev version from main branch
#   ./update-from-release.sh --check      # Check latest release without deploying
#
# =============================================================================

set -e

# Configuration
APP_DIR="/opt/discordbot"
BACKUP_DIR="/opt/discordbot-backups"
REPO_URL="https://github.com/cpike5/discordbot.git"
SERVICE_NAME="discordbot"
MAX_BACKUPS=5

# Database configuration (adjust for your setup)
# For SQLite (default):
DB_PATH="/var/lib/discordbot/discordbot.db"
# For MSSQL/MySQL/PostgreSQL, migrations will use connection string from appsettings

# Native audio library paths (adjust for your system)
# These will be copied to the deployment directory for Discord.NET voice support
LIBSODIUM_PATH="/usr/lib/x86_64-linux-gnu/libsodium.so.23"
LIBOPUS_PATH="/usr/lib/x86_64-linux-gnu/libopus.so.0"

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

# Get current installed version
get_current_version() {
    if [ -f "$APP_DIR/version.txt" ]; then
        cat "$APP_DIR/version.txt"
    elif [ -f "$APP_DIR/DiscordBot.Bot.dll" ]; then
        # Try to extract version from assembly
        echo "unknown"
    else
        echo "not installed"
    fi
}

# Check if running as root or with sudo
check_privileges() {
    if [ "$EUID" -ne 0 ]; then
        log_error "This script must be run with sudo or as root"
        exit 1
    fi
}

# Cleanup old backups, keeping only the most recent ones
cleanup_old_backups() {
    log_info "Cleaning up old backups (keeping last $MAX_BACKUPS)..."

    if [ -d "$BACKUP_DIR" ]; then
        local backup_count
        backup_count=$(ls -1d "$BACKUP_DIR"/discordbot.backup.* 2>/dev/null | wc -l)

        if [ "$backup_count" -gt "$MAX_BACKUPS" ]; then
            ls -1dt "$BACKUP_DIR"/discordbot.backup.* | tail -n +$((MAX_BACKUPS + 1)) | xargs rm -rf
            log_info "Removed $((backup_count - MAX_BACKUPS)) old backup(s)"
        fi
    fi
}

# Copy native audio libraries for Discord.NET voice support
copy_audio_libraries() {
    log_info "Setting up native audio libraries..."

    local copied=0

    # Copy libsodium
    if [ -f "$LIBSODIUM_PATH" ]; then
        cp "$LIBSODIUM_PATH" "$APP_DIR/libsodium.so"
        log_success "Copied libsodium.so from $LIBSODIUM_PATH"
        copied=$((copied + 1))
    else
        log_warn "libsodium not found at $LIBSODIUM_PATH - voice features may not work"
    fi

    # Copy libopus
    if [ -f "$LIBOPUS_PATH" ]; then
        cp "$LIBOPUS_PATH" "$APP_DIR/libopus.so"
        log_success "Copied libopus.so from $LIBOPUS_PATH"
        copied=$((copied + 1))
    else
        log_warn "libopus not found at $LIBOPUS_PATH - voice features may not work"
    fi

    if [ $copied -eq 0 ]; then
        log_warn "No audio libraries copied - check LIBSODIUM_PATH and LIBOPUS_PATH in script configuration"
    fi
}

# Run database migrations
run_migrations() {
    local temp_dir="$1"

    log_info "Checking for database migrations..."

    cd "$temp_dir/discordbot"

    # Build the project first (required for EF migrations)
    log_info "Building project for migrations..."
    if ! dotnet build src/DiscordBot.Bot/DiscordBot.Bot.csproj -c Release --no-restore -v q; then
        log_warn "Build failed - skipping migration step"
        return 0
    fi

    # Build connection string for the production database
    local connection_string="Data Source=$DB_PATH"

    # Check if there are any migrations
    if ! dotnet ef migrations list \
        --project src/DiscordBot.Infrastructure \
        --startup-project src/DiscordBot.Bot \
        --no-build \
        &>/dev/null; then
        log_warn "Could not list migrations - skipping migration step"
        return 0
    fi

    # Ensure database directory exists
    local db_dir
    db_dir=$(dirname "$DB_PATH")
    if [ ! -d "$db_dir" ]; then
        log_info "Creating database directory: $db_dir"
        mkdir -p "$db_dir"
        chown discordbot:discordbot "$db_dir"
    fi

    log_info "Applying database migrations to $DB_PATH..."
    if ConnectionStrings__DefaultConnection="$connection_string" \
        dotnet ef database update \
        --project src/DiscordBot.Infrastructure \
        --startup-project src/DiscordBot.Bot \
        --no-build; then
        log_success "Database migrations applied successfully"
    else
        log_error "Database migration failed"
        return 1
    fi
}

# Backup database if using SQLite
backup_database() {
    if [ -f "$DB_PATH" ]; then
        log_info "Backing up database..."
        local db_backup_name="discordbot.db.backup.$(date +%Y%m%d_%H%M%S)"
        mkdir -p "$BACKUP_DIR"
        cp "$DB_PATH" "$BACKUP_DIR/$db_backup_name"
        log_success "Database backed up: $BACKUP_DIR/$db_backup_name"
    fi
}

# Main deployment function
deploy_release() {
    local target_version="$1"
    local current_version
    local temp_dir="/tmp/discordbot-deploy-$$"

    check_privileges

    # Get current version
    current_version=$(get_current_version)
    log_info "Current version: $current_version"

    # Determine target version
    if [ -z "$target_version" ]; then
        target_version=$(get_latest_release)
        if [ -z "$target_version" ]; then
            log_error "Could not determine latest release. Check your internet connection or specify a version manually."
            exit 1
        fi
    fi

    log_info "Target version: $target_version"

    # Check if already on target version
    if [ "$current_version" = "$target_version" ]; then
        log_warn "Already running version $target_version. Use --force to redeploy."
        if [ "$FORCE_DEPLOY" != "true" ]; then
            exit 0
        fi
    fi

    # Create temp directory
    mkdir -p "$temp_dir"
    cd "$temp_dir"

    log_info "Cloning repository..."
    git clone --depth 1 --branch "$target_version" "$REPO_URL" discordbot

    if [ ! -d "discordbot" ]; then
        log_error "Failed to clone repository at tag $target_version"
        rm -rf "$temp_dir"
        exit 1
    fi

    cd discordbot

    log_info "Building release..."
    dotnet publish src/DiscordBot.Bot/DiscordBot.Bot.csproj \
        -c Release \
        -o "$temp_dir/publish" \
        --no-self-contained

    if [ ! -f "$temp_dir/publish/DiscordBot.Bot.dll" ]; then
        log_error "Build failed - DiscordBot.Bot.dll not found"
        rm -rf "$temp_dir"
        exit 1
    fi

    # Run database migrations before stopping the service
    # This allows us to rollback if migrations fail
    if ! run_migrations "$temp_dir"; then
        log_error "Migrations failed - aborting deployment"
        rm -rf "$temp_dir"
        exit 1
    fi

    log_info "Stopping $SERVICE_NAME service..."
    systemctl stop "$SERVICE_NAME" || log_warn "Service was not running"

    # Backup database (SQLite only)
    backup_database

    # Create backup
    local backup_name=""
    if [ -d "$APP_DIR" ]; then
        log_info "Creating backup..."
        mkdir -p "$BACKUP_DIR"
        backup_name="discordbot.backup.$(date +%Y%m%d_%H%M%S)"
        cp -r "$APP_DIR" "$BACKUP_DIR/$backup_name"
        log_success "Backup created: $BACKUP_DIR/$backup_name"

        # Cleanup old backups
        cleanup_old_backups
    fi

    log_info "Deploying new version..."
    mkdir -p "$APP_DIR"

    # Preserve configuration files
    local preserved_files=()
    if [ -f "$APP_DIR/appsettings.Production.json" ]; then
        cp "$APP_DIR/appsettings.Production.json" "$temp_dir/appsettings.Production.json.bak"
        preserved_files+=("appsettings.Production.json")
    fi

    # Deploy new files
    rm -rf "${APP_DIR:?}"/*
    cp -r "$temp_dir/publish/"* "$APP_DIR/"

    # Restore preserved configuration
    for file in "${preserved_files[@]}"; do
        if [ -f "$temp_dir/$file.bak" ]; then
            cp "$temp_dir/$file.bak" "$APP_DIR/$file"
            log_info "Restored: $file"
        fi
    done

    # Copy native audio libraries for Discord.NET voice support
    copy_audio_libraries

    # Write version file
    echo "$target_version" > "$APP_DIR/version.txt"

    # Set ownership
    chown -R discordbot:discordbot "$APP_DIR"

    log_info "Starting $SERVICE_NAME service..."
    systemctl start "$SERVICE_NAME"

    # Wait a moment for service to start
    sleep 3

    # Verify service is running
    if systemctl is-active --quiet "$SERVICE_NAME"; then
        log_success "Service started successfully"
    else
        log_error "Service failed to start. Check logs with: journalctl -u $SERVICE_NAME -n 50"
        log_info "Rolling back to previous version..."

        # Rollback
        if [ -d "$BACKUP_DIR/$backup_name" ]; then
            rm -rf "${APP_DIR:?}"/*
            cp -r "$BACKUP_DIR/$backup_name/"* "$APP_DIR/"
            chown -R discordbot:discordbot "$APP_DIR"
            systemctl start "$SERVICE_NAME"
            log_warn "Rolled back to previous version"
        fi

        rm -rf "$temp_dir"
        exit 1
    fi

    # Cleanup
    log_info "Cleaning up temporary files..."
    rm -rf "$temp_dir"

    log_success "================================================"
    log_success "Deployment complete!"
    log_success "Version: $target_version"
    log_success "================================================"

    # Show service status
    systemctl status "$SERVICE_NAME" --no-pager -l
}

# Show check mode (display versions without deploying)
check_versions() {
    local current_version
    local latest_version

    current_version=$(get_current_version)
    latest_version=$(get_latest_release)

    echo ""
    echo "Discord Bot Version Check"
    echo "========================="
    echo "Current installed: $current_version"
    echo "Latest release:    $latest_version"
    echo ""

    if [ "$current_version" = "$latest_version" ]; then
        log_success "You are running the latest version"
    elif [ "$current_version" = "not installed" ]; then
        log_info "Bot is not installed. Run: sudo $0 $latest_version"
    else
        log_info "Update available! Run: sudo $0"
    fi
}

# Deploy from main branch (dev version)
deploy_dev() {
    local current_version
    local temp_dir="/tmp/discordbot-deploy-$$"
    local dev_version

    check_privileges

    # Get current version
    current_version=$(get_current_version)
    log_info "Current version: $current_version"

    log_warn "Deploying DEV version from main branch"
    log_warn "This is not a stable release - use for testing only!"

    # Create temp directory
    mkdir -p "$temp_dir"
    cd "$temp_dir"

    log_info "Cloning repository (main branch)..."
    git clone --depth 1 --branch main "$REPO_URL" discordbot

    if [ ! -d "discordbot" ]; then
        log_error "Failed to clone repository"
        rm -rf "$temp_dir"
        exit 1
    fi

    cd discordbot

    # Get version from Directory.Build.props
    dev_version=$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' Directory.Build.props 2>/dev/null || echo "dev")
    dev_version="${dev_version}-$(date +%Y%m%d%H%M%S)"
    log_info "Dev version: $dev_version"

    log_info "Building release..."
    dotnet publish src/DiscordBot.Bot/DiscordBot.Bot.csproj \
        -c Release \
        -o "$temp_dir/publish" \
        --no-self-contained

    if [ ! -f "$temp_dir/publish/DiscordBot.Bot.dll" ]; then
        log_error "Build failed - DiscordBot.Bot.dll not found"
        rm -rf "$temp_dir"
        exit 1
    fi

    # Run database migrations before stopping the service
    # This allows us to rollback if migrations fail
    if ! run_migrations "$temp_dir"; then
        log_error "Migrations failed - aborting deployment"
        rm -rf "$temp_dir"
        exit 1
    fi

    log_info "Stopping $SERVICE_NAME service..."
    systemctl stop "$SERVICE_NAME" || log_warn "Service was not running"

    # Backup database (SQLite only)
    backup_database

    # Create backup
    local backup_name=""
    if [ -d "$APP_DIR" ]; then
        log_info "Creating backup..."
        mkdir -p "$BACKUP_DIR"
        backup_name="discordbot.backup.$(date +%Y%m%d_%H%M%S)"
        cp -r "$APP_DIR" "$BACKUP_DIR/$backup_name"
        log_success "Backup created: $BACKUP_DIR/$backup_name"

        # Cleanup old backups
        cleanup_old_backups
    fi

    log_info "Deploying dev version..."
    mkdir -p "$APP_DIR"

    # Preserve configuration files
    local preserved_files=()
    if [ -f "$APP_DIR/appsettings.Production.json" ]; then
        cp "$APP_DIR/appsettings.Production.json" "$temp_dir/appsettings.Production.json.bak"
        preserved_files+=("appsettings.Production.json")
    fi

    # Deploy new files
    rm -rf "${APP_DIR:?}"/*
    cp -r "$temp_dir/publish/"* "$APP_DIR/"

    # Restore preserved configuration
    for file in "${preserved_files[@]}"; do
        if [ -f "$temp_dir/$file.bak" ]; then
            cp "$temp_dir/$file.bak" "$APP_DIR/$file"
            log_info "Restored: $file"
        fi
    done

    # Copy native audio libraries for Discord.NET voice support
    copy_audio_libraries

    # Write version file
    echo "$dev_version" > "$APP_DIR/version.txt"

    # Set ownership
    chown -R discordbot:discordbot "$APP_DIR"

    log_info "Starting $SERVICE_NAME service..."
    systemctl start "$SERVICE_NAME"

    # Wait a moment for service to start
    sleep 3

    # Verify service is running
    if systemctl is-active --quiet "$SERVICE_NAME"; then
        log_success "Service started successfully"
    else
        log_error "Service failed to start. Check logs with: journalctl -u $SERVICE_NAME -n 50"
        log_info "Rolling back to previous version..."

        # Rollback
        if [ -d "$BACKUP_DIR/$backup_name" ]; then
            rm -rf "${APP_DIR:?}"/*
            cp -r "$BACKUP_DIR/$backup_name/"* "$APP_DIR/"
            chown -R discordbot:discordbot "$APP_DIR"
            systemctl start "$SERVICE_NAME"
            log_warn "Rolled back to previous version"
        fi

        rm -rf "$temp_dir"
        exit 1
    fi

    # Cleanup
    log_info "Cleaning up temporary files..."
    rm -rf "$temp_dir"

    log_success "================================================"
    log_success "DEV Deployment complete!"
    log_success "Version: $dev_version"
    log_warn   "Remember: This is a dev build, not a stable release"
    log_success "================================================"

    # Show service status
    systemctl status "$SERVICE_NAME" --no-pager -l
}

# Show usage
show_usage() {
    echo "Discord Bot Deployment Script"
    echo ""
    echo "Usage:"
    echo "  $0                    Deploy latest release"
    echo "  $0 <version>          Deploy specific version (e.g., v0.3.6)"
    echo "  $0 --dev              Deploy current dev version from main branch"
    echo "  $0 --check            Check versions without deploying"
    echo "  $0 --force            Force redeploy even if version matches"
    echo "  $0 --help             Show this help message"
    echo ""
    echo "Examples:"
    echo "  sudo $0               # Deploy latest release"
    echo "  sudo $0 v0.3.6        # Deploy version v0.3.6"
    echo "  sudo $0 --dev         # Deploy dev version from main"
    echo "  $0 --check            # Check what's available"
}

# Parse arguments
FORCE_DEPLOY="false"
DEPLOY_DEV="false"
TARGET_VERSION=""

while [[ $# -gt 0 ]]; do
    case $1 in
        --check|-c)
            check_versions
            exit 0
            ;;
        --dev|-d)
            DEPLOY_DEV="true"
            shift
            ;;
        --force|-f)
            FORCE_DEPLOY="true"
            shift
            ;;
        --help|-h)
            show_usage
            exit 0
            ;;
        v*)
            TARGET_VERSION="$1"
            shift
            ;;
        *)
            log_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Run deployment
if [ "$DEPLOY_DEV" = "true" ]; then
    deploy_dev
else
    deploy_release "$TARGET_VERSION"
fi