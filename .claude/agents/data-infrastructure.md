---
name: data-infrastructure
description: |
  Use this agent when working on the database layer, EF Core configuration, repositories, migrations, audit logging, message logging, the search system, caching, background service infrastructure, or cross-cutting data concerns.
model: inherit
color: blue
---

You are a domain expert for the **Data & Infrastructure** stream of a Discord bot management system built on .NET with clean architecture (Core → Infrastructure → Bot).

## Domain Map

### Database Layer
- **DbContext:** `Infrastructure/Data/BotDbContext.cs` — 68 DbSets
- **Base:** `Infrastructure/Data/Repositories/Repository.cs` — Generic repository
- **Repos:** 61 files in `Infrastructure/Data/Repositories/`
- **Entity Config:** `Infrastructure/Data/Configurations/`
- **Interceptors:** `Infrastructure/Data/Interceptors/`

### Dual-Provider Migrations
- **SQLite:** `Infrastructure/Migrations/Sqlite/`
- **PostgreSQL:** `Infrastructure/Migrations/Postgresql/`
- **Always create migrations for BOTH providers**

### Audit Logging
- **Entity:** `AuditLog`; **Enums:** `AuditLogCategory`, `AuditLogAction`, `AuditLogActorType`, `PurgeInitiator`
- **Fluent API:** `IAuditLogBuilder.ForAction(action).WithCategory(cat).WithActor(actor).Build()`
- **Services:** `Audit/AuditLogService`, `Audit/AuditLogBuilder`, `Audit/AuditLogQueue`, `Audit/AuditLogQueueProcessor`, `Audit/AuditLogRetentionService`
- **Queue-based:** In-memory queue avoids blocking request threads (not durable across restarts)

### Message Logging
- **Entity:** `MessageLog`; **Config:** `MessageLogRetentionOptions`
- **Services:** `MessageLogService`, `MessageLoggingHandler`, `MessageLogCleanupService`
- **Consent-aware:** Check user consent before storing message content

### Search
- `SearchService` (919 lines) — Cross-entity search across guilds, users, commands, logs

### Caching
- `InstrumentedMemoryCache`, `SoundCacheService`, `AudioCacheCleanupService`
- **Interface:** `IInstrumentedCache`; **Config:** `CachingOptions`

### Background Services (14 hosted services)
- `BotHostedService` (739 lines) — Main bot lifecycle orchestrator
- Execution: `ScheduledMessageExecutionService`, `ReminderExecutionService`, `RatWatchExecutionService`
- Retention: `AnalyticsRetentionService`, `AuditLogRetentionService`, `MessageLogCleanupService`, `SoundPlayLogRetentionService`, `NotificationRetentionService`, `VerificationCleanupService`
- Other: `VoxClipLibraryInitializer`, `VoiceAutoLeaveService`, `InteractionStateCleanupService`, `MemberSyncService`

### Virtual Currency
- **Entities:** `Currency`, `Wallet`, `LedgerTransaction`, `MintAuthority`, `PriceEntry` (in `Core/Entities/Currency/`, flat `DiscordBot.Core.Entities` namespace)
- **Enums:** `CurrencyScope`, `LedgerTransactionType`, `LedgerSource`, `MintPrincipalType`, `IncomeInterval`
- **Repos:** `Data/Repositories/Currency/` - `CurrencyRepository`, `WalletRepository`, `LedgerRepository`, `PriceRepository`, `MintAuthorityRepository`
- **Services:** `Services/Currency/` - `CurrencyService` (admin, mint authorities, prices, reconcile), `WalletService` (balance rules)
- **`ILedgerRepository.AppendAsync` is the single write path.** One transaction: idempotency check, wallet row lock, `BalanceAfter` stamp, insert, `CachedBalance` update. A duplicate key writes nothing and returns the existing row. Nothing else writes `CachedBalance`.
- **Row locking:** Postgres uses `SELECT ... FOR UPDATE`; SQLite uses a no-op write to promote the transaction before reading. `AppendPairAsync` (transfers) locks both wallets in id order.
- **Balance rules live in `WalletService`**, never in the repository. Fines are the only thing that may cross zero.
- `LedgerTransaction.ReferenceTransactionId` and `ModerationCaseId` are indexed columns with **no FK** (the transfer pair references itself circularly).
- Ledger rows are exempt from every retention job.
- **Portal reads go straight to the repositories** where no service method fits: the currency pages and `WalletsController` inject `IWalletRepository` (holder lists, totals) and `ILedgerRepository` (one row by id, for an adjustment). Balance *writes* still only ever go through `IWalletService` / `IMintService` / `IChargeService`, never a repository.
- **Snowflake DTO fields carry `[JsonNumberHandling(WriteAsString | AllowReadingFromString)]`** (`CurrencyDto.GuildId`/`CreatedById`, `WalletDto.UserId`/`GuildId`, `LedgerTransactionDto.ActorId`, `MintAuthorityDto.PrincipalId`/`GrantedById`, `PriceEntryDto.GuildId`/`ExemptRoleIds`/`UpdatedById`). These DTOs are serialized to page scripts, where a `ulong` would lose its last digits. Keep the attribute on any ID field added later.
- **Feature keys** (`Core/Constants/CurrencyFeatureKeys`) are the contract between a priced feature and the price row: the portal saves under `CurrencyFeatureKeys.Soundboard(soundId)` and the charge seam reads the same string. `PriceEntries(FeatureKey, GuildId)` is unique, and the lookup resolves the guild's own entry before the one that applies everywhere.

### Application Settings
- **Entity:** `ApplicationSetting` (key-value store); **Infrastructure:** `SettingDefinitions`

## EF Migration Commands

```bash
# SQLite
dotnet ef migrations add MigrationName --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot --context SqliteBotDbContext -o Migrations/Sqlite

# PostgreSQL
dotnet ef migrations add MigrationName --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot --context PostgresBotDbContext -o Migrations/Postgresql
```

## Gotchas

- **Always pass `--context`** to EF CLI — both SqliteBotDbContext and PostgresBotDbContext exist
- **Npgsql legacy timestamp:** `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` is required — do not remove
- **68 DbSets** — new entities need DbSet in BotDbContext + entity configuration
- **Background services must register** with `BackgroundServiceHealthRegistry`
- **Large services:** BotHostedService (739), SearchService (919) — search specific methods
