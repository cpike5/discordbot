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

**A third bug the fix exposed: existing deployments could not boot.** The two bugs above are about
*fresh* databases. Every database that had already been created under the broken registration has
exactly the 40 base-`BotDbContext` migration ids in `__EFMigrationsHistory` and none of the
`SqliteBotDbContext` ones. Once the registration is fixed, the migrator sees a history it does not
recognise at all and treats the whole current lineage - starting with the
`20260219205009_AddIsEnabledToGuildModerationConfig` re-baseline and its 54 `CreateTable`s - as
pending. Its first statement is `CREATE TABLE "ApplicationSettings"` against a database that
already has one, so `Program.cs`'s unguarded startup `MigrateAsync()` throws
`SQLite Error 1: 'table "ApplicationSettings" already exists'` and the process never starts.
The fix that repairs new installs would therefore have bricked every existing one.

**The upgrade path.** `Infrastructure/Data/Migrations/SqliteLegacyHistoryRepair.cs` runs from
`Program.cs` immediately before `MigrateAsync`, SQLite only. It detects the pre-fix state
(`__EFMigrationsHistory` contains `20260127225612_AddSsmlSupportToGuildTtsSettings` but not the
re-baseline), applies the schema delta, and records the re-baseline as applied with the
`ProductVersion` already in the history table. The remaining migrations then run normally, because
they only add new tables and columns. It is a no-op on a fresh database, on an already-repaired
one, and on PostgreSQL, so it is safe on every boot.

**The delta turned out to be one column.** Comparing the two end states object by object -
`sqlite_master` plus `PRAGMA table_info` for all 57 tables, built by migrating the base context and
the re-baseline separately into two temp files - showed them identical apart from
`GuildModerationConfigs.IsEnabled`, the column the re-baseline migration is actually named for.
That is the whole point of a re-baseline scaffolded as a model diff: it re-describes the schema the
legacy chain had already built, so almost none of it is new. This is why the repair is a single
`ALTER TABLE ... ADD COLUMN` and not a second "catch-up" migration full of `IF NOT EXISTS` DDL. It
supplies `DEFAULT 1` because SQLite cannot add a `NOT NULL` column without a default, and `1`
matches the entity's CLR initialiser - a guild that had moderation configured before the flag
existed stays moderated. (Two columns, `GuildAudioSettings.SilentPlayback` and
`MetricSnapshots.CpuUsagePercent`, carry `DEFAULT` clauses in a legacy database that a fresh one
lacks, because they arrived via `AddColumn(defaultValue:)`. SQLite cannot drop a default without
rebuilding the table, and EF never reads them, so they are left alone.)

**Guardrail.** Before stamping anything, the repair reads the re-baseline migration's own
`UpOperations` and checks that every table and column it declares already exists. If any is
missing, it throws with the list instead of writing a history row - a history row that lies about
the schema is unrecoverable, because every future migration then assumes those objects are there.
It also stands aside with a Warning, rather than guessing, on a history that contains neither
marker.

**The re-baseline dropped two sets of seed rows, not one.** `Themes` was the visible one (the
layout throws without a default theme). `PerformanceAlertConfigs` was the quiet one: eight default
alert thresholds that `Migrations/Postgresql/20260219132220_InitialPostgresql.cs` seeds and the
SQLite re-baseline did not, so every fresh SQLite install came up with an empty alert-configuration
table and nothing to threshold against. `20260914065633_SeedPerformanceAlertConfigsSqlite` restores
them, copied row for row from the Postgres migration, with `INSERT OR IGNORE` so it is a no-op on a
database that already has them.

**Rule that falls out of it.** A registration fix that changes which migrations EF considers
applied is a data-migration problem, not just a DI problem. Before shipping one, build the old
on-disk state the way the bug built it and boot against it - `tests/DiscordBot.Tests/Infrastructure/Data/SqliteLegacyHistoryRepairTests.cs`
does exactly that, by migrating the base `BotDbContext` into a temp file - and assert that the
upgraded schema is byte-for-byte what a from-scratch database gets. "It works on a new database"
is not evidence about the databases that already exist.
