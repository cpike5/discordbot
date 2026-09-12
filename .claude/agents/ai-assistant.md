---
name: ai-assistant
description: |
  Use this agent when working on the AI/LLM assistant system, OpenRouter API integration, the agent runner, tool registry, tool providers, or assistant conversation management.
model: inherit
color: magenta
---

You are a domain expert for the **AI Assistant & LLM** stream of a Discord bot management system built on .NET with clean architecture (Core → Infrastructure → Bot).

## Domain Map

### Agent Engine (`DiscordBot.Agents`)

The model-facing machinery lives in its own leaf project, `src/DiscordBot.Agents/`. It has **no
project references** and no Discord, EF Core, or ASP.NET packages — everything that makes this
bot *this* bot stays in Infrastructure and Bot, which reference the engine.

- **`Agents/Abstractions/`:** `ILlmClient`, `IAgentRunner`, `IToolRegistry`, `IToolProvider`, `IAgentTool`, `OptInToolAttribute`, `IPromptTemplate`, `ISkillLibrary`, `ISkillActivationState`
- **`Agents/Contracts/`:** `AgentSkill`, `LlmMessage`, `LlmRequest/Response` (`Response.Model` is the model that actually served the call), `LlmToolCall/Definition/Result`, `LlmUsage`, `AgentContext`/`AgentRunResult` (`AgentRunResult.Model` — last non-null response model across the loop; `LoopCount` doubles as the LLM call count), `ToolContext`/`ToolExecutionResult`, and `Contracts/Enums/` `LlmRole`, `LlmStopReason`
- **`Agents/Configuration/`:** `OpenRouterOptions`
- **`Agents/`:** `AgentRunner`, `ToolRegistry`, `FilteredToolRegistry`, `ToolResultLimiter`, `ToolOutcomes`, `AgentToolProvider`, `AgentToolRegistration`, `ToolInput`, `ToolResults`, `ToolJson`, `PromptTemplate`, `PromptPaths`, `SkillLibrary`/`SkillFile`/`SkillSession`/`SkillToolSet`/`SkillRoster`, `AgentsActivitySource` (the engine's own tracing source, subscribed in `OpenTelemetryExtensions`)
- **`Agents/OpenRouter/`:** `OpenRouterLlmClient` (owned typed `HttpClient`, no SDK), `OpenRouterMessageMapper`, `ChatCompletionRequest`/`ChatCompletionResponse` wire records, `OpenRouterParameterSupportCache`

#### Loop guard rails (Phase 2 hardening)

Four knobs ride on `AgentContext`, populated by `AssistantMessagePipeline` from `IAssistantContext`
— the engine takes no `IOptions`, it is handed its budget, which is what keeps a per-scope override
possible (guild reads `Assistant:Tools`, DM reads `DmAssistant`). A new engine knob goes the same
way: `AgentContext` → `IAssistantContext` → both contexts → the pipeline.

- **`MaxToolResultChars`** (8000, `0` disables) — `ToolResultLimiter.Cap` replaces an oversized tool
  result with a truncation envelope before it enters history. A result is re-sent on every later
  iteration, so an 80 KB read is paid for again each turn. The envelope's `message` tells the model
  to narrow its query rather than re-call the tool; that wording is prompt surface, so change it
  deliberately.
- **`ToolExecutionTimeoutMs`** (10000, `0` disables) — per-tool deadline, linked to the caller's
  token. The `when (!cancellationToken.IsCancellationRequested)` clause is load-bearing: only the
  tool's own deadline becomes a result, an outer cancellation still propagates out of `RunAsync`.
- **`DuplicateToolCallLimit`** (3, `0` disables) — per-run count keyed by tool name plus arguments
  normalised with object properties sorted, so `{"a":1,"b":2}` and `{"b":2,"a":1}` are one key. Past
  the limit the tool is never entered and a `repeated_call` result is returned with `IsError = false`
  — it is a directive, not a fault; flagging it would inflate the error metrics and prepend
  `Error: ` on the wire. A refused call is not counted in `TotalToolCalls`/`ToolNames`: nothing ran.
- **`MaxToolCallIterations`** — from `Assistant:Tools:MaxToolRounds` (default 8; the old
  `MaxToolCallsPerQuestion` still binds and still wins when both are set). Running out no longer
  fails the run: `AgentRunner` makes one more completion with `ToolChoice = "none"` and the tools
  *still attached* (dropping them would change the serialized prefix and throw away the cached tool
  schemas), and prefixes `AgentRunner.MaxIterationsNotice` to whatever comes back.
  `AgentRunResult.StoppedOnMaxIterations` (and `AssistantPipelineResult.StoppedOnMaxIterations`)
  distinguishes that from a clean run; the old error is the fallback when the wrap-up call fails or
  returns nothing. The same helper recovers a blank final text — once per run, never twice.

Both follow-up calls are billed like any other, so they count in `LoopCount` and therefore in the
usage ledger's `LlmCalls`.

**Prompt-cache ordering is a correctness concern, not a nicety.** Tool schemas serialize at position
0, ahead of the system message, so `ToolRegistry.GetEnabledTools()` sorts by name (ordinal) — DI
registration order would change silently under a reshuffle and invalidate every breakpoint behind
it, at correct answers and ~10x the price. The system-prompt breakpoint's TTL comes from
`OpenRouter:PromptCacheTtl` (default `"1h"`, empty = the provider's 5m).

#### Governance (Phase 3)

- **`IToolRegistry` has no enable/disable.** `EnableProvider`/`DisableProvider` and
  `ProviderEntry.IsEnabled` were never called and are gone; `GetEnabledTools()` means "every
  registered tool", which is what it always effectively meant. Scoping is a decorator now:
  `FilteredToolRegistry` wraps a registry with an allow-list, filters `GetEnabledTools()` (re-sorting
  after, because tool order is part of the cached prefix) and **also refuses an out-of-set call** —
  a model that saw the tool in an earlier cached prefix will sometimes call it anyway.
  `FindProviderName(toolName)` attributes a tool without executing it, for the span below.
- **The guild allow-list is per guild, on purpose.** `AssistantGuildSettings.EnabledTools` (JSON
  array, `"[]"` = the house default set, never "no tools") → `IToolAccessResolver` →
  `GuildAssistantContextFactory` wraps the registry once per run. The unit is the guild because a
  guild's tool array is shared by all its callers, so this still leaves one prompt-cache prefix per
  guild. Per-*user* filtering would give every permission level its own prefix — which is why
  caller permission is `ToolContext.CanMutate`, checked inside a tool, instead.
- **`ToolContext.CanMutate`** is the replacement for the deleted `UserRoles`. Defaults to `false`,
  so an unpopulated context is read-only. The guild side takes it from the caller's Discord
  permissions in `AssistantMessageHandler.CallerCanMutate` (Manage Server or Administrator — the
  Bot layer is the only one with a Discord client, which is why it travels as a plain `bool`
  through `IAssistantService.AskQuestionAsync` → `IGuildAssistantContextFactory.CreateAsync`); the
  DM assistant and the feature-request conversation set it unconditionally. The refusal is
  `ToolPermissions.MutationForbidden(action)` — a *successful* result carrying a directive, same
  reasoning as the duplicate refusal. A tool authored as `IAgentTool` declares `Mutation` and
  `AgentToolProvider` applies the check before entering it (Phase 4); the hand-written providers
  still check it themselves at the top of each write.
- **Per-tool spans.** Every tool call gets `agent.tool {name}` on `AgentsActivitySource`, tagged
  `gen_ai.tool.name`, `gen_ai.tool.call.id`, `bot.tool.provider`, `bot.tool.result_chars` (measured
  after the cap — that is what the run pays for) and `bot.tool.outcome` ∈ `ok` / `failed_result` /
  `timeout` / `repeated_call` / `error` / `unknown_tool`. Refused duplicates and unknown tools get
  spans too; nothing runs in either case, which is exactly why they are worth seeing.
  **`failed_result` is the important one:** house style returns an expected failure as a
  *successful* result, so `ToolOutcomes.Classify` reads a top-level `error` string or a false
  `success`/`available`/`found` flag off the payload. `ToolResults.Error`/`.NotFound` emit exactly
  those keys, so a tool written through the helpers follows the convention without knowing it
  exists; a hand-written provider has to follow it deliberately, or its failures are invisible. Only `error` and `unknown_tool` set span status to Error — a timeout, a
  refused repeat and an expected failure are normal traffic and must not inflate the error rate.
- **`AssistantInteractionLog.ToolNames`** (comma-joined, 512 chars, truncated at a whole name)
  backs the metrics page's per-tool table, so it works with no trace backend deployed. The page
  pairs it with `ToolCatalog` so a tool that was never called still gets a row.

Two contract details the boundary forced:
- **`AgentContext.RunKind` is a plain `string?`**, not `LlmMode`. `LlmMode` is application taxonomy and stays in Core; the engine carries the label for correlation only and never switches on it. The pipeline populates it from `IAssistantContext.Mode`.
- **`ToolContext` has no `UserRoles` and no `ActiveGuildId`.** `UserRoles` was never populated or read and is gone; `CanMutate` is its replacement (above). "Active guild" is a DM-assistant concept and now rides in `ToolContext.Items`, an open-ended `Dictionary<string, object?>` the app owns; read and write it through `DmToolContextExtensions.GetActiveGuildId()`/`SetActiveGuildId()` (`Infrastructure/Services/LLM/`), never by spelling the key inline.

#### Skills (Phase 5)

**The rule is one sentence: a tool named by *any* available skill is hidden from the advertised tool
array until one of the skills naming it is loaded.** `SkillToolSet.Compose` applies it;
`AgentRunner` calls it once when building the request and again after any round whose
`ISkillActivationState.Activated` count changed. Everything else the registry holds is advertised as
it always was — so a surface keeps its common tools always-on and puts only rare, heavy ones behind
a skill.

- **A skill is a markdown file**, `docs/agents/skills/<surface>/<key>.md`, with `summary` (required —
  it is the whole basis for the model's decision, so a file without one is logged and ignored),
  optional `key` (defaults to the file name) and optional `tools` (comma-separated). Everything under
  the closing `---` is the instructions, returned verbatim as `load_skill`'s result. `SkillFile`
  parses; `SkillLibrary` reads a directory through `IPromptTemplate`, so a skill is cached and
  hot-reloaded exactly like a prompt file. Authoring guide: `docs/agents/skills/README.md`.
- **Two directories, not a `surfaces:` field.** A field would have put this bot's guild/DM taxonomy
  inside the engine's file format — the same mistake `[DmOnlyTool]` would have been. The surface is
  the directory, and the engine's loader only ever knows about *a* directory:
  `Assistant:Tools:SkillsPath` (default `docs/agents/skills/guild`, ships empty) and
  `DmAssistant:SkillsPath` (default `docs/agents/skills/dm`). Blank disables skills for that surface.
- **`load_skill` is an ordinary tool**, `Infrastructure/Services/LLM/Tools/LoadSkillTool.cs`, with an
  ordinary `ToolCatalog` entry (category **Skills**, `Guild | Dm`, default-on). It could not live in
  `DiscordBot.Agents`: the catalogue is what routes a tool to a surface. The engine's one concession
  is `ISkillActivationState.LoaderToolName` — when a surface has no skills the loop drops that name
  from the array, so an empty skill directory costs exactly what it did before Phase 5.
- **The session is reachable from both ends.** The loop reads it off `AgentContext.Skills`; the tool
  reads the same instance out of `ToolContext.Items` through `SkillToolContextExtensions`
  (`agent.skills`), because a tool is handed a `ToolContext` and nothing else. The context factories
  put it in both places. Don't resolve it from the container instead — the two ends would be the same
  object only for as long as nobody changed a lifetime.
- **Narrowing happens twice, and a skill can never widen reach.** `SkillSessionFactory`
  (`ISkillSessionFactory`) cuts each skill's tool list to what the run's registry advertises before
  the run starts, so the roster and `load_skill`'s answer are honest; `SkillToolSet.Compose` then
  builds from `GetEnabledTools()` and only ever subtracts. The guild registry is a
  `FilteredToolRegistry` over the guild's allow-list, so a skill naming a tool a guild turned off
  cannot turn it back on — the name is dropped with a `Debug` line and the model never hears of it.
- **Stickiness differs by surface, and that is the design.** DM is multi-turn:
  `DmSkillActivationStore` (`IMemoryCache`, 24h, keyed by user — same shape as the active guild)
  remembers the activations and `DmAssistantContextFactory` replays them, so from turn 2 the tools
  are advertised on the first call and the instructions are already in the prompt (`SkillRoster`
  re-renders the body of anything replayed — the tool result that carried it is not in the sliding
  window). `DmAssistantContext.RecordUsageAsync` writes the set back, and clears it when the run
  cleared the conversation. The guild assistant is single-turn, so nothing is ever replayed and a
  skill costs a round **every** time it is used.
- **Loading costs a prompt-cache write.** Re-composing the tool array rewrites position 0 of the
  request and invalidates the cached prefix for the rest of that run. Expect a cache-miss spike after
  a `load_skill`; that is the mechanism working, not a regression. It is also why the loop re-composes
  only when the activated set actually changed.
- **A skill body is a tool result and is capped** by `MaxToolResultChars` (8000). A longer body is
  truncated and the model reads a fragment. Keep skill files well under it.
- **Adding a skill is one markdown file** — no catalogue entry, no DI, no code. Adding an engine knob
  for skills still goes the usual way: `AgentContext` → `IAssistantContext` (`Skills` is a defaulted
  interface member, so a context that knows nothing about skills still compiles) → both contexts →
  the pipeline.

### Core (`Core/Interfaces/LLM/`, `Core/DTOs/Llm/Reporting/`)
- **Interfaces:** `IAssistantService`, `IToolAccessResolver` (per-guild tool allow-list), `ILlmModelCatalogService`, `IOpenRouterModelCatalogClient`, `ILlmModelRepository`, `ILlmModelResolver` (per-mode default resolution, see below), `ILlmUsageRepository` (usage ledger grouped queries — `Core/Interfaces/ILlmUsageRepository.cs`), `ILlmUsageRecorder` (non-blocking ledger write path — `Core/Interfaces/LLM/ILlmUsageRecorder.cs`)
- **DTOs (reporting only — the engine contracts are in `DiscordBot.Agents`):** `LlmModelCatalogFilter`, `LlmCatalogModel`, `LlmCatalogRefreshResult`, `LlmModelEnableResult`, `LlmModelDto`/`LlmModelListResponseDto`/`LlmModeDefaultDto` (portal-facing, `Core/DTOs/Llm/Reporting/LlmModelDto.cs`), `LlmResolvedModel`/`LlmCatalogPricing`/`LlmModelResolutionSource` (`Core/DTOs/Llm/Reporting/LlmResolvedModel.cs`), `LlmUsageQuery`/`LlmUsageTotals`/`LlmUsageByUser`/`LlmUsageByModel`/`LlmUsageByMode`/`LlmUsageByDay`/`LlmUsagePagedRecords` (`Core/DTOs/Llm/Reporting/LlmUsage*.cs`), `AssistantPipelineResult.Model`/`.UsageRecord`
- **Models:** `ToolCatalog`/`ToolCatalogEntry` (`Core/Models/Llm/`) — the human-facing tool table (category, label, description, `ToolScopes`, default-on) behind the settings checklist and the metrics table; `ToolCatalog.Describe` synthesises an **Other** entry for an uncatalogued tool rather than dropping it. Add a new tool here when you add it to a provider.
- **Entities:** `AssistantGuildSettings` (has an `EnabledTools` JSON column), `AssistantInteractionLog` (has nullable `Model` and `ToolNames` columns), `DmAssistantInteractionLog` (same), `AssistantUsageMetrics`, `LlmModel` (local OpenRouter catalog row, PK = slug), `LlmUsageRecord` (usage ledger — one row per user message across every `LlmMode`; see "Usage Ledger" below)
- **Config:** `AssistantOptions`, `LlmOptions` (`Llm:CatalogRefreshHours`, `Llm:CatalogRefreshInitialDelayMinutes`, `Llm:UsageQueueCapacity`, `Llm:RetentionSweepIntervalHours`, `Llm:RetentionBatchSize`, `Llm:RetentionSweepInitialDelayMinutes`)
- **Enums:** `LlmMode` (`GuildAssistant`/`DmAssistant`/`FeatureRequests`) with its `LlmModeSettings` static helper (`KeyFor`, `LabelFor`, `All`) — `Core/Enums/LlmMode.cs`; `LlmCostSource` (`Billed`/`Estimated`) — `Core/Enums/LlmCostSource.cs`

### Infrastructure (`Infrastructure/Services/LLM/`)
- `Abstractions/LLM/` — the assistant abstractions whose signatures are made of engine types, and which therefore cannot live in Core: `IAssistantContext`, `IAssistantMessagePipeline`, `IDmToolProvider`, `IGuildAssistantContextFactory`, `IDmAssistantContextFactory`, `ISkillSessionFactory`
- `DmToolContextExtensions` — typed access to the DM assistant's entries in `ToolContext.Items`
- `SkillToolContextExtensions` — the same, for the run's skill session (`agent.skills`)
- `SkillSessionFactory` (`ISkillSessionFactory`) — builds a run's `SkillSession` and narrows each
  skill's tools to what that run's registry advertises
- `DmSkillActivationStore` (`IDmSkillActivationStore`) — the DM surface's cross-turn skill memory
- `ToolAccessResolver` — resolves a guild's allowed tool set (saved allow-list, else the house default), cached per guild and invalidated by `AssistantGuildSettingsService.UpdateSettingsAsync`, the single point every settings write goes through
- `ToolPermissions.MutationForbidden` — the shared refusal a write tool returns when `ToolContext.CanMutate` is false. Lives here, not in Agents: the wording is this bot's voice and the policy is this bot's.
- `LlmModelCatalogService` — Local model catalog: refresh (upsert by slug), filtered/sorted listing, enable/disable allowlist. Audited (`AuditLogCategory.Configuration`).
- `OpenRouter/OpenRouterModelCatalogClient` — **Second, separate** typed `HttpClient` against OpenRouter's `GET /models` (not `OpenRouterLlmClient`, which only does chat completions); same auth/attribution headers, no retry loop
- `Data/Repositories/LlmModelRepository` — `LlmModel` persistence (filtered query, enabled list, last-refresh, mark-unavailable)
- `LlmModelResolver` (singleton) — the **single resolution path** for a mode's effective model slug: `ISettingsService.GetStoredValueAsync(LlmModeSettings.KeyFor(mode))` (DB override) → bound `IOptions<T>.Value` for that mode → `OpenRouterOptions.DefaultModel` (last-resort fallback). Caches the result per `LlmMode` in a `ConcurrentDictionary`; the cache is cleared when `ISettingsService.SettingsChanged.UpdatedKeys` contains that mode's setting key, so a save through the AI Models tab takes effect on the next message with no restart. Resolves the scoped `ILlmModelRepository` via `IServiceScopeFactory` per call (same pattern as `SettingsService`) to also report the slug's `IsEnabled`/`IsAvailable` state and, when the catalog reports a price, an `LlmCatalogPricing` for the cost fallback. Logs a once-per-slug warning (not per message) when the resolved slug is not enabled — it still sends the request; the allowlist is enforced at save time, not send time. Registered ungated in `AssistantServiceExtensions` (needs no API key).
- `Data/Repositories/LlmUsageRepository` — `LlmUsageRecord` persistence: `AddRangeAsync` (bulk insert, used by the queue processor), `DeleteOlderThanAsync`/`DeleteByUserAsync`/`CountByUserAsync` (retention and purge build on these), and the grouped dashboard queries (`GetTotalsAsync`, `GetByUserAsync`, `GetByModelAsync`, `GetByModeAsync`, `GetByDayAsync`, `GetRecordsAsync`) over an `LlmUsageQuery` (date range + optional guild/mode/user filter). **Cost sums go through `double`, not `decimal`:** the SQLite EF provider refuses to translate `Sum(decimal)`/`OrderBy(decimal)` into SQL at all (`NotSupportedException`) — every cost aggregate is `Math.Round((decimal)g.Sum(r => (double)r.CostUsd), 8)`, and the by-user/by-model/by-mode breakdowns sort client-side after materializing, so one query shape works on both SQLite and PostgreSQL. Do not "fix" this back to a plain `Sum(r => r.CostUsd)` — it will build and then throw at runtime against SQLite.

### Tools
- `Services/LLM/Tools/` — individually authored `IAgentTool`s, one file each. Currently the DM
  assistant's memory tools: `SaveNoteTool`, `SearchNotesTool`, `GetNoteTool`, `ListNotesTool`,
  `DeleteNoteTool`, sharing `NotePayloads` for the one shape a note takes in a result. They replaced
  `MemoryToolProvider` + `MemoryTools` (417 lines → 364, all of it now XML-documented, and no
  provider name/description, no `switch`, no try/catch wrapper and no DI line).
- `Providers/CataloguedAgentToolProvider` + `GuildAgentToolProvider` / `DmAgentToolProvider` — the
  two surfaces over those tools, registered as `IToolProvider` and `IDmToolProvider`
- `Services/LLM/Tools/LoadSkillTool` — `load_skill`. Reads the run's session out of
  `ToolContext.Items`, activates, and returns the skill's instructions; the loop does the rest.

### Tool Providers (the ten not yet converted)
- `Providers/DocumentationToolProvider` — Maps 13 features to doc files
- `Bot/Services/LLM/Providers/UserGuildInfoToolProvider` — User profiles, guild info, roles
- `Bot/Services/LLM/Providers/RatWatchToolProvider` — Rat Watch leaderboards, stats
- Implementations in `Implementations/DocumentationTools`, `RatWatchTools`, `UserGuildInfoTools`

### Bot Layer
- `Services/AssistantService` — High-level orchestration (guild)
- `Services/DmAssistantService` — High-level orchestration (owner DM)
- `Handlers/AssistantMessageHandler` — Discord message handler (guild)
- `Handlers/DmAssistantMessageHandler` — Discord message handler (DM)
- `Pages/Guilds/AssistantSettings.cshtml` — Per-guild config
- `Pages/Guilds/AssistantMetrics.cshtml` — Usage metrics dashboard
- **Repos:** `AssistantGuildSettingsRepository`, `AssistantInteractionLogRepository`, `AssistantUsageMetricsRepository`
- `Controllers/LlmModelsController` — `api/admin/llm-models` (`RequireAdmin`): catalog list/filter, refresh, enable/disable (slug in the request body — OpenRouter slugs contain `/`); `GetDefaults` delegates entirely to `ILlmModelResolver` (one resolution path, shared with message-send time)
- `Services/LLM/LlmCatalogRefreshService` — `MonitoredBackgroundService`; periodic catalog refresh on `Llm:CatalogRefreshHours` (default 24h, `0` disables), first attempt delayed `Llm:CatalogRefreshInitialDelayMinutes` (default 5) after startup; registered only when `OpenRouter:ApiKey` is present
- `Services/LLM/LlmUsageRecorder` (singleton) — `ILlmUsageRecorder` over a bounded `Channel<LlmUsageRecord>` (capacity `Llm:UsageQueueCapacity`, default 10,000, `DropOldest`), same posture as `AuditLogQueue`; `Record` is a non-blocking `TryWrite`. Also registered under its concrete type (in addition to the interface) because `LlmUsageRecordProcessor` needs its internal `DequeueAsync`/`Count` members.
- `Services/LLM/LlmUsageRecordProcessor` — `MonitoredBackgroundService` draining `LlmUsageRecorder` in batches via a scoped `ILlmUsageRepository.AddRangeAsync`, mirroring `AuditLogQueueProcessor`. Both are registered **ungated** in `AssistantServiceExtensions` (no API key needed) — recording must work whenever any assistant mode runs, and the processor is harmless idling with none.
- `Services/LLM/AssistantInteractionLogRetentionService` — `MonitoredBackgroundService`, daily sweep (`Llm:RetentionSweepIntervalHours`, default 24, `0` disables → `SetStatus("Disabled")`) of three tables that nobody was cleaning up before this: guild `AssistantInteractionLog`, DM `DmAssistantInteractionLog`, and `LlmUsageRecords`, each via its repository's batched `DeleteOlderThanAsync(cutoff, batchSize, ct)` overload — looped until a batch returns fewer rows than `Llm:RetentionBatchSize` (capped at 1000), with a brief inter-batch delay, mirroring `SoundPlayLogRetentionService`. Guild logs and the ledger share `Assistant:Privacy:InteractionLogRetentionDays`; DM logs use `DmAssistant:InteractionLogRetentionDays`. Each table's sweep is skipped independently when its retention window is `0` or less — one disabled table doesn't disable the others. Startup delay and inter-batch delay are both driven by an injected `TimeProvider` (`Llm:RetentionSweepInitialDelayMinutes`, default 5, mirrors `CatalogRefreshInitialDelayMinutes`), registered ungated in `AssistantServiceExtensions`. Does **not** sweep `AssistantUsageMetrics`/`DmAssistantUsageMetrics` (daily aggregates, out of scope) or `LlmModel` (the catalog).
- `Pages/Admin/Settings.cshtml` "AI Models" tab (`ai-models-settings`) — model catalog table (read-only, driven by `LlmModelsController`) **plus** an editable per-mode defaults panel. Unlike the catalog table, the defaults panel **is** a `SettingCategory` (`SettingCategory.AiModels`) and goes through the normal settings path: `SettingsViewModel.AiModelsSettings` (three `SettingDto`s, keys = `LlmModeSettings.KeyFor(mode)`, `AllowedValues` = the currently enabled catalog slugs sorted, plus the current value if it isn't among them) is saved via `SettingsSectionService.SaveCategoryAsync("AiModels", ...)` like any other category — audit logging and reset-to-default come for free. `SettingsSectionService.ValidateAiModelSelectionsAsync` runs before the write and rejects a submitted slug that isn't in the catalog, isn't `IsEnabled`, or doesn't `SupportsTools`.

### Assistant Message Pipeline (`Infrastructure/Services/LLM/`)
`AssistantService` (guild) and `DmAssistantService` (DM) share one message-handling
pipeline instead of duplicating it. `IAssistantContext` (implemented by `GuildAssistantContext`
and `DmAssistantContext`) carries the scope-specific bits — cache-key prefix, rate limit,
tool registry, conversation history, prompt loading, and usage/interaction logging — while
`AssistantMessagePipeline` runs the shared flow (build `AgentContext`, invoke `IAgentRunner`,
price the usage, truncate the response) identically for both. `AssistantRateLimiter` is the
shared cache-backed rate limiter (namespaced by prefix so guild and DM windows never collide);
`IAssistantAccessGate` bundles the guild's enable/consent/channel checks; `IGuildAssistantContextFactory`
and `IDmAssistantContextFactory` build each scope's context so the services themselves stay
thin. Both factories are now `async` (`CreateAsync`) because they resolve the mode's model slug
via `ILlmModelResolver` on every call and pass it (plus any catalog pricing) into the context.
When changing rate limiting, cost calculation, response truncation, or the agentic-loop invocation,
change it once in `AssistantRateLimiter`/`AssistantMessagePipeline` — not in both services.

### Usage Ledger (`LlmUsageRecord`, `Core/Interfaces/ILlmUsageRepository.cs`, `Infrastructure/Services/LLM/`, `Bot/Services/LLM/`)

One `LlmUsageRecord` row per user message, across every `LlmMode` — tokens, cost, `CostSource`
(`Billed`/`Estimated`), which model answered, and (when the mode logs one) a link to that mode's
own interaction-log row via `InteractionLogId`. **Record usage once in the pipeline:**
`AssistantMessagePipeline.RunAsync` builds the `LlmUsageRecord` (shared by the guild and DM
assistants — `Mode` comes from `IAssistantContext.Mode` (the engine's `AgentContext.RunKind` carries only a correlation label), `Model` from `AgentRunResult.Model` or
the requested slug, `CostSource` from whether `TotalUsage.EstimatedCost` was reported) and hands
it back on `AssistantPipelineResult.UsageRecord`; `GuildAssistantContext`/`DmAssistantContext.RecordUsageAsync`
set `InteractionLogId` after their own interaction-log `AddAsync` (null when `LogInteractions` is
off) and then call `ILlmUsageRecorder.Record`. `FeatureRequestConversationService.RunAgentAsync`
does not go through the shared pipeline, so it builds and records its own row per agent turn
(`Mode = LlmMode.FeatureRequests`, `InteractionLogId = null` — feature requests keep no
interaction log); its cost is billed-`TotalUsage.EstimatedCost` when present, else the resolved
model's catalog pricing (`ILlmModelResolver`'s `LlmResolvedModel.Pricing`) falling back
per-field to the **guild assistant's** `AssistantCostOptions` rates (feature requests have no cost
options of their own — this is the closest existing fallback). Never add a second place that
builds or records an `LlmUsageRecord` — extend the pipeline (or the feature-request service, for
its one exception) instead.

`GuildAssistantContext`/`DmAssistantContext` accept an optional trailing `ILlmUsageRecorder?`
constructor parameter (default falls back to `NoOpUsageRecorder`, a silent drop-everything
implementation) so pre-ledger direct-construction tests keep compiling without passing one.
Production DI always supplies the real recorder via the context factories.

This PR ships the ledger's write path, read path (`ILlmUsageRepository`'s grouped queries), and
the entity/migrations. The `/admin/llm-usage` page / `LlmUsageController` / guild-metrics
"cost by user" table are separate work building on `ILlmUsageRepository`.

**Retention, purge, and export are wired up** (previously the guild/DM assistant interaction
logs had no cleanup at all — `IAssistantInteractionLogRepository.DeleteOlderThanAsync` and
`IDmAssistantInteractionLogRepository.DeleteOlderThanAsync` existed but had no caller):
`Services/LLM/AssistantInteractionLogRetentionService` (see above) sweeps all three tables on
`Llm:RetentionSweepIntervalHours`; `UserPurgeService.PurgeUserDataAsync`/`PreviewPurgeAsync` delete
(and count) the user's `LlmUsageRecords`, `AssistantInteractionLogs`, `DmAssistantInteractionLogs`,
and `DmAssistantUsageMetrics` rows inside the same transaction as the other tables;
`UserDataExportService.ExportUserDataAsync` includes `llm_usage_records.json`,
`assistant_interaction_logs.json`, `dm_assistant_interaction_logs.json` (question/response text
included — it's the user's own data), and `dm_assistant_usage_metrics.json` (so export and purge
agree on what tables exist for a user) via
`ExportLlmUsageRecordsAsync`/`ExportAssistantInteractionsAsync`/`ExportDmAssistantUsageMetricsAsync`.
`BulkPurgeEntityType`/`BulkPurgeService` were **not** extended with these tables (out of scope for
that change) — it would be natural to add them there too, since bulk purge already exists for
other per-user tables, but that's for a future PR to decide.

## Adding a New Tool

Two files, and one of them already exists.

1. One class implementing `IAgentTool` in `Infrastructure/Services/LLM/Tools/` (or
   `Bot/Services/LLM/Tools/` if it needs Discord.NET): a static `LlmToolDefinition` built with
   `ToolInput.ObjectSchema`/`.Schema`, and an `InvokeAsync` that reads its arguments with
   `ToolInput` and returns through `ToolResults`.
2. An entry in `ToolCatalog` (`Core/Models/Llm/`) with its category, label, admin-facing
   description and `ToolScopes`. **This is load-bearing, not a footnote:** the catalogue's scope is
   what routes the tool to the guild or DM surface, so a tool without an entry is advertised
   nowhere (and logs a warning saying so).
3. No DI edit. `AddAgentTools` scans Infrastructure and Bot from both assistant registrations.
4. If it writes, declare `Mutation` — the phrase after "isn't allowed to". `AgentToolProvider`
   refuses the call before entering the tool; do not also write the check by hand.
5. Report an expected failure with `ToolResults.Error` / `.NotFound`, which is what makes it
   `failed_result` rather than invisible in the traces.
6. Ship it dark with `[OptInTool("Section:Enabled")]` if it needs a flag.

Write an `IToolProvider` instead only when several tools share expensive state worth constructing
once for the group. Full pattern in `docs/architecture/patterns.md` § Agent Tool Authoring.

## Gotchas

- **API key in User Secrets:** `OpenRouter:ApiKey` — never commit. Without it the LLM services are not registered at all (so migrations run without a key); both `AddAssistant` and `AddDmAssistant` gate on it.
- **A tool's schema bytes are the prompt cache's prefix.** The tool array serializes at position 0
  of the request, so editing a description or reordering a schema's properties invalidates every
  breakpoint behind it — correct answers at ~10x the input price, with no other symptom. Build the
  definition from a `static readonly` field, and when converting a tool, pin its schema against the
  old literal in a test (`MemoryAgentToolsTests` does).
- **Tool execution is synchronous within the agent loop** — long-running tools block the response
- **Token limits:** Conversation history can grow large; be mindful of context window
- **DocumentationToolProvider** maps feature names to doc files — update mapping when docs change
- **OpenRouterMessageMapper** translates between internal DTOs and the OpenAI-compatible wire shape. The three traps: the system prompt is the **first message**, not a top-level parameter; each tool result is its own `role:"tool"` message carrying `tool_call_id`, not a block on a user turn; and tool-call arguments cross the wire as a **JSON string**, not an object.
- **No LLM SDK.** Build LLM work on `ILlmClient` and the owned wire records in `Services/LLM/OpenRouter/` — do not add a vendor SDK or `Microsoft.Extensions.AI`.
- **Model names are OpenRouter slugs** (`anthropic/claude-sonnet-4`), not vendor model IDs. Full list at https://openrouter.ai/models.
- **`provider.require_parameters` is sent whenever tools are present** — without it a slug can route to a provider with no native function calling, and the model then emits a tool-call-shaped string into the user-visible reply. The cost: a parameter the model doesn't support is refused with 404 "No endpoints found that can handle the requested parameters" instead of being dropped. Reasoning models (GPT-5 family, `openai/gpt-5.6-luna`) reject `temperature`, so `OpenRouterLlmClient` resends once without it and records the slug in the singleton `OpenRouterParameterSupportCache`. If a new model 404s that way for a *different* parameter, extend that fallback rather than dropping `require_parameters`.
- **Prompt caching is pass-through:** honoured for Claude-family slugs, silently ignored elsewhere (cached tokens read 0). A broken cache prefix still answers correctly, just at roughly 10x the input price — watch `CachedTokens` on the metrics page after changing prompt construction.
- **Cost:** OpenRouter reports real billed `usage.cost`, which wins over both fallback rate sources. When it's absent, `GuildAssistantContext`/`DmAssistantContext.CostRates` prefer the resolved model's catalog pricing (`LlmResolvedModel.Pricing`, per-price-field — a missing individual price still falls back to config) over the configured per-million rates; the configured rates are the last resort when there's no catalog row at all.
- **A catalog refresh never enables a model.** `LlmModelCatalogService.RefreshAsync` upserts descriptive fields and marks missing slugs `IsAvailable = false`, but `IsEnabled` (the admin allowlist) is only ever changed by an explicit `SetEnabledAsync` call — with exactly one exception: the very first refresh ever, on an empty table, bootstrap-enables the slugs the three modes (`Assistant:Sampling:Model`, `DmAssistant:Model`, `FeatureRequests:RequirementsGatheringModel`) were already configured to use, so upgrades keep working. Do not "helpfully" enable models on refresh when touching this service.
- **Two separate OpenRouter HTTP clients.** `OpenRouterLlmClient` (chat completions) and `OpenRouterModelCatalogClient` (`GET /models`, the catalog) are independent owned typed `HttpClient`s with their own wire records — do not route catalog fetches through the chat client or vice versa.
- **`ILlmModelResolver` is the only place a mode's model slug is resolved.** `GuildAssistantContextFactory`, `DmAssistantContextFactory`, `FeatureRequestConversationService`, and `LlmModelsController.GetDefaults` all call `ResolveAsync(LlmMode)` instead of reading `IOptions<AssistantOptions>.Value.Sampling.Model` (or the DM/feature-request equivalents) directly. If resolution order, caching, or the not-enabled warning needs to change, change it once in `LlmModelResolver` — never add a second place that reads the mode options or the DB setting. `GuildAssistantContext`/`DmAssistantContext` still accept the resolved slug (and `LlmCatalogPricing`) as optional trailing constructor parameters — omitting them falls back to the mode's `IOptions<T>` value, which is what keeps existing direct-construction tests compiling unchanged.
- **A catalog refresh never enables a model** (see `LlmModelCatalogService.RefreshAsync` below) **and disabling one is refused at save time, not resolve time.** `ILlmModelResolver` still returns a disabled or unknown slug — it only logs a once-per-slug warning — because an empty allowlist on a fresh install must not take the assistant offline. The allowlist is enforced by `SettingsSectionService.ValidateAiModelSelectionsAsync` when an admin saves the AI Models tab, and by `LlmModelCatalogService.SetEnabledAsync` refusing to disable a mode's current default.
- **`LlmUsageRecord` has no FK to `Users`/`Guilds`.** Deliberate: a DM user or a feature-request guild may have no row in those tables, and a `User`/`Guild` delete must never cascade away cost history. `UserId`/`GuildId` are plain columns with covering indexes, not relationships. Retention and purge (once wired) must delete by value (`DeleteOlderThanAsync`/`DeleteByUserAsync`), not rely on a cascade.
- **`ILlmUsageRepository`'s cost aggregates sum through `double`, not `decimal`** (`Math.Round((decimal)g.Sum(r => (double)r.CostUsd), 8)`) and sort by cost client-side after materializing. SQLite's EF provider throws `NotSupportedException` on `Sum(decimal)` and on `OrderBy`/`ORDER BY` over a decimal expression — there is no plain-decimal version of these queries that works against both providers as one code path.
