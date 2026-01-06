# Docker Containerization Support

## Executive Summary

Enable Docker containerization for the Discord bot application to simplify deployment and support self-hosting for OSS users. Phase 1 focuses on core containerization without audio support, which will be addressed in a future v0.7.x release.

## Problem Statement

Currently, deployment requires manual setup including .NET SDK installation, configuration, and service management. This creates friction for:
- **Self-hosting users** who want to run the bot without managing .NET dependencies
- **Environment consistency** across development, staging, and production
- **OSS adoption** where users expect simple container-based deployment

## Target Users

| User Type | Description |
|-----------|-------------|
| Project maintainer | Deploys to personal Linux VPS, currently using systemd with manual deployment |
| OSS community | Self-hosters wanting to run the bot on Docker Desktop, VPS, or any Docker-compatible environment |

## Goals

1. Enable the application to run in a Docker container
2. Provide a simple deployment experience (`docker run` or `docker-compose up`)
3. Support flexible database configuration (SQLite or external DB)
4. Integrate with existing GitHub repository via GHCR
5. Automate image builds on release

## Deliverables

### 1. Dockerfile

Multi-stage build process:
- **Build stage:** .NET 8 SDK + Node.js for Tailwind CSS build
- **Runtime stage:** Slim `mcr.microsoft.com/dotnet/aspnet:8.0` image

### 2. docker-compose.yml

Minimal configuration:
- Bot container only
- Volume mount for SQLite database (optional)
- Environment variable support via `.env` file

### 3. Environment Configuration

`.env.example` template covering:
- `Discord__Token` — Bot token (required)
- `Discord__TestGuildId` — Test guild for instant command registration (optional)
- `Discord__OAuth__ClientId` — OAuth client ID for admin UI
- `Discord__OAuth__ClientSecret` — OAuth client secret
- `ConnectionStrings__DefaultConnection` — Database connection string
- Other application settings as needed

### 4. GitHub Actions Workflow

Automated build and push to GHCR:
- **Trigger:** Release tags only (e.g., `v0.6.0`)
- **Registry:** `ghcr.io/cpike5/discordbot`
- **Tags:** Version tag + `latest`
- **Architecture:** amd64 only (initially)

### 5. Health Check Endpoint

New `/health` endpoint reporting:
- Bot connection status (Discord gateway connected)
- Database connectivity
- Overall health status

Benefits all deployment methods (Docker, systemd, monitoring tools).

### 6. Documentation

Deployment guide covering:
- Quick start with `docker run`
- docker-compose setup
- Environment variable reference
- Volume mounts for persistence
- Database configuration options
- Troubleshooting common issues

## Technical Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Base image | `mcr.microsoft.com/dotnet/aspnet:8.0` | Standard Microsoft image, well-supported |
| Container registry | GitHub Container Registry (GHCR) | Native GitHub integration, same ecosystem as repo |
| Configuration | Environment variables | Standard Docker pattern, matches existing systemd `.env` approach |
| Database | SQLite (default) or external | SQLite for simple setups, MySQL/PostgreSQL/MSSQL for production |
| Architecture | amd64 only | Covers most use cases, arm64 can be added if requested |
| Build trigger | Release tags only | Clean versioning, no noise from development commits |
| Compose scope | Minimal (bot only) | Users add their own services as needed |

## Database Strategy

| Option | Use Case | Configuration |
|--------|----------|---------------|
| SQLite | Simple deployments, development | Mount volume to `/app/data`, use default connection string |
| External DB | Production, high availability | Set `ConnectionStrings__DefaultConnection` to MySQL/PostgreSQL/MSSQL |

## Out of Scope (v0.8.0)

- **arm64 builds** — Can be added in future if users request
- **Optional service containers** — Users add Seq, database containers as needed
- **Kubernetes manifests** — May consider in future based on demand

## Milestone

**Target: v0.8.0**

This feature is planned after audio support (v0.7.0) is completed and stable. This ensures:
- Docker image ships with full functionality including audio
- No need to immediately update container after release
- Audio library dependencies (libopus, libsodium, ffmpeg) are included from the start

See [audio-support.md](audio-support.md) for audio feature requirements.

## Example Usage

### Quick Start
```bash
docker run -d \
  --name discordbot \
  -e Discord__Token=your-bot-token \
  -v discordbot-data:/app/data \
  ghcr.io/cpike5/discordbot:latest
```

### With docker-compose
```yaml
services:
  bot:
    image: ghcr.io/cpike5/discordbot:latest
    env_file: .env
    volumes:
      - ./data:/app/data
    restart: unless-stopped
```

## Success Criteria

- [ ] Application runs successfully in Docker container
- [ ] SQLite database persists across container restarts (volume mount)
- [ ] External database connection works when configured
- [ ] Health check endpoint returns accurate status
- [ ] GitHub Actions builds and pushes image on release tag
- [ ] Image available at `ghcr.io/cpike5/discordbot:<version>`
- [ ] Documentation enables users to deploy without assistance

## Related Documents

- [audio-support.md](audio-support.md) — Phase 2 audio requirements
- [versioning-strategy.md](../articles/versioning-strategy.md) — Release process
