# API Reference

This section contains auto-generated API documentation from the source code XML comments.

## Namespaces

### DiscordBot.Core
Domain layer containing entities, interfaces, DTOs, and enums.

- **Entities**: `Guild`, `User`, `CommandLog`
- **Interfaces**: Repository interfaces (`IGuildRepository`, `IUserRepository`, etc.)
- **DTOs**: Data transfer objects for API communication

### DiscordBot.Infrastructure
Infrastructure layer with data access implementations.

- **Data**: `BotDbContext`, Entity configurations
- **Repositories**: Repository implementations

### DiscordBot.Bot
Application layer with API controllers and bot services.

- **Controllers**: REST API endpoints
- **Commands**: Discord slash command modules
- **Services**: Business logic services
- **Handlers**: Interaction and component handlers
