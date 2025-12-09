# Discord Bot Management System

A Discord bot built with .NET 8 and Discord.NET that provides a foundation for managing Discord servers through slash commands and a REST API. The system combines a hosted Discord bot service with a Web API for external management and monitoring.

## Features

- Slash command framework with automatic discovery and registration
- Structured logging with Serilog (console and rotating file outputs)
- Clean architecture with separation of concerns
- Graceful bot lifecycle management
- Health check command for monitoring bot status

## Quick Start

### Prerequisites

- .NET 8 SDK
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

### Running the Bot

From the solution root:

```bash
dotnet run --project src/DiscordBot.Bot
```

The bot will:
- Connect to Discord and appear online
- Register slash commands
- Log output to console and `logs/discordbot-YYYY-MM-DD.log`
- Start a Web API on `http://localhost:5000` (default)

To stop the bot gracefully, press `Ctrl+C`.

## Architecture

The solution follows a three-layer clean architecture pattern:

| Layer | Project | Responsibility |
|-------|---------|----------------|
| **Domain** | `DiscordBot.Core` | Entities, interfaces, DTOs, enums |
| **Infrastructure** | `DiscordBot.Infrastructure` | EF Core, repositories, logging configuration |
| **Application** | `DiscordBot.Bot` | Web API controllers, bot hosted service, command modules |

### Key Components

- **BotHostedService**: Manages Discord bot lifecycle (startup, login, shutdown)
- **InteractionHandler**: Discovers and registers slash command modules automatically
- **DiscordSocketClient**: Singleton client for Discord gateway communication
- **Command Modules**: Slash command implementations inheriting from `InteractionModuleBase`

For detailed architecture documentation, see [docs/mvp-plan.md](docs/mvp-plan.md).

## Commands

### /ping

Check the bot's latency and responsiveness.

**Usage:**
```
/ping
```

**Response:**
- Displays bot latency in milliseconds
- Shows timestamp of response
- Logs execution details to structured logs

## Logging

The bot uses Serilog for structured logging with two outputs:

1. **Console Sink**: Real-time output during development
   - Format: `[HH:mm:ss LEVEL] SourceContext: Message`
   - Log levels: Information and above (configurable)

2. **File Sink**: Rolling file logs for persistence
   - Location: `logs/discordbot-YYYY-MM-DD.log`
   - Retention: 7 days (configurable)
   - Format: ISO 8601 timestamp with full context

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

### Adding Migrations (Future)

When the data layer is implemented:

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
│   ├── DiscordBot.Core/          # Domain layer (entities, interfaces)
│   ├── DiscordBot.Infrastructure/ # Data access and logging
│   └── DiscordBot.Bot/            # Application layer (API + bot)
│       ├── Commands/              # Slash command modules
│       ├── Handlers/              # Command and interaction handlers
│       ├── Services/              # Bot hosted service
│       └── Program.cs             # DI composition root
├── tests/
│   └── DiscordBot.Tests/          # Unit and integration tests
├── docs/                          # Documentation
└── logs/                          # Log files (created at runtime)
```

## Configuration Reference

### appsettings.json

```json
{
  "Discord": {
    "Token": "",           // Set via user secrets
    "TestGuildId": null    // Optional: Guild ID for instant command registration
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    }
  }
}
```

### User Secrets

User Secrets ID: `7b84433c-c2a8-46db-a8bf-58786ea4f28e`

Required secrets:
- `Discord:Token`: Your Discord bot token

Optional secrets:
- `Discord:TestGuildId`: Guild ID for development command testing

## Dependencies

### Core Packages

- **Discord.Net** (3.18.0): Discord API client and gateway
- **Serilog** (10.x): Structured logging framework
- **Microsoft.Extensions.Hosting**: Generic host for background services

### Development

- **xUnit**: Test framework
- **FluentAssertions**: Assertion library
- **Moq**: Mocking framework

## Security

- Never commit bot tokens to version control
- Use .NET User Secrets for local development
- Use environment variables or secret management services in production
- The `.gitignore` includes `appsettings.Development.json` and log files

## Troubleshooting

### Bot doesn't appear online

1. Check that the bot token is configured correctly
2. Verify the token is valid in Discord Developer Portal
3. Check logs for connection errors
4. Ensure bot has proper gateway intents enabled

### Commands not appearing

1. Check if `TestGuildId` is set in configuration
2. Without test guild, global commands take up to 1 hour to register
3. Check logs for command registration errors
4. Verify bot has `applications.commands` scope

### Permission errors

1. Ensure bot has necessary permissions in Discord server
2. Check role hierarchy (bot role must be high enough)
3. Review bot permissions in Discord Developer Portal

## Contributing

1. Create a feature branch from `main`
2. Make your changes
3. Add tests for new functionality
4. Submit a pull request

## License

This project is for educational and development purposes.

## Related Documentation

- [MVP Implementation Plan](docs/mvp-plan.md) - Detailed architecture and roadmap
- [Requirements](docs/requirements.md) - Technology stack and specifications
- [Design System](docs/design-system.md) - UI design tokens and components (future)
