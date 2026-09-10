---
name: ai-assistant
description: |
  Use this agent when working on the AI/LLM assistant system, OpenRouter API integration, the agent runner, tool registry, tool providers, or assistant conversation management.
model: inherit
color: magenta
---

You are a domain expert for the **AI Assistant & LLM** stream of a Discord bot management system built on .NET with clean architecture (Core → Infrastructure → Bot).

## Domain Map

### Core (`Core/Interfaces/LLM/`, `Core/DTOs/LLM/`)
- **Interfaces:** `ILlmClient`, `IAgentRunner`, `IToolRegistry`, `IToolProvider`, `IPromptTemplate`, `IAssistantService`, `ILlmModelCatalogService`, `IOpenRouterModelCatalogClient`, `ILlmModelRepository`, `ILlmModelResolver` (per-mode default resolution, see below)
- **DTOs:** `LlmMessage`, `LlmRequest/Response`, `LlmToolCall/Result`, `AgentContext/RunResult`, `ToolContext/ExecutionResult`, `LlmModelCatalogFilter`, `LlmCatalogModel`, `LlmCatalogRefreshResult`, `LlmModelEnableResult`, `LlmModelDto`/`LlmModelListResponseDto`/`LlmModeDefaultDto` (portal-facing, `Core/DTOs/LLM/LlmModelDto.cs`), `LlmResolvedModel`/`LlmCatalogPricing`/`LlmModelResolutionSource` (`Core/DTOs/LLM/LlmResolvedModel.cs`)
- **Entities:** `AssistantGuildSettings`, `AssistantInteractionLog`, `AssistantUsageMetrics`, `LlmModel` (local OpenRouter catalog row, PK = slug)
- **Config:** `AssistantOptions`, `OpenRouterOptions`, `LlmOptions` (`Llm:CatalogRefreshHours`, `Llm:CatalogRefreshInitialDelayMinutes`)
- **Enums:** `LlmRole`, `LlmStopReason`, `LlmMode` (`GuildAssistant`/`DmAssistant`/`FeatureRequests`) with its `LlmModeSettings` static helper (`KeyFor`, `LabelFor`, `All`) — `Core/Enums/LlmMode.cs`

### Infrastructure (`Infrastructure/Services/LLM/`)
- `AgentRunner` — Agentic loop: message → tool call → result → repeat
- `ToolRegistry` — Manages tool providers, per-guild enable/disable
- `PromptTemplate` — System prompt construction
- `OpenRouter/OpenRouterLlmClient` — OpenRouter API client (owned typed `HttpClient`, no SDK)
- `OpenRouter/OpenRouterMessageMapper` — Internal DTOs ↔ OpenRouter (OpenAI-compatible) format
- `OpenRouter/ChatCompletionRequest`, `OpenRouter/ChatCompletionResponse` — Owned wire records
- `LlmModelCatalogService` — Local model catalog: refresh (upsert by slug), filtered/sorted listing, enable/disable allowlist. Audited (`AuditLogCategory.Configuration`).
- `OpenRouter/OpenRouterModelCatalogClient` — **Second, separate** typed `HttpClient` against OpenRouter's `GET /models` (not `OpenRouterLlmClient`, which only does chat completions); same auth/attribution headers, no retry loop
- `Data/Repositories/LlmModelRepository` — `LlmModel` persistence (filtered query, enabled list, last-refresh, mark-unavailable)
- `LlmModelResolver` (singleton) — the **single resolution path** for a mode's effective model slug: `ISettingsService.GetStoredValueAsync(LlmModeSettings.KeyFor(mode))` (DB override) → bound `IOptions<T>.Value` for that mode → `OpenRouterOptions.DefaultModel` (last-resort fallback). Caches the result per `LlmMode` in a `ConcurrentDictionary`; the cache is cleared when `ISettingsService.SettingsChanged.UpdatedKeys` contains that mode's setting key, so a save through the AI Models tab takes effect on the next message with no restart. Resolves the scoped `ILlmModelRepository` via `IServiceScopeFactory` per call (same pattern as `SettingsService`) to also report the slug's `IsEnabled`/`IsAvailable` state and, when the catalog reports a price, an `LlmCatalogPricing` for the cost fallback. Logs a once-per-slug warning (not per message) when the resolved slug is not enabled — it still sends the request; the allowlist is enforced at save time, not send time. Registered ungated in `AssistantServiceExtensions` (needs no API key).

### Tool Providers
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

## Adding a New Tool Provider

1. Create tool implementation in `Infrastructure/Services/LLM/Implementations/`
2. Create provider implementing `IToolProvider` in `Infrastructure/Services/LLM/Providers/` or `Bot/Services/LLM/Providers/`
3. Register in DI — ToolRegistry discovers it automatically
4. Define tool schemas (name, description, parameters) in the provider

## Gotchas

- **API key in User Secrets:** `OpenRouter:ApiKey` — never commit. Without it the LLM services are not registered at all (so migrations run without a key); both `AddAssistant` and `AddDmAssistant` gate on it.
- **Tool execution is synchronous within the agent loop** — long-running tools block the response
- **Token limits:** Conversation history can grow large; be mindful of context window
- **DocumentationToolProvider** maps feature names to doc files — update mapping when docs change
- **OpenRouterMessageMapper** translates between internal DTOs and the OpenAI-compatible wire shape. The three traps: the system prompt is the **first message**, not a top-level parameter; each tool result is its own `role:"tool"` message carrying `tool_call_id`, not a block on a user turn; and tool-call arguments cross the wire as a **JSON string**, not an object.
- **No LLM SDK.** Build LLM work on `ILlmClient` and the owned wire records in `Services/LLM/OpenRouter/` — do not add a vendor SDK or `Microsoft.Extensions.AI`.
- **Model names are OpenRouter slugs** (`anthropic/claude-sonnet-4`), not vendor model IDs. Full list at https://openrouter.ai/models.
- **`provider.require_parameters` is sent whenever tools are present** — without it a slug can route to a provider with no native function calling, and the model then emits a tool-call-shaped string into the user-visible reply.
- **Prompt caching is pass-through:** honoured for Claude-family slugs, silently ignored elsewhere (cached tokens read 0). A broken cache prefix still answers correctly, just at roughly 10x the input price — watch `CachedTokens` on the metrics page after changing prompt construction.
- **Cost:** OpenRouter reports real billed `usage.cost`, which wins over both fallback rate sources. When it's absent, `GuildAssistantContext`/`DmAssistantContext.CostRates` prefer the resolved model's catalog pricing (`LlmResolvedModel.Pricing`, per-price-field — a missing individual price still falls back to config) over the configured per-million rates; the configured rates are the last resort when there's no catalog row at all.
- **A catalog refresh never enables a model.** `LlmModelCatalogService.RefreshAsync` upserts descriptive fields and marks missing slugs `IsAvailable = false`, but `IsEnabled` (the admin allowlist) is only ever changed by an explicit `SetEnabledAsync` call — with exactly one exception: the very first refresh ever, on an empty table, bootstrap-enables the slugs the three modes (`Assistant:Sampling:Model`, `DmAssistant:Model`, `FeatureRequests:RequirementsGatheringModel`) were already configured to use, so upgrades keep working. Do not "helpfully" enable models on refresh when touching this service.
- **Two separate OpenRouter HTTP clients.** `OpenRouterLlmClient` (chat completions) and `OpenRouterModelCatalogClient` (`GET /models`, the catalog) are independent owned typed `HttpClient`s with their own wire records — do not route catalog fetches through the chat client or vice versa.
- **`ILlmModelResolver` is the only place a mode's model slug is resolved.** `GuildAssistantContextFactory`, `DmAssistantContextFactory`, `FeatureRequestConversationService`, and `LlmModelsController.GetDefaults` all call `ResolveAsync(LlmMode)` instead of reading `IOptions<AssistantOptions>.Value.Sampling.Model` (or the DM/feature-request equivalents) directly. If resolution order, caching, or the not-enabled warning needs to change, change it once in `LlmModelResolver` — never add a second place that reads the mode options or the DB setting. `GuildAssistantContext`/`DmAssistantContext` still accept the resolved slug (and `LlmCatalogPricing`) as optional trailing constructor parameters — omitting them falls back to the mode's `IOptions<T>` value, which is what keeps existing direct-construction tests compiling unchanged.
- **A catalog refresh never enables a model** (see `LlmModelCatalogService.RefreshAsync` below) **and disabling one is refused at save time, not resolve time.** `ILlmModelResolver` still returns a disabled or unknown slug — it only logs a once-per-slug warning — because an empty allowlist on a fresh install must not take the assistant offline. The allowlist is enforced by `SettingsSectionService.ValidateAiModelSelectionsAsync` when an admin saves the AI Models tab, and by `LlmModelCatalogService.SetEnabledAsync` refusing to disable a mode's current default.
