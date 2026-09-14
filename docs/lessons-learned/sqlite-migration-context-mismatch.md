# A fresh SQLite deployment silently got only half its schema for months

**Symptom.** Building the Phase 4 cluster 4b Playwright test for the new
`/Guilds/FeatureRequests/{guildId}` page, seeding a `FeatureRequests` row directly into the E2E
fixture's throwaway SQLite database (`BotHostFixture.DatabasePath`, the same pattern every other
`SeedX` helper in `tests/DiscordBot.E2E/BrowserTests.cs` already used) failed with
`SQLite Error 1: 'no such table: FeatureRequests'` - on a database the host itself had just created
and migrated at startup. `Reminders`/`ScheduledMessages`, seeded the identical way for the sibling
Playwright tests in the same PR, worked fine.

**Investigation.** `dotnet ef database update --context SqliteBotDbContext` against a fresh file
created every table correctly, including `FeatureRequests` - so the migrations themselves were
never the problem. Dumping `__EFMigrationsHistory` from the actual host-managed database (a
`try`/`catch` added temporarily to the failing seed helper) showed the applied set stopping dead
at `20260127225612_AddSsmlSupportToGuildTtsSettings` - dozens of migrations short of what a fresh
`dotnet ef database update` applies, and short of every migration this repo has added since late
January.

**Cause.** `Infrastructure/Extensions/ServiceCollectionExtensions.cs`'s `AddInfrastructure`
registered the **base** `BotDbContext` for the SQLite path (`AddBotDbContext<BotDbContext>(...)`),
not the concrete `SqliteBotDbContext` the Postgres branch two lines above already used the
equivalent pattern for (`AddBotDbContext<PostgresBotDbContext>` + a forwarding
`AddScoped<BotDbContext>(sp => sp.GetRequiredService<PostgresBotDbContext>())`). Every migration
since `20260219205009_AddIsEnabledToGuildModerationConfig` (the Sqlite/Postgres migration-set
split - see CLAUDE.md "Database and migrations") carries `[DbContext(typeof(SqliteBotDbContext))]`,
which EF Core's migrator only discovers for that exact context type. `Program.cs`'s startup
`db.Database.MigrateAsync()` resolves `BotDbContext` from DI; with the base type registered, it
silently found nothing pending past the last **base**-attributed migration and returned
successfully - no exception, no log line, `/health` green. Every SQLite deployment since the split
(the *default* provider - "Default database is SQLite... created on first run", CLAUDE.md) has
been missing `FeatureRequests`, `LlmModels`, `LlmUsageRecords`, the virtual-currency tables, and
everything else added in the last several months, unless its database happened to already have
them from being migrated once, separately, via the correct `dotnet ef ... --context
SqliteBotDbContext` tooling.

**Fix.** `AddInfrastructure`'s SQLite branch now mirrors the Postgres branch exactly:
`AddBotDbContext<SqliteBotDbContext>(...)` plus the same `BotDbContext` forwarding registration.
`tests/DiscordBot.Tests/Infrastructure/Extensions/ServiceCollectionExtensionsTests.cs`'s
`AddInfrastructure_WithSqliteConnectionString_ResolvesSqliteBotDbContext` - a test whose *name*
already promised this and whose *assertion* (`BeOfType<BotDbContext>()`) quietly didn't - now
asserts `BeOfType<SqliteBotDbContext>()` and would have caught this on day one had it matched its
own name.

**A second bug the fix exposed.** Once `SqliteBotDbContext` is what actually runs, the
`20260219205009_AddIsEnabledToGuildModerationConfig` migration - effectively a from-scratch
baseline for the newly-split context, since it re-creates tables like `Themes` to match the
current model shape - turned out to have never carried over the `InsertData` seed rows the old,
now-orphaned `20260115204355_AddThemeSupport` migration (base-`BotDbContext`-attributed, so not
part of `SqliteBotDbContext`'s chain at all) used to insert. A brand-new SQLite database got a
`Themes` table with zero rows, and `ThemeService.GetDefaultThemeAsync` throws
`InvalidOperationException("No active themes available in the system")` the moment anything
renders the shared layout - which is every page, including Login. `Migrations/Postgresql/20260219132220_InitialPostgresql.cs`
already seeded correctly (a genuine from-scratch "Initial" migration written after the split, not
a diff against the old chain), so this half of the bug was SQLite-only. Fixed by a new
`20260914060035_SeedDefaultThemesSqlite` migration using `INSERT OR IGNORE` (idempotent against an
older database whose history predates the split and already has these two rows from the original
migration) rather than `migrationBuilder.InsertData` (which would throw on that same database).

**Rule that falls out of it.** When a DbContext hierarchy splits into provider-specific
subclasses with their own migration sets, *every* DI registration and every "resolve the DbContext
and do X" call site must resolve the concrete subclass, never the shared base - EF Core's migrator
(and anything else that inspects `[DbContext(typeof(X))]` metadata) matches on the exact runtime
type, not assignability. Grep for `GetRequiredService<BotDbContext>()`/`AddDbContext<BotDbContext>`
after touching this area again; the only place the base type belongs in DI is as a forwarding
target (`services.AddScoped<BotDbContext>(sp => sp.GetRequiredService<TConcrete>())`) so unrelated
code that only needs the shared `DbSet<T>` surface doesn't have to care which provider is active.
And when a "re-baseline" migration for a newly-split context recreates a table that an older,
now-orphaned migration used to seed, check whether that seed data needs to be carried forward too
- a model-diff tool has no way to know a `CreateTable` needs an `InsertData` alongside it if the
only record of that data lived in a migration the tool no longer considers part of the chain.
