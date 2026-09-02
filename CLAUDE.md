# CLAUDE.md

See [CLAUDE-REFERENCE.md](CLAUDE-REFERENCE.md) for comprehensive lookup tables.

## Quick Reference

```bash
# Entity Framework — SQLite (--context required)
dotnet ef migrations add MigrationName --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot --context SqliteBotDbContext -o Migrations/Sqlite
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot --context SqliteBotDbContext

# Entity Framework — PostgreSQL (--context required)
dotnet ef migrations add MigrationName --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot --context PostgresBotDbContext -o Migrations/Postgresql
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot --context PostgresBotDbContext

# Data Migration (SQLite → PostgreSQL or vice versa)
dotnet run --project src/DiscordBot.Bot -- migrate-data --source "Data Source=data/discordbot.db" --target "Host=localhost;Database=discordbot;Username=discordbot;Password=changeme"
```

**User Secrets ID:** `7b84433c-c2a8-46db-a8bf-58786ea4f28e`

## Critical Gotchas

### JavaScript and Discord Snowflake IDs

**CRITICAL**: Discord IDs (`ulong` in C#) are 64-bit integers exceeding JavaScript's `Number.MAX_SAFE_INTEGER`. **Always treat Discord IDs as strings in JavaScript**:

```razor
<!-- WRONG - loses precision -->
window.guildId = @Model.GuildId;

<!-- CORRECT - preserves all digits -->
window.guildId = '@Model.GuildId';
```

### Configuration

- **Never commit tokens** - use User Secrets for `Discord:Token`, `Discord:OAuth:ClientId`, `Discord:OAuth:ClientSecret`, `OpenRouter:ApiKey`, `AzureSpeech:SubscriptionKey`
- **LLM provider** - the assistant talks to [OpenRouter](https://openrouter.ai) (OpenAI-compatible chat completions) through the owned typed client in `Infrastructure/Services/LLM/OpenRouter/`. No vendor SDK: build LLM work on `ILlmClient` and the owned wire records. Model names are OpenRouter slugs (`anthropic/claude-sonnet-4`, `openai/gpt-4o`). Without `OpenRouter:ApiKey` the assistant services are not registered, so migrations still run.
- **Command propagation** - Without `Discord:TestGuildId`, global commands take up to 1 hour to appear
- **Discord terminology** - Use "guild" not "server" in URLs/code (Discord API convention)
- **Database provider** - Set `Database:Provider` to `Sqlite` or `PostgreSql` to explicitly select a provider; omit for auto-detection from the connection string (`Host=`/`Server=` → PostgreSQL, file-path `Data Source` → SQLite). Default is SQLite at `data/discordbot.db`.

### PostgreSQL

- **EF CLI requires `--context`** - Both `SqliteBotDbContext` and `PostgresBotDbContext` design-time factories exist; always pass `--context` to EF CLI commands (see Quick Reference above).
- **Separate migration sets** - SQLite migrations live in `Migrations/Sqlite/`, PostgreSQL in `Migrations/Postgresql/`.
- **Npgsql legacy timestamp** - `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` is applied at startup. Do not remove this switch; removing it causes `DateTime` write errors with `timestamp with time zone` columns.

## Agent Definitions

Domain-expert agents live in `.claude/agents/`. **Maintenance rule:** When completing feature work that adds new services, entities, repositories, or significantly changes patterns within a stream, update the relevant agent definition as part of the same work.
