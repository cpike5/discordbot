# Agent Groundwork Overhaul

The umbrella plan: extract the agent engine into its own project, then rebuild the tooling
groundwork inside it. This supersedes the sequencing in the two documents it sits over — they stay
as the detail.

| Document | What it holds |
| --- | --- |
| [plans/agent-tooling-improvements.md](./agent-tooling-improvements.md) | The review. Findings F1–F13, evidence, and why each matters. |
| [specs/agent-tooling-implementation.md](../specs/agent-tooling-implementation.md) | The code shape of each enhancement — option keys, interfaces, migrations, tests. Sections referenced below as *impl §x.y*. |
| **This document** | The extraction, and the order the whole overhaul runs in. |

**Status**: Every phase, 0 through 6, is implemented — see §4.3a and §5.1a for where the manifest
and the spec needed correcting, §5.2a for what Phase 3 shipped, §5.3a for Phase 4, §5.4a for
Phase 5, and §5.5a for Phase 6. **F13 (impl §1.3) has still not shipped**; it is the one piece of
this plan that remains open, and it is still meant to go out as its own PR.

---

## 1. Why extraction goes first

The earlier recommendation was to extract *after* the loop fixes, on the reasoning that four PRs
all edit `AgentRunner` and a file move crossing them is churn. That was the right call for
shipping the loop fixes alone as incremental maintenance. It is the wrong call for a deliberate
overhaul, and the difference is worth stating rather than quietly reversing:

- Every enhancement after Phase 1 lands in its **final home**, written once. Otherwise the loop
  work is written against `DiscordBot.Infrastructure`, moved, and its tests re-homed — the same
  code reviewed twice.
- The extraction is the only step that is a **pure move with no behaviour change**. That property
  is worth a great deal at review time, and it evaporates the moment it is interleaved with logic
  changes.
- The boundary itself surfaces design decisions (§4) that the enhancements would otherwise bake in
  on the wrong side — `ToolContext`'s Discord fields and the engine's telemetry source are both
  decisions that get harder, not easier, after another 1,500 lines land on top.

One thing does not wait: **F13, the documentation path-traversal fix, ships first and alone**
(impl §1.3). It is a security fix in a tool provider, untouched by any of this, and it should not
sit behind a refactor.

A correction to the earlier framing while sequencing this: I described `ILlmUsageRecorder`'s
dependency on the `LlmUsageRecord` entity as an extraction blocker. It is not — that interface is
consumed by the assistant contexts and the pipeline, never by `AgentRunner`, so it stays on the
application side and never crosses the boundary. The entity leak is still worth fixing; it is just
not in this plan's way.

---

## 2. Target architecture

Today, four projects with the agent layer spread across three of them:

```
Core ◄── Infrastructure ◄── Bot
 │            │               │
 └─ LLM DTOs  ├─ engine       ├─ 3 tool providers
    + 17 ifaces├─ 8 providers  ├─ handlers, controllers, pages
              ├─ contexts     └─ DI wiring
              └─ repositories
```

After:

```
         DiscordBot.Agents  (leaf — no DiscordBot references)
                  ▲
                  │
Core ◄── Infrastructure ◄── Bot
```

`DiscordBot.Agents` owns the model-facing machinery and nothing else: the loop, the tool contracts,
the OpenRouter client, the prompt template, and — after Phase 4 and 5 — the tool authoring model
and the skill loader. It knows nothing about Discord, EF Core, guilds, or this bot.

Everything that makes the bot *this* bot stays where it is: tools, contexts, entities,
repositories, handlers, pages, DI.

**Core does not reference Agents.** That is the constraint that keeps the shared kernel
dependency-free, and §3.3 is how it is met.

### Sizing

| Layer | LOC | Moves? |
| --- | --- | --- |
| Engine — loop, registry, prompt template, OpenRouter client + wire records, engine DTOs | ~3,100 | **Yes** |
| Tools — 11 providers + static definitions | ~3,700 | No |
| App glue — guild/DM contexts, pipeline, model resolver, catalog, assistant services | ~2,200 | No |
| Persistence — 8 entities, 6 EF configs, 6 repositories, 12 migrations | ~2,300 | No |
| Web — 2 handlers, 2 controllers, 3 pages, DI extensions | ~1,950 | No |

Roughly 3,100 of 13,300 lines move. "Most of it" stays, and should.

### Why the rest cannot go

- **The entities have real foreign keys.** `AssistantGuildSettings`, `AssistantInteractionLog` and
  `AssistantUsageMetrics` carry `HasOne(...).HasForeignKey(...)` relationships to `Guild` and
  `ApplicationUser` with cascade deletes, configured in `BotDbContext` and spread over 12
  migrations in two provider-specific sets. Moving them means a second `DbContext` (four migration
  sets) or dropping the FKs. Neither is worth it.
- **Tools are supposed to be coupled.** Nine of eleven providers reach domain services through Core
  interfaces; `UserGuildInfoToolProvider` and `BotManagementToolProvider` use Discord.NET directly.
  A tool that reads moderation cases *is* application code. The library's job is to define
  `IToolProvider` / `IAgentTool`; the app's job is to implement them.
- **The contexts are glue by definition** — they exist to translate a guild message or a DM into an
  agent run.

---

## 3. Phase 0 — Prerequisites

Three small changes that make Phase 1 a clean move rather than a negotiation. All are worth doing
on their own merits, which is the test of a good prerequisite.

### 3.1 Delete `IDocumentationToolService`

`Core/Interfaces/IDocumentationToolService.cs` references `DTOs.LLM` and is referenced by
**nothing** — no implementation, no consumer, no test. It is one of only four non-LLM Core files
that point at LLM namespaces, and deleting it removes a quarter of the problem for free.

### 3.2 Split `DTOs/LLM` along its real seam

The folder holds two unrelated things. Separating them is what makes the manifest in §4.1 obvious
instead of arguable.

**Engine contracts** (12 files) — what a model call is made of:
`LlmRequest`, `LlmResponse`, `LlmMessage`, `LlmUsage`, `LlmToolCall`, `LlmToolDefinition`,
`LlmToolResult`, `ToolContext`, `ToolExecutionResult`, `AgentContext`, `AgentRunResult`,
`Enums/LlmRole`, `Enums/LlmStopReason`.

**Application reporting DTOs** (11 files) — what the admin dashboard and model catalogue need:
`AssistantCostRates`, `AssistantPipelineResult`, `LlmCatalogModel`, `LlmCatalogRefreshResult`,
`LlmModelCatalogFilter`, `LlmModelDto`, `LlmResolvedModel`, `LlmUsageApiDtos`,
`LlmUsagePagedRecords`, `LlmUsageQuery`, `LlmUsageTotals`.

The second group stays in Core permanently. Do the split as a namespace-only change first
(`Core.DTOs.LLM` → `Core.DTOs.Llm.Reporting` for the second group) so Phase 1 is a move of one
intact folder.

### 3.3 Confirm Core stays clean

After 3.1 and 3.2, the non-LLM Core files pointing at engine namespaces are:

| File | Resolution |
| --- | --- |
| `IDocumentationToolService` | deleted (3.1) |
| `ILlmModelRepository` | stays — only touches reporting DTOs, which stay in Core |
| `ILlmUsageRepository` | stays — same |
| `Models/FeatureRequests/FeatureRequestConversationState` | holds `List<LlmMessage>`; see below |

`FeatureRequestConversationState` is the only genuine crossing. Two options: reference
`DiscordBot.Agents` from Core (acyclic, but Core stops being dependency-free), or have the state
hold its own minimal turn record and map to `LlmMessage` at the point of use. **Take the second.**
It is a handful of lines, and it preserves the property that Core depends on nothing — which is
the rule the whole layering rests on, and the kind of thing that is easy to give up once and
impossible to get back.

Verify with a build, not by reading: after Phase 1, `DiscordBot.Core.csproj` must still have zero
`ProjectReference` elements.

---

## 4. Phase 1 — The extraction

One PR. **No logic changes** — if a reviewer finds a behaviour difference, it is a bug in the PR.

### 4.1 File manifest

New project `src/DiscordBot.Agents/`, `net8.0`, same `Nullable`/`ImplicitUsings`/
`GenerateDocumentationFile` properties as the other projects (they come from each csproj, not
`Directory.Build.props` — copy them from `DiscordBot.Core.csproj`).

| From | To | Files |
| --- | --- | --- |
| `Core/DTOs/LLM` | `Agents/Contracts` | the 12 engine contracts from §3.2 |
| `Core/Interfaces/LLM` | `Agents/Abstractions` | `ILlmClient`, `IAgentRunner`, `IToolProvider`, `IToolRegistry`, `IPromptTemplate` |
| `Core/Configuration` | `Agents/Configuration` | `OpenRouterOptions` |
| `Infrastructure/Services/LLM` | `Agents` | `AgentRunner`, `ToolRegistry`, `PromptTemplate` |
| `Infrastructure/Services/LLM/OpenRouter` | `Agents/OpenRouter` | all 7 files (client, mapper, wire records, parameter cache, catalog client) |
| `tests/.../Infrastructure/LLM` | `tests/.../Agents` | `AgentRunnerTests`, `PromptTemplateTests`, `OpenRouter*Tests` (5 files) |

**Staying put**, explicitly, because each looks like it might move:

- `IDmToolProvider`, `IAssistantContext`, `IAssistantMessagePipeline`, `IAssistantRateLimiter`,
  `IAssistantAccessGate`, `IAssistantTelemetryReader`, `IGuildAssistantContextFactory`,
  `IDmAssistantContextFactory` — all application concepts.
- `ILlmUsageRecorder`, `ILlmModelCatalogService`, `ILlmModelResolver`,
  `IOpenRouterModelCatalogClient` — persistence and app policy. (`IOpenRouterModelCatalogClient`
  is a judgement call: its *client* moves with the OpenRouter folder, but the interface is consumed
  only by the app's catalog service. Move both and let the app depend on the interface from
  Agents — keeping an OpenRouter interface in Core while its only implementation lives in Agents
  is the worse of the two.)
- `LlmMode`, `LlmModeSettings` — see §4.2.
- `AssistantOptions`, `DmAssistantOptions`, `Configuration/Assistant/*` (914 lines) — these
  describe *this bot's* assistants, not an agent engine.

The three pure files (`AgentRunner`, `ToolRegistry`, `PromptTemplate`) already have zero non-LLM
`DiscordBot.*` usings, which is why this is a move and not a rewrite.

### 4.2 Decisions the boundary forces

Three types do not survive the move unchanged. Deciding them here, rather than discovering them
mid-move:

**`ToolContext` keeps its ids, loses `UserRoles`, and gains a bag.** It currently carries
`UserId`, `GuildId`, `ChannelId`, `MessageId`, `List<string> UserRoles`, and `ulong? ActiveGuildId`.
The four ids stay: they are opaque correlation identifiers, and a tool needs them in every
implementation — genericising them would make every tool worse to write for a purity that buys
nothing in a single-app repo. `UserRoles` is deleted: it is declared "for permission checks",
never populated, never read (F9), and its replacement is decided in Phase 3. `ActiveGuildId` is a
DM-assistant concept and moves into a `Dictionary<string, object?> Items` bag that the app
populates and DM tools read by key.

**`LlmMode` stays in Core.** It enumerates `GuildAssistant` / `DmAssistant` / `FeatureRequests`
and carries their settings keys — application taxonomy. `AgentContext.Mode` becomes a plain
`string? RunKind` the app populates for correlation; the engine never switches on it (it does not
today either).

**The engine gets its own `ActivitySource`.** This corrects impl §2.3, which proposes adding tool
spans to `BotActivitySource` — that type lives in `DiscordBot.Bot/Tracing/`, which the engine
cannot reference. `DiscordBot.Agents` declares `ActivitySource("DiscordBot.Agents")` and
`OpenTelemetryExtensions.cs:199` gains one `tracing.AddSource("DiscordBot.Agents")` line beside
the three already there. Phase 3's telemetry is written against that.

### 4.3 Mechanics that will bite

- **`Dockerfile` copies csproj files individually** for restore-layer caching (lines 24–26). A new
  project that is not added there fails the Docker build with a restore error while
  `dotnet build` succeeds locally. Add `COPY src/DiscordBot.Agents/DiscordBot.Agents.csproj
  src/DiscordBot.Agents/` before the restore.
- **`DiscordBot.sln`** needs the project, or CI builds and tests a solution that does not contain
  it — green, and meaningless.
- **`docfx.json`** globs `src/**/*.csproj`, so API docs pick it up with no change. Worth a build of
  the docs to confirm.
- **`tests/DiscordBot.Tests`** gains a fourth `ProjectReference`. Keep **one** test project — the
  repo's rule is one xUnit project mirroring `src/`, and the engine tests are already grouped under
  `tests/.../Infrastructure/LLM/`, so they move to `tests/.../Agents/` and mirror correctly.
- **Namespace churn is the whole diff.** Do the move with `git mv` and a mechanical
  find-and-replace, in a commit that contains nothing else, so `git log --follow` still works and
  the review is skimmable.

### 4.3a Corrections found while doing it

Two things in the manifest above did not survive contact with the compiler. Both are recorded here
because later phases build on them.

**The OpenRouter model-catalogue client stays in Infrastructure.** §4.1 moved all seven OpenRouter
files and accepted that the app would depend on `IOpenRouterModelCatalogClient` from Agents. That
is not possible: the client's return type, `LlmCatalogModel`, is also a parameter of Core's
`ILlmModelRepository`, so moving it would force `DiscordBot.Core` to reference `DiscordBot.Agents`
and break §4.4's first acceptance criterion. The alternative — a separate Agents-side catalogue
record plus a mapping layer — is a logic change in a PR that is supposed to contain none. So
`IOpenRouterModelCatalogClient`, `OpenRouterModelCatalogClient`, `ModelCatalogWireRecords` and
`OpenRouterModelCatalogClientTests` stay exactly where they are. This costs nothing: the model
catalogue feeds a database table and an admin page, and is application policy rather than engine
machinery. Five OpenRouter files move, not seven.

**Five assistant abstractions move to Infrastructure, not Core.** §4.1 listed `IAssistantContext`,
`IAssistantMessagePipeline`, `IDmToolProvider`, `IGuildAssistantContextFactory` and
`IDmAssistantContextFactory` as staying put, and §3.3 claimed Core would be clean after 3.1 and
3.2. Both overlooked that these five have engine types *in their signatures*: `IAssistantContext`
exposes `IToolRegistry`, `ToolContext` and `List<LlmMessage>`; `IAssistantMessagePipeline` takes an
`IAgentRunner`; `IDmToolProvider` extends `IToolProvider`; the two factories return
`IAssistantContext`. They are application concepts, so they do not belong in Agents; but Core
cannot see the engine, so they cannot stay in Core either. They move to
`Infrastructure/Abstractions/LLM/` (`DiscordBot.Infrastructure.Abstractions.LLM`), the one place
that can see both sides. The rule that falls out is worth keeping: *an assistant abstraction whose
signature is made of engine types lives in Infrastructure; one made only of Core types stays in
Core.* `IAssistantAccessGate`, `IAssistantRateLimiter`, `IAssistantTelemetryReader`,
`ILlmUsageRecorder`, `ILlmModelCatalogService`, `ILlmModelResolver` and
`IOpenRouterModelCatalogClient` all pass that test and stayed.

One smaller note: `ToolRegistryTests` moves to `tests/.../Agents/` alongside the other engine
tests. §4.1 did not list it, but `ToolRegistry` moves, and the test project mirrors `src/`.

### 4.4 Acceptance

- `DiscordBot.Core.csproj` has zero `ProjectReference` elements.
- `DiscordBot.Agents.csproj` has zero `ProjectReference` elements and no Discord, EF Core, or
  ASP.NET package references. Its whole dependency set is
  `Microsoft.Extensions.{Http, Logging.Abstractions, Options.ConfigurationExtensions,
  Caching.Memory}` and `System.Diagnostics.DiagnosticSource`.
- Full test suite green with no test edited beyond its namespace and folder.
- `docker build` succeeds.

---

## 5. Phases 2–6 — The enhancements

Detail in the implementation spec; this is the order and what the extraction changes about each.

### Phase 2 — Engine hardening (impl §1.1, §1.4, §1.5, §1.6, §1.7)

All of it lands in `DiscordBot.Agents`, which is the point of going second.

1. Tool result cap (§1.1)
2. Tool execution timeout (§1.4)
3. Duplicate-call guard (§1.5)
4. Budget wrap-up + blank-text recovery + the `MaxToolRounds` rename (§1.6)
5. Cache ordering and TTL (§1.7a, §1.7b)
6. Rolling tool-result breakpoint (§1.7c) — **probe before building**

The three new knobs (`MaxToolResultChars`, `ToolExecutionTimeoutMs`, `DuplicateToolCallLimit`) ride
on `AgentContext`, populated by the app's pipeline from `IAssistantContext`, exactly as
`MaxToolCallIterations` already does. The engine takes no `IOptions` — it is handed its budget.
That is a better shape than the spec's original and the boundary is what makes it obvious.

Section-aware documentation (§1.2) is a *tool* change and stays in Infrastructure. It can ship in
parallel with this phase; it shares no files.

#### 5.1a What shipped, and the two corrections

Items 1–5 are done, across three commits matching PRs 4–6 in §6. Three notes for later phases:

- **A fourth knob rides along with the three.** `MaxToolCallIterations` was already on
  `AgentContext`; `Assistant:Tools:MaxToolRounds` (default 8) is now what fills it, with
  `MaxToolCallsPerQuestion` kept as an `[Obsolete]` forwarding property that still wins when both
  are set. `ApplyFlatLegacyKeyPrecedence` only walked `AssistantOptions`' own obsolete properties,
  so it could not have enforced that for a key inside a nested group; it now walks the nested
  group's legacy names first and the flat ones second, which preserves "the flat key is oldest and
  wins" while extending the guarantee downward. Any future rename *inside* a nested options group
  gets that precedence for free.
- **The two follow-up calls count in `LoopCount`.** The budget wrap-up and the blank-text recovery
  are real completions and are billed, so they are counted — `LoopCount` feeds the usage ledger's
  `LlmCalls`, and a follow-up that did not appear there would make the ledger lie. A run can
  therefore report `LoopCount == MaxToolRounds + 1`, which is not a budget violation.
- **§1.7c is not attempted.** It needs a probe against live traffic (does OpenRouter forward
  `cache_control` on a `role: "tool"` message into Anthropic's `tool_result` block?), which is not
  something a test run can answer. Item 6 stays open, and the history-message fallback is still the
  real plan. §1.7's stated verification — comparing the cached fraction of input in the
  `LlmUsageRecord` ledger before and after — applies to §1.7b as shipped and needs a day of real
  traffic on each side.

### Phase 3 — Governance (impl §2.1, §2.2, §2.3, §2.4)

Mostly application-side, which is the right answer and is now visible in the diff.

- **Per-guild allow-list** (§2.1): `AssistantGuildSettings.EnabledTools`, `IToolAccessResolver`,
  the settings-page checklist — all app. `FilteredToolRegistry` is the one piece that goes in
  Agents, as a decorator over `IToolRegistry`.
- **Registry cleanup** (§2.2): delete `EnableProvider`/`DisableProvider` — Agents.
- **Telemetry** (§2.3): tool spans in Agents against its own `ActivitySource` (§4.2); the
  `ToolNames` column, migrations, and the metrics page in the app.
- **Caller access** (§2.4): `ToolContext.CanMutate` in Agents; population and enforcement in app
  tools. This is where `UserRoles`' deletion in §4.2 gets its replacement, and it must land before
  any write-capable tool.

Four migrations in this phase. CI is SQLite-only, so each needs a manual `database update` against
a scratch Postgres, recorded in the PR.

#### 5.2a What shipped, and where the sequencing bent

All four sections are done. Three notes for later phases:

- **§2.1 and §2.2 shipped as one commit, not two.** The plan sequenced the allow-list (PR 8) ahead
  of the registry cleanup (PR 9), but `FilteredToolRegistry` has to implement `IToolRegistry`, so
  writing it before the cleanup means writing `EnableProvider`/`DisableProvider` delegations and
  deleting them one commit later. The cleanup is the mechanism the decorator replaces, not a tidy-up
  beside it, so the two went together. The `enabled` parameter came off `RegisterProvider` with them
  — one production call site, and it never passed it.
- **`IToolRegistry` gained a method while losing two.** `FindProviderName(toolName)` attributes a
  tool to its provider without executing it, which §2.3's span tag needs. It is the same first-match
  walk `ExecuteToolAsync` does, so the two cannot disagree.
- **§2.3's `failed_result` convention is now load-bearing, and it is `ToolOutcomes.Classify`.** A
  top-level `error` string, or a false top-level `success`/`available`/`found` flag. §4.1's per-tool
  spec template must require it: a tool that reports an expected failure some other way is invisible
  in the traces, and expected failures are the majority of what is worth seeing. Phase 4's
  `IAgentTool` helpers (`ToolResults`) are the natural place to make the convention the default
  rather than a rule.
- **A catalogue entry is now part of adding a tool.** `ToolCatalog` (`Core/Models/Llm/`) maps name →
  category, label, description, scope, default-on. A tool missing from it still works and still
  appears — in an **Other** bucket — but is absent from the settings checklist and therefore not in
  the house default set. Phase 4's conversion PR and Phase 6's contract test should both check it.

### Phase 4 — Tool authoring model (impl §3.1)

`IAgentTool`, the assembly scan, `AgentToolProvider`, and the `ToolInput` / `ToolResults` /
`ToolJson` helpers — all in Agents. The scan covers the Infrastructure and Bot assemblies, so the
registration extension takes the assemblies to scan as a parameter rather than assuming its own.

After this, a new tool is one file in the app and no DI edit. Convert `MemoryToolProvider` in the
same PR as the proof, and leave the other ten to be converted when they are next touched.

#### 5.3a What shipped, and where the shape differs from the spec

Done, including the `MemoryToolProvider` conversion. Four notes, three of them about the three
Phase 3 rules this phase was supposed to absorb (§5.2a) rather than restate.

- **`[DmOnlyTool]` did not survive, and the catalogue replaced it.** impl §3.1 proposed two
  attributes; the opt-in one (`[OptInTool("Section:Enabled")]`) shipped as specified, but a
  `[DmOnlyTool]` attribute would have put this bot's guild/DM taxonomy inside the engine, and it
  would have been a *second* place declaring where a tool is advertised — `ToolCatalog.Scopes`
  already says. So the catalogue routes instead: `CataloguedAgentToolProvider` (Infrastructure)
  keeps the scanned tools whose scopes include its own surface, and `GuildAgentToolProvider` /
  `DmAgentToolProvider` are its two subclasses, registered as `IToolProvider` and `IDmToolProvider`
  exactly where the hand-written providers are. This is what makes §5.2a's "a catalogue entry is now
  part of adding a tool" structural rather than a rule: an uncatalogued tool has
  `ToolScopes.None`, reaches no surface, and is logged as a warning naming itself and its type.
- **The `CanMutate` check is a declaration, not an `if`.** `IAgentTool.Mutation` is a `string?` —
  the phrase that follows "isn't allowed to" — and `AgentToolProvider` refuses the call *before
  entering the tool*. A bool would have read better on the interface but would have cost the
  refusal its wording ("isn't allowed to use save_note"), and the wording is the part the model
  relays. The refusal itself stays this bot's: `AgentToolProvider` takes an optional
  `Func<string, ToolExecutionResult>` and the two surfaces pass `ToolPermissions.MutationForbidden`,
  so the engine keeps a neutral default (`ToolResults.Forbidden`) and the bot keeps its voice.
- **`ToolResults` makes `ToolOutcomes.Classify` the default, which changes converted tools' traces.**
  `ToolResults.Error`/`.NotFound` return a *successful* result carrying a top-level `error` (and
  `found: false`) key, so a converted tool's bad-input and not-found paths now report
  `failed_result` where the old provider's `CreateError` reported `error` and reached the model
  prefixed `Error: `. That is the house convention arriving, not a regression — but it does mean a
  tool's outcome mix shifts when it is converted, and anyone reading the metrics page across the
  change should expect it.
- **The conversion is pinned byte-for-byte.** The tool array serializes at position 0 of the
  request, so a moved byte in a schema invalidates the whole cached prefix at correct answers and
  ~10x the input price. `MemoryAgentToolsTests` asserts each of the five schemas against the literal
  the deleted `MemoryTools` carried, and each description against its old string; `ToolInput.Schema`
  writes its optional keywords in a fixed order for the same reason. Do the same on the next
  conversion. One intentional difference: `ToolJson.Compact` drops nulls, so a note with no tag no
  longer carries `"tag":null` — cheaper, same meaning.

### Phase 5 — Skills (impl §3.2)

The loader, the roster rendering, and `load_skill` in Agents; the skill markdown in
`docs/agents/skills/`. Note the Dockerfile already ships `docs/agents/` into the image (line 84),
so skill files deploy with no packaging change.

The adaptation from `pike-assistant` still holds and is the main design risk: its rooms are
multi-turn, so activation is sticky and nearly free from turn 2. Our DM assistant can do the same;
**the guild assistant is single-turn**, so a skill there costs an extra round every time it is
used. Guild keeps common tools always-on and puts only rare, heavy ones behind skills.

#### 5.4a What shipped, and the two decisions the design risk came down to

Done, on the DM surface only. The guild surface has the machinery and an empty skill directory,
which is the recommendation above taken literally rather than a job half finished.

The mechanism, in one sentence: *a tool named by any available skill is hidden from the advertised
tool array until one of the skills naming it is loaded*. `SkillToolSet.Compose` applies it, the loop
calls it once at the start and again after any round that activated a skill, and
`docs/architecture/patterns.md` § Agent Skills is the pattern page.

- **The surface split is two directories, not a `surfaces:` field.** A skill file declaring which
  assistants it belongs to would have put this bot's guild/DM taxonomy inside the engine's file
  format — the same mistake `[DmOnlyTool]` would have been in Phase 4, and rejected for the same
  reason. `docs/agents/skills/dm/` and `docs/agents/skills/guild/` instead, one config key each, and
  the engine's loader only ever knows about *a* directory. The cost is that a skill wanted on both
  surfaces is written twice; the two would want different wording anyway, because they advertise
  different tools and only one of them is multi-turn.
- **The narrowing happens twice, and the second time is the one that matters.**
  `SkillSessionFactory` cuts each skill's tool list down to what the run's registry advertises before
  the run starts, so the roster and `load_skill`'s answer are honest about what the model will get.
  `SkillToolSet.Compose` then builds the advertised set from `GetEnabledTools()` and only subtracts,
  so even a session seeded wrongly could not widen anything. On the guild surface that registry is a
  `FilteredToolRegistry` over the guild's allow-list, which is what makes §3.2's "`load_skill` can
  never widen a guild's reach" structural rather than a promise.

Four smaller notes:

- **`load_skill` is an ordinary tool with an ordinary catalogue entry** (category **Skills**, scopes
  `Guild | Dm`, on by default), exactly as Phase 4 requires — the catalogue is what routes it to a
  surface, so it could not have lived in `DiscordBot.Agents` whatever the spec said. The engine's one
  concession is `ISkillActivationState.LoaderToolName`: when a surface has no skills, the loop drops
  that name from the array rather than paying for a schema nothing can use. So a surface with an
  empty skill directory costs exactly what it did before this phase.
- **Stickiness is `IMemoryCache`, not a column.** `DmSkillActivationStore` keys activated skill keys
  by user for 24 hours, the same shape and lifetime as the DM assistant's active guild. That keeps
  Phase 5 migration-free; losing it on restart costs one extra round rather than anything a user
  notices. Clearing the conversation clears it too — the instructions a skill puts in the prompt are
  part of what "start again" means.
- **A pre-activated skill's instructions go back into the prompt, not into history.** The DM
  conversation is a sliding window of user and assistant text; the tool result that carried the
  instructions on the loading turn is not in it. `SkillRoster.Render` re-renders the body of anything
  replayed, which means a DM user's system prompt now varies with their activation set — one
  prompt-cache prefix per combination, on a surface with exactly one user.
- **`PromptTemplate`'s path resolution moved into `PromptPaths`** so the skill directory is found by
  the same application-directory-then-working-directory search a prompt file is. Pure refactor, no
  behaviour change.

### Phase 6 — Discipline (impl §3.3, §4.1, §4.2, §4.3)

Prompt-surface report, per-tool specs in `docs/tools/`, the tool contract test, and the eval
project. The contract test belongs in the existing test project and runs over the app's registered
tools, not the library's — it is asserting house conventions, which are an app concern.

#### 5.5a What shipped, and the one thing the spec had backwards

Done, all four sections in one PR as §6 sequenced it.

- **The report counts what a surface advertises, which is not `GetEnabledTools()`.** §3.3 was
  written before skills existed and would naturally have been implemented against the registry. That
  would have been wrong after Phase 5: a tool held behind an unloaded skill is registered, is
  callable the moment the skill loads, and is **not** in the per-request prefix. `PromptSurfaceReporter`
  therefore rebuilds the surface the way a run sees it — the guild's allow-list decorator, the skill
  session, then `SkillToolSet.Compose` — and measures that. The rows it returns still cover every
  registered tool, flagged `Advertised` / `BehindSkill`, because "what would it cost to turn this on"
  is the second question anyone asks.
- **The measurement is the real wire encoding.** `PromptSurface.Measure` serializes through
  `OpenRouterMessageMapper.ToOpenRouterTools` and `OpenRouterJson.Options` rather than approximating,
  so the number is comparable with the bill and moves automatically if the request encoding ever
  changes. Tokens are characters ÷ 4, stated as an estimate; the useful question is whether a tool is
  2% or 20% of the prefix, and that ratio survives a rough tokenizer.
- **The reporter is registered ungated and answers null.** Every other assistant service is
  registered only when `OpenRouter:ApiKey` is present, but the metrics page must render without one.
  So it is registered unconditionally and resolves `IToolRegistry` and `ISkillSessionFactory` from
  the container — the only service-locator in this stream, and there because absence is one of its
  answers rather than a failure.
- **The contract test found nothing, which is the result.** All 29 registered tools already satisfied
  every rule on the first run. That is worth stating rather than hiding: the value is not the bugs it
  found today but that the next tool cannot quietly skip a catalogue entry, a property description or
  the `failed_result` convention. Two rules were added beyond impl §4.2's list, both from Phase 3–5
  house rules that had been prose until now: a tool declaring `Mutation` is refused through the real
  `AgentToolProvider` when `CanMutate` is false, and a missing required argument comes back through
  `ToolResults` classified `failed_result`. A third, `SkillContractTests`, checks every skill file's
  named tools resolve on that skill's own surface — at runtime a name the surface lacks is dropped
  with a `Debug` line, so a typo and a correct exclusion are indistinguishable in production.
- **Definitions are read from uninitialized instances**, which turns `IAgentTool.Definition`'s "build
  it from a static field" from advice into something the build enforces. A definition composed from
  injected state can differ between two runs, and a differing schema is a cold prompt cache at
  roughly ten times the input price. The hand-written providers are *not* read this way — a provider
  may legitimately decide from options whether to advertise anything at all, as
  `CodeExecutionToolProvider` does — so their definitions come from their static declaration classes.
- **Six tool pages, not twenty-nine.** impl §4.1 says to write them as each tool is touched, so the
  pages that exist are the six tools Phases 4 and 5 converted. `docs/specs/assistant-tool-catalog.md`
  is archived under `docs/specs/archive/` with a header pointing at `docs/tools/`, as specified.
- **The evals are honest about their scope.** "The real pipeline" in impl §4.3 cannot mean
  `DmAssistantService`: that needs a Discord client and a populated guild database, and faking either
  would make the suite a test of the fake. What is real is everything below it — `AgentRunner`,
  `OpenRouterLlmClient`, `ToolRegistry`, `DmAgentToolProvider`, the shipped `IAgentTool`s over a
  throwaway SQLite database, and the shipped skill files. Twelve cases: eight on the memory tools
  (including one that asserts *no* tool was called, because a model that reaches for one on every
  question costs a round trip per question) and four on whether skills load when they should and stay
  out of the way when they should not — which is the open empirical question §7 names.

---

## 6. Sequencing

| PR | Phase | Content | Risk |
| --- | --- | --- | --- |
| 1 | — | F13 doc path containment (impl §1.3) | **still open** — low; ships alone, and is now the only unshipped item |
| 2 | 0 | Delete `IDocumentationToolService`; split `DTOs/LLM`; decouple `FeatureRequestConversationState` | low |
| 3 | 1 | **The extraction** — pure move | medium — large diff, zero logic |
| 4 | 2 | Result cap + timeout + duplicate guard | low |
| 5 | 2 | Budget wrap-up + `MaxToolRounds` rename | medium — changes a user-visible failure mode |
| 6 | 2 | Cache order + TTL; probe and then (maybe) the rolling breakpoint | low / medium |
| 7 | — | Section-aware documentation tool (§1.2) — parallel with 4–6 | low |
| 8 | 3 | Per-guild allow-list + `FilteredToolRegistry` + settings UI + registry cleanup | medium — 2 migrations |
| 9 | 3 | Tool telemetry + `ToolNames` + metrics table | low — 2 migrations |
| 10 | 3 | Caller access | low — blocks write tools until done |
| 11 | 4 | `IAgentTool` + helpers + one conversion | medium |
| 12 | 5 | Skills | **done** — high risk as predicted; see §5.4a |
| 13 | 6 | Report, specs, contract test, evals | **done** — see §5.5a |

Phases 2 and 3 can overlap once PR 3 lands; 4 and 5 should not start until 2 is finished, because
both build on the loop.

---

## 7. Risks

- **The extraction is a large diff that must contain no logic change.** The mitigation is
  discipline, not tooling: `git mv` plus mechanical namespace replacement in one commit, and a
  reviewer whose only question is "did anything change besides a namespace?"
- **The Postgres path is untested by CI.** Four migrations across Phase 3 and a SQLite-only test
  suite. Manual verification per PR, recorded in the PR.
- **§1.7c may be impossible.** Whether OpenRouter forwards `cache_control` on a `role: "tool"`
  message into Anthropic's `tool_result` block is an empirical question about someone else's
  translation layer. Probe first; the history-message fallback is the real plan.
- **Skills were the one phase with genuine design risk**, and the risk turned out to be where §5.4a
  says: what the engine is allowed to know about a surface, and where the narrowing happens. Both are
  settled. What remains open is empirical — whether the DM assistant's model actually loads the right
  skill often enough to be worth the round, which needs real traffic on the metrics page (a
  `load_skill` call followed by the skill's own tools is the shape to look for).

## 8. Documentation to update

Not optional, per the repo's own rule, and it is the thing most likely to be skipped:

- `CLAUDE.md` — the project table gains `DiscordBot.Agents` and the rule about where a new tool
  goes changes.
- `docs/architecture/system-overview.md` — the layer diagram.
- `docs/architecture/service-catalog.md` — engine services move projects.
- `docs/architecture/patterns.md` — the tool-authoring pattern is replaced in Phase 4. **Done**:
  § Agent Tool Authoring.
- `docs/articles/ai-assistant.md` — option renames, new keys, the tool list.
- `docs/articles/configuration-guide.md` — `MaxToolRounds`, `MaxToolResultChars`,
  `DuplicateToolCallLimit`, `PromptCacheTtl`.
- `.claude/agents/ai-assistant.md` — the agent definition for this stream describes the provider
  model; it is wrong the moment Phase 4 lands.
