# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Discord bot management system built with .NET 8 and Discord.NET. Combines a Discord bot hosted service with a Web API and Razor Pages admin UI for management.

**Current version:** v0.3.7-dev (pre-release). Version is centralized in `Directory.Build.props` at solution root. See [versioning-strategy.md](docs/articles/versioning-strategy.md) for release process.

## Prerequisites

- .NET 8 SDK
- Node.js (for Tailwind CSS build - runs automatically on `dotnet build`)

## Build & Run Commands

```bash
# Build entire solution
dotnet build

# Run the bot (from solution root)
dotnet run --project src/DiscordBot.Bot

# Run tests
dotnet test

# Run single test
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Add EF Core migration
dotnet ef migrations add MigrationName --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot

# Apply migrations
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot

# Rebuild Tailwind CSS manually (auto-runs on dotnet build)
cd src/DiscordBot.Bot && npm run build:css

# Build documentation
dotnet tool restore                    # First time: restore DocFX tool
.\build-docs.ps1                       # Windows: build docs
.\build-docs.ps1 -Serve                # Windows: build and serve locally at http://localhost:8080
./build-docs.sh                        # Linux/macOS: build docs
./build-docs.sh --serve                # Linux/macOS: build and serve locally
```

## Configuration

Discord bot token must be configured via User Secrets (never commit tokens):

```bash
cd src/DiscordBot.Bot
dotnet user-secrets set "Discord:Token" "your-bot-token-here"
dotnet user-secrets set "Discord:TestGuildId" "your-guild-id"  # Optional: instant command registration
```

UserSecretsId: `7b84433c-c2a8-46db-a8bf-58786ea4f28e`

### Authentication Configuration

Discord OAuth must be configured via User Secrets for admin UI authentication:

```bash
cd src/DiscordBot.Bot
dotnet user-secrets set "Discord:OAuth:ClientId" "your-client-id"
dotnet user-secrets set "Discord:OAuth:ClientSecret" "your-client-secret"

# Optional: Default admin user (change password immediately after first login)
dotnet user-secrets set "Identity:DefaultAdmin:Email" "admin@example.com"
dotnet user-secrets set "Identity:DefaultAdmin:Password" "InitialPassword123!"
```

#### Discord Developer Portal Setup

1. Go to https://discord.com/developers/applications
2. Select your bot application (or create new)
3. Go to OAuth2 section
4. Add redirect URIs:
   - Development: `https://localhost:5001/signin-discord`
   - Production: `https://yourdomain.com/signin-discord`
5. Copy Client ID and Client Secret to user secrets

See [Identity Configuration](docs/articles/identity-configuration.md) for detailed authentication setup and troubleshooting.

## Configuration Options

The application uses the `IOptions<T>` pattern for strongly-typed configuration. Options classes are located in `src/DiscordBot.Core/Configuration/`:

| Options Class | appsettings Section | Purpose |
|--------------|---------------------|---------|
| `ApplicationOptions` | `Application` | App metadata (title, base URL, version) |
| `DiscordOAuthOptions` | `Discord:OAuth` | OAuth client credentials (use user secrets) |
| `CachingOptions` | `Caching` | Cache duration settings for various services |
| `VerificationOptions` | `Verification` | Verification code generation settings |
| `BackgroundServicesOptions` | `BackgroundServices` | Background task intervals and delays |
| `IdentityConfigOptions` | `Identity` | ASP.NET Identity settings (use user secrets for DefaultAdmin) |
| `MessageLogRetentionOptions` | `MessageLogRetention` | Message log cleanup settings |

### Default Values

All options have sensible defaults. You only need to configure values that differ from defaults. See `appsettings.json` for the default configuration structure.

## Architecture

Three-layer clean architecture:

| Layer | Project | Purpose |
|-------|---------|---------|
| Domain | `DiscordBot.Core` | Entities, interfaces, DTOs, enums, authorization roles |
| Infrastructure | `DiscordBot.Infrastructure` | EF Core DbContext (with Identity), repositories, Serilog config |
| Application | `DiscordBot.Bot` | Web API controllers, Razor Pages admin UI, bot hosted service, command modules, DI composition |

**Key patterns:**
- `DiscordSocketClient` registered as singleton, managed by `BotHostedService`
- Repository pattern with interfaces in Core, implementations in Infrastructure
- Serilog as logging provider, inject `ILogger<T>` via DI
- ASP.NET Core Identity for authentication with Discord OAuth support
- SQLite for dev/test, MSSQL/MySQL/PostgreSQL for production
- Tailwind CSS for admin UI styling (auto-builds via npm on `dotnet build`)

## Key Documentation

Reference these docs for detailed specifications (build and serve locally with `.\build-docs.ps1 -Serve`):

| Doc | Purpose |
|-----|---------|
| [design-system.md](docs/articles/design-system.md) | UI tokens, color palette, component specs |
| [commands-page.md](docs/articles/commands-page.md) | Commands page feature and metadata service |
| [interactive-components.md](docs/articles/interactive-components.md) | Discord button/component patterns |
| [identity-configuration.md](docs/articles/identity-configuration.md) | Authentication setup, troubleshooting |
| [authorization-policies.md](docs/articles/authorization-policies.md) | Role hierarchy, guild access |
| [api-endpoints.md](docs/articles/api-endpoints.md) | REST API documentation |
| [log-aggregation.md](docs/articles/log-aggregation.md) | Seq centralized logging setup |
| [versioning-strategy.md](docs/articles/versioning-strategy.md) | SemVer versioning, CI/CD, release process |
| [issue-tracking-process.md](docs/articles/issue-tracking-process.md) | Issue hierarchy, labels, GitHub workflow |


## HTML Prototypes

All HTML prototypes are located in `docs/prototypes/`. Open them directly in a browser to preview UI components.

| Folder | Purpose |
|--------|---------|
| `docs/prototypes/` | Component showcases, feedback patterns, dashboard layouts |
| `docs/prototypes/components/` | Data display components (cards, tables, lists, badges) |
| `docs/prototypes/forms/` | Form components and validation patterns |
| `docs/prototypes/pages/` | Full page prototypes (servers, settings, commands) |
| `docs/prototypes/features/` | Issue-specific feature prototypes organized by version/feature |
| `docs/prototypes/css/` | Shared CSS infrastructure and Tailwind config |

**When creating new prototypes:**
- Place new prototypes in `docs/prototypes/features/` organized by issue or feature
- Use shared CSS from `docs/prototypes/css/`
- Follow existing patterns in `docs/prototypes/component-showcase.html`

## Discord.NET Specifics

- Using Discord.NET 3.18.0
- Slash commands only (no prefix commands)
- `InteractionHandler` discovers and registers command modules from assembly
- Command modules inherit from `InteractionModuleBase<SocketInteractionContext>`
- Precondition attributes for permission checks: `RequireAdminAttribute`, `RequireOwnerAttribute`, `RateLimitAttribute`

**Interactive Components Pattern:**
- Use `ComponentIdBuilder` to create custom IDs: `{handler}:{action}:{userId}:{correlationId}:{data}`
- Store component state via `IInteractionStateService` (15-min default expiry)
- Component handlers go in separate `*ComponentModule` classes using `[ComponentInteraction]` attribute

**Adding a New Command:**
1. Create module in `Commands/` inheriting from `InteractionModuleBase<SocketInteractionContext>`
2. Use `[SlashCommand("name", "description")]` attribute on methods
3. Inject dependencies via constructor (logger, services, etc.)
4. If using buttons/components, create separate component handler module

## Admin UI (Razor Pages)

Located in `src/DiscordBot.Bot/Pages/`:
- Dashboard (`Index.cshtml`) - Bot status, guild stats, command stats cards
- Commands (`Pages/Commands/Index.cshtml`) - View registered slash commands, modules, parameters, preconditions
- Account pages - Login, logout, Discord OAuth flow, account linking
- Admin/Users - Full CRUD for user management (SuperAdmin only)

**Shared Components** in `Pages/Shared/Components/`:
- Reusable partial views (`_Alert`, `_Badge`, `_Button`, `_Card`, etc.)
- ViewModels in `ViewModels/Components/` define component parameters

**Authorization:**
- Role hierarchy: SuperAdmin > Admin > Moderator > Viewer
- Guild-specific access via `GuildAccessRequirement` and `GuildAccessHandler`
- Discord claims transformation adds guild membership info

**Adding a New Razor Page:**
1. Create page in `Pages/` with `.cshtml` and `.cshtml.cs` files
2. Use `[Authorize(Policy = "RequireAdmin")]` or appropriate policy
3. Inject services via constructor in PageModel
4. Use shared components via `@Html.Partial("Components/_ComponentName", viewModel)`

### UI Page Routes

| Page | URL Pattern | Description |
|------|-------------|-------------|
| Dashboard | `/` | Main dashboard with bot status, stats |
| Commands | `/Commands` | Registered slash commands list |
| Command Logs | `/CommandLogs` | Command execution history |
| Command Log Details | `/CommandLogs/{id:guid}` | Single command log entry |
| Command Analytics | `/CommandLogs/Analytics` | Usage analytics and charts |
| Guilds | `/Guilds` | Connected Discord servers list |
| Guild Details | `/Guilds/Details?id={id}` | Single guild overview |
| Guild Edit | `/Guilds/Edit/{id:long}` | Edit guild settings |
| Guild Welcome | `/Guilds/Welcome/{id:long}` | Welcome message config |
| Scheduled Messages | `/Guilds/ScheduledMessages/{guildId:long}` | Guild scheduled messages |
| Scheduled Message Create | `/Guilds/ScheduledMessages/Create/{guildId:long}` | New scheduled message |
| Scheduled Message Edit | `/Guilds/ScheduledMessages/Edit/{guildId:long}/{id:guid}` | Edit scheduled message |
| Users | `/Admin/Users` | User management (SuperAdmin) |
| User Details | `/Admin/Users/Details?id={id}` | User profile and roles |
| User Create | `/Admin/Users/Create` | Create new user |
| User Edit | `/Admin/Users/Edit?id={id}` | Edit user |
| Audit Logs | `/Admin/AuditLogs` | System audit trail |
| Audit Log Details | `/Admin/AuditLogs/Details/{id:long}` | Single audit entry |
| Message Logs | `/Admin/MessageLogs` | Discord message history |
| Message Log Details | `/Admin/MessageLogs/Details/{id:long}` | Single message entry |
| Bot Control | `/Admin/BotControl` | Start/stop/restart bot |
| Settings | `/Admin/Settings` | Application settings |
| Login | `/Account/Login` | User authentication |
| Logout | `/Account/Logout` | Sign out |
| Link Discord | `/Account/LinkDiscord` | OAuth account linking |
| Search | `/Search` | Global search |
| Components | `/Components` | Component showcase (dev) |

**Note:** Use `Guilds/` not `Servers/` for guild-related pages. Discord API terminology uses "guild" for servers.

## Development Endpoints

When running locally (`dotnet run --project src/DiscordBot.Bot`):
- Admin UI: `https://localhost:5001`
- Swagger API docs: `https://localhost:5001/swagger`
- Seq UI: `http://localhost:5341` (when running Seq locally)
- Logs: `logs/discordbot-YYYY-MM-DD.log`

## Common Issues

- **Commands not appearing**: Without `TestGuildId` configured, global commands take up to 1 hour to propagate
- **Bot doesn't connect**: Check bot token in user secrets and that gateway intents are enabled in Discord Developer Portal
- **OAuth fails**: Verify redirect URIs in Discord Developer Portal match your environment
