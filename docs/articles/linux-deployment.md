# Linux VPS Deployment Guide

**Last Updated:** 2025-12-27
**Applies to:** v0.3.x
**Target:** Ubuntu 22.04 LTS / Debian 12 (other systemd-based distros should work with minor adjustments)

---

## Overview

This guide covers deploying the Discord Bot Management System to a Linux VPS using systemd for service management. The deployment follows security best practices: secrets are stored in a separate environment file with restrictive permissions.

The application includes native systemd integration via `Microsoft.Extensions.Hosting.Systemd`, which provides:

- **Startup notification** - systemd knows when the application is ready
- **Graceful shutdown** - proper handling of SIGTERM for clean disconnection
- **Watchdog support** - health monitoring integration with systemd
- **journald integration** - logs forwarded to the system journal

### What This Guide Covers

1. Prerequisites and server preparation
2. Installing .NET 8 runtime
3. Cloning and building the application
4. Creating application directory structure
5. Configuring systemd service with secrets
6. Setting up reverse proxy (optional)
7. Database configuration
8. Maintenance and updates

---

## Prerequisites

### Server Requirements

| Requirement | Minimum | Recommended |
|------------|---------|-------------|
| RAM | 512 MB | 1 GB+ |
| Disk | 1 GB | 5 GB+ |
| CPU | 1 vCPU | 2 vCPU |
| OS | Ubuntu 20.04+ / Debian 11+ | Ubuntu 22.04 LTS |

### Required Information

Before starting, gather:

- Discord Bot Token (from Discord Developer Portal)
- Discord OAuth Client ID and Secret (if using admin UI authentication)
- Discord Test Guild ID (optional, for instant command registration)
- Domain name (if exposing admin UI publicly)

---

## Step 1: Server Preparation

### Update System Packages

```bash
sudo apt update && sudo apt upgrade -y
```

### Install Required Tools

```bash
sudo apt install -y git curl wget apt-transport-https
```

### Create Application User

Create a dedicated non-root user to run the bot:

```bash
# Create system user (no login shell, no home directory login)
sudo useradd --system --create-home --shell /usr/sbin/nologin discordbot

# Create application directories
sudo mkdir -p /opt/discordbot
sudo mkdir -p /var/log/discordbot
sudo mkdir -p /var/lib/discordbot

# Set ownership
sudo chown -R discordbot:discordbot /opt/discordbot
sudo chown -R discordbot:discordbot /var/log/discordbot
sudo chown -R discordbot:discordbot /var/lib/discordbot
```

---

## Step 2: Install .NET 8 Runtime

### Option A: Microsoft Package Repository (Recommended)

```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install ASP.NET Core Runtime
sudo apt update
sudo apt install -y aspnetcore-runtime-8.0
```

### Option B: Snap (Alternative)

```bash
sudo snap install dotnet-runtime-80 --classic
sudo snap alias dotnet-runtime-80.dotnet dotnet
```

### Verify Installation

```bash
dotnet --info
```

You should see ASP.NET Core 8.0.x in the output.

---

## Step 3: Build the Application

You can either build on the server or build locally and copy the published output. Building on server is shown here.

### Install .NET SDK (for building)

```bash
sudo apt install -y dotnet-sdk-8.0
```

### Clone and Build

```bash
# Clone repository to a temporary build location
cd /tmp
git clone https://github.com/cpike5/discordbot.git
cd discordbot

# Restore and publish (self-contained optional)
dotnet publish src/DiscordBot.Bot/DiscordBot.Bot.csproj \
    -c Release \
    -o /tmp/discordbot-publish \
    --no-self-contained

# Copy to application directory
sudo cp -r /tmp/discordbot-publish/* /opt/discordbot/
sudo chown -R discordbot:discordbot /opt/discordbot

# Clean up build artifacts
rm -rf /tmp/discordbot /tmp/discordbot-publish
```

### Alternative: Build Locally and SCP

Build on your local machine:

```bash
# On local machine
dotnet publish src/DiscordBot.Bot/DiscordBot.Bot.csproj \
    -c Release \
    -o ./publish \
    --no-self-contained

# Copy to server
scp -r ./publish/* user@your-server:/tmp/discordbot-publish/
```

Then on the server:

```bash
sudo cp -r /tmp/discordbot-publish/* /opt/discordbot/
sudo chown -R discordbot:discordbot /opt/discordbot
```

---

## Step 4: Configure the Application

### Create Production Configuration Override

Create an override file for production-specific settings that aren't secrets:

```bash
sudo nano /opt/discordbot/appsettings.Production.json
```

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/var/lib/discordbot/discordbot.db"
  },
  "Application": {
    "Title": "Discord Bot",
    "BaseUrl": "https://your-domain.com"
  },
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/discordbot/discordbot-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "buffered": true,
          "flushToDiskInterval": "00:00:01",
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

Set permissions:

```bash
sudo chown discordbot:discordbot /opt/discordbot/appsettings.Production.json
sudo chmod 640 /opt/discordbot/appsettings.Production.json
```

---

## Step 5: Create Systemd Service

The application includes a sample service file in the `deployment/` directory. This section explains how to configure it.

### Create Environment File (Secrets)

First, create the environment file for secrets. A template is provided in `deployment/discordbot.env.template`:

```bash
# Create secrets directory
sudo mkdir -p /etc/discordbot

# Copy template and edit with your values
sudo cp /tmp/discordbot/deployment/discordbot.env.template /etc/discordbot/discordbot.env
sudo nano /etc/discordbot/discordbot.env
```

Edit the file with your actual secrets:

```bash
# Discord Bot Token (REQUIRED)
Discord__Token=YOUR_BOT_TOKEN_HERE

# Discord OAuth (REQUIRED for admin UI login)
Discord__OAuth__ClientId=YOUR_CLIENT_ID_HERE
Discord__OAuth__ClientSecret=YOUR_CLIENT_SECRET_HERE

# Default Admin User (optional - created on first run)
Identity__DefaultAdmin__Email=admin@example.com
Identity__DefaultAdmin__Password=ChangeThisPassword123!

# Optional: Discord Test Guild ID (for instant command registration)
# Discord__TestGuildId=123456789012345678

# Optional: Seq API Key (for centralized logging)
# Serilog__WriteTo__2__Args__apiKey=YOUR_SEQ_API_KEY
```

Secure the secrets file:

```bash
sudo chmod 700 /etc/discordbot
sudo chmod 600 /etc/discordbot/discordbot.env
sudo chown root:discordbot /etc/discordbot/discordbot.env
```

### Install Service File

Copy the sample service file from the deployment directory:

```bash
sudo cp /tmp/discordbot/deployment/discordbot.service /etc/systemd/system/discordbot.service
```

Or create it manually:

```bash
sudo nano /etc/systemd/system/discordbot.service
```

```ini
[Unit]
Description=Discord Bot Management System
Documentation=https://github.com/cpike5/discordbot
After=network.target
Wants=network-online.target

[Service]
# Type=notify enables proper startup notification with .NET systemd integration
# The application will notify systemd when it's ready to accept connections
Type=notify

User=discordbot
Group=discordbot
WorkingDirectory=/opt/discordbot
ExecStart=/usr/bin/dotnet /opt/discordbot/DiscordBot.Bot.dll

# Restart policy
Restart=always
RestartSec=10

# Timeouts
NotifyAccess=main
TimeoutStartSec=120
TimeoutStopSec=30

# Process management
KillMode=mixed

# Security hardening
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/lib/discordbot /var/log/discordbot

# Resource limits (optional - uncomment and adjust as needed)
# MemoryMax=512M
# CPUQuota=80%

# Logging - forward to journald
StandardOutput=journal
StandardError=journal
SyslogIdentifier=discordbot

# Environment
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

# Load secrets from environment file
EnvironmentFile=/etc/discordbot/discordbot.env

[Install]
WantedBy=multi-user.target
```

> **Note:** The service uses `Type=notify` which requires the `Microsoft.Extensions.Hosting.Systemd` NuGet package. This is already included in the application and configured in `Program.cs` via `UseSystemd()`. The application automatically notifies systemd when it's ready to accept connections.

### Alternative: Inline Secrets (Less Secure)

If you prefer to keep secrets in the service file itself (not recommended for production):

```ini
[Service]
# ... other settings ...

# Replace EnvironmentFile line with individual Environment lines:
Environment=Discord__Token=YOUR_BOT_TOKEN_HERE
Environment=Discord__OAuth__ClientId=YOUR_CLIENT_ID_HERE
Environment=Discord__OAuth__ClientSecret=YOUR_CLIENT_SECRET_HERE
```

Then restrict the service file permissions:

```bash
sudo chmod 600 /etc/systemd/system/discordbot.service
```

---

## Step 6: Start and Enable the Service

```bash
# Reload systemd to recognize new service
sudo systemctl daemon-reload

# Enable service to start on boot
sudo systemctl enable discordbot

# Start the service
sudo systemctl start discordbot

# Check status
sudo systemctl status discordbot
```

### Useful Commands

```bash
# View logs
sudo journalctl -u discordbot -f

# View recent logs
sudo journalctl -u discordbot --since "10 minutes ago"

# Restart service
sudo systemctl restart discordbot

# Stop service
sudo systemctl stop discordbot

# Check if service is enabled
sudo systemctl is-enabled discordbot
```

---

## Step 7: Database Setup

### SQLite (Default - Recommended for Single Server)

The application uses SQLite by default. The database file is created automatically on first run at the path specified in `ConnectionStrings:DefaultConnection`.

**Migrations are applied automatically on startup** - no manual steps required.

If you need to apply migrations manually (rare), do it from your **development machine**, not the server:

```bash
# From your local dev machine with the source code
dotnet ef database update \
    --project src/DiscordBot.Infrastructure \
    --startup-project src/DiscordBot.Bot \
    --connection "Data Source=/path/to/production/discordbot.db"
```

Or copy the database file, apply migrations locally, and copy it back.

### PostgreSQL (Alternative for High Availability)

If using PostgreSQL:

```bash
# Install PostgreSQL
sudo apt install -y postgresql postgresql-contrib

# Create database and user
sudo -u postgres psql << EOF
CREATE USER discordbot WITH PASSWORD 'your-secure-password';
CREATE DATABASE discordbot OWNER discordbot;
GRANT ALL PRIVILEGES ON DATABASE discordbot TO discordbot;
EOF
```

Update connection string in `/etc/discordbot/secrets.env`:

```bash
ConnectionStrings__DefaultConnection=Host=localhost;Database=discordbot;Username=discordbot;Password=your-secure-password
```

---

## Step 8: Reverse Proxy Setup (Optional)

If exposing the admin UI publicly, use a reverse proxy with HTTPS.

### Nginx

```bash
sudo apt install -y nginx certbot python3-certbot-nginx
```

Create site configuration:

```bash
sudo nano /etc/nginx/sites-available/discordbot
```

```nginx
server {
    listen 80;
    server_name your-domain.com;

    # General proxy settings
    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }

    # SignalR WebSocket endpoints - requires special configuration
    location /hubs/ {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;

        # WebSocket support - REQUIRED for SignalR
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";

        # Forward client information
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # Long timeout for WebSocket connections (24 hours)
        proxy_read_timeout 86400;
        proxy_send_timeout 86400;

        # Disable buffering for real-time communication
        proxy_buffering off;
        proxy_cache off;
    }
}
```

Enable and get SSL certificate:

```bash
sudo ln -s /etc/nginx/sites-available/discordbot /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx

# Get Let's Encrypt certificate
sudo certbot --nginx -d your-domain.com
```

### Cloudflare Configuration (If Using Cloudflare)

If your domain is proxied through Cloudflare, you must configure it to support WebSocket connections:

1. **Enable WebSockets:**
   - Go to your Cloudflare dashboard
   - Navigate to **Network** settings
   - Enable **WebSockets** toggle

2. **SSL/TLS Mode:**
   - Navigate to **SSL/TLS** > **Overview**
   - Set encryption mode to **Full** or **Full (strict)**
   - This ensures end-to-end encryption between Cloudflare and your origin server

3. **Caching Rules (Optional but Recommended):**
   - Navigate to **Caching** > **Cache Rules**
   - Create a rule to bypass cache for `/hubs/*` paths
   - This ensures SignalR connections aren't cached

**Note:** The application is configured to handle forwarded headers from Cloudflare and nginx automatically via the `UseForwardedHeaders()` middleware.

### Update Discord OAuth Redirect URI

In Discord Developer Portal, add your production redirect URI:

```
https://your-domain.com/signin-discord
```

---

## Step 9: Firewall Configuration

```bash
# Allow SSH (if not already)
sudo ufw allow OpenSSH

# Allow HTTP/HTTPS (if using reverse proxy)
sudo ufw allow 'Nginx Full'

# Enable firewall
sudo ufw enable

# Check status
sudo ufw status
```

**Note:** The bot connects outbound to Discord's API/Gateway. No inbound ports are needed for bot functionality itself.

---

## Updating the Application

### Manual Update Script

Create an update script:

```bash
sudo nano /opt/discordbot/update.sh
```

```bash
#!/bin/bash
set -e

echo "Stopping discordbot service..."
sudo systemctl stop discordbot

echo "Backing up current version..."
sudo cp -r /opt/discordbot /opt/discordbot.backup.$(date +%Y%m%d_%H%M%S)

echo "Cloning latest version..."
cd /tmp
rm -rf discordbot
git clone https://github.com/cpike5/discordbot.git
cd discordbot

echo "Building..."
dotnet publish src/DiscordBot.Bot/DiscordBot.Bot.csproj \
    -c Release \
    -o /tmp/discordbot-publish \
    --no-self-contained

echo "Deploying..."
sudo cp -r /tmp/discordbot-publish/* /opt/discordbot/
sudo chown -R discordbot:discordbot /opt/discordbot

echo "Starting service..."
sudo systemctl start discordbot

echo "Cleaning up..."
rm -rf /tmp/discordbot /tmp/discordbot-publish

echo "Update complete! Checking status..."
sudo systemctl status discordbot --no-pager
```

```bash
sudo chmod +x /opt/discordbot/update.sh
```

Run updates:

```bash
sudo /opt/discordbot/update.sh
```

---

## Troubleshooting

### Bot Won't Start

1. **Check logs:**
   ```bash
   sudo journalctl -u discordbot -n 50 --no-pager
   ```

2. **Verify token:**
   ```bash
   sudo grep -i token /etc/discordbot/discordbot.env
   ```

3. **Check permissions:**
   ```bash
   ls -la /opt/discordbot/
   ls -la /var/lib/discordbot/
   ls -la /var/log/discordbot/
   ```

4. **Test manually:**
   ```bash
   sudo -u discordbot bash -c 'cd /opt/discordbot && ASPNETCORE_ENVIRONMENT=Production dotnet DiscordBot.Bot.dll'
   ```

### Commands Not Appearing

- Without `Discord__TestGuildId`, global commands take up to 1 hour to propagate
- Check bot has proper permissions in Discord Developer Portal
- Verify bot is invited with `applications.commands` scope

### OAuth Errors

1. **Invalid redirect_uri:** Ensure Discord Developer Portal has exact match:
   - Development: `https://localhost:5001/signin-discord`
   - Production: `https://your-domain.com/signin-discord`

2. **Invalid client secret:** Verify secret in `/etc/discordbot/discordbot.env` matches Discord Developer Portal

### Database Errors

1. **Check database file permissions:**
   ```bash
   ls -la /var/lib/discordbot/
   ```

2. **Manually create database directory:**
   ```bash
   sudo mkdir -p /var/lib/discordbot
   sudo chown discordbot:discordbot /var/lib/discordbot
   ```

### Systemd Integration Issues

If using `Type=notify` and the service times out on startup:

1. **Verify systemd integration is active:**
   ```bash
   sudo journalctl -u discordbot | grep -i "systemd"
   ```

   You should see a log message like: "Application started. Press Ctrl+C to shut down."

2. **Check service status shows correct state:**
   ```bash
   sudo systemctl status discordbot
   ```

   With `Type=notify`, the service should show `Active: active (running)` only after the application signals it's ready.

3. **If startup times out:**
   - Increase `TimeoutStartSec` in the service file (default is 120 seconds)
   - Check for slow database migrations or Discord connection issues
   - Verify the application is actually calling `UseSystemd()` in Program.cs

4. **Fallback to simple service type:**
   If `Type=notify` doesn't work, you can change to `Type=exec` (but you'll lose proper startup notification):
   ```ini
   [Service]
   Type=exec
   # Remove NotifyAccess line if present
   ```

### Memory Issues

Increase memory limit in service file:

```ini
MemoryMax=1G
```

Then reload:

```bash
sudo systemctl daemon-reload
sudo systemctl restart discordbot
```

### SignalR/WebSocket Connection Failures

If the dashboard's real-time features aren't working (bot status not updating, connection errors in browser console):

1. **Check browser console for errors:**
   - Open Developer Tools (F12) → Console tab
   - Look for errors like "WebSocket failed to connect" or "SignalR" errors

2. **Verify nginx WebSocket configuration:**
   - Ensure the `/hubs/` location block has `Connection "upgrade"` (not `keep-alive`)
   - Check that `proxy_read_timeout` and `proxy_send_timeout` are set for long-lived connections

3. **If using Cloudflare:**
   - Enable WebSockets in Cloudflare Network settings
   - Set SSL/TLS mode to "Full" or "Full (strict)"
   - Ensure `/hubs/*` paths bypass Cloudflare caching

4. **Test nginx configuration:**
   ```bash
   sudo nginx -t
   sudo systemctl reload nginx
   ```

5. **Check application logs for SignalR errors:**
   ```bash
   sudo journalctl -u discordbot -f | grep -i signalr
   ```

6. **Common error messages:**
   - `"The connection could not be found on the server"` → Usually indicates missing WebSocket upgrade headers in nginx
   - `"Handshake was canceled"` → Often caused by SSL/TLS misconfiguration between Cloudflare and nginx
   - `"No Connection with that ID: Status code '404'"` → Proxy is not forwarding WebSocket connections correctly

---

## Security Checklist

- [ ] Created dedicated `discordbot` system user
- [ ] Secrets stored in `/etc/discordbot/discordbot.env` with `600` permissions
- [ ] Service file has `NoNewPrivileges=true` and other hardening options
- [ ] Firewall enabled with only necessary ports open
- [ ] Using HTTPS via reverse proxy for admin UI
- [ ] Changed default admin password after first login
- [ ] Discord OAuth redirect URIs properly configured
- [ ] Log files in `/var/log/discordbot/` with appropriate retention

---

## Directory Structure Summary

| Path | Purpose | Owner |
|------|---------|-------|
| `/opt/discordbot/` | Application binaries and config | `discordbot:discordbot` |
| `/var/lib/discordbot/` | Database and persistent data | `discordbot:discordbot` |
| `/var/log/discordbot/` | Application log files | `discordbot:discordbot` |
| `/etc/discordbot/` | Secrets and sensitive config | `root:root` |
| `/etc/systemd/system/discordbot.service` | Systemd service definition | `root:root` |

---

## Related Documentation

- [Identity Configuration](identity-configuration.md) - Authentication setup and troubleshooting
- [Environment Configuration](environment-configuration.md) - Environment-specific settings
- [Log Aggregation](log-aggregation.md) - Seq centralized logging setup
- [API Endpoints](api-endpoints.md) - REST API documentation

---

## Changelog

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-12-27 | Claude Code | Initial documentation |
