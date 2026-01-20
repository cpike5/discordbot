# Discord Bot Management System

[![Version](https://img.shields.io/badge/version-v0.12.1--dev-blue)](https://github.com/cpike5/discordbot/releases)
[![CI](https://github.com/cpike5/discordbot/actions/workflows/ci.yml/badge.svg)](https://github.com/cpike5/discordbot/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![Discord.NET](https://img.shields.io/badge/Discord.NET-3.19.0--beta-5865F2)](https://github.com/discord-net/Discord.Net)

A Discord bot built with .NET 8 and Discord.NET that provides a foundation for managing Discord servers through slash commands, a REST API, and a Razor Pages admin UI. The system combines a hosted Discord bot service with a Web API and admin dashboard for management and monitoring.

## Table of Contents

- [Features](#features)
- [Quick Start](#quick-start)
  - [Prerequisites](#prerequisites)
  - [Configuration](#configuration)
  - [Getting Started Checklist](#getting-started-checklist)
  - [Running the Bot](#running-the-bot)
- [Architecture](#architecture)
  - [Key Components](#key-components)
- [Commands](#commands)
- [Logging](#logging)
  - [Log Levels](#log-levels)
- [Development](#development)
  - [Adding New Commands](#adding-new-commands)
  - [Building and Testing](#building-and-testing)
  - [Adding Migrations](#adding-migrations)
- [Project Structure](#project-structure)
- [Configuration Reference](#configuration-reference)
  - [Essential Configuration](#essential-configuration)
  - [Feature-Specific Configuration](#feature-specific-configuration)
- [Dependencies](#dependencies)
- [Production Deployment](#production-deployment)
- [Security](#security)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [License](#license)
- [Related Documentation](#related-documentation)

## Features

### Core Infrastructure
- Slash command framework with automatic discovery and registration
- Razor Pages admin UI with Tailwind CSS
- ASP.NET Core Identity with Discord OAuth integration
- Role-based authorization (SuperAdmin, Admin, Moderator, Viewer)
- User management dashboard with Discord account linking
- Structured logging with Serilog (console and rotating file outputs)
- Clean architecture with separation of concerns
- Graceful bot lifecycle management
- Swagger/OpenAPI documentation for REST API

### Bot Features
- **Rat Watch** - Accountability system for tracking commitments with community voting and leaderboards ([docs](docs/articles/rat-watch.md))
- **AI Assistant** - Claude-powered conversational assistant with tool usage, conversation history, and cost tracking ([docs](docs/articles/ai-assistant.md))
- **Auto-Moderation** - Configurable content filtering with spam detection, flagged event management, and automated actions
- **Scheduled Messages** - Automated announcements with flexible scheduling (one-time, recurring, cron expressions) ([docs](docs/articles/scheduled-messages.md))
- **Welcome System** - Configurable welcome messages and automatic role assignment for new members ([docs](docs/articles/welcome-system.md))
- **Member Directory** - Searchable, filterable member list with bulk export functionality ([docs](docs/articles/member-directory.md))
- **Moderation System** - Comprehensive moderation toolkit with warnings, kicks, bans, mutes, case history, notes, tags, and watchlists
- **Reminders** - Personal reminders with natural language time parsing (e.g., "10m", "tomorrow 3pm") ([docs](docs/articles/reminder-system.md))
- **Message Logging** - Consent-aware Discord message capture with GDPR-compliant data handling
- **Audit Logging** - Comprehensive audit trail for user, guild, bot, and system events with fluent builder API ([docs](docs/articles/audit-log-system.md))
- **Performance Dashboard** - Real-time monitoring with health metrics, command performance, API usage, and alerting ([docs](docs/articles/bot-performance-dashboard.md))
- **SignalR Real-time Updates** - Live dashboard updates for bot status and command execution ([docs](docs/articles/signalr-realtime.md))
- **Soundboard** - Audio playback in voice channels with guild-managed sound libraries ([docs](docs/articles/soundboard.md))
- **Text-to-Speech** - Azure Cognitive Services integration for voice synthesis in voice channels ([docs](docs/articles/tts-support.md))
- **Member Portal** - Public-facing TTS and Soundboard interfaces for guild members (OAuth required)
- **Global Search** - Cross-guild search for commands, logs, users, and content
- **Consent & Privacy** - GDPR-compliant user consent management and data handling ([docs](docs/articles/consent-privacy.md))

## Quick Start

### Prerequisites

- .NET 8 SDK
- Node.js (for Tailwind CSS build)
- Discord bot token ([Create a bot](https://discord.com/developers/applications))

### Configuration

1. **Set up Discord bot token** (required):

```bash
cd src/DiscordBot.Bot
dotnet user-secrets set "Discord:Token" "your-bot-token-here"
```

2. **Optional: Configure test guild for faster command registration**:

```bash
dotnet user-secrets set "Discord:TestGuildId" "your-guild-id-here"
```

When `TestGuildId` is set, commands register instantly to that guild. Without it, commands register globally (can take up to 1 hour to propagate).

3. **Optional: Configure Discord OAuth for admin UI**:

```bash
dotnet user-secrets set "Discord:OAuth:ClientId" "your-client-id"
dotnet user-secrets set "Discord:OAuth:ClientSecret" "your-client-secret"
```

4. **Optional: Configure default admin user**:

```bash
dotnet user-secrets set "Identity:DefaultAdmin:Email" "admin@example.com"
dotnet user-secrets set "Identity:DefaultAdmin:Password" "InitialPassword123!"
```

### Getting Started Checklist

Complete these steps for a successful first-time setup:

#### 1. Discord Developer Portal Setup
Follow the [Discord Bot Setup Guide](docs/articles/discord-bot-setup.md) to:
- ✅ Create bot application
- ✅ Enable Message Content Intent (required for message logging)
- ✅ Enable Server Members Intent (required for member directory)
- ✅ Add bot to server with proper permissions
- ✅ Copy bot token and (optionally) guild ID

#### 2. Required Configuration
```bash
cd src/DiscordBot.Bot
dotnet user-secrets set "Discord:Token" "your-bot-token-here"
dotnet user-secrets set "Discord:TestGuildId" "your-guild-id-here"  # Optional but recommended
```

#### 3. Optional Feature Configuration

**Admin UI Authentication** (Recommended)
```bash
dotnet user-secrets set "Discord:OAuth:ClientId" "your-client-id"
dotnet user-secrets set "Discord:OAuth:ClientSecret" "your-client-secret"
dotnet user-secrets set "Identity:DefaultAdmin:Email" "admin@example.com"
dotnet user-secrets set "Identity:DefaultAdmin:Password" "InitialPassword123!"
```

**AI Assistant** ([Setup guide](docs/articles/ai-assistant.md))
```bash
dotnet user-secrets set "Anthropic:ApiKey" "your-anthropic-api-key"
```
- Get API key from [Anthropic Console](https://console.anthropic.com)
- Requires user consent via `/consent grant type:assistant`

**Text-to-Speech** ([Setup guide](docs/articles/tts-support.md))
```bash
dotnet user-secrets set "AzureSpeech:SubscriptionKey" "your-azure-speech-key"
dotnet user-secrets set "AzureSpeech:Region" "eastus"
```
- Get subscription key from [Azure Portal](https://portal.azure.com)

**Audio Features (Soundboard)** ([Setup guide](docs/articles/audio-dependencies.md))
- Install FFmpeg and ensure it's in PATH
- On Windows: Ensure libsodium.dll and opus.dll are in build output directory
- On Linux: Install libsodium and libopus packages

**Observability (Optional)** ([Setup guide](docs/articles/elastic-stack-setup.md))
```bash
dotnet user-secrets set "Elastic:ApiKey" "your-elasticsearch-api-key"
dotnet user-secrets set "ElasticApm:ServerUrl" "http://localhost:8200"
```

#### 4. Verify Installation
```bash
# Build and run
dotnet run --project src/DiscordBot.Bot

# Verify:
# - Bot appears online in Discord
# - Admin UI accessible at https://localhost:5001
# - Test with /ping command in Discord
# - Check logs in logs/discordbot-YYYY-MM-DD.log
```

### Running the Bot

From the solution root:

```bash
dotnet run --project src/DiscordBot.Bot
```

The bot will:
- Connect to Discord and appear online
- Register slash commands
- Log output to console and `logs/discordbot-YYYY-MM-DD.log`
- Start Web API and admin UI on `https://localhost:5001` (or `http://localhost:5000`)
- Swagger docs available at `/swagger`

To stop the bot gracefully, press `Ctrl+C`.

## Architecture

The solution follows a three-layer clean architecture pattern:

| Layer | Project | Responsibility |
|-------|---------|----------------|
| **Domain** | `DiscordBot.Core` | Entities, interfaces, DTOs, enums, authorization roles |
| **Infrastructure** | `DiscordBot.Infrastructure` | EF Core (with Identity), repositories, logging configuration |
| **Application** | `DiscordBot.Bot` | Web API controllers, Razor Pages admin UI, bot hosted service, command modules |

### Key Components

- **BotHostedService**: Manages Discord bot lifecycle (startup, login, shutdown)
- **InteractionHandler**: Discovers and registers slash command modules automatically
- **DiscordSocketClient**: Singleton client for Discord gateway communication
- **Command Modules**: Slash command implementations inheriting from `InteractionModuleBase`
- **Razor Pages**: Admin UI with Tailwind CSS for user/guild management
- **Identity System**: ASP.NET Core Identity with Discord OAuth external login

For detailed architecture documentation, see [docs/articles/architecture-history.md](docs/articles/architecture-history.md).

## Commands

Commands are organized by feature. All commands use Discord's slash command system.

### 🔧 General & Utility
- `/ping` - Check bot latency and responsiveness
- `/userinfo [user]` - Display detailed user information
- `/serverinfo` - Display server statistics and information
- `/roleinfo <role>` - Display role information and permissions

See [utility-commands.md](docs/articles/utility-commands.md) for details.

### 🛡️ Moderation
**Prerequisites:** Moderation enabled for guild + Moderator role

**Actions:**
- `/warn <user> [reason]` - Issue a formal warning
- `/kick <user> [reason]` - Kick a user from the server (requires Kick Members permission)
- `/ban <user> [reason]` - Ban a user from the server (requires Ban Members permission)
- `/mute <user> <duration> [reason]` - Temporarily mute a user
- `/purge <count>` - Delete multiple messages from channel

**History & Management:**
- `/mod-history <user>` - View a user's moderation history
- `/mod-stats [user]` - View moderation statistics for guild or specific moderator
- `/mod-notes add/list/delete` - Manage moderator notes on users
- `/mod-tag add/remove/list` - Manage user tags for tracking
- `/watchlist add/remove/list` - Manage user watchlist for enhanced monitoring
- `/investigate <user>` - Comprehensive user investigation report

### 🐀 Rat Watch
**Prerequisites:** Rat Watch enabled for guild

Accountability system for tracking commitments with community voting.

- **Rat Watch** (Context Menu) - Right-click any message to create a Rat Watch on the author
- `/rat-clear` - Clear yourself from all active Rat Watches in the server
- `/rat-stats [user]` - View a user's rat record (guilty verdicts, recent incidents)
- `/rat-leaderboard` - View the top rats in the server
- `/rat-settings [timezone]` - Configure Rat Watch settings (Admin only)

See [rat-watch.md](docs/articles/rat-watch.md) for voting workflow and analytics.

### 🤖 AI Assistant
**Prerequisites:** User consent via `/consent grant type:assistant`

The AI assistant responds to:
- Direct mentions of the bot (@BotName)
- Contextual conversation in configured channels
- Requires Anthropic API key configuration

Features tool usage, conversation history, and cost tracking. See [ai-assistant.md](docs/articles/ai-assistant.md).

### ⏰ Scheduling & Reminders

**Scheduled Messages** (Admin only)
- `/schedule-list` - List scheduled messages for the server
- `/schedule-create` - Create a new scheduled message (one-time, recurring, or cron)
- `/schedule-delete` - Delete a scheduled message
- `/schedule-toggle` - Enable/disable a scheduled message
- `/schedule-run` - Manually trigger a scheduled message

See [scheduled-messages.md](docs/articles/scheduled-messages.md).

**Personal Reminders** (All users)
- `/remind set <time> <message>` - Set a personal reminder (e.g., "10m", "tomorrow 3pm")
- `/remind list` - View your pending reminders
- `/remind cancel <id>` - Cancel a pending reminder

Reminders use natural language time parsing and deliver via DM. See [reminder-system.md](docs/articles/reminder-system.md).

### 🎵 Audio & Voice

**Voice Channel Control:**
- `/join` - Join your current voice channel
- `/join-channel <channel>` - Join a specific voice channel
- `/leave` - Leave the voice channel

**Soundboard:**
- `/play <sound>` - Play a sound in voice channel (autocomplete available)
- `/sounds` - List available sounds for the guild
- `/stop` - Stop current audio playback

See [soundboard.md](docs/articles/soundboard.md). Requires FFmpeg, libsodium, and libopus.

**Text-to-Speech:**
- `/tts <message> [voice]` - Speak message in voice channel using Azure TTS

See [tts-support.md](docs/articles/tts-support.md). Requires Azure Speech subscription.

### 👋 Welcome System
**Prerequisites:** Admin permissions

- `/welcome show` - Display current welcome configuration
- `/welcome enable` - Enable welcome messages
- `/welcome disable` - Disable welcome messages
- `/welcome channel <channel>` - Set welcome message channel
- `/welcome message <message>` - Set custom welcome message text
- `/welcome test [user]` - Test welcome message delivery

See [welcome-system.md](docs/articles/welcome-system.md).

### 🔐 Consent & Privacy

- `/consent grant/revoke/status` - Manage data collection consent preferences
- `/privacy preview-delete/delete-data` - View privacy information and request data deletion

Users control consent for: message logging, assistant interactions, and analytics. See [consent-privacy.md](docs/articles/consent-privacy.md).

### 👤 Account Management

- `/verify-account` - Link Discord account to web admin account

Generates a verification code to enter in admin UI. See [bot-verification.md](docs/articles/bot-verification.md).

### 🔧 Admin Commands
**Prerequisites:** Admin or SuperAdmin role

- `/status` - Display bot status and statistics
- `/guilds` - List connected guilds
- `/shutdown` - Gracefully shut down the bot (owner only)

## Logging

The bot uses Serilog for structured logging with multiple outputs:

1. **Console Sink**: Real-time output during development
   - Format: `[HH:mm:ss LEVEL] SourceContext: Message`
   - Log levels: Information and above (configurable)

2. **File Sink**: Rolling file logs for persistence
   - Location: `logs/discordbot-YYYY-MM-DD.log`
   - Retention: 7 days (configurable)
   - Format: ISO 8601 timestamp with full context

3. **Elasticsearch Sink**: Centralized log aggregation (recommended for production)
   - Structured JSON logs with rich context
   - Searchable and filterable via Kibana
   - Correlation with Elastic APM traces
   - See [log-aggregation.md](docs/articles/log-aggregation.md) for setup

4. **Seq Sink** (optional): Alternative log aggregation for development
   - Simple setup for local development
   - Real-time log streaming and querying

### Distributed Tracing

**Elastic APM** provides distributed tracing and performance monitoring:
- Automatic instrumentation for HTTP requests, database queries, Discord API calls
- Trace correlation with structured logs
- Performance metrics and error tracking
- See [log-aggregation.md](docs/articles/log-aggregation.md) for APM setup

### Log Levels

Configure in `appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Discord": "Information"
      }
    }
  }
}
```

## Development

### Adding New Commands

1. Create a new module class in `src/DiscordBot.Bot/Commands/`:

```csharp
using Discord.Interactions;

namespace DiscordBot.Bot.Commands;

public class MyModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<MyModule> _logger;

    public MyModule(ILogger<MyModule> logger)
    {
        _logger = logger;
    }

    [SlashCommand("mycommand", "Description of my command")]
    public async Task MyCommandAsync()
    {
        _logger.LogInformation("MyCommand executed by {User}", Context.User.Username);
        await RespondAsync("Hello from my command!");
    }
}
```

2. The `InteractionHandler` will automatically discover and register the command on bot startup.

3. Restart the bot to apply changes.

### Building and Testing

```bash
# Build entire solution
dotnet build

# Run tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"
```

### Adding Migrations

```bash
# Add migration
dotnet ef migrations add MigrationName --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot

# Apply migrations
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

## Project Structure

```
discordbot/
├── src/
│   ├── DiscordBot.Core/           # Domain layer (entities, interfaces, DTOs)
│   ├── DiscordBot.Infrastructure/ # EF Core DbContext, repositories, logging
│   └── DiscordBot.Bot/            # Application layer (API + bot + admin UI)
│       ├── Authorization/         # Custom authorization handlers
│       ├── Commands/              # Slash command modules
│       ├── Components/            # Interactive component utilities
│       ├── Controllers/           # REST API controllers
│       ├── Extensions/            # DI and service extensions
│       ├── Handlers/              # Interaction handlers
│       ├── Pages/                 # Razor Pages admin UI
│       │   ├── Account/           # Login, logout, OAuth pages
│       │   ├── Admin/Users/       # User management CRUD
│       │   └── Shared/            # Layouts, partials, components
│       ├── Preconditions/         # Permission check attributes
│       ├── Services/              # Bot hosted service, state management
│       ├── ViewModels/            # Page and component view models
│       └── Program.cs             # DI composition root
├── tests/
│   └── DiscordBot.Tests/          # Unit and integration tests
├── docs/                          # DocFX documentation
└── logs/                          # Log files (created at runtime)
```

## Configuration Reference

### Essential Configuration

**Discord Bot Token** (Required)
```bash
cd src/DiscordBot.Bot
dotnet user-secrets set "Discord:Token" "your-bot-token-here"
```

**Test Guild ID** (Recommended for Development)
```bash
dotnet user-secrets set "Discord:TestGuildId" "your-guild-id-here"
```
Commands register instantly to test guild. Without this, global registration takes up to 1 hour.

### Feature-Specific Configuration

All configuration uses the `IOptions<T>` pattern with 31+ option classes in `src/DiscordBot.Core/Configuration/`. See [CLAUDE.md](CLAUDE.md) for the complete configuration options reference.

#### Admin UI & Authentication
```bash
dotnet user-secrets set "Discord:OAuth:ClientId" "your-client-id"
dotnet user-secrets set "Discord:OAuth:ClientSecret" "your-client-secret"
dotnet user-secrets set "Identity:DefaultAdmin:Email" "admin@example.com"
dotnet user-secrets set "Identity:DefaultAdmin:Password" "InitialPassword123!"
```

**Discord OAuth Setup:**
1. Go to [Discord Developer Portal](https://discord.com/developers/applications)
2. Select your bot application → OAuth2 section
3. Add redirect URIs:
   - Development: `https://localhost:5001/signin-discord`
   - Production: `https://yourdomain.com/signin-discord`
4. Copy Client ID and Client Secret

See [identity-configuration.md](docs/articles/identity-configuration.md) for detailed authentication setup.

#### AI Assistant (Claude API)
```bash
dotnet user-secrets set "Anthropic:ApiKey" "your-anthropic-api-key"
dotnet user-secrets set "Anthropic:DefaultModel" "claude-sonnet-4-20250514"  # Optional
```

**Configuration:** `appsettings.json` → `Assistant` section
- Rate limiting, token limits, cost tracking
- Requires user consent: `/consent grant type:assistant`
- See [ai-assistant.md](docs/articles/ai-assistant.md)

#### Text-to-Speech (Azure Cognitive Services)
```bash
dotnet user-secrets set "AzureSpeech:SubscriptionKey" "your-azure-speech-key"
dotnet user-secrets set "AzureSpeech:Region" "eastus"  # Optional, defaults to eastus
```

**Configuration:** `appsettings.json` → `AzureSpeech` section (lines 125-132)
- Default voice, speed, pitch, volume settings
- Max text length: 500 characters
- See [tts-support.md](docs/articles/tts-support.md)

#### Audio Features (Soundboard)
**Configuration:** `appsettings.json` → `Soundboard` section (lines 193-202)
- FFmpeg path (auto-detected if in PATH)
- Max duration, file size, sounds per guild
- Supported formats: mp3, wav, ogg

**Dependencies:** See [audio-dependencies.md](docs/articles/audio-dependencies.md)
- FFmpeg (required for audio processing)
- libsodium (required for voice encryption)
- libopus (required for voice encoding)

#### Observability & Monitoring

**Elasticsearch & Elastic APM** (Production)
```bash
dotnet user-secrets set "Elastic:ApiKey" "your-elasticsearch-api-key"
dotnet user-secrets set "ElasticApm:ServerUrl" "http://localhost:8200"
dotnet user-secrets set "ElasticApm:SecretToken" "your-apm-secret-token"
```

**Configuration:**
- `appsettings.json` → `ElasticApm` section (lines 5-18)
- `appsettings.json` → `Observability` section (lines 25-28) for Kibana/Seq URLs
- See [elastic-stack-setup.md](docs/articles/elastic-stack-setup.md)
- See [log-aggregation.md](docs/articles/log-aggregation.md)

**OpenTelemetry**
- `appsettings.json` → `OpenTelemetry` section (lines 98-117)
- Priority-based sampling with configurable rates
- Prometheus metrics export support

### User Secrets ID
`7b84433c-c2a8-46db-a8bf-58786ea4f28e`

### appsettings.json Structure
The default configuration file includes 31+ option sections:
- Background service intervals and delays
- Retention policies (logs, analytics, notifications)
- Performance monitoring thresholds
- Cache durations and cleanup intervals
- Feature-specific settings (RatWatch, Reminders, Moderation, etc.)

See [CLAUDE.md](CLAUDE.md) for the complete configuration options reference table.

## Dependencies

### Core Packages

- **Discord.Net** (3.19.0-beta.1): Discord API client and gateway
- **AspNet.Security.OAuth.Discord** (8.0.0): Discord OAuth authentication
- **Microsoft.AspNetCore.Identity.EntityFrameworkCore** (8.0.0): ASP.NET Core Identity
- **Serilog.AspNetCore** (8.0.0): Structured logging framework
- **Elastic.Serilog.Sinks** (10.0.0): Elasticsearch log sink for Serilog
- **Elastic.Apm.NetCoreAll** (1.29.0): Elastic APM distributed tracing
- **Anthropic.SDK** (5.8.0): Claude API client for AI assistant
- **Microsoft.CognitiveServices.Speech** (1.41.0): Azure Text-to-Speech
- **OpenTelemetry** (1.14.0): Metrics and tracing instrumentation
- **Cronos** (0.11.1): Cron expression parsing for scheduled messages
- **Swashbuckle.AspNetCore** (6.5.0): Swagger/OpenAPI documentation

### Audio Libraries
- **libsodium** (1.0.20.1): Voice encryption
- **OpusDotNet** (1.3.1): Voice codec
- **FFmpeg** (external): Audio processing (must be installed separately)

### Development

- **xUnit**: Test framework
- **FluentAssertions**: Assertion library
- **Moq**: Mocking framework
- **Tailwind CSS**: Utility-first CSS framework (built via npm)

## Production Deployment

### Environment Variables

All `appsettings.json` values can be overridden with environment variables using the `__` (double underscore) separator:

```bash
# Discord configuration
Discord__Token="your-bot-token"
Discord__TestGuildId="123456789"

# Database
ConnectionStrings__DefaultConnection="Server=...;Database=...;User Id=...;Password=..."

# OAuth
Discord__OAuth__ClientId="your-client-id"
Discord__OAuth__ClientSecret="your-client-secret"

# AI Assistant
Anthropic__ApiKey="your-anthropic-api-key"

# Azure TTS
AzureSpeech__SubscriptionKey="your-azure-key"
AzureSpeech__Region="eastus"

# Elastic APM
ElasticApm__ServerUrl="http://your-apm-server:8200"
ElasticApm__SecretToken="your-apm-token"
```

### Database Migration

**Production migration:**
```bash
dotnet ef database update \
  --project src/DiscordBot.Infrastructure \
  --startup-project src/DiscordBot.Bot \
  --connection "your-production-connection-string"
```

Supported databases:
- SQLite (default for development)
- SQL Server
- MySQL
- PostgreSQL

### Linux Systemd Service

See [linux-deployment.md](docs/articles/linux-deployment.md) for:
- Systemd unit file configuration
- Service management commands
- Log aggregation setup
- Auto-restart configuration

### Health & Monitoring Endpoints

- **Health Check:** `https://your-domain/health`
- **Metrics (Prometheus):** `https://your-domain/metrics`
- **Swagger API:** `https://your-domain/swagger` (disable in production)

### Performance Considerations

- Enable Elastic APM for distributed tracing
- Configure OpenTelemetry sampling rates in production (default: 10%)
- Set up log aggregation (Elasticsearch or Seq)
- Monitor performance dashboard at `/Admin/Performance`
- Configure alert thresholds in `PerformanceAlerts` section

## Security

- Never commit bot tokens to version control
- Use .NET User Secrets for local development
- Use environment variables or secret management services in production
- The `.gitignore` includes `appsettings.Development.json` and log files

## Troubleshooting

### Bot doesn't appear online

1. Check that the bot token is configured correctly
2. Verify the token is valid in Discord Developer Portal
3. Check logs for connection errors in `logs/discordbot-YYYY-MM-DD.log`
4. Ensure bot has proper gateway intents enabled (Message Content, Server Members)

### Commands not appearing

1. Check if `TestGuildId` is set in configuration
2. Without test guild, global commands take up to 1 hour to register
3. Check logs for command registration errors
4. Verify bot has `applications.commands` scope
5. Try kicking and re-inviting the bot to refresh permissions

### Permission errors

1. Ensure bot has necessary permissions in Discord server
2. Check role hierarchy (bot role must be above roles it manages)
3. Review bot permissions in Discord Developer Portal
4. Verify OAuth2 scopes include `bot` and `applications.commands`

### Audio features not working

1. **FFmpeg not found:**
   - Ensure FFmpeg is installed and in PATH
   - Or set `Soundboard:FfmpegPath` in configuration
   - Test: `ffmpeg -version` in terminal

2. **Voice connection fails:**
   - On Windows: Ensure `libsodium.dll` and `opus.dll` are in build output directory
   - On Linux: Install libsodium and libopus packages (`apt install libsodium23 libopus0`)
   - Check logs for "Could not find libsodium" errors

See [audio-dependencies.md](docs/articles/audio-dependencies.md) for platform-specific setup.

### AI Assistant not responding

1. Verify Anthropic API key is configured:
   ```bash
   dotnet user-secrets list
   ```
2. Check user has granted consent: `/consent status`
3. Ensure user has granted consent: `/consent grant type:assistant`
4. Check logs for API errors or rate limiting
5. Verify API key has sufficient credits in [Anthropic Console](https://console.anthropic.com)

### Text-to-Speech fails

1. Verify Azure Speech subscription key is configured
2. Check region matches your Azure resource (default: eastus)
3. Ensure bot is connected to voice channel first (`/join`)
4. Check logs for Azure API errors
5. Verify subscription has sufficient quota

### OAuth login fails

1. Verify redirect URIs in Discord Developer Portal match your environment
2. Check ClientId and ClientSecret are configured correctly
3. Review logs for OAuth errors
4. Ensure cookies are enabled in browser
5. Try clearing browser cookies and cache

For more troubleshooting guidance, see [troubleshooting-guide.md](docs/articles/troubleshooting-guide.md).

## Contributing

1. Create a feature branch from `main`
2. Make your changes
3. Add tests for new functionality
4. Submit a pull request

## License

This project is for educational and development purposes.

## Related Documentation

### Architecture & Configuration
- [Architecture History](docs/articles/architecture-history.md) - Original implementation plan
- [Requirements](docs/articles/requirements.md) - Technology stack and specifications
- [Design System](docs/articles/design-system.md) - UI design tokens and components
- [Identity Configuration](docs/articles/identity-configuration.md) - Authentication setup
- [Authorization Policies](docs/articles/authorization-policies.md) - Role hierarchy and access control
- [Versioning Strategy](docs/articles/versioning-strategy.md) - SemVer versioning, CI/CD, release process

### Observability & Monitoring
- [Log Aggregation](docs/articles/log-aggregation.md) - Centralized logging with Elasticsearch and Elastic APM
- [Elastic Stack Setup](docs/articles/elastic-stack-setup.md) - Local development environment setup
- [Kibana Dashboards](docs/articles/kibana-dashboards.md) - Log analysis and dashboard usage guide
- [Elastic APM](docs/articles/elastic-apm.md) - Distributed tracing and performance monitoring

### API & Integration
- [API Endpoints](docs/articles/api-endpoints.md) - REST API documentation
- [SignalR Real-time Updates](docs/articles/signalr-realtime.md) - Live dashboard updates
- [Interactive Components](docs/articles/interactive-components.md) - Button/component patterns

### Features
- [Rat Watch](docs/articles/rat-watch.md) - Accountability system with voting and leaderboards
- [Reminder System](docs/articles/reminder-system.md) - Personal reminders with natural language time parsing
- [Utility Commands](docs/articles/utility-commands.md) - User/server/role information commands
- [Scheduled Messages](docs/articles/scheduled-messages.md) - Automated message scheduling
- [Member Directory](docs/articles/member-directory.md) - Guild member management
- [Soundboard](docs/articles/soundboard.md) - Audio playback in voice channels
- [Audio Dependencies](docs/articles/audio-dependencies.md) - FFmpeg, libsodium, libopus setup
- [Consent & Privacy](docs/articles/consent-privacy.md) - User consent and data privacy management
- [Bot Performance Dashboard](docs/articles/bot-performance-dashboard.md) - Performance monitoring
- [User Management](docs/articles/user-management.md) - Admin UI user management
- [Bot Verification](docs/articles/bot-verification.md) - Discord account linking flow

### Development
- [CLAUDE.md](CLAUDE.md) - Guidance for Claude Code AI assistant
- [Issue Tracking Process](docs/articles/issue-tracking-process.md) - GitHub workflow and labels
- [Testing Guide](docs/articles/testing-guide.md) - Unit and integration testing
- [Form Implementation Standards](docs/articles/form-implementation-standards.md) - Razor Pages form patterns
