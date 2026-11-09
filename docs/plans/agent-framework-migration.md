# Plan: move the assistant onto cpike.Agent and retire `DiscordBot.Agents`

> Status: **in progress**; decisions D1–D5 agreed; P1 done. Reassessed on 2026-11-09 against discordbot
> `b5d7684` (after the repo cleanup) and cpike-agent-framework `77b5f1f` (`v0.2.0-beta.4`).
>
> **What the reassessment changed.** The framework has shipped `v0.2.0-beta.1` to `beta.4`, so
> publishing (P2) is no longer the blocker: only the three Core gaps (H1–H3, renamed from G1–G3 to stay
> clear of the framework's own G-series) still need a release. The framework's other new work since the
> first plan (F2–F5, F8, G1–G6) is all in `cpike.Agent.Hosting` and `Contracts`: partial replies,
> named failures, delivery observers, history budgets, mode labellers, attachment type resolution. None
> of it touches the `AgentService` path this plan uses, so D1 (Core, not Hosting) stands. discordbot's
> cleanup deleted the Grafana dashboards, the alert rules, the Grafana docs and `.claude/agents/`, so the
> dashboard rework in §4.9 and the agent-definition update in PR 5 are gone.

## Summary

`src/DiscordBot.Agents` is an in-repo agent engine: the loop (`AgentRunner`), tool contracts,
`ToolRegistry`, skills, `PromptTemplate` and the OpenRouter client. `cpike.Agent` is the same idea,
generalised into packages (`cpike.Agent.OpenRouter`, `cpike.Agent.Core`, and optionally `.Hosting`,
`.Testing`, `.Evals`). The goal is to run the guild, DM and feature-request assistants on
`cpike.Agent.Core`'s `AgentService`, then delete `DiscordBot.Agents` and its tests.

The good news: the seam is narrow. Production code calls the runner in exactly two places
(`AssistantMessagePipeline.cs:46` and `FeatureRequestConversationService.cs:229`), and nothing
outside the engine calls `ILlmClient`. The usage ledger, interaction logs, model catalogue, rate
limiter, consent, settings pages and Discord handlers do not touch engine types and stay as they are.

The work is in three places:

1. **Prerequisites:** discordbot had to move from .NET 8 to .NET 10 (done, P1); the three Core gaps
   need a framework release (P2); the feed is private (P3).
2. **The tool surface:** 6 `IAgentTool` classes and 11 `IToolProvider`s (about 27 tools) use the
   engine's tool contract (`ToolContext` argument, `ToolExecutionResult` return, `Mutation`), which
   differs from the framework's (`AgentRunScope` from DI, `string` return, `RequiresEdit`).
3. **A handful of behaviours** the engine has and the framework does not, or does differently:
   per-run temperature and output-token ceiling, per-tool timeout, the `clear_conversation` signal,
   per-surface tool sets from `ToolCatalog`, and the engine's metric names.

Recommended path: upgrade to .NET 10 first (its own PR), publish a framework release with H1–H3, then swap the runner
behind the existing `IAssistantMessagePipeline` and port every tool to the framework contract in
the same PR, with no transitional adapter; a follow-up PR deletes the old engine. Use
`cpike.Agent.Core` directly, not the HTTP `TurnPipeline` (rationale in [Decisions](#decisions)).

---

## 1. How the two engines line up

| discordbot (`DiscordBot.Agents`) | cpike.Agent | Notes |
| --- | --- | --- |
| `IAgentRunner.RunAsync(string, AgentContext)` | `AgentService.RunAsync(AgentRunRequest)`, scoped, one DI scope per run | Framework throws on a failed model call; the pipeline catches and maps to `Success = false` as today. |
| `AgentContext.SystemPrompt` | A mode (`AgentDefinition`) from `IAgentPromptStore`, or `AgentRunRequest.CustomAgent` | Per-turn text (guild id) must move to a `PromptPart` with `PromptPlacement.TurnContext`. |
| `AgentContext.ConversationHistory` (`List<LlmMessage>`) | `AgentRunRequest.History` (`ConversationTurn(IsUser, Text)`) | DM and feature-request history map 1:1. Guild has none. |
| `AgentContext.Model` | `AgentRunRequest.Model` | `LlmModelResolver` keeps choosing the slug. |
| `AgentContext.MaxTokens` | `Agent:MaxTokensPerTurn` (process-wide) | **Gap H1**: no per-run ceiling. Guild uses 512, DM 4096, feature requests 1024. |
| `AgentContext.Temperature` | none, by design (`ModelRouter.cs:259`: deprecated on current models) | **Decision D3**: drop it. |
| `MaxToolCallIterations` | `AgentRunRequest.MaxToolRounds` | Direct. |
| `MaxToolResultChars` | `AgentRunRequest.MaxToolResultChars` | Direct. Framework default 24000; discordbot uses 8000. |
| `DuplicateToolCallLimit` | `Agent:DuplicateToolCallLimit` | Process-wide; every surface uses 3 today, so fine. |
| `ToolExecutionTimeoutMs` (10 s, per tool) | none | **Gap H2.** |
| `ToolContext { UserId, GuildId, ChannelId, MessageId, CanMutate, Items }` | `AgentRunScope { UserKey, UserName, CanEditData, IsAdmin, Items }` | Discord ids go in `Items` behind a typed accessor (see §4.2). |
| `IAgentTool.Mutation` + `AgentToolProvider` refusal | `AgentToolDefinition.RequiresEdit`, refusal `edit_forbidden` | The refusal changes shape (§4.3). |
| `IToolProvider` / `ToolRegistry` / `FilteredToolRegistry` | Registered `IAgentTool`s, mode `tools:` lists, `AgentRunRequest.DisabledTools`, `IAgentToolSource` | Per-guild allow-list → `DisabledTools`. |
| `OptInToolAttribute` | `cpike.Agent.Tools.OptInToolAttribute` | Same idea; unused in discordbot today. |
| `SkillLibrary`, `SkillSession`, `SkillRoster`, `SkillToolSet`, host `LoadSkillTool` | `MarkdownSkillStore`, built-in `load_skill`, `{{skills}}` token, `AgentRunRequest.PreloadedSkills`, `AgentResult.LoadedSkills` | Skill file front matter (`key`, `summary`, `tools`) is already compatible. Delete the host `load_skill`: two tools with that name throw on the first run. |
| `PromptTemplate` (`{{VAR}}`, 5-min cache) | `MarkdownAgentPromptStore` (hot reload; tokens `{{date}}`, `{{tools}}`, `{{mode}}`, `{{user}}`, `{{skills}}`) | `{{GUILD_ID}}` and `{{BASE_URL}}` are not framework tokens (§4.5). |
| `AgentRunResult.{Response, LoopCount, TotalToolCalls, ToolNames, Model, StoppedOnMaxIterations}` | `AgentResult.{Response, Iterations, ToolCalls, ToolCallDetails[].Name, Model, StoppedOnMaxIterations}` | Direct. |
| `LlmUsage.{Input, Output, Cached, CacheWrite, EstimatedCost}` | `AgentResult.{InputTokens, OutputTokens, CacheReadTokens, CacheWriteTokens, CostUsd}` | `CostUsd` non-null → `CostSource.Billed`, as today. |
| `AgentRunResult.ConversationCleared` (hard-coded tool name in the loop) | none | Host derives it (§4.6). |
| `OpenRouterOptions` (`DefaultModel`, `TimeoutSeconds`, `MaxRetries`, `PromptCacheTtl`, …) | `cpike.Agent.OpenRouter.OpenRouterOptions` (`Model`, `Timeout`, …) + `Agent:TransientRetryLimit` | Key renames (§4.8). |
| `OpenRouterLlmClient` temperature fallback, `require_parameters`, 1h cache marker on system prompt | `OpenRouterClient` + loop: 1h persona marker, history/turn/rolling markers, reasoning and `tool_choice` rejection retries | Framework caching is a superset. |
| Meter/ActivitySource `DiscordBot.Agents`, `discordbot.agent.*`, span `agent.tool <name>`, `bot.tool.*` tags | Meter/ActivitySource `cpike.Agent`, `cpike.agent.*`, `gen_ai.*` and `cpike.tool.*` tags | Dashboards and docs must move (§4.9). |
| `PromptSurface` / `PromptSurfaceReporter` | `PromptSurfaceAnalyzer`, `UsePromptCachePreflight()` | Reporter is rebuilt on the framework's analyzer. |
| `OpenRouterModelCatalogClient` (host, reuses `OpenRouterJson`, `OpenRouterException`) | framework model directory | Keep the host client for now (it feeds the `LlmModel` table and pricing); re-point its two engine imports. Replacing it is optional follow-up. |

## 2. Prerequisites (blocking)

### P1. .NET 8 → .NET 10, as its own PR

> **Done** on this branch: all projects on `net10.0` with a `global.json`, Microsoft/EF Core/Npgsql
> packages on 10.x, Docker images on `10.0-noble`, CI and release on `10.0.x`, and the SessionStart
> hook installing `dotnet-sdk-10.0` (it is in noble-updates). Full suite green on Postgres; the
> image builds and serves the portal offline. No schema change: `has-pending-model-changes` is clean
> for both contexts once the Postgres design-time factory sets the legacy timestamp switch, which it
> was missing. Not verified: a live Discord login, voice, and the SQLite provider at run time.

The packages target `net10.0` only (`docs/design/net10-upgrade.md` in the framework, decision:
no multi-targeting). discordbot must move first. This is the largest risk in the whole plan and has
nothing to do with the assistant, so it ships separately and soaks before the swap.

- All seven `*.csproj` `TargetFramework` → `net10.0`; add a `global.json` pinning the 10.0 SDK.
- Packages: `Microsoft.EntityFrameworkCore.*` and `Npgsql.EntityFrameworkCore.PostgreSQL` 8 → 10,
  `Microsoft.Extensions.*` `8.*` → 10, `AspNet.Security.OAuth.Discord` 8 → 10,
  `Serilog.AspNetCore`, `Microsoft.Extensions.Hosting.Systemd`, `Microsoft.Bcl.TimeProvider`
  (likely removable), OpenTelemetry instrumentation, Elastic APM. Check `Swashbuckle.AspNetCore`
  6.5 on .NET 10 (the OpenAPI stack changed; a newer Swashbuckle or `Microsoft.AspNetCore.OpenApi`
  may be needed). Check `Discord.Net` 3.20.x and `Microsoft.CognitiveServices.Speech` run on 10.
- EF Core: regenerate nothing by hand, but run both providers' `migrations add` once to confirm
  the model snapshot has no pending changes after the upgrade (an empty migration is discarded).
  Keep `Npgsql.EnableLegacyTimestampBehavior`.
- `Dockerfile`: `sdk:8.0-noble` / `aspnet:8.0-noble` → `10.0-noble`.
- `.github/workflows/ci.yml` and `release.yml`: `DOTNET_VERSION` → `10.0.x` (or `global-json-file`).
- `.claude/hooks/session-start.sh`: installs `dotnet-sdk-8.0` from the Ubuntu archive because the
  Microsoft CDN is blocked in web sessions. Switch to `dotnet-sdk-10.0` and **verify the package is
  in the noble archive** before merging; if it is not, the web-session build breaks.
- Review Microsoft's .NET 9 and .NET 10 breaking-change lists (ASP.NET Core, EF Core, System.Text.Json)
  and note in the PR which apply.
- `CLAUDE.md`, `README.md`, `docs/articles/*` ".NET 8" references.
- Done when: full suite green on Postgres, route smoke test green, the Docker image runs, and a
  live bot answers a guild mention and a DM on the **old** engine.

### P2. Publish the framework packages

> **Mostly done.** `v0.2.0-beta.1`–`beta.4` are on the feed. H1–H3 (§3) are on the framework's
> `claude/nice-hawking-jizt2h` branch, rebased onto `main`, with the full framework suite green; they
> need merging and a `v0.2.0-beta.5` release before PR 4 can use them.

discordbot needs `cpike.Agent.OpenRouter` and `cpike.Agent.Core` (transitively), plus
`cpike.Agent.Testing` in the test projects. Pin `0.2.0-beta.5` or later. Without H1 the swap can
still proceed on `beta.4` with one global output ceiling, at the cost noted in §3.

### P3. Package access

> **Done** on this branch, following pike-assistant's setup so both consumers work the same way: a
> committed `nuget.config` mapping only `cpike.Agent.*` to the private `cpike5` feed; CI and release
> write `GITHUB_TOKEN` (`packages: read`) into the runner's copy; the Docker build takes a
> `nuget_token` BuildKit secret into a throwaway copy; developers and cloud sessions supply a classic
> `read:packages` token. `docs/articles/private-package-feed.md` has the details and the
> restore-from-a-local-pack fallback. `cpike.Agent.Core` 0.2.0-beta.5 is referenced (unused) from
> Infrastructure so CI proves the restore before PR 4 depends on it.

The packages stay **private** (D4). Every package must grant this repository Actions access
(package settings → *Manage Actions access*) for the CI token to read it.

## 3. Framework gaps (host-neutral, land in cpike-agent-framework)

These are recorded in the framework's `docs/design/host-adoption.md` under "Second consumer". All three
are built and tested on the framework branch, waiting for a release.

| # | Change | Needed by | Blocking? |
| --- | --- | --- | --- |
| H1 | `AgentRunRequest.MaxOutputTokens` (per-run ceiling, still raised by `ToolOutputBudget.Large`). | Guild 512 vs DM 4096 tokens on one process. | Yes for parity; workaround is one global ceiling plus the existing `MaxResponseLength` truncation, which costs money on the guild path. |
| H2 | A per-tool execution deadline: `Agent:ToolTimeoutSeconds`, overridable per definition, returning a structured `tool_timeout` failure while caller cancellation still propagates. | Today's 10 s guard on every tool. | No. Only `execute_python` and `fetch_url` are slow, and both have their own timeouts; can follow. |
| H3 | Make `FailureCodeOf` public (already F9 in `host-adoption.md`). | Porting `ToolContractTests`' "failures are countable" rule. | No; the test can parse `code` itself. |

The framework needs **no new seam** for the rest: surfaces, per-guild allow-lists, skills, read-only
callers and conversation clearing all map onto existing types.

## 4. Design of the discordbot side

### 4.1 Where the framework is called

Keep `IAssistantMessagePipeline` and `IAssistantContext` as the seam. `AssistantMessagePipeline`
becomes:

1. Create an async DI scope.
2. Seed `AgentRunScope` from the context: `UserKey = userId`, `CanEditData = CanMutate`, Discord ids
   into `Items` (see 4.2).
3. Build `AgentRunRequest { Mode, Prompt, History, Parts, Model, MaxToolRounds, MaxToolResultChars,
   DisabledTools, PreloadedSkills }`.
4. `AgentService.RunAsync`, catching `OpenRouterException`/`HttpRequestException`/timeouts into
   `Success = false` as today.
5. Map `AgentResult` to `AssistantPipelineResult` and `LlmUsageRecord` (field table in §1).

`FeatureRequestConversationService` stops building its own `AgentContext` and goes through the same
pipeline with a third `IAssistantContext` implementation, which also removes its duplicated cost
code. `IAssistantContext` loses its engine-typed members (`IToolRegistry`, `ToolContext`,
`ISkillActivationState`, `List<LlmMessage>`, `Temperature`) and gains `Mode` key, `Parts`,
`DisabledTools`, `PreloadedSkills`, `History`.

### 4.2 Discord context in the run scope

Add `DiscordRunContext` (Infrastructure): a **live view** over `AgentRunScope.Items` with typed
`UserId`, `GuildId`, `ChannelId`, `MessageId` and `ActiveGuildId` (the DM `set_active_guild` state,
today `Items["dm.activeGuildId"]`). Register it scoped. Tools inject it instead of receiving a
`ToolContext`. It must read through to the scope, not copy it, because the scope is seeded after
initializers run (`host-adoption.md`, "learned the hard way").

### 4.3 Tools

Target shape per tool: one class implementing `cpike.Agent.IAgentTool`, returning compact JSON via
`ToolJson.Compact`, failures as `{ ok:false, code, message }`, and `RequiresEdit = true` where it
declares `Mutation` today.

| Today | Becomes |
| --- | --- |
| `save_note`, `delete_note` (`Mutation`) | `RequiresEdit = true` |
| `search_notes`, `get_note`, `list_notes` | direct port |
| host `load_skill` | **deleted**; framework built-in via `AddBuiltInTools()` |
| `DocumentationToolProvider` (4 tools) + `DmDocumentationToolProvider` wrapper | 4 tool classes sharing a `DocumentationIndex` service (the path-containment guard moves with it). The DM wrapper disappears: one tool, two surfaces, via `ToolCatalog` scopes. |
| `UserGuildInfoToolProvider` (3), `RatWatchToolProvider` (3) | 6 tool classes in Bot |
| `ConversationToolProvider`: `clear_conversation`, `summarize_conversation` | 2 classes; `clear_conversation` gets `RequiresEdit = true` and `[NotDelegable]` |
| `BotManagementToolProvider` (4) | 4 classes; `set_active_guild` writes through `DiscordRunContext` and the cache |
| `DmModerationToolProvider` (2), `DmAnalyticsToolProvider` (2) | 4 classes |
| `CodeExecutionToolProvider`: `execute_python` | 1 class, `[NotDelegable]` |
| `WebFetchToolProvider`: `fetch_url` | 1 class, `[ExternalContent("web")]`; add the persona line about `<external_content>` blocks |
| `FeatureRequestToolProvider`: `submit_feature_request` | 1 class, `RequiresEdit = true` |
| `Implementations/*Tools.cs` schema holders, `ToolPermissions`, `CataloguedAgentToolProvider`, `Guild/DmAgentToolProvider`, `IDmToolProvider`, `SkillToolContextExtensions`, `DmToolContextExtensions` | deleted |

`ToolPermissions.MutationForbidden` returned a *successful* `{forbidden:true}`; the framework
returns a failure with code `edit_forbidden`. Check both personas' wording about refusals still
reads right.

Registration: `AddAgentCore(configuration, agent => agent.AddBuiltInTools().AddToolsFrom(infraAssembly).AddToolsFrom(botAssembly))`.
`AgentToolAssemblies` stays as the list of assemblies to scan.

**All tools port in one PR (D2).** There is no transitional adapter: the runner swap and the
port of all ~27 tools land together. That avoids throwaway bridging code and any period with two
tool contracts in the codebase, at the cost of a large PR. To keep it reviewable:

- Commit it in reviewable slices (one commit per former provider, then the pipeline, prompts,
  config and telemetry), so it can be read commit by commit even though it merges as one.
- Port mechanically first: keep each tool's name, description, schema and result payload
  byte-for-byte where the contract allows, so the prompt surface the model sees changes only where
  §4.3 says it must (the refusal shape, `load_skill`). Wording improvements wait for a later PR.
- Port each tool's tests in the same commit as the tool. `ToolContractTests` must pass on the new
  contract before the PR is opened.

### 4.4 Surfaces, the catalogue and the per-guild allow-list

`ToolCatalog` stays the single source of truth for which tool belongs to which surface (it drives
the settings UI, `ToolAccessResolver` and `ToolContractTests`).

- One mode per surface: `guild`, `dm`, `feature-requests`.
- A host `CatalogPromptStore : IAgentPromptStore` decorates `MarkdownAgentPromptStore`: it reads the
  three prompt files and **replaces each mode's `tools:` list** with
  `ToolCatalog.ForScope(surface)` minus tools named only by that surface's skills, and sets
  `Skills` to that surface's skill keys. Registered before `AddAgentCore` so the framework's `TryAdd`
  keeps it. Reason: a blank `tools:` line means *every registered tool*, which would leak DM tools
  into the guild and expose skill-gated tools before their skill loads; hand-maintained lists would
  drift from the catalogue.
- The per-guild allow-list (`AssistantGuildSettings.EnabledTools` via `ToolAccessResolver`) becomes
  `AgentRunRequest.DisabledTools = guildTools − allowed`. `FilteredToolRegistry` goes.
- `Assistant:Tools:EnableDocumentationTools = false` (today: null registry, no tools) becomes
  `DisabledTools = every guild tool`.
- Caching: tools are part of the cached prefix, so guilds with different allow-lists get different
  prefixes. That was already true.

### 4.5 Prompts

- Move `docs/agents/assistant-agent.md` → `docs/agents/guild.md`, `dm-owner-agent.md` → `dm.md`,
  and `Bot/Templates/feature-request-gathering-prompt.md` → `docs/agents/feature-requests.md`, each
  with `key:` / `displayName:` front matter. The framework layers a mode on a base persona
  (`Prompts:DefaultAgent`), so add a short `docs/agents/bot.md` persona ("You are @DiskordBott …",
  the open-source line, and the `<external_content>` rule) and trim the shared lines out of the modes.
- `{{GUILD_ID}}` changes per turn and today sits in the system prompt, which defeats the cache.
  Move it, with the guild name now prepended to the user message
  (`"{guildId}\n{guildName}\n---\n…"`), into a `PromptPart(..., PromptPlacement.TurnContext)`. Update
  the "How messages arrive" section of the guild prompt to describe the `<turn_context>` block.
- `{{BASE_URL}}` is static per deployment. Render it in `CatalogPromptStore` when it loads the file
  (or replace it with a tool result). Do not add it as a per-turn part.
- Configuration: `Prompts:Directory = docs/agents`, `Prompts:SkillsDirectory = docs/agents/skills`,
  `Prompts:DefaultAgent = bot`. Skill keys must be unique across `guild/` and `dm/` (they are).
- Copy `docs/agents/**` to the build output (a `None`/`Content` item in the Bot csproj) so `dotnet
  run`, tests and the container all resolve it the same way; the Dockerfile `COPY` for it can stay or
  go. The `{{skills}}` token replaces `SkillRoster.Append`: put it where the roster should appear.
- Hot reload replaces `PromptTemplate`'s 5-minute cache. A file that fails to parse keeps its last
  good version; the DM fallback string goes away.

### 4.6 Conversation clearing, skills and history

- `ConversationCleared`: the pipeline sets it when `AgentResult.ToolCallDetails` contains a
  successful `clear_conversation` call. Better: the tool sets `Items["dm.conversationCleared"]` via
  `DiscordRunContext` and the pipeline reads that, so the loop holds no tool names.
- DM skill activations: `IDmSkillActivationStore` keeps storing keys; feed them in as
  `PreloadedSkills` and write back `AgentResult.LoadedSkills`.
- History: `DmConversationMessage` and `FeatureRequestTurn` map to `ConversationTurn`. The framework
  does not trim `History` on `AgentService`; keep today's message-count cap, or trim with
  `HistoryWindow` to `Agent:ConversationHistoryTokens`.

### 4.7 DI and the "no API key" gate

- Keep one gate: `AddOpenRouter`, `AddAgentCore`, the pipeline, context factories and assistant
  services register only when `OpenRouter:ApiKey` is set. Collapse the duplicated checks in
  `AddAssistant` and `AddDmAssistant` into one helper.
- Fix the latent bug while here: `FeatureRequestServiceExtensions` registers its service ungated
  and resolves the runner at call time, which throws without a key.
- Keep ungated: `ToolCatalog`, `ToolAccessResolver`, the model catalogue and resolver, the usage
  ledger and retention service.
- Offline mode (`Discord:OfflineMode`): optionally register `ScriptedOpenRouterClient` so the
  assistant pages can be exercised without a key. Not required.

### 4.8 Configuration

| Today | Becomes |
| --- | --- |
| `OpenRouter:DefaultModel` | `OpenRouter:Model` (read the old key as a fallback for one release; `LlmModelResolver` reads the framework option) |
| `OpenRouter:TimeoutSeconds` | `OpenRouter:Timeout` (TimeSpan) |
| `OpenRouter:MaxRetries`, `RetryBaseDelayMs` | `Agent:TransientRetryLimit` |
| `OpenRouter:EnablePromptCachingByDefault`, `PromptCacheTtl` | gone (framework layout) |
| `OpenRouter:ApiKey`, `BaseUrl`, `AppUrl`, `AppTitle` | same names |
| `Assistant:Sampling:Temperature`, `DmAssistant:Temperature` | obsolete (D3); log a warning if set |
| `Assistant:Sampling:MaxTokens`, `DmAssistant:MaxTokens` | per-run `MaxOutputTokens` once H1 lands |
| `*:ToolExecutionTimeoutMs` | `Agent:ToolTimeoutSeconds` once H2 lands |
| `*:DuplicateToolCallLimit` | `Agent:DuplicateToolCallLimit` |
| `Assistant:Tools:AgentPromptPath`, `DmAssistant:OwnerSystemPromptPath`, `*:SkillsPath` | `Prompts:*` |
| new | `Agent:WarnOnPromptCacheMiss` (on), `SubAgents:Enabled` (off) |

`OpenRouterOptions` moves from the engine to the framework package, so `LlmModelResolver`,
`AssistantMetrics.cshtml.cs`, `SettingDefinitions` and the catalogue client's DI import it from there.
`OpenRouterModelCatalogClient` re-points `OpenRouterJson`/`OpenRouterException` to the framework's
equivalents or to local copies. Update `configuration-guide.md`.

### 4.9 Telemetry

- `OpenTelemetryExtensions`: `AddMeter(AgentMetrics.MeterName)` and
  `AddSource(AgentTelemetry.ActivitySourceName)` (both `"cpike.Agent"`), plus the OpenRouter client's
  source/meter. Remove the `DiscordBot.Agents` names.
- What changes for anyone querying: `discordbot.agent.tool.*` becomes `cpike.agent.tool.calls` /
  `cpike.agent.tool.duration` (seconds, not milliseconds), `discordbot.agent.completion.*` becomes
  `cpike.agent.runs` / `cpike.agent.run.iterations` plus the OpenRouter client's call metrics, labels
  `tool`/`outcome` become `gen_ai.tool.name`/`cpike.tool.outcome`, and span tags `bot.tool.*` become
  `cpike.tool.*`. The turn metrics (`cpike.agent.turns`, `cpike.turn.*`) come from `TurnPipeline`, so
  this host does not emit them. Check exact names against the framework's `docs/telemetry.md` at the
  pinned version.
- The repo no longer carries dashboards or alert rules (removed in the cleanup), so there is nothing
  else to regenerate. `discordbot.llm.*` (Bot's own `LlmMetrics`, fed by the ledger) is unaffected.
- Docs: `service-catalog.md`, `system-overview.md`, and any telemetry names in
  `configuration-guide.md`.

### 4.10 Tests

- **Delete** `tests/DiscordBot.Tests/Agents/` (21 files): they test the engine, which the framework
  tests itself.
- **Port** `ToolContractTests` to the framework contract: name shape, description length (600),
  schema shape, catalogue entry both ways, `RequiresEdit` set on every catalogued mutating tool
  (replaces the `CanMutate` refusal test, now the loop's job), and "missing required argument returns
  a `code`". `RegisteredAgentTools` discovers `cpike.Agent.IAgentTool` types.
- **Rewrite** `AssistantMessagePipelineTests`, `AssistantServiceTests`,
  `FeatureRequestConversationServiceTests`, the two context-factory tests and
  `AssistantContextSkillTests` on `cpike.Agent.Testing`'s `ScriptedOpenRouterClient` instead of
  mocking `IAgentRunner`. Assert the result and usage mapping, `DisabledTools` from the allow-list,
  `PreloadedSkills` round-trip, and `ConversationCleared`.
- **New**: a prompt-layout test per surface that pins tools sorted, the guild id in the turn context
  and not in the system prompt, and the per-surface tool sets (`CatalogPromptStore`).
- **Keep** `SkillContractTests` (shipped skill files), retargeted at the framework's parser.
- **Evals**: `EvalHarness` builds `AgentService` through DI with `AddOpenRouter`/`AddAgentCore`.
  Optionally move to `cpike.Agent.Evals`' `EvalCase`/`EvalRunner`. Run once with a key before and
  after the swap and compare: this is the behavioural check that the swap kept the assistant's
  quality.
- Unused `using DiscordBot.Agents.*` lines in about 35 files go in the removal PR.

## 5. Sequence of PRs

| # | Repo | PR | Notes |
| --- | --- | --- | --- |
| 1 | discordbot | .NET 10 upgrade (P1) | Old engine still in place. Soak in production before 3. |
| 2 | framework | H1–H3 (built on the framework branch); merge and cut `v0.2.0-beta.5` (P2) | Earlier betas exist; this one adds the Core gaps. |
| 3 | discordbot | Feed access: `nuget.config`, CI/release `packages: read` + token, Dockerfile secret, docs; references `cpike.Agent.Core` | Proves restore in CI and the image build. **Done.** |
| 4 | discordbot | **Swap and port**: reference the packages, `CatalogPromptStore`, prompt moves, `DiscordRunContext`, new `AssistantMessagePipeline`, every tool ported to `cpike.Agent.IAgentTool` with its tests, ported `ToolContractTests`, config keys, telemetry names | `AgentRunner` and every old tool type are unregistered and unused. Run evals before and after. |
| 5 | discordbot | **Removal**: delete `src/DiscordBot.Agents`, its sln entry, Dockerfile line, `tests/.../Agents/`, unused usings; update `CLAUDE.md`, `patterns.md` (§ Agent Tool Authoring, § Agent Skills), `service-catalog.md`, `feature-map.md`, `system-overview.md`, `docs/tools/README.md`, `docs/agents/skills/README.md` | Pure deletion and docs; `DiscordBot.Agents` is gone. |

"Deprecate" is short here: after PR 4 nothing references the engine, and PR 5 follows straight
after. If PR 5 cannot follow promptly, mark the engine's public types `[Obsolete]` in PR 4 so no
new code starts depending on them.

## Decisions

Agreed with the owner on 2026-11-09: D1 to D5.

- **D1. Core, not Hosting (agreed).** `TurnPipeline` is built around an HTTP caller: a
  `ClaimsPrincipal`, string session ids, SSE, `IConversationStore`. Discord messages fit only with
  adapters for each, and the existing `IAssistantMessagePipeline` already does the gating (consent,
  rate limit, owner) and persistence. Use `AgentService` directly. Revisit `cpike.Agent.Hosting` only
  if a web chat surface is added to the portal.
- **D2. Port every tool in the swap PR (agreed).** No transitional adapter. The swap PR is larger,
  but there is no throwaway code and no window with two tool contracts live. §4.3 says how to keep
  it reviewable.
- **D3. Drop temperature (agreed).** The framework omits it on purpose, discordbot's own client
  already carries a fallback for models that reject it, and the configured values (0.3 / 0.7) are
  near-defaults. The temperature options become obsolete and log a warning when set.
- **D4. Private packages (agreed).** The packages stay private on GitHub Packages; the discordbot
  repository has read access. See P3 for where credentials are needed.
- **D5. Provider-default effort (agreed).** No surface sets reasoning today. `Effort` stays null on
  every run and `OpenRouter:Effort` stays unset. A per-surface setting can come later; the framework
  supports it per run.

## Risks

- **.NET 10 upgrade** touches every package and the web-session toolchain; it is why P1 is separate.
- **Behaviour drift** from different loop details: wrap-up call wording, `<loop_note>`, result-cap
  envelope text, refusal shape, framework default `MaxToolResultChars` (24000 vs 8000; set it per run).
  Evals before and after PR 4 are the check.
- **Metric continuity**: the renames reset any external dashboard's history. The repo carries none
  now, but note the cut-over date for whoever queries the old names.
- **Prompt-surface totals**: the framework records an open question (F12) about two
  `PromptSurfaceAnalyzer` totals reading higher than the first host's own analyzer. Rebuild
  `PromptSurfaceReporter` on it, but compare its numbers against today's report before trusting them.
- **SQLite**: no schema change is planned, so neither migration set is touched.
