# LLM Model Management and Usage Ledger — Implementation Plan

**Status:** Proposed
**Date:** 2026-09-10
**Builds on:** `openrouter-migration-plan.md` (shipped). That plan listed "per-guild model selection" and "a live `/models` directory" as follow-ups; this plan delivers the directory, an admin allowlist, per-mode defaults, and a usage ledger.

## Goals

1. **Catalog.** Pull every model OpenRouter offers into the portal, with filtering and sorting.
2. **Allowlist.** Admins choose which catalog models are enabled for the bot. Nothing is enabled by default, and a catalog refresh never enables a model on its own.
3. **Per-mode defaults.** For each LLM "mode" (guild assistant, DM assistant, feature-request gathering) an admin picks the default model from the enabled set, in the web UI, and the change takes effect without a restart.
4. **Usage ledger.** Track tokens and cost per chat message across every mode, record which model answered, and show cost breakdowns by user (portal-wide and per guild).

Out of scope: per-guild model overrides, streaming, reasoning/effort settings, and any change to how the API key is supplied (it stays in configuration).

## What exists today

| Concern | Current state | Source |
| --- | --- | --- |
| Model slugs | Four config values, all read once at startup: `OpenRouter:DefaultModel`, `Assistant:Sampling:Model`, `DmAssistant:Model`, `FeatureRequests:RequirementsGatheringModel`. | `Core/Configuration/*Options.cs` |
| Consumers | `GuildAssistantContextFactory` and `DmAssistantContextFactory` capture `IOptions<T>.Value` in their constructors; `FeatureRequestConversationService` (singleton) does the same; `OpenRouterLlmClient.BuildRequest` falls back to `OpenRouterOptions.DefaultModel`. There is no `IOptionsMonitor` anywhere in `src/`. | `Infrastructure/Services/LLM/`, `Bot/Services/FeatureRequests/` |
| Admin settings | `/Admin/Settings` is a tabbed page (`General`, `Features`, `Commands`, `Advanced`, `BotControl`, `Appearance`) backed by the `ApplicationSettings` table. `SettingDefinitions.All` declares each editable key; `ISettingsService.GetSettingValueAsync<T>(key)` resolves **DB → `IConfiguration[key]` → definition default**, so a DB row shadows the appsettings key of the same name. Saves are audited for free via `SettingsSectionService`. `ISettingsService.SettingsChanged` fires on every save. | `Infrastructure/Services/SettingsService.cs`, `SettingDefinitions.cs`, `Bot/Services/Settings/SettingsSectionService.cs` |
| Runtime reads of DB settings | Precedent exists: `AssistantAccessGate`, `AssistantMessageHandler`, `DmAssistantMessageHandler` read `Assistant:GloballyEnabled` / `DmAssistant:Enabled` through `ISettingsService` per message. | those files |
| Catalog | No fetch of `GET /api/v1/models` exists. The live endpoint returns ~436 models with `id`, `name`, `description`, `context_length`, `pricing` (per-token USD strings; `-1` means unknown), `architecture.input_modalities`, `supported_parameters` (contains `tools` for ~370 models), `created`. Alias entries (`~vendor/model-latest`) carry `alias_target`. | verified against the live endpoint on 2026-09-10 |
| Per-message logging | `AssistantInteractionLog` (guild) and `DmAssistantInteractionLog` (DM) already store input/output/cached tokens, latency, tool calls and `EstimatedCostUsd` per user message. Neither stores the model slug. Feature-request conversations log **nothing**. Daily aggregates exist per guild (`AssistantUsageMetrics`) and per DM user (`DmAssistantUsageMetrics`). | `Core/Entities/` |
| Cost fallback | `AssistantMessagePipeline.CalculateCost` uses OpenRouter's billed `usage.cost` when present, else flat Sonnet-priced per-million rates from config. Rates are not keyed by model, so they are wrong for any other model. | `AssistantMessagePipeline.cs:71-84` |
| Usage UI | `/guild/{id}/assistant-metrics` shows per-guild daily totals and recent interactions. No cross-guild view, no DM view, no per-user breakdown. | `Pages/Guilds/AssistantMetrics.cshtml` |

## Design

### 1. Model catalog and allowlist

**Entity `LlmModel`** (new table `LlmModels`, Core → Infrastructure config → both migrations):

| Column | Notes |
| --- | --- |
| `Id` (string PK) | OpenRouter slug, e.g. `anthropic/claude-sonnet-4.6` |
| `Name`, `Description` | from catalog; description truncated to 1,000 chars |
| `Vendor` | slug prefix before `/`, for grouping and filtering |
| `ContextLength` (int) | |
| `PromptPricePerMillion`, `CompletionPricePerMillion`, `CacheReadPricePerMillion`, `CacheWritePricePerMillion` (decimal?) | catalog per-token price × 1,000,000; null when the catalog reports `-1` |
| `SupportsTools`, `SupportsImages` (bool) | from `supported_parameters` / `input_modalities` |
| `ReleasedAt` (DateTime?) | from `created` |
| `FirstSeenAt`, `LastSeenAt` (DateTime) | maintained by refresh |
| `IsAvailable` (bool) | false once a refresh no longer returns the slug; row is kept |
| `IsEnabled` (bool, default false) | the admin allowlist flag. **Only ever set by an admin action.** |
| `EnabledAt`, `EnabledBy` (nullable) | |

Alias rows (`~vendor/...`) are skipped on import; admins pick concrete slugs.

**`IOpenRouterModelCatalogClient`** (Core interface; Infrastructure `Services/LLM/OpenRouter/OpenRouterModelCatalogClient`): a second typed `HttpClient` against `GET models`, with owned wire records (`ModelListResponse`, `ModelInfo`, `ModelPricing`, `ModelArchitecture`) serialized with the existing `OpenRouterJson.Options`. Same auth and attribution headers as `OpenRouterLlmClient`; no retry loop (a refresh is user-initiated or scheduled, and can simply fail with a message).

**`ILlmModelCatalogService`** (Core; Infrastructure impl):

- `RefreshAsync(userId?)` — fetch, upsert by slug, set `LastSeenAt`, mark missing rows `IsAvailable = false`. Never touches `IsEnabled`. Returns counts (added, updated, removed) and is audited (`AuditLogCategory.Configuration`, new action `LlmCatalogRefreshed`).
- `GetCatalogAsync(filter)` — server-side filter/sort support so the controller stays thin: text search on slug/name, vendor, enabled-only, available-only, tools-only, sort by name/vendor/price/context/released.
- `SetEnabledAsync(slug, enabled, userId)` — audited (`LlmModelEnabled` / `LlmModelDisabled`). Refuses to disable a model that is currently the default for any mode; the UI tells the admin to change the default first.
- `GetEnabledAsync()` — for the mode pickers.

**Bootstrap.** A one-time seed inside the first `RefreshAsync` after the migration enables the slugs currently configured for the three modes, so the pickers are not empty on upgrade and existing behaviour is preserved. Slugs that arrive later through a refresh stay disabled. This is a deliberate exception to "nothing enabled by default" and is called out in the decisions section below.

**Background refresh.** `LlmCatalogRefreshService : MonitoredBackgroundService`, interval from a new `Llm:CatalogRefreshHours` option (default 24, `0` disables). It is registered only when `OpenRouter:ApiKey` is present, matching the existing gating.

### 2. Per-mode defaults

**`LlmMode` enum** (Core): `GuildAssistant`, `DmAssistant`, `FeatureRequests`. Adding a mode later means adding an enum member and a setting definition.

**Setting keys.** Reuse the existing configuration keys as `SettingDefinitions` entries (data type `String`, new `SettingCategory.AiModels`), so the DB-shadows-config behaviour works with no new plumbing:

| Mode | Key |
| --- | --- |
| Guild assistant | `Assistant:Sampling:Model` |
| DM assistant | `DmAssistant:Model` |
| Feature requests | `FeatureRequests:RequirementsGatheringModel` |

`OpenRouter:DefaultModel` stays configuration-only as the last-resort fallback inside `OpenRouterLlmClient`.

**`ILlmModelResolver`** (Core; Infrastructure impl, singleton): `ResolveAsync(LlmMode)` returns the effective slug in this order: DB setting → bound options value (which already applies the legacy flat-key precedence) → `OpenRouterOptions.DefaultModel`. Results are held in a small in-memory cache that is cleared by `ISettingsService.SettingsChanged`, so a save takes effect on the next message with no restart. If the resolved slug is not in the enabled set, the resolver logs a warning but still returns it. The allowlist is enforced at save time (see validation), not at send time, so a fresh install with an empty allowlist still works from configuration.

**Consumers change at the factory boundary only:**

- `GuildAssistantContextFactory` / `DmAssistantContextFactory` resolve the slug per call and pass it into the context, which stops reading `_options.Sampling.Model` / `_options.Model`.
- `FeatureRequestConversationService` resolves inside its per-message scope instead of using the captured options value.
- `AgentContext` gains `LlmMode Mode` (needed by the ledger, section 3) and callers set it.

**Validation on save.** `SettingsSectionService` gets a hook so a category can validate before persisting; the `AiModels` category rejects a slug that is not enabled, or that lacks `SupportsTools` (all three modes send tools, and `provider.require_parameters` would otherwise fail the call). The rejection message names the model.

**Per-model cost fallback.** `AssistantCostRates` is built from the model's catalog pricing when the row exists, falling back to the configured per-million rates only when it does not. This corrects the fallback cost for any non-Sonnet model. Billed `usage.cost` still wins when OpenRouter reports it.

### 3. Usage ledger and per-user breakdowns

**Entity `LlmUsageRecord`** (new table `LlmUsageRecords`), one row per user message across every mode. No message text is stored; the existing interaction logs keep the text.

| Column | Notes |
| --- | --- |
| `Id` (long) | |
| `Timestamp` | UTC |
| `Mode` (LlmMode) | |
| `UserId` (ulong), `GuildId` (ulong?) | null guild for DM and DM-based feature requests |
| `Model` | slug actually used, from the response's `model` field when present, else the requested slug |
| `InputTokens`, `OutputTokens`, `CachedTokens`, `CacheWriteTokens` | summed over the agent loop |
| `LlmCalls`, `ToolCalls` | loop count and tool invocations |
| `CostUsd` (decimal) | |
| `CostSource` (enum `Billed` / `Estimated`) | whether OpenRouter reported the cost or the fallback rates were used |
| `LatencyMs`, `Success` | |
| `InteractionLogId` (long?) | link to the mode's interaction log row when one exists |

Indexes: `(UserId, Timestamp)`, `(GuildId, Timestamp)`, `(Mode, Timestamp)`, `(Model, Timestamp)`.

**Write path.** `LlmResponse` gains `Model`; `AgentRunResult` gains `Model` and `LlmCalls` (it already has `LoopCount`, which is renamed only in the result surface if the team prefers clarity; otherwise reuse it). A single `ILlmUsageRecorder` is called from `AssistantMessagePipeline` (covers guild and DM in one place, matching the shared-pipeline rule in the agent definition) and from `FeatureRequestConversationService`, which today records nothing. Writes go through a bounded channel drained by a background worker, like `AuditLogQueue`, so recording never adds latency to a reply.

**Existing logs.** `AssistantInteractionLog` and `DmAssistantInteractionLog` each gain a nullable `Model` column so the recent-interactions tables can show it. No other change to those entities.

**Read path.** `ILlmUsageRepository` with grouped queries: totals and breakdown by user, by model, by mode, and by day, all over a date range and optional guild filter; plus a paged per-user message list. The existing `AssistantUsageMetrics` daily aggregates stay as they are.

**UI.**

- New admin page `/admin/llm-usage` (`RequireAdmin`): date-range filter, hero totals (messages, tokens, cost, share of cost that was billed vs estimated), tables by user, by model, by mode, and a per-user drill-down of message rows. Data via a `LlmUsageController` (`ApiControllerBase`) so the page can reuse `date-range-filter.js` and `ajax-sort.js`.
- `/guild/{id}/assistant-metrics` gains a "cost by user" table and a model column on recent interactions, fed from the ledger filtered by guild.

**Retention and purge.** Ledger rows are deleted by the same cleanup that trims interaction logs, using `Assistant:Privacy:InteractionLogRetentionDays`. The user-purge and data-export flows must include `LlmUsageRecords`; this is a GDPR requirement, not optional, and is a checklist item in phase 3.

### 4. Web UI for the catalog and defaults

A new **"AI Models" tab** on `/Admin/Settings` (new `SettingCategory.AiModels`, wired into `SettingsSectionService.LoadViewModelAsync` and the tab strip), rendered by a partial and its own module `wwwroot/js/llm-models.js`:

- **Defaults panel** at the top: three selects (one per mode) populated from enabled models, showing price per million and context length beside each option, with a warning badge when the current value is disabled or unavailable. Saved through the standard category save so audit and reset-to-default come for free.
- **Catalog table** below: search box, vendor select, toggles for "enabled only", "available only", "tool-capable only", sortable columns (name, vendor, prompt price, completion price, context, released), and an enable switch per row. A "Refresh from OpenRouter" button shows last refresh time and the add/update/remove counts. Rows that are enabled but no longer available are highlighted.
- Client-side filter and sort over the JSON payload is sufficient at ~440 rows; the controller still supports server-side filters so the page stays fast if the catalog grows.

Endpoints (`LlmModelsController`, `RequireAdmin`):

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/admin/llm-models` | catalog list with filter and sort query parameters |
| POST | `/api/admin/llm-models/refresh` | refresh from OpenRouter |
| PUT | `/api/admin/llm-models/{slug}/enabled` | body `{ enabled: bool }` |
| GET | `/api/admin/llm-models/defaults` | current per-mode defaults with resolution source (db, config, fallback) |

Defaults are saved through the settings page handler, not the controller, so there is one write path and one audit path.

## Delivery

Three PRs, each independently shippable and green.

### PR 1 — Catalog and allowlist (no behaviour change)

- `LlmModel` entity, EF configuration, both migrations, `data-model.md`.
- Catalog client and wire records, with stub-handler tests (happy path, `-1` pricing, alias skipping, HTTP error).
- `ILlmModelCatalogService`, repository, refresh background service, audit actions.
- `LlmModelsController` list/refresh/enabled endpoints and tests.
- "AI Models" tab with the catalog table and refresh button. The defaults panel is present but read-only, showing where each mode's model currently comes from.
- Docs: `feature-map.md`, `service-catalog.md`, `ui-inventory.md`, `api-endpoints.md`, `configuration-guide.md` (`Llm:CatalogRefreshHours`), `settings-page.md`, `.claude/agents/ai-assistant.md`.

### PR 2 — Per-mode defaults and runtime resolution

- `LlmMode`, three `SettingDefinitions` entries, `SettingCategory.AiModels` save validation.
- `ILlmModelResolver` with cache invalidation on `SettingsChanged`; factory and feature-request consumers switched to it.
- Per-model cost fallback from catalog pricing.
- Defaults panel becomes editable.
- Tests: resolver precedence (db → options → fallback), invalidation on save, save-time rejection of disabled and non-tool models, factories pass the resolved slug, bootstrap seed enables only the configured slugs.
- Docs: `configuration-guide.md` (note that the three keys can now be overridden from the UI), `ai-assistant.md`.

### PR 3 — Usage ledger and breakdowns

- `LlmUsageRecord` entity, `Model` columns on the two interaction logs, both migrations, `data-model.md`.
- `LlmResponse.Model` from the wire response; `AgentRunResult.Model`; `AgentContext.Mode`.
- `ILlmUsageRecorder` queue and worker; calls from `AssistantMessagePipeline` and `FeatureRequestConversationService`.
- Repository grouped queries; `LlmUsageController`; `/admin/llm-usage` page; guild metrics additions; sidebar link.
- Retention cleanup, user purge, and data export extended to the ledger.
- Tests: recorder writes one row per message with the right mode and cost source; grouped queries; purge removes ledger rows; `AgentRunnerTests` unchanged and green.
- Docs: `feature-map.md`, `service-catalog.md`, `ui-inventory.md`, `api-endpoints.md`, `ai-assistant.md`, `.claude/agents/ai-assistant.md`, and a `lessons-learned` note if anything fights back.

## Decisions to confirm

Each has a recommendation; the plan proceeds on it unless told otherwise.

1. **Bootstrap-enable the currently configured slugs on the first refresh.** Recommended: yes, so upgrades keep working and the pickers are not empty. Everything else stays disabled until an admin enables it.
2. **Allowlist enforcement at send time.** Recommended: enforce in the UI and at save time, warn at runtime. Refusing at send time would take the assistant offline on any install whose allowlist is empty.
3. **Ledger granularity.** Recommended: one row per user message with an `LlmCalls` count, not one row per LLM call. Per-message is what the breakdowns need, and it keeps the table small.
4. **Ledger as a new table** rather than adding columns to the three per-mode logs. Recommended: new table, so per-user breakdowns across modes are one query and feature-request usage finally has a home.
5. **Retention.** Recommended: ledger follows `Assistant:Privacy:InteractionLogRetentionDays` (default 90 days) rather than a new option.

## Risks

| Risk | Mitigation |
| --- | --- |
| A default model disappears from OpenRouter | Refresh marks it unavailable and the UI flags it; the resolver still sends it and OpenRouter returns a 404 with the existing per-status hint. Admin picks a replacement. |
| Admin picks a model without native tool calling | Rejected at save time using `SupportsTools`; the runtime `require_parameters` guard remains as the backstop. |
| Prompt caching silently stops when a non-Claude model is chosen | Documented in the picker (badge "prompt caching supported" on Claude-family slugs); cost remains correct because billed `usage.cost` wins. |
| Catalog payload size (benchmarks blocks make it ~1 MB) | Wire records only declare the fields used; the rest is ignored by the serializer. |
| Ledger write adds latency | Bounded channel and background worker, same posture as the audit log queue. |
| PostgreSQL migrations untested by CI | Both migration sets ship; the Postgres path is verified manually against a throwaway database before merge, and the PR says so. |
| Singleton `SettingsService` hits the DB per read | Resolver caches per mode and invalidates on `SettingsChanged`, so message handling adds no query in steady state. |
