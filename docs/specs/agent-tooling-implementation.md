# Agent Tooling — Implementation Spec

The concrete changes behind [docs/plans/agent-tooling-improvements.md](../plans/agent-tooling-improvements.md).
That document says *what is wrong and why*; this one says *what the code becomes*. Read the plan
first — the finding IDs (F1…F13) are shared.

**Status**: partially implemented. §1.1, §1.4, §1.5, §1.6 and §1.7a/b shipped as Phase 2 of the
groundwork overhaul, in `DiscordBot.Agents` rather than `DiscordBot.Infrastructure` — see
[§5.1a of the plan](../plans/agent-groundwork-overhaul.md) for the corrections that came out of
doing it. **§2.1–§2.4 shipped as Phase 3**; [§5.2a of the plan](../plans/agent-groundwork-overhaul.md)
records where the sequencing bent, and three specifics below are corrected by what doing it found:
§2.2 had to ship *with* §2.1 rather than after it, §2.3's spans use the engine's own
`ActivitySource` and `IToolRegistry` gained `FindProviderName` to tag them, and §2.4 took option B.
§1.3 (F13, the documentation path containment) is still unshipped and still wants its own PR.
§1.7c wants a probe against live traffic before anyone builds it. Everything else here is
proposed.

**Sequencing note.** [plans/agent-groundwork-overhaul.md](../plans/agent-groundwork-overhaul.md)
supersedes the tier order used here: the engine moves to a `DiscordBot.Agents` project *before*
these changes are made, so most of §1 and §3 lands there rather than in `DiscordBot.Infrastructure`.
Two specifics below are corrected by it — the three new loop knobs ride on `AgentContext` rather
than `IOptions`, and §2.3's tool spans use the engine's own `ActivitySource` rather than
`BotActivitySource`, which lives in `DiscordBot.Bot` and is unreachable from the engine.

Each section names the files touched, the shape of the change, the configuration and schema it
adds, and how it is verified. Sections are ordered so each can ship as one PR without depending on
a later one.

---

## Tier 1 — Loop and cost

Five PRs. No schema changes, no new abstractions, no user-visible UI.

### 1.1 Cap tool results (F1)

**Problem restated**: a tool result is appended to `conversationHistory` and re-sent on every
subsequent iteration. One 80 KB documentation read costs ~20k tokens on iteration 1 and again on
2, 3, 4, 5.

**Change**: `AgentRunner` truncates every tool result before it enters the history. This is the
backstop; §1.2 fixes the tool that actually needs it, but the backstop is what makes the next
careless tool safe.

`AssistantToolOptions` gains:

```csharp
/// <summary>
/// Hard ceiling on the characters of a single tool result entering conversation history.
/// A longer result is truncated with an explicit marker so the model knows it is reading a
/// fragment. Default 8000 (~2,000 tokens). 0 disables the cap.
/// </summary>
public int MaxToolResultChars { get; set; } = 8000;
```

In `AgentRunner`, after the `ToolExecutionResult` → `JsonElement` conversion and before
`toolResults.Add(...)`:

```csharp
contentElement = ToolResultLimiter.Cap(contentElement, context.MaxToolResultChars);
```

`ToolResultLimiter` is a small static in `Infrastructure/Services/LLM/`:

- serialize the element; if within the cap, return it unchanged (no allocation for the common case);
- otherwise return
  `{"truncated": true, "shown_chars": N, "total_chars": M, "content": "<first N chars>", "message": "This result was truncated. Narrow your query or request a specific section rather than re-calling this tool."}`.

The message matters as much as the cap: without it the model re-calls the same tool hoping for
more, which is exactly the loop §1.5 refuses.

**`AgentRunner` needs the option.** It currently takes no options — `MaxToolCallIterations` arrives
on `AgentContext`. Keep that pattern: add `MaxToolResultChars`, `ToolExecutionTimeoutMs`, and
`DuplicateToolCallLimit` to `AgentContext`, populated by `AssistantMessagePipeline` from
`IAssistantContext`. That keeps `AgentRunner` free of `IOptions` and keeps per-scope override
possible (the DM assistant may want a higher cap than a guild).

So `IAssistantContext` gains three members, implemented in both contexts:

```csharp
int MaxToolResultChars { get; }
int ToolExecutionTimeoutMs { get; }
int DuplicateToolCallLimit { get; }
```

`GuildAssistantContext` reads them off `_options.Tools`; `DmAssistantContext` off
`DmAssistantOptions` (which needs the three new properties too, since it does not share
`AssistantToolOptions`).

**Tests**: `AgentRunnerTests` — a result under the cap passes through byte-identical; one over is
replaced by the truncation envelope; `0` disables. `ToolResultLimiterTests` for the envelope shape.

### 1.2 Section-aware documentation tool (F1)

**Files**: `Implementations/DocumentationTools.cs`, `Providers/DocumentationToolProvider.cs`.

`get_feature_documentation` gains an optional `section` argument:

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `feature_name` | string | yes | unchanged |
| `section` | string | no | heading text, matched case-insensitively against `##`/`###` headings |

Behaviour:

- **No `section`**: return the content *above the first `##`* (the overview) plus a `sections`
  array of the document's headings. Typically a few hundred tokens instead of twenty thousand.
- **With `section`**: return that heading's body, up to the next heading of the same or higher
  level, plus the `sections` list so the model can pick another in one more call.
- **Unmatched `section`**: `{"error": "section_not_found", "sections": [...], "message": "..."}` —
  a directive result, not an error, naming the sections that do exist.

Description change (prompt surface, so it is part of the spec): add
*"Returns the feature's overview and its section headings; pass `section` to read one section in
full. Prefer reading one section over reading the whole document."*

**Tests**: heading extraction over a fixture with `#`, `##`, `###`, code fences containing `#`
(must not be parsed as headings), and no headings at all.

### 1.3 Contain the documentation path (F13 — new)

**This is a security finding, not a cost one.** `DocumentationToolProvider` builds the path as:

```csharp
if (!FeatureDocumentationMap.TryGetValue(featureName, out var fileName))
    fileName = $"{featureName}.md";
var docPath = Path.Combine(_assistantOptions.Value.Tools.DocumentationBasePath, fileName);
```

`featureName` comes from the model, and the model's input is a Discord message from any user in
any guild where the assistant is enabled. Nothing validates it. `Path.Combine` happily accepts
`../../docs/agents/assistant-agent` and produces a path outside `docs/articles` — which reads back
the assistant's own system prompt, the one the prompt itself forbids disclosing. `CLAUDE.md`,
`README.md`, and every spec under `docs/` are reachable the same way. The `.md` suffix limits the
blast radius to markdown, which is why this is "read files you did not intend to publish" rather
than "read secrets", but the mechanism is a straightforward prompt-injection target.

**Change**, before any file access:

```csharp
var baseDir = Path.GetFullPath(_assistantOptions.Value.Tools.DocumentationBasePath);
var fullPath = Path.GetFullPath(Path.Combine(baseDir, fileName));

if (!fullPath.StartsWith(baseDir + Path.DirectorySeparatorChar, StringComparison.Ordinal))
{
    _logger.LogWarning("Rejected documentation path outside base directory: {Feature}", featureName);
    return CreateJsonResult(new { error = true, message = "..." /* same as not-found */ });
}
```

Plus a character allow-list on the unmapped fallback (`[a-z0-9-]` only), so the escape is refused
before it is resolved rather than after. Return the *same* payload as not-found: a distinct
"rejected" message tells an attacker their probe was understood.

`DmDocumentationToolProvider` wraps `DocumentationToolProvider`, so it inherits the fix.

**Tests**: `../`, absolute paths, backslash separators, a null byte, and a legitimate
unmapped-but-real feature name (`welcome-system`) still resolving.

### 1.4 Enforce the tool timeout (F4)

**File**: `AgentRunner`.

```csharp
using var toolCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
if (context.ToolExecutionTimeoutMs > 0)
{
    toolCts.CancelAfter(context.ToolExecutionTimeoutMs);
}

try
{
    executionResult = await context.ToolRegistry.ExecuteToolAsync(
        toolCall.Name, toolCall.Input, context.ExecutionContext, toolCts.Token);
}
catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
{
    // The tool's own deadline elapsed, not the caller's.
    executionResult = ToolExecutionResult.CreateError(
        $"Tool '{toolCall.Name}' timed out after {context.ToolExecutionTimeoutMs}ms. " +
        "Do not retry it; answer with what you have or try a different approach.");
}
```

The `when` clause is load-bearing: an outer cancellation (Discord interaction expiring, host
shutdown) must still propagate as cancellation, not be laundered into a tool result.

Note the default of 5000ms is tight for a documentation read plus a database round trip on a cold
SQLite file. Raise it to 10000 in the same PR and say so in the configuration guide.

**Tests**: a tool that delays past the deadline yields the timeout result and the loop *continues*;
an outer cancellation still throws `OperationCanceledException` out of `RunAsync`.

### 1.5 Duplicate-call guard (F6)

**File**: `AgentRunner`, a per-run `Dictionary<string, int>` keyed by tool name plus normalised
arguments (joined by a separator that cannot appear in a tool name, e.g. ``).

`NormalizeArgs` re-serializes the `JsonElement` with object properties sorted by name, so
`{"a":1,"b":2}` and `{"b":2,"a":1}` are one key. Depth-limited (say 8) and length-capped to avoid
pathological inputs.

Before executing:

```csharp
if (context.DuplicateToolCallLimit > 0 && ++seen[key] > context.DuplicateToolCallLimit)
{
    // Expected-failure result, not an exception: this is guidance, not a fault.
    contentElement = JsonSerializer.SerializeToElement(new
    {
        error = "repeated_call",
        message = $"You have already called '{toolCall.Name}' with these exact arguments "
                + $"{context.DuplicateToolCallLimit} time(s) in this run. Do not repeat it: use the "
                + "result you already have, call it with different arguments, or answer with what you know.",
    });
    // …added to toolResults with IsError = false, and the tool is not invoked.
}
```

`IsError = false` is deliberate — it is a normal result carrying a directive, and flagging it as an
error would both inflate the error metrics of §2.3 and prepend `Error: ` on the wire, which reads
as a malfunction rather than an instruction.

Default `DuplicateToolCallLimit = 3` (a shape runs twice, the third is refused), `0` disables.

**Tests**: property-order-insensitive keying; the third identical call is refused and the tool's
execute is never entered; a changed argument resets nothing but produces a different key.

### 1.6 Budget wrap-up, and the option rename (F5)

Four parts.

**(a) `LlmRequest` and the wire request gain `ToolChoice`.** `ChatCompletionRequest` has no such
field today. Add `public string? ToolChoice { get; init; }` to the wire record and
`public string? ToolChoice { get; set; }` to `LlmRequest`, mapped straight through in
`BuildRequest`. Only `"none"` is used for now; the field is a string because OpenRouter also
accepts an object form that is not needed here.

**(b) The wrap-up call.** When the loop exits on `loopCount >= MaxToolCallIterations`, instead of
returning `Success = false`:

```csharp
var wrapUp = await RequestTextOnlyReplyAsync(request, conversationHistory, WrapUpInstruction, ct);
```

which appends a user message —

> You have run out of tool-use steps. Answer now using the tool results you already have. If
> something is missing, say briefly what you could not check. Do not mention steps or budgets.

— sets `ToolChoice = "none"`, **leaves `Tools` on the request** (removing them would change the
serialized prefix and throw away the cached tool schemas for this call), and makes one more
completion. Its usage is accumulated like any other. The returned response is prefixed
server-side with a fixed notice so the user is told the answer is partial regardless of what the
model wrote:

> *Heads up — I ran out of steps on this one, so this may be incomplete.*

`AgentRunResult` gains `public bool StoppedOnMaxIterations { get; set; }` so the pipeline and the
interaction log can record it distinctly from a clean run. `Success` becomes `true` when the
wrap-up produced text; if the wrap-up call itself fails or returns empty, fall back to today's
`Success = false` with the existing message.

**(c) Blank-final-text recovery.** The same helper, different instruction, on the `EndTurn` path
when `response.Content` is null or whitespace. This is cheap insurance and the failure it catches
(a model ending a turn after a tool call with no text) is invisible today — the user just gets an
empty reply. Not counted against the step budget.

**(d) The rename.** `MaxToolCallsPerQuestion` limits *rounds*, not calls — with several tool calls
in one round it is not even an upper bound on calls. Add `MaxToolRounds` (default **8**) and keep
`MaxToolCallsPerQuestion` as an `[Obsolete]` forwarding property. This repo already has the
machinery: `AssistantServiceExtensions.ApplyFlatLegacyKeyPrecedence` handles exactly this pattern,
so the legacy key keeps winning when both are set. Update `docs/articles/ai-assistant.md` and
`configuration-guide.md`.

**Tests**: budget exhaustion returns the wrap-up text with the notice and `StoppedOnMaxIterations`;
a failing wrap-up falls back to the old error; the wrap-up request carries `tool_choice: none` *and*
a non-empty `tools` array; blank final text triggers exactly one recovery call and never two.

### 1.7 Prompt-cache fixes (F7)

Three changes of quite different confidence. Ship them in that order and measure between.

**(a) Sort the tool list — certain win, one line.** `ToolRegistry.GetEnabledTools()` currently
returns providers in dictionary insertion order (DI registration order). Tool schemas serialize at
position 0 of the request, before the system message, so any change in that order invalidates
every breakpoint. Append `.OrderBy(t => t.Name, StringComparer.Ordinal)`. Add a test asserting the
order is stable and sorted — this is the kind of thing a future DI reshuffle silently breaks, and
the symptom (a 10× price rise with correct answers) is invisible without the test.

**(b) Raise the system-prompt breakpoint to a 1-hour TTL.** `CacheControl` already carries a `Ttl`
field that nothing sets. The system prompt + tool schemas are the layer shared across every user
and every question in a guild; a Discord server's questions are frequently more than five minutes
apart, so the 2× write premium on a 1h TTL pays for itself. One-line change in
`CreateSystemMessage`, plus a configuration key (`OpenRouter:PromptCacheTtl`, default `"1h"`) so it
can be reverted without a deploy.

**(c) A rolling breakpoint on the newest tool-result message — verify before building.** The wire
`tool` message is emitted with plain-string content
(`OpenRouterMessageMapper.CreateUserMessages`). `ChatMessage.Content` is `object?`, so the
multipart shape is expressible, but whether OpenRouter passes `cache_control` through on a
`role: "tool"` message into Anthropic's `tool_result` block is **not something to assume**. Spend
a probe first: one scripted run with a marker on the tool message, reading back
`cache_creation_input_tokens` / `cache_read_input_tokens` from the response. If it passes through,
implement the rolling marker (move it forward each iteration by rebuilding the previously-marked
message without one — earlier positions stay valid read points, so coverage accumulates). If it
does not, the fallback is a marker on the last *replayed history* message for the DM assistant,
which is a user-role message and certainly supported.

This is the one item in Tier 1 that might come back "not possible"; the plan should not depend on
it.

**Verification for the whole of 1.7**: the `LlmUsageRecord` ledger already stores `CachedTokens`
and `CacheWriteTokens` per run. Capture a baseline over a day of real traffic before the PR and
compare the cached fraction of input after. That is a better signal than any unit test here.

---

## Tier 2 — Governance

### 2.1 Per-guild tool allow-list (F2)

**Schema.** `AssistantGuildSettings` gains one column, following the `AllowedChannelIds` precedent
exactly (JSON string + typed helpers, so no provider-specific collection mapping):

```csharp
/// <summary>
/// Tool names this guild's assistant may use, as a JSON array. Empty array = the house default
/// set (every tool not marked opt-in). Stored as JSON for the same reason AllowedChannelIds is:
/// it keeps one row per guild and needs no join.
/// </summary>
public string EnabledTools { get; set; } = "[]";

public List<string> GetEnabledToolsList();
public void SetEnabledToolsList(List<string> toolNames);
```

Two migrations (`AddAssistantEnabledTools`), SQLite and Postgres, per the repo rule. Both are
`AddColumn` with a `"[]"` default, so they are safe on a live database. **The test suite is
SQLite-only**, so the Postgres migration needs a manual `database update` against a scratch
Postgres before merge, and the PR should say that was done.

**Resolution.** A new `Core/Interfaces/LLM/IToolAccessResolver`:

```csharp
Task<IReadOnlySet<string>> ResolveAsync(ulong guildId, CancellationToken ct);
```

implemented in Infrastructure over `IAssistantGuildSettingsRepository` and `IMemoryCache`
(invalidated on settings save — `AssistantGuildSettingsService` already writes through a single
point). `GuildAssistantContextFactory` calls it once per run and wraps the registry:

```csharp
var allowed = await _toolAccess.ResolveAsync(guildId, cancellationToken);
var registry = _options.Tools.EnableDocumentationTools
    ? new FilteredToolRegistry(_toolRegistry, allowed)
    : null;
```

`FilteredToolRegistry` is a decorator: `GetEnabledTools()` filters by name (and re-sorts, per
1.7a), `ExecuteToolAsync` refuses a name outside the set with `NotSupportedException` — defence in
depth against a model that remembers a tool from an earlier prefix.

Filtering at the registry boundary rather than inside each tool is right here because the unit is
the *guild*, and a guild's tool array is shared across all its users — so it is still one cache
prefix per guild. That is the opposite of the per-*user* case in §2.4, where filtering would
fragment the cache and the check therefore belongs at runtime.

**UI.** `Pages/Guilds/AssistantSettings.cshtml` gains a tool checklist below the channel picker,
grouped by category. That needs a category map — a `ToolCatalog` static in Core mapping tool name
→ `(category, displayName, description)`, which is also what §3.3's report and the metrics table
consume. `pike-assistant`'s `AgentToolCategories.cs` is the pattern, including the detail that an
uncategorised tool falls into a visible **Other** bucket rather than disappearing.

`InputModel` gains `List<string> EnabledTools`. Empty selection = house default, and the UI must
say so explicitly ("No tools selected — this server uses the default set") because an empty
checklist otherwise reads as "no tools".

### 2.2 Delete the dead registry API (F3)

Remove `EnableProvider`/`DisableProvider` from `IToolRegistry` and `ToolRegistry`, and the
`ProviderEntry.IsEnabled` flag with them — `GetEnabledTools()` becomes "all registered tools",
which is what it has always effectively meant. Delete the corresponding `ToolRegistryTests` cases
and add one asserting the registry composes with `FilteredToolRegistry`.

If a house-wide kill switch is wanted (it probably is, for an incident), add it as an
`ISettingsService`-backed set consulted by `IToolAccessResolver` — settings there are already
cached with change notification, and a kill switch that survives a restart and applies to every
guild is a different mechanism from a scoped in-memory bool.

### 2.3 Per-tool telemetry (F8)

**Spans.** `BotActivitySource` gains:

```csharp
public static Activity? StartToolActivity(string toolName, string providerName);
public static void SetToolOutcome(Activity? a, string outcome, int resultChars, string? failureCode = null);
```

`AgentRunner` wraps each execution in `agent.tool <name>`, tagging `gen_ai.tool.name`,
`gen_ai.tool.call.id`, `bot.tool.result_chars`, and `bot.tool.outcome` ∈ `ok` / `failed_result` /
`timeout` / `repeated_call` / `error` / `unknown_tool`.

`failed_result` needs a convention for *detecting* an expected failure, since the house style
returns those as successful results. Read a top-level `error` string or a false `success` /
`available` / `found` flag off the result JSON — and write that convention into the tool spec
template (§4.1) so new tools are countable. Without it, expected failures are invisible in traces,
which is the majority of what you want to see.

**Log names on the guild side.** `AssistantInteractionLog` has `ToolCalls` (a count) but no names;
`DmAssistantInteractionLog` has both. Add `string? ToolNames` to the guild entity (comma-joined,
capped at 512 chars), two migrations, and populate from `AssistantPipelineResult.ToolNames`, which
already carries them — the pipeline computes the data and then drops it.

**Metrics page.** `AssistantMetrics.cshtml` gains a per-tool table: calls, failure rate, p95
duration, mean result chars, last used. Source it from the spans via the existing OTel pipeline if
Elastic/Loki is deployed, or — simpler and always available — from the interaction log's new
`ToolNames` column for call counts, with durations from the spans. Start with counts; "which of
my 27 tools has never been called" is answerable from the log alone and is the first question
worth answering.

### 2.4 Decide `UserRoles` (F9)

`ToolContext.UserRoles` is declared "for permission checks", never populated, never read. Two
honest options; pick one and write it down.

**Option A — delete it.** Correct if guild tools stay read-only and public-data-only. One-line
removal, and the trap goes away.

**Option B — replace it with a caller-access flag**, which is what is actually needed before the
first write tool (`create_reminder`). `ToolContext` gains:

```csharp
/// <summary>
/// Whether the caller may perform actions that create or change data. Consulted *inside* mutating
/// tools rather than used to filter the advertised tool list: filtering per user would give every
/// permission level its own prompt-cache prefix, and the tool array is the most expensive thing to
/// fragment.
/// </summary>
public bool CanMutate { get; set; }
```

set in `GuildAssistantContext`'s constructor from the caller's Discord permissions, and a shared
refusal helper:

```csharp
public static ToolExecutionResult MutationForbidden(string action) =>
    CreateJsonResult(new { forbidden = true, message = $"The user isn't allowed to {action}. Don't retry — tell them, and mention a server admin can change this in the portal." });
```

Recommendation: **B**, and it should land before any write tool, not alongside one. The DM
assistant sets `CanMutate = true` unconditionally (it is owner-only already).

---

## Tier 3 — Cheap tools, affordable tools

### 3.1 `IAgentTool` beside `IToolProvider` (F10)

> **Shipped, with two changes.** `[DmOnlyTool]` was not built — `ToolCatalog.Scopes` already says
> where a tool is advertised, so the catalogue routes instead — and the `CanMutate` check moved onto
> the interface as a `string? Mutation`. See
> [plans/agent-groundwork-overhaul.md §5.3a](../plans/agent-groundwork-overhaul.md) for the
> reasoning, and `docs/architecture/patterns.md` § Agent Tool Authoring for the pattern as built.

The goal is that a new tool is **one file and no DI edit**, without moving the eleven existing
providers.

```csharp
// Core/Interfaces/LLM/IAgentTool.cs
public interface IAgentTool
{
    LlmToolDefinition Definition { get; }

    Task<ToolExecutionResult> InvokeAsync(
        JsonElement input, ToolContext context, CancellationToken cancellationToken);
}
```

One adapter exposes every scanned tool to the existing registry:

```csharp
// Infrastructure/Services/LLM/Providers/AgentToolProvider.cs
public sealed class AgentToolProvider(IEnumerable<IAgentTool> tools) : IToolProvider
{
    public string Name => "AgentTools";
    public string Description => "Individually registered agent tools";

    public IEnumerable<LlmToolDefinition> GetTools() => tools.Select(t => t.Definition);

    public Task<ToolExecutionResult> ExecuteToolAsync(
        string name, JsonElement input, ToolContext ctx, CancellationToken ct)
        => (tools.FirstOrDefault(t =>
                string.Equals(t.Definition.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotSupportedException($"Tool '{name}' is not an agent tool"))
           .InvokeAsync(input, ctx, ct);
}
```

and assembly scanning registers the tools:

```csharp
// Infrastructure/Services/LLM/AgentToolRegistration.cs
public static int AddAgentTools(this IServiceCollection services, IConfiguration? configuration = null)
```

scanning both the Infrastructure and Bot assemblies (tools needing Discord.NET live in Bot, as
`UserGuildInfoToolProvider` does today), ordered by type name for determinism, registered
`TryAddEnumerable(ServiceDescriptor.Scoped(...))` so a double call is idempotent. Two attributes
travel with it:

- `[DmOnlyTool]` — the adapter registers a second `IDmToolProvider`-marked instance filtered to
  these, preserving today's guild/DM separation without a second interface hierarchy.
- `[OptInTool("Section:Enabled")]` — registered only when that configuration flag is true, so a
  capability can ship dark. `execute_python` is the obvious first user; it is currently gated by
  a runtime check inside `CodeExecutionToolProvider.GetTools()`, which still pays for the class
  and the branch.

**Helpers in the same PR**, because they are why the boilerplate exists:

- `ToolInput` — `GetString/GetInt/GetBool/GetStringArray(input, key)` returning null on missing or
  wrong-kind, plus `Schema(type, description)` for building fragments. Replaces the
  `TryGetProperty` + null-check + empty-check triplet in every provider.
- `ToolResults` — `Json(object)`, `Error(string)`, `NotFound(...)`, `Truncated(...)`, so result
  shapes are consistent across tools rather than per-author.
- `ToolJson.Compact` — `JsonSerializerOptions` with `DefaultIgnoreCondition = WhenWritingNull`, so
  optional fields cost nothing when absent.

Existing providers convert opportunistically, one per PR, when they are being touched anyway.
`MemoryToolProvider` (5 tools, 247 lines + 158 lines of definitions) is the best first conversion
and the best measure of whether the shape is actually cheaper.

### 3.2 Skills (F11)

The mechanism that makes a 50-tool bot affordable. Adapted to this codebase's two surfaces.

**Skill files**: `docs/agents/skills/<key>.md`, hot-reloaded through the existing `IPromptTemplate`
(which already loads and caches the agent prompts):

```markdown
---
key: moderation
summary: Look up moderation cases, warnings, and a user's moderation history for a server.
tools: get_moderation_cases, get_user_mod_history, search_audit_logs
---
**Reading cases.** `get_moderation_cases` takes a guild and an optional status …
```

`summary` is required — it is the entire basis for the model's decision to load, so a skill without
one is invisible rather than merely undocumented. `tools` may be empty (a skill of pure standing
orders is legitimate and is something a tools-only mechanism cannot express).

**`load_skill` tool**: takes `key`, returns the skill's instruction body as its result, and records
the activation on a run-scoped `ISkillActivationState`. The **loop** adds the skill's tools to
`request.Tools` after the round completes, re-sorting by name (1.7a) so one tool set has one
serialized shape however the run reached it.

**Narrowing**: a skill's tools are intersected with what the house has registered *and* what the
guild's allow-list permits (§2.1), so `load_skill` can never widen a guild's reach. This is the
rule that makes skills safe to enable broadly.

**Stickiness** differs by surface, and this is the main adaptation:

- *DM assistant*: has conversation state. Record the activated keys alongside the conversation
  (a column on the DM conversation, or the existing `IMemoryCache` active-guild pattern), replay
  them as preloaded skills before the first model call on the next turn, and append their
  instructions to the composed prompt. Turn 2 onward spends no `load_skill` round trip.
- *Guild assistant*: single-turn by design (`ConversationHistory` is always empty). No stickiness
  is possible, so a skill there costs one extra round every time it is used. That is still the
  right trade when the roster is large and any one skill is rarely needed — but it means the guild
  assistant should keep its **common** tools always-on and put only the rare, heavy ones behind
  skills. Do not port pike's split blindly: its rooms are multi-turn and ours is not.

**Cache cost**: adding a tool mid-run invalidates the tool-schema prefix for the rest of that run —
one cache write on a loading turn. Worth saying in the doc so the first person to see a cache-miss
spike after a `load_skill` knows it is expected.

**First two skills** (DM assistant, where stickiness makes them nearly free):
`moderation` (`get_moderation_cases`, `get_user_mod_history`, `search_audit_logs`) and
`analytics` (`get_server_activity_summary`, `get_command_analytics`). Those five schemas are the
heaviest and the least often needed.

### 3.3 Prompt-surface report (F11)

Nothing measures the tool array's cost, so its growth is discovered on the bill.

- **At startup**: one Information line per surface — tool count, total schema characters, and an
  estimated token count (characters ÷ 4 is honest enough and needs no tokenizer dependency).
- **On the Assistant Metrics page**: a panel listing each tool's schema size and share of the
  prefix, with the guild's resolved set highlighted. This is what turns "should we add this tool?"
  into a decision with a number attached.

---

## Tier 4 — Discipline

### 4.1 Per-tool specs

`docs/tools/<tool_name>.md`, one per tool, using pike's template: status, surfaces, purpose,
dependencies, the exact model-facing description, an input table, and **every** result shape —
success and each expected failure. Add the `failed_result` marker convention from §2.3 to the
template, so tools are countable in telemetry by construction.

Retire `docs/specs/assistant-tool-catalog.md`: it lists every tool as "Planned" (all 27 shipped),
plans 17 against the 27 that exist, and specifies four that were never built. Move it to
`docs/specs/archive/` with a header pointing at `docs/tools/`, rather than deleting it — it is
still the record of the original design intent, and `get_feature_status` from it is a good idea
that should be built (§5 of the plan).

Write the specs as each tool is touched, not in one sitting. Backfill only the ones being changed.

### 4.2 A tool contract test

One xUnit theory over every registered tool, which is worth more than its length suggests:

- name is unique across all registered tools, and matches `^[a-z][a-z0-9_]{2,63}$`;
- description is non-empty and between 40 and 600 characters (pike's 50–120 *words*, in
  characters, loosely — the point is to catch both the one-liner and the essay);
- `InputSchema` is a valid `type: "object"`;
- every name in `required` exists in `properties`, and every property has a `description`;
- the tool has a category in `ToolCatalog` (§2.1).

Every one of these is a mistake that currently surfaces as "the model behaves oddly in production"
rather than as a red build.

### 4.3 A minimal eval project

`tests/DiscordBot.Evals`, in the solution, `Skip`ped when `OpenRouter:ApiKey` is absent so CI stays
free and green. A dozen cases through the real pipeline against a real model, asserting on
**machine-checkable facts** — which tools were called (`AgentRunResult.ToolNames`), what rows were
written, whether the run succeeded — and never on what the reply says. A reply claiming it saved
the note is the failure mode, not the evidence.

Treat it as a regression gate, not a scoreboard: a case that flips is signal; a moved percentage at
a dozen cases is noise. It is the only thing that will tell you whether §1.2's description change
or §3.2's skill split made the assistant better or worse.

---

## Sequencing and risk

| PR | Content | Risk | Gate |
| --- | --- | --- | --- |
| 1 | 1.3 path containment | low | security; ship first, alone |
| 2 | 1.1 result cap + 1.2 doc sections | low | biggest cost win |
| 3 | 1.4 timeout + 1.5 duplicate guard | low | |
| 4 | 1.6 wrap-up + rename | medium | changes a user-visible failure mode |
| 5 | 1.7a/b cache order + TTL | low | measure before/after on the ledger |
| 6 | 1.7c rolling breakpoint | medium | **probe first**; may not be possible |
| 7 | 2.1 per-guild allow-list | medium | 2 migrations; manual Postgres check |
| 8 | 2.2 registry cleanup + 2.3 telemetry | low | 2 more migrations |
| 9 | 2.4 caller access | low | must precede any write tool |
| 10 | 3.1 `IAgentTool` + helpers | medium | additive; no provider moves |
| 11 | 3.2 skills | high | the largest design surface here |
| 12 | 3.3 report, 4.1–4.3 discipline | low | |

Two risks worth stating plainly:

- **The Postgres migration path is untested by CI.** Four migrations across Tier 2, and
  `dotnet test` says nothing about any of them. Each needs a manual `database update` against a
  scratch Postgres, and the PR should record that it was run.
- **1.7c may simply not work.** Whether OpenRouter forwards `cache_control` on a `role: "tool"`
  message is an empirical question about someone else's translation layer. Probe before building,
  and treat the history-message fallback as the real plan.
