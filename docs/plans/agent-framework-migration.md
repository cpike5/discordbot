# Plan: move the assistant onto cpike.Agent and retire `DiscordBot.Agents`

> Status: **proposed**; decisions D1–D5 agreed. Nothing here is built. Written against discordbot `a673cc9` and
> cpike-agent-framework `97f8a7a` (0.2.0-dev, unreleased).

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

1. **Prerequisites that block everything:** discordbot is on .NET 8 and the packages are `net10.0`
   only; no package has been published; the feed is private.
2. **The tool surface:** 6 `IAgentTool` classes and 11 `IToolProvider`s (about 27 tools) use the
   engine's tool contract (`ToolContext` argument, `ToolExecutionResult` return, `Mutation`), which
   differs from the framework's (`AgentRunScope` from DI, `string` return, `RequiresEdit`).
3. **A handful of behaviours** the engine has and the framework does not, or does differently:
   per-run temperature and output-token ceiling, per-tool timeout, the `clear_conversation` signal,
   per-surface tool sets from `ToolCatalog`, and the engine's metric names on the Grafana dashboards.

Recommended path: upgrade to .NET 10 first (its own PR), publish `0.2.0-beta.1`, then swap the runner
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
| `AgentContext.MaxTokens` | `Agent:MaxTokensPerTurn` (process-wide) | **Gap G1**: no per-run ceiling. Guild uses 512, DM 4096, feature requests 1024. |
| `AgentContext.Temperature` | none, by design (`ModelRouter.cs:259`: deprecated on current models) | **Decision D3**: drop it. |
| `MaxToolCallIterations` | `AgentRunRequest.MaxToolRounds` | Direct. |
| `MaxToolResultChars` | `AgentRunRequest.MaxToolResultChars` | Direct. Framework default 24000; discordbot uses 8000. |
| `DuplicateToolCallLimit` | `Agent:DuplicateToolCallLimit` | Process-wide; every surface uses 3 today, so fine. |
| `ToolExecutionTimeoutMs` (10 s, per tool) | none | **Gap G2.** |
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

No tag exists in cpike-agent-framework, so no package exists (`host-adoption.md` F0). Cut
`v0.2.0-beta.1` per its `docs/releasing.md` and confirm the `.nupkg`s carry `lib/net10.0/`. discordbot
needs `cpike.Agent.OpenRouter` and `cpike.Agent.Core` (transitively), plus `cpike.Agent.Testing` in
the test projects. The framework gaps in §3 that the swap depends on should land in that release.

### P3. Package access

The packages stay **private** on GitHub Packages (D4), and the discordbot repository has already
been granted read access to them. Every place that restores discordbot still needs a credential:

- **`nuget.config`** at the repo root naming `https://nuget.pkg.github.com/cpike5/index.json`, with
  no credentials in it.
- **CI and release workflows** (`ci.yml`, `release.yml`): add `permissions: packages: read` and
  register the source with `GITHUB_TOKEN` before restore
  (`dotnet nuget update source cpike5 --username ${{ github.actor }} --password ${{ secrets.GITHUB_TOKEN }} --store-password-in-clear-text`).
  The repo grant is what lets that token read the packages.
- **Dockerfile**: pass the token as a BuildKit secret (`RUN --mount=type=secret,id=nuget ...`),
  never as a build arg, and wire the secret through the image build step in `release.yml`.
- **Developer machines**: a classic PAT with `read:packages`, registered once per the framework's
  `docs/releasing.md`.
- **Claude web sessions**: the environment needs `nuget.pkg.github.com` allowed and a `read:packages`
  credential for that host; without it the SessionStart hook's `dotnet restore` fails.

## 3. Framework gaps (host-neutral, land in cpike-agent-framework)

These are recorded in the framework's `docs/design/host-adoption.md` under "Second consumer".

| # | Change | Needed by | Blocking? |
| --- | --- | --- | --- |
| G1 | `AgentRunRequest.MaxOutputTokens` (per-run ceiling, still raised by `ToolOutputBudget.Large`). | Guild 512 vs DM 4096 tokens on one process. | Yes for parity; workaround is one global ceiling plus the existing `MaxResponseLength` truncation, which costs money on the guild path. |
| G2 | A per-tool execution deadline: `Agent:ToolTimeoutSeconds`, overridable per definition, returning a structured `tool_timeout` failure while caller cancellation still propagates. | Today's 10 s guard on every tool. | No. Only `execute_python` and `fetch_url` are slow, and both have their own timeouts; can follow. |
| G3 | Make `FailureCodeOf` public (already F9 in `host-adoption.md`). | Porting `ToolContractTests`' "failures are countable" rule. | No; the test can parse `code` itself. |

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
reads right, and update the dashboard panel that counted forbidden calls if any.

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
| `Assistant:Sampling:MaxTokens`, `DmAssistant:MaxTokens` | per-run `MaxOutputTokens` once G1 lands |
| `*:ToolExecutionTimeoutMs` | `Agent:ToolTimeoutSeconds` once G2 lands |
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
- `deployment/observability/grafana/generate_dashboards.py` (source of truth) and the regenerated
  `discordbot-assistant.json`, `-overview.json`, `-runtime.json`:
  `discordbot_agent_tool_count_total` → `cpike_agent_tool_calls_total`,
  `discordbot_agent_tool_duration_milliseconds_bucket` → `cpike_agent_tool_duration_seconds_bucket`
  (unit changes ms → s), `discordbot_agent_completion_*` → `cpike_agent_runs_total` /
  `cpike_agent_run_iterations` and the OpenRouter client's call metrics; labels `tool`/`outcome` →
  `gen_ai_tool_name`/`cpike_tool_outcome`; Tempo panels `bot.tool.*` → `cpike.tool.*`. Check exact
  names against the framework's `docs/telemetry.md` at the pinned version.
- `discordbot.llm.*` (Bot's own `LlmMetrics`, fed by the ledger) is unaffected; `alerts.yml` uses
  only those.
- Docs: `grafana-agent-debugging.md`, `grafana-observability.md`, `service-catalog.md`,
  `system-overview.md`.

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
| 2 | framework | G1 (+ G2, G3 if ready); cut `v0.2.0-beta.1` (P2) | |
| 3 | discordbot | Feed access: `nuget.config`, CI/release `packages: read` + token, Dockerfile secret, session environment credential | No code change; proves restore works everywhere. |
| 4 | discordbot | **Swap and port**: reference the packages, `CatalogPromptStore`, prompt moves, `DiscordRunContext`, new `AssistantMessagePipeline`, every tool ported to `cpike.Agent.IAgentTool` with its tests, ported `ToolContractTests`, config keys, telemetry names, dashboards | `AgentRunner` and every old tool type are unregistered and unused. Run evals before and after. |
| 5 | discordbot | **Removal**: delete `src/DiscordBot.Agents`, its sln entry, Dockerfile line, `tests/.../Agents/`, unused usings; update `CLAUDE.md`, `patterns.md` (§ Agent Tool Authoring, § Agent Skills), `service-catalog.md`, `feature-map.md`, `system-overview.md`, `docs/tools/README.md`, `docs/agents/skills/README.md`, `.claude/agents/ai-assistant.md` | Pure deletion and docs; `DiscordBot.Agents` is gone. |

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
- **Dashboard continuity**: metric renames reset history. Keep the old panels for one release or
  note the cut-over date on the dashboard.
- **SQLite**: no schema change is planned, so neither migration set is touched.
