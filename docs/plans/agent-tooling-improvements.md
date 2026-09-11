# Agent Tooling Review & Improvement Plan

A review of this bot's LLM tool layer, benchmarked against `pike-assistant` — a separate .NET
assistant with a considerably more mature agent integration (~100 tools, per-mode tool gating,
on-demand skills, per-tool specs, and a behavioural eval suite). The goal is not to copy that
architecture wholesale: it is to identify what is currently *blocking capability expansion here*
and propose the smallest set of changes that unblocks it.

**Status**: proposed. Nothing in this document is implemented. The concrete code changes behind
it are in [docs/specs/agent-tooling-implementation.md](../specs/agent-tooling-implementation.md);
the order the whole overhaul runs in — including the library extraction — is in
[docs/plans/agent-groundwork-overhaul.md](./agent-groundwork-overhaul.md), which supersedes the
tier sequencing below.

---

## 1. What exists today

Three separate assistant surfaces share one tool abstraction:

| Surface | Tool providers | Registry | Gating |
| --- | --- | --- | --- |
| Guild assistant (`@mention`) | `Documentation`, `UserGuildInfo`, `RatWatch` | scoped `IToolRegistry` from DI | one global bool |
| DM assistant (owner) | `Memory`, `Conversation`, `BotManagement`, `DmModeration`, `DmAnalytics`, `DmDocumentation`, `CodeExecution`, `WebFetch` | hand-built in `DmAssistantContextFactory.BuildDmToolRegistry` | `IDmToolProvider` marker |
| Feature-request conversation | `FeatureRequestToolProvider` | none — resolved by concrete type and called directly | n/a |

27 tools total, across 11 providers, in ~2,900 lines of provider + definition code.

The shape of a tool today is three artefacts:

1. a static `XxxTools` class holding name constants and hand-written JSON Schema strings
   (`Infrastructure/Services/LLM/Implementations/`),
2. an `IToolProvider` implementation with a `switch` over tool names and one private
   `ExecuteXxxAsync` per tool (`.../Providers/`),
3. a manual `services.AddScoped<IToolProvider, XxxToolProvider>()` line in
   `AssistantServiceExtensions` or `DmAssistantServiceExtensions`.

The loop is `AgentRunner.RunAsync` — solid on the basics: it accumulates usage across iterations,
survives an LLM client throw without losing the usage ledger, converts tool exceptions into error
results, and handles every stop reason explicitly. The OpenRouter client retries transient
failures. Cost is priced from billed usage with configured rates as fallback.

**The architecture is sound. The problems are all at the edges — gating, budget, cost control,
and the cost of adding the 28th tool.**

---

## 2. What `pike-assistant` does differently

Five ideas from that codebase are worth importing, roughly in order of value:

1. **One class per tool, assembly-scanned.** `sealed class FooTool : IAgentTool` with a
   `Definition` property and an `InvokeAsync`. `AddAgentTools()` scans the assembly and registers
   every implementation. No provider, no switch, no DI edit.
2. **Skills — on-demand tool bundles.** A room lists `skills: reports, scripting` beside its
   `tools:`. Only a one-line summary per skill is in the prompt; the model calls `load_skill` and
   *then* the tools' schemas join the request. Activation is sticky for the conversation, so the
   cost is one cache write on the turn that first loads it. This took 5,874 tokens out of a
   7,271-token always-on prefix. It is the mechanism that makes "many tools" affordable.
3. **Per-tool specs before code.** `docs/tools/<tool_name>.md` (102 of them) pinning name,
   model-facing description, input schema, and *every* result shape — success and each expected
   failure — before any C# is written. Premised on "a tool's description and its results are
   prompt surface".
4. **Loop hardening.** Duplicate-call refusal, a tools-blocked wrap-up call when the step budget
   is exhausted (instead of discarding the run), a recovery call when a final turn produces no
   text, and per-tool OpenTelemetry spans with outcome/result-size tags.
5. **Behavioural evals.** A separate project that talks to a real model through the real loop and
   asserts on *rows written and tools called*, never on what the reply claims. Skipped without an
   API key, so CI stays free.

---

## 3. Findings

Ordered by impact on the stated goal (expanding capability). Each names the evidence.

### F1 — `get_feature_documentation` returns whole files, uncapped

`DocumentationToolProvider` reads the mapped markdown file with `File.ReadAllTextAsync` and returns
it (`Providers/DocumentationToolProvider.cs:183`). There is no size cap anywhere on the path. The
largest mapped doc, `bot-performance-dashboard.md`, is 80 KB — roughly 20,000 tokens returned from
a single tool call, then re-sent on every subsequent iteration of the run. `consent-privacy.md` is
60 KB, `authorization-policies.md` 44 KB.

This is the single most expensive defect in the tool layer, and it gets worse every time someone
grows a doc. **Fix**: cap tool results house-wide in `AgentRunner` (hard ceiling, explicit
truncation marker so the model knows), and separately make the documentation tool return
*sections* — a table of contents plus a requested heading — rather than a whole file.

### F2 — Guild tool access is one global boolean

`GuildAssistantContextFactory.cs:63` is the entire gate:

```csharp
_options.Tools.EnableDocumentationTools ? _toolRegistry : null
```

Every guild gets every guild-scoped tool, or none. `AssistantGuildSettings` has no tool column.
There is no way to give one server Rat Watch tools and not another, and no way to ship a new tool
to a subset of servers first. This blocks any capability expansion that isn't universally
appropriate.

**Fix**: a per-guild allow-list (`AssistantGuildSettings.EnabledTools`, JSON array, empty = house
default) resolved at context construction into a filtered registry, plus a checklist on the
existing Assistant Settings page. Pike's equivalent is the mode's `tools:` frontmatter; the
per-guild row is the same idea with the guild as the unit instead of the room.

### F3 — `IToolRegistry.EnableProvider` / `DisableProvider` is dead API

Nothing outside `ToolRegistry` itself and its tests calls either method. Worse, they mutate state
on a registry that is registered **scoped** — so even if something did call them, the effect would
vanish at the end of the request. It reads like a runtime kill switch and is not one.

**Fix**: delete both from the interface and resolve the tool set once, per run, from configuration
+ guild settings (F2). Add a genuine house-wide kill switch backed by `ISettingsService` if one is
wanted. A per-run resolved set is also what keeps the tool array byte-stable for prompt caching
(F7).

### F4 — `ToolExecutionTimeoutMs` is configured, documented, and never read

`AssistantToolOptions.ToolExecutionTimeoutMs` (default 5000) is documented in
`docs/articles/ai-assistant.md:292` and in the configuration guide. Grep finds no consumer. Nothing
bounds a tool's execution: `WebFetchToolProvider` and `CodeExecutionToolProvider` carry their own
timeouts, but a slow database query in `DmAnalyticsTools` or a large file read in the documentation
tools can hang the run indefinitely.

**Fix**: enforce it in `AgentRunner` with a linked `CancellationTokenSource`, returning a
structured `{"error":"timeout", "message": "..."}` result so the model can proceed rather than the
run dying.

### F5 — Exhausting the tool budget throws the whole run away

`AgentRunner.cs` (final return) sets `Success = false` with
`"Exceeded maximum tool call iterations"`. The user sees an error; every token already spent is
billed and discarded. With `MaxToolCallsPerQuestion` defaulting to **5**, and that value actually
limiting *rounds* rather than calls (`MaxToolCallIterations`), this is reachable in normal use as
soon as the tool surface grows.

**Fix**: pike's wrap-up — one more call with `tool_choice: none` and the definitions left on the
request (so the cached prefix still reads), asking the model to answer from what it has. Return it
under a fixed "I ran out of steps" notice. Also: rename the option to `MaxToolRounds` (keeping the
old key bound as legacy, which this codebase already has machinery for), raise the default to 8,
and make it a per-guild override.

### F6 — No duplicate-tool-call guard

A model that dislikes a result will re-issue the identical call until the budget is gone — which,
at a 5-round budget, means the user gets F5's error instead of an answer. The system prompt already
tries to paper over this in prose ("do not repeat a search with slightly different wording",
`docs/agents/assistant-agent.md`), which is the tell that the loop should be enforcing it.

**Fix**: a per-run tally keyed by tool name + normalised arguments; past N identical executions
(default 3) return a directive `repeated_call` result instead of invoking the tool.

### F7 — Prompt caching leaves most of the win on the table

`OpenRouterMessageMapper.cs:71` places exactly one `cache_control` marker, on the system prompt.
Two consequences:

- **Tool schemas are not order-stable.** `ToolRegistry.GetEnabledTools()` returns providers in
  dictionary insertion order, which is DI registration order. Tool schemas serialize *before* the
  system message, so any reordering silently invalidates the prefix — the request still returns the
  right answer, at roughly ten times the price. Sorting the tool list by name costs one line.
- **No rolling tool-result breakpoint.** The DM assistant runs up to 10 iterations with conversation
  history; iterations 2..N re-send every prior tool result uncached. A rolling marker on the newest
  tool-result message is the standard fix (pike documents the four-breakpoint layout in
  `docs/agent-prompts.md § Prompt caching`).

### F8 — No per-tool telemetry

Grep finds no `ActivitySource` use anywhere in the LLM layer. The bot has OpenTelemetry
(`BotActivitySource`) and uses it for background services, but a tool call produces log lines only:
no span, no result-size tag, no outcome tag, no failure code. The guild interaction log records
`ToolCalls` (a count) but not `ToolNames` — only the DM log has names
(`DmAssistantInteractionLog.ToolNames`). So "which tool is slow / failing / never used" is not
answerable today, which is exactly the question you need answered before deciding what to build
next.

**Fix**: an `agent.tool <name>` span per call tagged with outcome (`ok` / `failed_result` /
`timeout` / `error` / `unknown_tool`) and result size; add `ToolNames` to
`AssistantInteractionLog` (both migrations); surface a per-tool usage table on the existing
Assistant Metrics page.

### F9 — `ToolContext.UserRoles` is a permission mechanism that isn't one

`ToolContext.UserRoles` is declared and documented as "User's roles in the guild (for permission
checks)". Nothing ever populates it, and no tool reads it. It is an empty list that looks like an
authorization hook — a trap for the next person adding a tool that needs one.

**Fix**: either populate it in `GuildAssistantContext`'s constructor and use it, or delete it and
adopt pike's `ICallerAccess` pattern: a request-scoped flag consulted *inside* mutating tools,
returning a structured refusal. The runtime-check approach matters for caching — filtering the tool
list per user would give every permission level its own cache prefix.

### F10 — Adding a tool costs three files and a DI edit

Detailed in §1. On top of the boilerplate, every provider hand-rolls the same input parsing
(`input.TryGetProperty("content", out var e)`, null check, empty check, clamp) — see
`MemoryToolProvider.cs:72-95` and its near-identical twins in every other provider. Pike's
`ToolInput` / `ToolJson` / `ToolAccess` helpers exist precisely because those copies drifted.

### F11 — The tool surface grows linearly and unboundedly

Nothing measures the prompt cost of the tool array, and nothing takes tools *out* of it. Today the
guild assistant carries 9 tool schemas and the DM assistant 27. The capabilities worth adding
(§5) would roughly double that, and every schema is charged on every request whether called or
not. This is the ceiling that skills exist to raise.

### F12 — Tool tests and specs are thin

Two provider test files (`DocumentationToolProviderTests`, `UserGuildInfoToolProviderTests`) cover
11 providers. `docs/specs/assistant-tool-catalog.md` is a 1,000-line monolith that still lists
every tool as "Planned", plans 17 tools against the 27 that shipped, and describes tools that were
never built (`list_guild_members`, `check_user_permissions`, `get_feature_status`). There is no
per-tool spec and no behavioural eval of any kind.

### F13 — The documentation tool can read markdown outside its base directory

`DocumentationToolProvider` falls back to `fileName = $"{featureName}.md"` for any feature name not
in its map, then `Path.Combine`s it onto `DocumentationBasePath` with no containment check
(`Providers/DocumentationToolProvider.cs:158-168`). `featureName` comes from the model, and the
model's input is a Discord message from any user in any guild where the assistant is enabled. A
name of `../../docs/agents/assistant-agent` resolves outside `docs/articles` and returns the
assistant's own system prompt — the one that prompt explicitly forbids disclosing. `CLAUDE.md` and
every spec under `docs/` are reachable the same way.

The `.md` suffix bounds this to markdown rather than secrets, so it is "read files you did not
intend to publish" rather than a credential leak — but it is a direct prompt-injection target and
it is the cheapest thing on this list to fix.

**Fix**: resolve both paths with `Path.GetFullPath` and reject anything not under the base
directory, plus an `[a-z0-9-]` allow-list on the unmapped fallback. Return the same payload as
not-found, so a probe learns nothing. Detail in the implementation spec §1.3.

---

## 4. Proposed plan

Four tiers. Each is independently shippable and each is one PR (the repo's small-blast-radius
rule). Tier 1 pays for itself immediately; Tier 3 is the one that actually unlocks expansion.

### Tier 1 — Stop the bleeding (F1, F4, F5, F6, F7, F13)

Loop and cost fixes, no new abstractions, no schema changes.

0. **Contain the documentation path** — resolve-and-verify against the base directory. Ships
   first, alone, as a security fix.
1. **Result cap in `AgentRunner`** — `Assistant:Tools:MaxToolResultChars` (default ~8,000) with an
   explicit truncation marker in the returned JSON.
2. **Section-aware documentation tool** — `get_feature_documentation` gains an optional `section`
   argument and returns a heading list + the requested section; no argument returns the doc's
   overview plus its table of contents. Kills the 20k-token single call.
3. **Enforce `ToolExecutionTimeoutMs`** in the loop via a linked CTS; timeout becomes a structured
   result, not a dead run.
4. **Budget wrap-up call** on max-iterations, and rename `MaxToolCallsPerQuestion` →
   `MaxToolRounds` (legacy key retained), default 5 → 8.
5. **Duplicate-call guard**, `Assistant:Tools:DuplicateToolCallLimit` (default 3, 0 disables).
6. **Cache fixes**: sort `GetEnabledTools()` by name; add a rolling `cache_control` marker on the
   newest tool-result message and one on the last replayed history message.

*Verification*: unit tests per item; a manual before/after on a real question comparing
`CachedTokens` and total cost on the `LlmUsageRecord` ledger.

### Tier 2 — Make the surface governable (F2, F3, F8, F9)

7. **Per-guild tool allow-list** — `AssistantGuildSettings.EnabledTools` (JSON array, empty = house
   default), both migrations, resolved once per run into a filtered registry; checklist UI on the
   Assistant Settings page grouped by category (pike's `AgentToolCategories` is the pattern).
8. **Delete `EnableProvider`/`DisableProvider`**; add a house-wide kill switch through
   `ISettingsService` if wanted.
9. **Per-tool spans and names** — `agent.tool <name>` spans with outcome tags; `ToolNames` on
   `AssistantInteractionLog`; a per-tool table on Assistant Metrics (calls, p95 duration, failure
   rate, mean result size).
10. **Resolve `UserRoles`** — populate and use it, or replace it with an `ICallerAccess`-style
    runtime edit gate. Decide before any write-capable guild tool ships.

### Tier 3 — Make adding tools cheap, and adding *many* tools affordable (F10, F11)

11. **`IAgentTool` + assembly scanning, alongside the existing providers.** A new
    `IAgentTool { AgentToolDefinition Definition; Task<ToolExecutionResult> InvokeAsync(...) }`, an
    `AddAgentTools()` assembly scan, and one adapter `IToolProvider` that exposes all scanned
    `IAgentTool`s to the existing registry. Nothing existing has to move; new tools are one file,
    and providers can be converted opportunistically. Ship `ToolInput`/`ToolJson`/`ToolResults`
    helpers in the same PR.
12. **Skills.** Markdown files with `key` / `summary` / `tools` frontmatter, a `load_skill` tool,
    and sticky activation per conversation (DM assistant has conversation state already; the guild
    assistant is single-turn, so skills there are load-per-run — still a win when the roster is
    large and the load is rare). Start with two: `moderation` and `analytics` in DMs, which are the
    heaviest schemas and the rarest calls.
13. **Prompt-surface report** — a startup log line and an Assistant Metrics panel showing per-tool
    schema token cost and the total prefix, so tool-array growth is visible rather than discovered
    on the bill.

### Tier 4 — Discipline (F12)

14. **Per-tool specs** in `docs/tools/<tool_name>.md` using pike's template; retire
    `docs/specs/assistant-tool-catalog.md` to `docs/specs/archive/` with a pointer. New tools get a
    spec before code.
15. **A shared tool contract test** — one theory over every registered tool asserting: unique
    snake_case name, description present and 50–120 words, valid `type: object` schema, every
    `required` name present in `properties`, and a category assigned. Cheap, and catches the
    mistakes that are otherwise found by the model behaving oddly in production.
16. **A minimal eval project** — `tests/DiscordBot.Evals`, skipped without `OpenRouter:ApiKey`,
    asserting on tools called and rows written for a dozen representative questions. Not a
    scoreboard: a regression gate for prompt and tool-description changes.

---

## 5. Capability candidates once the plumbing is fixed

Grounded in services that already exist, so each is a tool wrapper rather than a feature build.
None should ship before Tier 1–2.

**Guild assistant (read-only, safe to make universal):**

- `get_feature_status(feature)` — is this feature enabled for this guild, and what role does it
  need. Specced in the 2024 catalog, never built; it is the answer to the most common support
  question, which the prompt currently handles by guessing ("the feature is probably disabled").
- `search_soundboard` / `list_vox_words` — "what sounds do you have" is unanswerable today.
- `get_my_reminders` / `get_my_consent_status` — user's own data only.
- `search_scheduled_messages` (admin-gated), `get_leaderboard` beyond Rat Watch.

**Guild assistant (write, needs F9's edit gate first):**

- `create_reminder` — the natural-language time parser is already there; this is the highest-value
  write tool and the best test of the gate.

**DM/owner assistant:**

- `search_logs`, `get_alert_status`, `get_deployment_info` — triage from a phone.
- `set_guild_setting` / `toggle_feature` — write-through to `IAssistantGuildSettingsService`.
- `triage_feature_requests` — `FeatureRequestToolProvider` already exists and is wired only into
  its own conversation service; exposing it as a DM skill is nearly free.

**Cross-cutting:**

- **`web_search` as an OpenRouter server tool.** The bot has `fetch_url` (fetch a known URL) but no
  search. OpenRouter's `openrouter:web_search` is declared in the request's tool array and executed
  API-side — no `IToolProvider`, no HTTP client, no scraping. Per-guild gated, off by default, and
  worth measuring against the cost it adds.

---

## 6. Deliberately not proposed

- **Rewriting the provider model.** The adapter in Tier 3 lets both shapes coexist; a big-bang
  migration of 11 providers would be a large diff with no user-visible change.
- **Parallel tool execution.** Sequential is fine at this call volume and the added failure modes
  are not worth it yet.
- **Sub-agent delegation** (pike has it). No current use case here.
- **Replacing the OpenRouter client or adding an LLM SDK.** The owned client is the right call and
  matches both codebases' conventions.
- **Anything touching the PostgreSQL provider path beyond the two migrations Tier 2 needs.** Note
  that the test suite is SQLite-only, so those migrations need manual verification against Postgres.

---

## References

- This repo: [implementation spec](../specs/agent-tooling-implementation.md),
  `docs/articles/ai-assistant.md`, `docs/specs/assistant-tool-catalog.md` (stale),
  `docs/architecture/service-catalog.md`, `.claude/agents/ai-assistant.md`
- `pike-assistant`: `docs/agent-tools.md` (tool design standards), `docs/agent-prompts.md`
  (composition, skills, prompt caching), `docs/tools/load_skill.md`, `docs/evals.md`
