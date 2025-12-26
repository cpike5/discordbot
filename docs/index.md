# Discord Bot Management System Documentation

Welcome to the Discord Bot Management System documentation. This site provides comprehensive documentation for developers working with or extending the bot.

## Quick Links

### Getting Started
- [Requirements](articles/requirements.md) - Technology stack and architecture
- [ROADMAP](../ROADMAP.md) - Current development roadmap

### API Reference
- API Documentation (see [API Reference](/api/index.html) after building docs)
- [REST Endpoints](articles/api-endpoints.md) - REST API documentation

### Developer Guides
- [Interactive Components](articles/interactive-components.md) - Button and component patterns
- [Database Schema](articles/database-schema.md) - Entity definitions
- [Repository Pattern](articles/repository-pattern.md) - Data access patterns
- [Permissions](articles/permissions.md) - Permission system

### Bot Commands
- [Admin Commands](articles/admin-commands.md) - Slash command reference

### Design
- [Design System](articles/design-system.md) - UI design tokens and components

## Project Structure

| Project | Description |
|---------|-------------|
| `DiscordBot.Core` | Domain entities, interfaces, DTOs, enums |
| `DiscordBot.Infrastructure` | EF Core DbContext, repositories, Serilog config |
| `DiscordBot.Bot` | Web API controllers, bot hosted service, command modules |

## Building the Documentation

```bash
# Install DocFX (first time only)
dotnet tool restore

# Build documentation
dotnet docfx docfx.json

# Build and serve locally
dotnet docfx docfx.json --serve
```
