# CLAUDE.md

Orientation for an agent about to change this repository. The README is for a
human evaluating the project; this file is for you, today, with no prior context.

## What this is

A Discord bot with an admin web portal, in one .NET 10 process. The bot side is
Discord.NET slash commands, voice/audio (soundboard, TTS, VOX clips), moderation,
reminders, scheduled messages, and an LLM-backed assistant. The web side is
ASP.NET Core Razor Pages plus REST controllers, styled with Tailwind, with
plain per-page JavaScript modules in `wwwroot/js/` and SignalR for live updates;
it is being ported page by page to a Blazor Web App under `Blazor/` (see Gotchas). Storage is EF Core
on SQLite by default or PostgreSQL. Auth is ASP.NET Identity plus Discord OAuth.
Observability is Serilog and OpenTelemetry. It ships as a GHCR Docker image.

Solution layout (clean architecture, dependencies point inward):

| Project | Owns |
| --- | --- |
| `src/DiscordBot.Agents` | The agent engine: the agentic loop (`AgentRunner`), tool contracts, `ToolRegistry`, `PromptTemplate`, and the OpenRouter chat client. A leaf library — it references no other project and knows nothing about Discord, EF Core, or guilds. |
| `src/DiscordBot.Core` | Entities, enums, DTOs, service and repository interfaces, options classes. No framework dependencies, and no project references at all. |
| `src/DiscordBot.Infrastructure` | EF Core `BotDbContext` (SQLite and Postgres variants), repositories, migrations, infrastructure services. |
| `src/DiscordBot.Bot` | Everything hosted: Discord command modules, bot services, Razor Pages, controllers, SignalR hubs, DI registration in `Extensions/*ServiceExtensions.cs`, `Program.cs`. |
| `src/DiscordBot.DocGen` | Small CLI that runs the feature-request document generator against the database. Rarely touched. |
| `tests/DiscordBot.Tests` | One xUnit project mirroring `src/`. Moq, FluentAssertions. |
| `tests/DiscordBot.ComponentTests` | bUnit tests for the `Blazor/` tree, one class per component. |
| `tests/DiscordBot.E2E` | Playwright browser tests against the real host in web-only mode; skipped unless `E2E_ENABLED=1`. |
| `tests/DiscordBot.Evals` | Assistant evals: a dozen cases through the real agent loop against a real model. Every test skips itself when `OpenRouter:ApiKey` is absent, so a normal `dotnet test` runs them as skips and costs nothing. |

A new service goes: interface in Core, implementation in Bot or Infrastructure,
registration in the matching `*ServiceExtensions.cs`. Follow that split so Core
stays framework-free.

A new **tool** is not engine work: it is one file in Infrastructure or Bot, next to the domain
services it calls. Implement `IAgentTool` from `DiscordBot.Agents.Abstractions`, put it in
`Services/LLM/Tools/`, and add a `ToolCatalog` entry (`Core/Models/Llm/`) — the catalogue is what
decides which assistant advertises it, so a tool without one reaches nothing. There is no DI edit:
the assembly scan finds it. `IToolProvider` is still there and still right for a group of tools that
share expensive state, but it is no longer the default. Only the model-facing machinery itself
belongs in `DiscordBot.Agents`. See `docs/architecture/patterns.md` § Agent Tool Authoring.
A new **skill** — instructions for a rare, heavy tool group, kept out of every request until the
model loads it — is one markdown file in `docs/agents/skills/<surface>/` and nothing else: no
catalogue entry, no DI, no code. See `docs/architecture/patterns.md` § Agent Skills.

A new tool also gets a page in `docs/tools/` (template in that directory's README) and is held to
the house rules by `ToolContractTests` — name shape, description length, schema shape, a catalogue
entry, the mutation refusal, and failures that `ToolOutcomes.Classify` can count. Adding a house rule
about tools means adding it there, not to a checklist.
An assistant abstraction whose signature is made of engine types (`IAssistantContext`,
`IAssistantMessagePipeline`, the context factories) lives in
`Infrastructure/Abstractions/LLM/` rather than Core, because Core cannot see the engine.

## Read before you search

`docs/architecture/` is the map. Read the relevant page before grepping; one page
usually replaces five tool calls.

- `system-overview.md` for layers, data flow, and deployment.
- `feature-map.md` for what commands, services, pages, and entities make up each feature.
- `service-catalog.md` to find an existing service before writing a new one.
- `patterns.md` for how things are done here: DI, options, command modules,
  `GuildPageModelBase`, `ApiControllerBase`, repositories, audit logging,
  background services. Copy the pattern, do not invent a new one.
- `data-model.md` for entities and relationships.
- `ui-inventory.md` for every page route and reusable component.

Feature-level docs are in `docs/articles/` (indexed in `docs/index.md` and
`docs/toc.yml`). Configuration is fully documented in
`docs/articles/configuration-guide.md`, REST endpoints in
`docs/articles/api-endpoints.md`. Domain-expert agent definitions in
`.claude/agents/` carry the deep per-stream detail.

## Build and test

```bash
dotnet build DiscordBot.sln                 # ~1.5 min cold, seconds warm
dotnet test DiscordBot.sln                  # ~5,200 unit + ~900 bUnit tests, ~1.5 min
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"
dotnet test tests/DiscordBot.Evals   # skips entirely without OpenRouter:ApiKey
```

`tests/DiscordBot.ComponentTests` (bUnit, wired into `DiscordBot.sln`) covers the `Blazor/` tree
component-by-component — see "Component (bUnit) Tests" in `docs/articles/testing-guide.md`.

CI (`.github/workflows/ci.yml`) runs restore, build in Release, and the full test
suite on every PR to `main`. Both must be green before you push.

**Tailwind.** `dotnet build` of the Bot project runs `npm install` and the
Tailwind build first, so it needs Node. Set `SkipTailwind=true` (or `CI=true`) to
skip that step when you are not touching CSS; the checked-in CSS is used as-is.
The web SessionStart hook installs Node when it can and sets `SkipTailwind` when
it cannot, so a plain `dotnet build` works either way.

**Test database.** Tests use SQLite in-memory via
`tests/DiscordBot.Tests/TestHelpers/TestDbContextFactory.cs`. There is no
PostgreSQL test path, so a green test run says nothing about the Postgres
provider or its migrations. Say so when you report on a change that touches
them. An in-memory database also lives inside one connection, so writers cannot
actually contend: a test about concurrent writes needs
`TestDbContextFactory.CreateSharedDatabase()`, which is file-backed.

**Browser (Playwright) tests.** `tests/DiscordBot.E2E` drives the real app with headless
Chromium and is gated behind `E2E_ENABLED=1` (unset, every test reports Skipped, so the commands
above stay green); Chromium is pre-installed in this repo's remote sessions at
`PLAYWRIGHT_BROWSERS_PATH=/opt/pw-browsers`. See `docs/articles/testing-guide.md` "Browser
(Playwright) tests".

**Background-service tests fail in a full run but pass alone** when something
starves them. Two rules keep them green: never block a thread-pool thread on
other pool threads (a `Barrier` inside `Parallel.For`, a `Thread.Sleep` loop in
`Task.Run`); use `TestHelpers/ConcurrencyTestHelper` to run that work on
dedicated threads. And any wait with a wall-clock deadline must poll with
`ConfigureAwait(false)`, as `LogTestHelper` does, because a plain `await`
resumes through xUnit's worker queue and can sit there for seconds behind
other test classes. `docs/lessons-learned/flaky-tests-thread-pool-starvation.md`
has the details.

## Running it locally

The process exits at startup if `Discord:Token` is not configured. Set
`Discord:Enabled` to `false` (e.g. `Discord__Enabled=false`) to run the web UI
web-only, without a bot token or gateway connection — the bot logs a line and
returns without logging in, slash commands aren't registered, and Discord OAuth
login is hidden if `Discord:OAuth:ClientId`/`ClientSecret` aren't set too. Used
for browser/UI (Playwright) testing and for running the admin portal without a
bot; see `docs/articles/configuration-guide.md` ("Discord:Enabled (web-only
mode)"). Put secrets in User Secrets (ID
`7b84433c-c2a8-46db-a8bf-58786ea4f28e`), never in `appsettings*.json`:
`Discord:Token`, `Discord:OAuth:ClientId`, `Discord:OAuth:ClientSecret`,
`OpenRouter:ApiKey`, `AzureSpeech:SubscriptionKey`.

```bash
dotnet run --project src/DiscordBot.Bot     # web UI on http://localhost:5124
```

Default database is SQLite at `data/discordbot.db`, created on first run. Set
`Discord:TestGuildId` for development; without it, global slash commands take up
to an hour to appear in Discord. Voice features need FFmpeg, libsodium, and
libopus on the host (`docs/articles/audio-dependencies.md`).

## Database and migrations

Two providers, two migration sets, two design-time contexts. The EF CLI cannot
pick a context on its own, so `--context` is mandatory:

```bash
# SQLite
dotnet ef migrations add Name --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot --context SqliteBotDbContext -o Migrations/Sqlite
# PostgreSQL
dotnet ef migrations add Name --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot --context PostgresBotDbContext -o Migrations/Postgresql
# Apply: same flags with `database update`
# Copy data between providers
dotnet run --project src/DiscordBot.Bot -- migrate-data --source "Data Source=data/discordbot.db" --target "Host=localhost;Database=discordbot;Username=discordbot;Password=changeme"
```

`Migrations/Sqlite` holds two lineages: 40 superseded migrations attributed to the
base `BotDbContext` (ending at `20260127225612_AddSsmlSupportToGuildTtsSettings`)
and the live one attributed to `SqliteBotDbContext` (from the
`20260219205009_AddIsEnabledToGuildModerationConfig` re-baseline onward) — EF matches
a migration to a context by exact runtime type, so a SQLite migration scaffolded
with anything but `--context SqliteBotDbContext` is invisible to the running app
and applies silently to nothing. A database created before that split was fixed has
only the 40 legacy ids in `__EFMigrationsHistory`, so `SqliteLegacyHistoryRepair`
(called from `Program.cs` immediately before `MigrateAsync`, SQLite only) brings it
to the re-baseline first; see
`docs/lessons-learned/sqlite-migration-context-mismatch.md`.

A schema change ships with **both** migrations, and with `data-model.md` updated
if it adds or changes an entity. `Database:Provider` (`Sqlite` or `PostgreSql`)
selects the provider explicitly; omitted, it is inferred from the connection
string (`Host=` or `Server=` means Postgres).

Do not remove `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)`
from startup. Without it, `DateTime` writes to `timestamp with time zone`
columns throw. For the same reason, `PostgresBotDbContext` overrides
`ConfigureConventions` to pin every `DateTime`/`DateTime?` column to
`timestamp without time zone` — Npgsql 10 otherwise defaults new columns to
`timestamp with time zone`, which would drift from the existing schema.

EF Core 10 makes `Migrate()`/`MigrateAsync()` throw `PendingModelChangesWarning`
by default when the model doesn't match the last migration's snapshot, and
`Program.cs` calls `MigrateAsync` at startup — so an unnoticed drift is a boot
crash, not a silent mismatch. After any package upgrade that touches EF Core
or a provider (Npgsql, Sqlite), run `dotnet ef migrations has-pending-model-changes`
for **both** `SqliteBotDbContext` and `PostgresBotDbContext` before assuming
the upgrade is done — provider convention changes (e.g. a default column-type
mapping) can add pending changes to one provider's snapshot without affecting
the other.

## Conventions

- **Specs before code** for anything medium or larger: `docs/specs/` for feature
  specs, `docs/plans/` for implementation plans, `docs/requirements/` for BRD/PRD
  style documents. `docs/lessons-learned/` gets a note after a feature that
  fought back.
- **Update the docs you would have wanted.** A new feature updates
  `feature-map.md` and `service-catalog.md`; a new page updates `ui-inventory.md`;
  a new options class updates `configuration-guide.md`; a new endpoint updates
  `api-endpoints.md`.
- **Agent definitions.** When work adds services, entities, or repositories in a
  stream, or changes its patterns, update the matching file in `.claude/agents/`
  in the same PR. Those files are only useful while they are true.
- **Guild, not server.** Discord's API calls them guilds; URLs, routes, and code
  do too (`/Guilds/...`, `guildId`).
- **Small blast radius.** One concern per PR. No drive-by refactors or
  reformatting in unrelated files.
- **Versioning** lives in `Directory.Build.props`. Tags `v*` trigger a GitHub
  release and a GHCR image (`docs/articles/versioning-strategy.md`).
- **HTML prototypes** for UI work go in `docs/prototypes/features/`, using the
  shared CSS in `docs/prototypes/css/`.

## Gotchas

- **Discord IDs in JavaScript.** Snowflakes are `ulong`, larger than
  `Number.MAX_SAFE_INTEGER`. Rendering one as a number silently rounds the last
  digits and every lookup fails. Always emit them as strings:

  ```razor
  window.guildId = '@Model.GuildId';   <!-- quoted -->
  ```

- **The assistant talks to OpenRouter, not a vendor SDK.** `ILlmClient` is
  implemented by `OpenRouterLlmClient` (`DiscordBot.Agents/OpenRouter/`):
  an owned typed `HttpClient` over OpenRouter's OpenAI-compatible chat completions,
  plus owned wire records. There is no LLM SDK dependency — build LLM work on
  `ILlmClient` and those records rather than adding one. Model names are OpenRouter
  slugs (`anthropic/claude-sonnet-4`, `openai/gpt-4o`), not vendor model IDs.
  Without `OpenRouter:ApiKey` the assistant services are never registered, which is
  what lets migrations run without a key.
- **Discord.NET is the official NuGet package** (`Discord.Net` 3.20.x). An older
  branch carried a local fork for a voice fix; if you see references to
  `local-packages/` or `3.19.0-fork`, they are stale.
- **The UI is being ported from Razor Pages to Blazor.** Both coexist under
  `src/DiscordBot.Bot/` until the port finishes: `Pages/` (Razor Pages, legacy,
  being ported one cluster at a time) and `Blazor/` (new UI, Blazor Web App,
  Interactive Server only, per-page interactivity). **New UI work goes in
  `Blazor/`, not `Pages/`.** Legacy Razor Pages reusable UI is partials under
  `Pages/Shared/Components/` with view models in `ViewModels/Components/`,
  `.cshtml` plus `.cshtml.cs`, guild pages inheriting `GuildPageModelBase`. The
  Blazor equivalent exists now: the component library (Phase 2) under `Blazor/Shared/`, and the
  shell layouts plus `GuildContext` (Phase 3) under `Blazor/Layout/`, `Blazor/Guilds/`,
  `Blazor/Portal/` — Phase 4 is now porting pages cluster by cluster
  (`docs/plans/blazor-port-plan.md`). When a Phase 4 cluster deletes a `.cshtml`, sweep
  `asp-page`/`RedirectToPage`/`Url.Page` references to it and extend `DeletedPageRoutes` in
  `DeletedPagesGuardTests`. See "Blazor components" in `docs/architecture/patterns.md` for
  hosting, auth-in-circuits and the `HttpContext`-is-prerender-only rule.
- **Component interactions** (buttons, selects) are handled in separate
  `*ComponentModule` classes, with custom IDs built by `ComponentIdBuilder` and
  state kept in `IInteractionStateService` (expiry from `Caching:InteractionStateExpiryMinutes`). Putting handlers
  in the slash-command module breaks the ID routing convention.
- **Remote (web) sessions** cannot reach the Microsoft dotnet-install CDN. The
  SessionStart hook installs the SDK from the Ubuntu archive instead; do not try
  to route around that.
