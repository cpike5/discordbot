# Loki Production Setup on Linux VPS

**Version:** 1.0
**Last Updated:** 2026-01-02
**Target:** Ubuntu 22.04 LTS / Debian 12

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Directory Structure](#directory-structure)
- [Docker Compose Configuration](#docker-compose-configuration)
- [Loki Configuration](#loki-configuration)
- [Promtail Configuration](#promtail-configuration)
- [Grafana Configuration](#grafana-configuration)
- [Nginx Reverse Proxy](#nginx-reverse-proxy)
- [SSL/TLS with Let's Encrypt](#ssltls-with-lets-encrypt)
- [Firewall Configuration](#firewall-configuration)
- [Starting the Stack](#starting-the-stack)
- [Connecting Applications](#connecting-applications)
- [Security Hardening](#security-hardening)
- [Monitoring and Alerting](#monitoring-and-alerting)
- [Maintenance](#maintenance)
- [Troubleshooting](#troubleshooting)
- [Related Documentation](#related-documentation)

---

## Overview

This guide covers deploying Grafana Loki as a production log aggregation system on a Linux VPS. Loki is a horizontally-scalable, highly-available log aggregation system inspired by Prometheus.

### Components

| Component | Purpose | Default Port |
|-----------|---------|--------------|
| **Loki** | Log storage and query engine | 3100 |
| **Promtail** | Log collection agent | 9080 |
| **Grafana** | Visualization and dashboards | 3000 |
| **Nginx** | Reverse proxy with TLS termination | 80, 443 |

### Why Loki?

- **Cost-effective**: Indexes only labels, not full log content
- **Prometheus-like**: Uses LogQL query language similar to PromQL
- **Kubernetes-native**: First-class Kubernetes support (also works standalone)
- **Grafana integration**: Native data source in Grafana
- **Scalable**: Supports single-binary to microservices deployment

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Linux VPS                                       │
│                                                                              │
│   ┌───────────────────────────────────────────────────────────────────────┐ │
│   │                         Nginx Reverse Proxy                            │ │
│   │                                                                        │ │
│   │   ┌─────────────────────┐     ┌─────────────────────────┐            │ │
│   │   │ logs.yourdomain.com │     │ grafana.yourdomain.com  │            │ │
│   │   │     (Port 443)      │     │      (Port 443)         │            │ │
│   │   └──────────┬──────────┘     └───────────┬─────────────┘            │ │
│   │              │                             │                          │ │
│   └──────────────┼─────────────────────────────┼──────────────────────────┘ │
│                  │                             │                            │
│                  ▼                             ▼                            │
│   ┌──────────────────────────┐   ┌──────────────────────────┐             │
│   │         Loki             │   │        Grafana           │             │
│   │    (Log Storage)         │◄──│    (Visualization)       │             │
│   │                          │   │                          │             │
│   │  localhost:3100          │   │  localhost:3000          │             │
│   └──────────────────────────┘   └──────────────────────────┘             │
│                  ▲                                                          │
│                  │                                                          │
│   ┌──────────────────────────┐                                             │
│   │       Promtail           │                                             │
│   │    (Log Collector)       │                                             │
│   │                          │                                             │
│   │  Reads: /var/log/*       │                                             │
│   └──────────────────────────┘                                             │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                    │
                    │ HTTPS (Push API for remote apps)
                    ▼
            Remote Applications
            (Discord Bot, etc.)
```

---

## Prerequisites

### Server Requirements

| Resource | Minimum | Recommended | Notes |
|----------|---------|-------------|-------|
| RAM | 2 GB | 4 GB+ | Loki needs memory for query processing |
| Disk | 20 GB | 50 GB+ | Depends on log retention period |
| CPU | 2 vCPU | 4 vCPU | More cores improve query performance |
| OS | Ubuntu 22.04 | Ubuntu 22.04 LTS | Or Debian 12 |

### Install Required Software

```bash
# Update system
sudo apt update && sudo apt upgrade -y

# Install Docker
sudo apt install -y docker.io docker-compose-v2

# Add user to docker group
sudo usermod -aG docker $USER

# Install Nginx
sudo apt install -y nginx

# Install Certbot for SSL
sudo apt install -y certbot python3-certbot-nginx

# Log out and back in for docker group to take effect
# Verify installation
docker --version
docker compose version
nginx -v
```

### DNS Configuration

Before proceeding, configure DNS records for your domain:

| Record Type | Name | Value |
|-------------|------|-------|
| A | grafana.yourdomain.com | Your VPS IP |
| A | logs.yourdomain.com | Your VPS IP |

---

## Directory Structure

```bash
# Create directory structure
sudo mkdir -p /opt/loki/{config,data/loki,data/grafana}
sudo chown -R $USER:$USER /opt/loki
cd /opt/loki

# Create log directory for applications
sudo mkdir -p /var/log/discordbot
sudo chown -R $USER:$USER /var/log/discordbot
```

Final structure:

```
/opt/loki/
├── docker-compose.yml
├── config/
│   ├── loki-config.yml
│   ├── promtail-config.yml
│   └── grafana-datasources.yml
└── data/
    ├── loki/
    └── grafana/
```

---

## Docker Compose Configuration

Create the main Docker Compose file:

```bash
nano /opt/loki/docker-compose.yml
```

```yaml
version: "3.8"

networks:
  loki:
    driver: bridge

volumes:
  loki-data:
  grafana-data:

services:
  # ============================================
  # LOKI - Log Aggregation Engine
  # ============================================
  loki:
    image: grafana/loki:3.0.0
    container_name: loki
    restart: unless-stopped
    user: "10001:10001"
    ports:
      - "127.0.0.1:3100:3100"  # Only expose to localhost
    volumes:
      - ./config/loki-config.yml:/etc/loki/config.yml:ro
      - loki-data:/loki
    command: -config.file=/etc/loki/config.yml
    networks:
      - loki
    healthcheck:
      test: ["CMD-SHELL", "wget --no-verbose --tries=1 --spider http://localhost:3100/ready || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 30s
    deploy:
      resources:
        limits:
          memory: 1G
        reservations:
          memory: 256M

  # ============================================
  # PROMTAIL - Log Collector
  # ============================================
  promtail:
    image: grafana/promtail:3.0.0
    container_name: promtail
    restart: unless-stopped
    volumes:
      - ./config/promtail-config.yml:/etc/promtail/config.yml:ro
      - /var/log:/var/log:ro
      - /var/log/discordbot:/var/log/discordbot:ro
    command: -config.file=/etc/promtail/config.yml
    networks:
      - loki
    depends_on:
      loki:
        condition: service_healthy
    deploy:
      resources:
        limits:
          memory: 256M
        reservations:
          memory: 64M

  # ============================================
  # GRAFANA - Visualization
  # ============================================
  grafana:
    image: grafana/grafana:11.0.0
    container_name: grafana
    restart: unless-stopped
    user: "472:472"
    environment:
      # Security
      - GF_SECURITY_ADMIN_USER=${GRAFANA_ADMIN_USER:-admin}
      - GF_SECURITY_ADMIN_PASSWORD=${GRAFANA_ADMIN_PASSWORD:-changeme}
      - GF_SECURITY_SECRET_KEY=${GRAFANA_SECRET_KEY:-SW2YcwTIb9zpOOhoPsMm}
      - GF_USERS_ALLOW_SIGN_UP=false
      - GF_USERS_ALLOW_ORG_CREATE=false

      # Server
      - GF_SERVER_ROOT_URL=https://grafana.yourdomain.com
      - GF_SERVER_DOMAIN=grafana.yourdomain.com

      # Auth
      - GF_AUTH_ANONYMOUS_ENABLED=false
      - GF_AUTH_BASIC_ENABLED=true

      # Logging
      - GF_LOG_MODE=console
      - GF_LOG_LEVEL=warn
    ports:
      - "127.0.0.1:3000:3000"  # Only expose to localhost
    volumes:
      - grafana-data:/var/lib/grafana
      - ./config/grafana-datasources.yml:/etc/grafana/provisioning/datasources/datasources.yml:ro
    networks:
      - loki
    depends_on:
      loki:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "wget --no-verbose --tries=1 --spider http://localhost:3000/api/health || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
    deploy:
      resources:
        limits:
          memory: 512M
        reservations:
          memory: 128M
```

---

## Loki Configuration

Create the Loki configuration file:

```bash
nano /opt/loki/config/loki-config.yml
```

```yaml
# Loki Configuration for Production
# https://grafana.com/docs/loki/latest/configuration/

auth_enabled: false

server:
  http_listen_port: 3100
  grpc_listen_port: 9096
  log_level: warn

  # Graceful shutdown
  graceful_shutdown_timeout: 30s

common:
  instance_addr: 127.0.0.1
  path_prefix: /loki
  storage:
    filesystem:
      chunks_directory: /loki/chunks
      rules_directory: /loki/rules
  replication_factor: 1
  ring:
    kvstore:
      store: inmemory

# Query performance settings
query_range:
  results_cache:
    cache:
      embedded_cache:
        enabled: true
        max_size_mb: 100

  # Parallelism for large queries
  parallelise_shardable_queries: true

# Schema configuration
schema_config:
  configs:
    - from: 2024-01-01
      store: tsdb
      object_store: filesystem
      schema: v13
      index:
        prefix: index_
        period: 24h

# Storage settings
storage_config:
  tsdb_shipper:
    active_index_directory: /loki/tsdb-index
    cache_location: /loki/tsdb-cache

  filesystem:
    directory: /loki/chunks

# Compactor for retention and optimization
compactor:
  working_directory: /loki/compactor
  shared_store: filesystem
  retention_enabled: true
  retention_delete_delay: 2h
  compaction_interval: 10m
  delete_request_store: filesystem

# Limits and policies
limits_config:
  # Retention
  retention_period: 30d

  # Ingestion limits
  ingestion_rate_mb: 10
  ingestion_burst_size_mb: 20
  per_stream_rate_limit: 5MB
  per_stream_rate_limit_burst: 15MB

  # Query limits
  max_query_length: 721h  # 30 days
  max_query_parallelism: 16
  max_query_series: 10000
  max_entries_limit_per_query: 10000

  # Cardinality limits (prevent label explosion)
  max_label_name_length: 1024
  max_label_value_length: 2048
  max_label_names_per_series: 30

# Ingester settings
ingester:
  lifecycler:
    ring:
      kvstore:
        store: inmemory
      replication_factor: 1
  chunk_idle_period: 30m
  chunk_retain_period: 1m
  max_transfer_retries: 0
  wal:
    enabled: true
    dir: /loki/wal

# Frontend settings for query optimization
frontend:
  max_outstanding_per_tenant: 2048
  compress_responses: true

# Query scheduler
query_scheduler:
  max_outstanding_requests_per_tenant: 2048
```

---

## Promtail Configuration

Create the Promtail configuration:

```bash
nano /opt/loki/config/promtail-config.yml
```

```yaml
# Promtail Configuration
# https://grafana.com/docs/loki/latest/clients/promtail/configuration/

server:
  http_listen_port: 9080
  grpc_listen_port: 0
  log_level: warn

positions:
  filename: /tmp/positions.yaml
  sync_period: 10s

clients:
  - url: http://loki:3100/loki/api/v1/push
    tenant_id: default
    batchwait: 1s
    batchsize: 1048576  # 1MB
    timeout: 10s

scrape_configs:
  # ============================================
  # Discord Bot Application Logs (JSON format)
  # ============================================
  - job_name: discordbot
    static_configs:
      - targets:
          - localhost
        labels:
          job: discordbot
          app: discordbot
          environment: production
          __path__: /var/log/discordbot/*.log

    pipeline_stages:
      # Parse Serilog Compact JSON format
      - json:
          expressions:
            timestamp: "@t"
            level: "@l"
            message: "@m"
            message_template: "@mt"
            exception: "@x"
            source_context: SourceContext
            correlation_id: CorrelationId
            trace_id: TraceId
            span_id: SpanId
            guild_id: GuildId
            user_id: UserId
            command_name: CommandName
            execution_time_ms: ExecutionTimeMs

      # Set timestamp from log entry
      - timestamp:
          source: timestamp
          format: RFC3339Nano
          fallback_formats:
            - "2006-01-02T15:04:05.999999999Z07:00"
            - "2006-01-02T15:04:05Z07:00"

      # Extract labels for indexing (keep cardinality low)
      - labels:
          level:
          source_context:

      # Structured metadata (high-cardinality fields, not indexed)
      - structured_metadata:
          correlation_id:
          trace_id:
          span_id:
          guild_id:
          user_id:
          command_name:

      # Format output
      - output:
          source: message

  # ============================================
  # System Logs
  # ============================================
  - job_name: syslog
    static_configs:
      - targets:
          - localhost
        labels:
          job: syslog
          environment: production
          __path__: /var/log/syslog

    pipeline_stages:
      - regex:
          expression: '^(?P<timestamp>\w+\s+\d+\s+\d+:\d+:\d+)\s+(?P<host>\S+)\s+(?P<service>[^:\[]+)(?:\[(?P<pid>\d+)\])?:\s+(?P<message>.*)$'
      - labels:
          service:
      - output:
          source: message

  # ============================================
  # Nginx Access Logs
  # ============================================
  - job_name: nginx-access
    static_configs:
      - targets:
          - localhost
        labels:
          job: nginx
          log_type: access
          environment: production
          __path__: /var/log/nginx/access.log

    pipeline_stages:
      - regex:
          expression: '^(?P<remote_addr>\S+) - (?P<remote_user>\S+) \[(?P<time_local>[^\]]+)\] "(?P<request>[^"]*)" (?P<status>\d+) (?P<body_bytes_sent>\d+) "(?P<http_referer>[^"]*)" "(?P<http_user_agent>[^"]*)"'
      - labels:
          status:
      - output:
          source: request

  # ============================================
  # Nginx Error Logs
  # ============================================
  - job_name: nginx-error
    static_configs:
      - targets:
          - localhost
        labels:
          job: nginx
          log_type: error
          environment: production
          __path__: /var/log/nginx/error.log
```

---

## Grafana Configuration

Create the Grafana datasources configuration:

```bash
nano /opt/loki/config/grafana-datasources.yml
```

```yaml
apiVersion: 1

datasources:
  - name: Loki
    type: loki
    access: proxy
    url: http://loki:3100
    isDefault: true
    editable: false
    jsonData:
      maxLines: 1000
      timeout: 60
```

---

## Nginx Reverse Proxy

### Main Nginx Configuration

```bash
sudo nano /etc/nginx/nginx.conf
```

Ensure these settings are in the `http` block:

```nginx
http {
    # ... existing settings ...

    # Logging format with request timing
    log_format main '$remote_addr - $remote_user [$time_local] "$request" '
                    '$status $body_bytes_sent "$http_referer" '
                    '"$http_user_agent" "$http_x_forwarded_for" '
                    'rt=$request_time uct="$upstream_connect_time" '
                    'uht="$upstream_header_time" urt="$upstream_response_time"';

    # Gzip compression
    gzip on;
    gzip_vary on;
    gzip_proxied any;
    gzip_comp_level 6;
    gzip_types text/plain text/css text/xml application/json application/javascript
               application/rss+xml application/atom+xml image/svg+xml;

    # Security headers (applied globally)
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;

    # Rate limiting zones
    limit_req_zone $binary_remote_addr zone=api_limit:10m rate=10r/s;
    limit_req_zone $binary_remote_addr zone=push_limit:10m rate=100r/s;
    limit_conn_zone $binary_remote_addr zone=conn_limit:10m;

    # Include site configs
    include /etc/nginx/sites-enabled/*;
}
```

### Grafana Site Configuration

```bash
sudo nano /etc/nginx/sites-available/grafana
```

```nginx
# Grafana - Log Visualization
# grafana.yourdomain.com

# Upstream definition
upstream grafana {
    server 127.0.0.1:3000;
    keepalive 64;
}

# HTTP -> HTTPS redirect
server {
    listen 80;
    listen [::]:80;
    server_name grafana.yourdomain.com;

    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    location / {
        return 301 https://$server_name$request_uri;
    }
}

# HTTPS server
server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name grafana.yourdomain.com;

    # SSL certificates (managed by Certbot)
    ssl_certificate /etc/letsencrypt/live/grafana.yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/grafana.yourdomain.com/privkey.pem;
    include /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;

    # Security headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header Content-Security-Policy "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' data:; connect-src 'self' wss: https:;" always;

    # Logging
    access_log /var/log/nginx/grafana.access.log main;
    error_log /var/log/nginx/grafana.error.log warn;

    # Connection limits
    limit_conn conn_limit 20;

    # Root location
    location / {
        proxy_pass http://grafana;

        # Headers
        proxy_set_header Host $http_host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Port $server_port;

        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;

        # Buffering
        proxy_buffering on;
        proxy_buffer_size 4k;
        proxy_buffers 8 4k;
    }

    # WebSocket support for live dashboard updates
    location /api/live/ {
        proxy_pass http://grafana;

        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $http_host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # WebSocket timeouts
        proxy_read_timeout 86400s;
        proxy_send_timeout 86400s;
    }

    # API endpoints
    location /api/ {
        limit_req zone=api_limit burst=20 nodelay;

        proxy_pass http://grafana;

        proxy_set_header Host $http_host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

### Loki Site Configuration

```bash
sudo nano /etc/nginx/sites-available/loki
```

```nginx
# Loki - Log Aggregation API
# logs.yourdomain.com

# Upstream definition
upstream loki {
    server 127.0.0.1:3100;
    keepalive 32;
}

# HTTP -> HTTPS redirect
server {
    listen 80;
    listen [::]:80;
    server_name logs.yourdomain.com;

    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    location / {
        return 301 https://$server_name$request_uri;
    }
}

# HTTPS server
server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name logs.yourdomain.com;

    # SSL certificates (managed by Certbot)
    ssl_certificate /etc/letsencrypt/live/logs.yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/logs.yourdomain.com/privkey.pem;
    include /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;

    # Security headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;

    # Logging
    access_log /var/log/nginx/loki.access.log main;
    error_log /var/log/nginx/loki.error.log warn;

    # Client body size for log ingestion
    client_max_body_size 10M;

    # Connection limits
    limit_conn conn_limit 50;

    # API key authentication for push endpoint
    # Uncomment and configure for production
    # map $http_x_scope_orgid $api_key_valid {
    #     default 0;
    #     "your-secret-api-key" 1;
    # }

    # Health check (no auth required)
    location /ready {
        proxy_pass http://loki/ready;
        proxy_set_header Host $host;
    }

    location /metrics {
        proxy_pass http://loki/metrics;
        proxy_set_header Host $host;

        # Restrict metrics to internal networks
        allow 10.0.0.0/8;
        allow 172.16.0.0/12;
        allow 192.168.0.0/16;
        allow 127.0.0.1;
        deny all;
    }

    # Push API for log ingestion
    location /loki/api/v1/push {
        limit_req zone=push_limit burst=200 nodelay;

        # Basic auth for push endpoint
        auth_basic "Loki Push API";
        auth_basic_user_file /etc/nginx/.loki-htpasswd;

        proxy_pass http://loki;

        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Scope-OrgID default;

        # Timeouts for large log batches
        proxy_connect_timeout 10s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;

        # Disable buffering for streaming
        proxy_buffering off;
    }

    # Query API (authenticated)
    location /loki/api/ {
        limit_req zone=api_limit burst=50 nodelay;

        # Basic auth for query API
        auth_basic "Loki Query API";
        auth_basic_user_file /etc/nginx/.loki-htpasswd;

        proxy_pass http://loki;

        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Scope-OrgID default;

        # Query timeouts
        proxy_connect_timeout 10s;
        proxy_send_timeout 300s;  # Long timeout for complex queries
        proxy_read_timeout 300s;
    }

    # Block all other endpoints
    location / {
        return 403;
    }
}
```

### Create Authentication Files

```bash
# Install apache2-utils for htpasswd
sudo apt install -y apache2-utils

# Create htpasswd file for Loki API
sudo htpasswd -c /etc/nginx/.loki-htpasswd loki-push
# Enter a strong password when prompted

# Add additional users if needed
sudo htpasswd /etc/nginx/.loki-htpasswd loki-query

# Set proper permissions
sudo chmod 640 /etc/nginx/.loki-htpasswd
sudo chown root:www-data /etc/nginx/.loki-htpasswd
```

### Enable Sites

```bash
# Enable sites
sudo ln -sf /etc/nginx/sites-available/grafana /etc/nginx/sites-enabled/
sudo ln -sf /etc/nginx/sites-available/loki /etc/nginx/sites-enabled/

# Remove default site
sudo rm -f /etc/nginx/sites-enabled/default

# Test configuration
sudo nginx -t

# Reload nginx
sudo systemctl reload nginx
```

---

## SSL/TLS with Let's Encrypt

### Obtain Certificates

```bash
# Create webroot directory
sudo mkdir -p /var/www/certbot

# Obtain certificate for Grafana
sudo certbot certonly --webroot \
    -w /var/www/certbot \
    -d grafana.yourdomain.com \
    --email your-email@example.com \
    --agree-tos \
    --no-eff-email

# Obtain certificate for Loki
sudo certbot certonly --webroot \
    -w /var/www/certbot \
    -d logs.yourdomain.com \
    --email your-email@example.com \
    --agree-tos \
    --no-eff-email
```

### Automated Renewal

Certbot automatically installs a systemd timer for renewal. Verify it:

```bash
# Check timer status
sudo systemctl status certbot.timer

# Test renewal (dry run)
sudo certbot renew --dry-run
```

### Post-Renewal Hook

Create a hook to reload Nginx after certificate renewal:

```bash
sudo nano /etc/letsencrypt/renewal-hooks/post/reload-nginx.sh
```

```bash
#!/bin/bash
systemctl reload nginx
```

```bash
sudo chmod +x /etc/letsencrypt/renewal-hooks/post/reload-nginx.sh
```

---

## Firewall Configuration

### UFW (Recommended)

```bash
# Enable UFW if not already
sudo ufw enable

# Allow SSH (important - don't lock yourself out!)
sudo ufw allow ssh

# Allow HTTP (for Let's Encrypt and redirects)
sudo ufw allow 80/tcp

# Allow HTTPS
sudo ufw allow 443/tcp

# Verify rules
sudo ufw status verbose
```

### iptables Alternative

```bash
# Allow HTTP and HTTPS
sudo iptables -A INPUT -p tcp --dport 80 -j ACCEPT
sudo iptables -A INPUT -p tcp --dport 443 -j ACCEPT

# Save rules
sudo apt install -y iptables-persistent
sudo netfilter-persistent save
```

---

## Starting the Stack

### Create Environment File

```bash
nano /opt/loki/.env
```

```bash
# Grafana credentials
GRAFANA_ADMIN_USER=admin
GRAFANA_ADMIN_PASSWORD=your-secure-password-here
GRAFANA_SECRET_KEY=your-random-32-char-secret-key

# Domain (update in docker-compose.yml as well)
DOMAIN=yourdomain.com
```

```bash
# Secure the env file
chmod 600 /opt/loki/.env
```

### Start Services

```bash
cd /opt/loki

# Start all services
docker compose up -d

# Check status
docker compose ps

# View logs
docker compose logs -f

# Check health endpoints
curl -s http://localhost:3100/ready
curl -s http://localhost:3000/api/health | jq
```

### Create Systemd Service (Optional)

For automatic startup:

```bash
sudo nano /etc/systemd/system/loki-stack.service
```

```ini
[Unit]
Description=Loki Observability Stack
Requires=docker.service
After=docker.service

[Service]
Type=oneshot
RemainAfterExit=yes
WorkingDirectory=/opt/loki
ExecStart=/usr/bin/docker compose up -d
ExecStop=/usr/bin/docker compose down
TimeoutStartSec=0

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable loki-stack
```

---

## Connecting Applications

### .NET Application (Serilog)

Install the Loki sink NuGet package:

```bash
dotnet add package Serilog.Sinks.Grafana.Loki
```

Configure in `appsettings.Production.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/discordbot/discordbot-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
        }
      },
      {
        "Name": "GrafanaLoki",
        "Args": {
          "uri": "https://logs.yourdomain.com",
          "labels": [
            {
              "key": "app",
              "value": "discordbot"
            },
            {
              "key": "environment",
              "value": "production"
            }
          ],
          "credentials": {
            "login": "loki-push",
            "password": "your-password-here"
          },
          "propertiesAsLabels": ["level"],
          "batchPostingLimit": 100,
          "queueLimit": 10000,
          "period": "00:00:02"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

### Direct HTTP Push (curl)

Test the push API:

```bash
# Push a test log entry
curl -X POST "https://logs.yourdomain.com/loki/api/v1/push" \
    -u "loki-push:your-password" \
    -H "Content-Type: application/json" \
    -d '{
        "streams": [{
            "stream": {
                "job": "test",
                "level": "info"
            },
            "values": [
                ["'$(date +%s)000000000'", "Test log message from curl"]
            ]
        }]
    }'
```

### Promtail from Remote Server

If you have applications on other servers, install Promtail there:

```yaml
# promtail-remote-config.yml
server:
  http_listen_port: 9080
  grpc_listen_port: 0

positions:
  filename: /tmp/positions.yaml

clients:
  - url: https://logs.yourdomain.com/loki/api/v1/push
    basic_auth:
      username: loki-push
      password: your-password-here

scrape_configs:
  - job_name: remote-app
    static_configs:
      - targets:
          - localhost
        labels:
          job: remote-app
          host: ${HOSTNAME}
          __path__: /var/log/myapp/*.log
```

---

## Security Hardening

### 1. Network Isolation

Loki and Grafana only listen on localhost (127.0.0.1). All external access goes through Nginx.

### 2. Authentication

- **Grafana**: Built-in authentication with strong admin password
- **Loki Push API**: HTTP Basic Auth via Nginx
- **Loki Query API**: HTTP Basic Auth via Nginx

### 3. Rate Limiting

Configured in Nginx:
- API endpoints: 10 requests/second per IP
- Push endpoint: 100 requests/second per IP

### 4. TLS Everywhere

All external connections use HTTPS with Let's Encrypt certificates.

### 5. Container Security

```yaml
# Already in docker-compose.yml
user: "10001:10001"  # Non-root user
deploy:
  resources:
    limits:
      memory: 1G    # Memory limits
```

### 6. Log Rotation

Configure Docker log rotation:

```bash
sudo nano /etc/docker/daemon.json
```

```json
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "3"
  }
}
```

```bash
sudo systemctl restart docker
```

---

## Monitoring and Alerting

### Grafana Alerting

1. Navigate to Grafana > Alerting > Alert Rules
2. Create rules for:
   - High error rate: `sum(rate({job="discordbot", level="Error"} [5m])) > 0.1`
   - Log volume spike: `sum(rate({job="discordbot"} [5m])) > 100`
   - No logs (dead application): `count_over_time({job="discordbot"} [5m]) == 0`

### Health Check Script

```bash
nano /opt/loki/healthcheck.sh
```

```bash
#!/bin/bash

RED='\033[0;31m'
GREEN='\033[0;32m'
NC='\033[0m'

echo "=== Loki Stack Health Check ==="
echo ""

# Check Loki
if curl -sf http://localhost:3100/ready > /dev/null 2>&1; then
    echo -e "Loki:    ${GREEN}OK${NC}"
else
    echo -e "Loki:    ${RED}FAILED${NC}"
fi

# Check Grafana
if curl -sf http://localhost:3000/api/health > /dev/null 2>&1; then
    echo -e "Grafana: ${GREEN}OK${NC}"
else
    echo -e "Grafana: ${RED}FAILED${NC}"
fi

# Check Nginx
if systemctl is-active --quiet nginx; then
    echo -e "Nginx:   ${GREEN}OK${NC}"
else
    echo -e "Nginx:   ${RED}FAILED${NC}"
fi

# Check SSL certificates
echo ""
echo "=== SSL Certificate Expiry ==="
for domain in grafana.yourdomain.com logs.yourdomain.com; do
    expiry=$(sudo openssl x509 -enddate -noout -in /etc/letsencrypt/live/$domain/fullchain.pem 2>/dev/null | cut -d= -f2)
    if [ -n "$expiry" ]; then
        echo "$domain: $expiry"
    fi
done

echo ""
echo "=== Container Status ==="
docker compose -f /opt/loki/docker-compose.yml ps --format "table {{.Name}}\t{{.Status}}"

echo ""
echo "=== Disk Usage ==="
df -h /opt/loki
docker system df
```

```bash
chmod +x /opt/loki/healthcheck.sh
```

### Cron Monitoring

```bash
# Add to crontab
crontab -e
```

```cron
# Health check every 5 minutes
*/5 * * * * /opt/loki/healthcheck.sh >> /var/log/loki-health.log 2>&1

# Daily disk usage alert
0 8 * * * df -h /opt/loki | mail -s "Loki Disk Usage" admin@yourdomain.com
```

---

## Maintenance

### Backup

```bash
nano /opt/loki/backup.sh
```

```bash
#!/bin/bash
set -e

BACKUP_DIR="/backup/loki"
DATE=$(date +%Y%m%d)

mkdir -p $BACKUP_DIR

echo "Stopping services..."
cd /opt/loki
docker compose stop

echo "Backing up data..."
tar -czvf $BACKUP_DIR/loki-data-$DATE.tar.gz data/
tar -czvf $BACKUP_DIR/loki-config-$DATE.tar.gz config/

echo "Starting services..."
docker compose start

echo "Cleaning old backups (keeping 7 days)..."
find $BACKUP_DIR -name "*.tar.gz" -mtime +7 -delete

echo "Backup complete: $BACKUP_DIR"
ls -lh $BACKUP_DIR
```

```bash
chmod +x /opt/loki/backup.sh

# Add to crontab for weekly backup
crontab -e
# 0 2 * * 0 /opt/loki/backup.sh >> /var/log/loki-backup.log 2>&1
```

### Update Components

```bash
cd /opt/loki

# Pull latest images
docker compose pull

# Recreate containers
docker compose up -d

# Clean up old images
docker image prune -f

# Verify
docker compose ps
```

### Clear Old Data

```bash
# If disk space is low, reduce retention
# Edit config/loki-config.yml and set:
# limits_config:
#   retention_period: 7d

# Then restart
docker compose restart loki

# Force compaction
docker compose exec loki wget -q -O- http://localhost:3100/compactor/ring
```

---

## Troubleshooting

### Logs Not Appearing

1. **Check Promtail logs:**
   ```bash
   docker compose logs promtail
   ```

2. **Verify log file permissions:**
   ```bash
   ls -la /var/log/discordbot/
   # Promtail needs read access
   ```

3. **Check Loki is receiving data:**
   ```bash
   curl -s 'http://localhost:3100/loki/api/v1/labels' | jq
   ```

4. **Verify log format:**
   ```bash
   tail -1 /var/log/discordbot/*.log | jq .
   ```

### Query Timeouts

1. **Reduce query range** - Query smaller time windows
2. **Add label filters** - Filter by `job`, `level`, etc.
3. **Increase limits** in `loki-config.yml`:
   ```yaml
   limits_config:
     query_timeout: 5m
   ```

### High Memory Usage

1. **Check container stats:**
   ```bash
   docker stats
   ```

2. **Reduce cache size:**
   ```yaml
   query_range:
     results_cache:
       cache:
         embedded_cache:
           max_size_mb: 50  # Reduce from 100
   ```

3. **Enable WAL compression:**
   ```yaml
   ingester:
     wal:
       replay_memory_ceiling: 500MB
   ```

### Nginx 502 Bad Gateway

1. **Check if containers are running:**
   ```bash
   docker compose ps
   ```

2. **Verify ports are correct:**
   ```bash
   ss -tlnp | grep -E '3000|3100'
   ```

3. **Check Nginx error logs:**
   ```bash
   sudo tail -f /var/log/nginx/grafana.error.log
   sudo tail -f /var/log/nginx/loki.error.log
   ```

### SSL Certificate Issues

1. **Check certificate validity:**
   ```bash
   sudo certbot certificates
   ```

2. **Force renewal:**
   ```bash
   sudo certbot renew --force-renewal
   sudo systemctl reload nginx
   ```

---

## Related Documentation

- [Log Aggregation with Seq](log-aggregation.md) - Alternative using Seq
- [Jaeger and Loki Setup](jaeger-loki-setup.md) - Combined tracing and logging
- [Distributed Tracing](tracing.md) - OpenTelemetry tracing
- [Linux VPS Deployment](linux-deployment.md) - Base deployment guide
- [Grafana Dashboards Specification](grafana-dashboards-specification.md) - Dashboard patterns

### External Resources

- [Loki Documentation](https://grafana.com/docs/loki/latest/)
- [LogQL Reference](https://grafana.com/docs/loki/latest/logql/)
- [Promtail Configuration](https://grafana.com/docs/loki/latest/clients/promtail/configuration/)
- [Grafana Documentation](https://grafana.com/docs/grafana/latest/)
- [Nginx Documentation](https://nginx.org/en/docs/)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-01-02 | Initial documentation |

---

*Last Updated: January 2, 2026*
