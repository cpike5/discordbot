# OpenRouter Migration Plan — LLM Integration Overhaul

**Status:** Proposed
**Date:** 2026-08-30
**Reference implementation:** `pike-assistant` (`src/PikeAssistant.Server/OpenRouter/` + `docs/planning/openrouter-migration.md` there — the migration is shipped and battle-tested; port its patterns, trimmed to this bot's needs)

## Goal

Replace the Anthropic SDK integration (`Anthropic` NuGet package v12.2.0) with a direct OpenRouter integration: an owned typed `HttpClient` against OpenRouter's OpenAI-compatible `POST /chat/completions`, with owned wire records and no vendor SDK. This unlocks any OpenRouter-routed model (Claude, GPT, Gemini, open-weight models) behind the existing provider-agnostic abstraction.

## Why this is a small migration

The bot's LLM layer already has a clean seam. All Anthropic-specific code is confined to:

| File | Role |
| --- | --- |
| `src/DiscordBot.Infrastructure/Services/LLM/Anthropic/AnthropicLlmClient.cs` | `ILlmClient` impl — the only API call site |
| `src/DiscordBot.Infrastructure/Services/LLM/Anthropic/AnthropicMessageMapper.cs` | DTO ↔ SDK-type mapping |
| `src/DiscordBot.Bot/Extensions/AssistantServiceExtensions.cs` | DI construction of `AnthropicClient`, key gating |
| `src/DiscordBot.Core/Configuration/AnthropicOptions.cs` | Options |

Everything else — `AgentRunner`, `ToolRegistry`, all tool providers, all `DiscordBot.Core/DTOs/LLM/` DTOs, `AssistantService`/`DmAssistantService`/`FeatureRequestConversationService` — is provider-agnostic and stays untouched. There is no Phase-0 "extract owned types" step like pike-assistant needed; that work was effectively already done here.

**Explicitly out of scope** (Claude Code *CLI* integrations, not the Messages API):
- Mogwai (`ClaudeCodeToolProvider`, `ClaudeCodeTools`, `MogwaiOptions`, `Dockerfile.mogwai`, `ANTHROPIC_API_KEY` env for the CLI)
- `DiscordBot.DocGen` (`DocGen:ClaudeBinaryPath`)

Also out of scope for this pass (possible follow-ups): streaming responses, per-guild model selection, a live `/models` directory, reasoning/effort configuration, vision/PDF input, OpenRouter web-search server tool. The bot uses none of these today.

## Architecture after migration

```
AssistantService / DmAssistantService / FeatureRequestConversationService
        └─ IAgentRunner (AgentRunner)            [unchanged]
              └─ ILlmClient
                    └─ OpenRouterLlmClient       [new]
                          ├─ OpenRouterMessageMapper   [new]
                          ├─ owned wire records        [new, ported from pike-assistant]
                          └─ HttpClient (typed, via AddHttpClient)
```

New folder: `src/DiscordBot.Infrastructure/Services/LLM/OpenRouter/`. The `Anthropic/` folder and the NuGet reference are deleted in the cutover commit.

---

## Phase 1 — Wire records, mapper, client (new code + tests; no wiring changes)

### 1.1 Owned wire records — `Services/LLM/OpenRouter/`

Port from pike-assistant's `ChatCompletionRequest.cs` / `ChatCompletionResponse.cs`, trimmed to what this bot uses:

- `OpenRouterJson.Options` — the single serializer config: `JsonSerializerDefaults.Web` + `SnakeCaseLower` naming + `WhenWritingNull` ignore. This is the whole serialization story.
- `ChatCompletionRequest { Model, Messages, MaxTokens, Temperature, Tools, ToolChoice, Provider }`
- `ChatMessage { Role, Content (string or List<ContentPart>), ToolCallId, ToolCalls }`
- `ContentPart { Type, Text, CacheControl }` + `CacheControl { Type = "ephemeral" }` (for prompt caching pass-through)
- `ToolDefinition { Type = "function", Function }` + `FunctionDefinition { Name, Description, Parameters (JsonElement) }`
- `ProviderPreferences { RequireParameters }`
- `ChatCompletionResponse { Id, Model, Choices, Usage, Error }` with `Message` / `FinishReason` convenience accessors
- `ResponseMessage { Content, ToolCalls }`, `ToolCall { Id, Type, Function }`, `FunctionCall { Name, Arguments }` — **`Arguments` is a JSON string**, not an object
- `TokenUsage { PromptTokens, CompletionTokens, Cost (decimal?), PromptTokensDetails }` with `CacheReadTokens`/`CacheWriteTokens` accessors
- `ApiErrorBody { Message, Code, Metadata }` — OpenRouter can return an error object **on a 200**

Drop from the port: streaming chunk types, reasoning config, modalities/images, plugins, annotations, stream options. Add `Temperature` (pike-assistant doesn't send it; we want it — see 1.4).

### 1.2 `OpenRouterMessageMapper` (replaces `AnthropicMessageMapper`)

The structural translation, per pike-assistant's feature-equivalence map:

| Concern | Anthropic (today) | OpenRouter (new) |
| --- | --- | --- |
| System prompt | Top-level `System` param; `LlmRole.System` in messages **throws** | First message `{role:"system"}`. Remove the throw; `LlmRequest.SystemPrompt` maps to the leading system message |
| Tool definitions | Schema decomposed into `Properties`/`Required` | Pass `LlmToolDefinition.InputSchema` **verbatim** as `function.parameters` — simpler than today |
| Assistant tool calls | `ToolUseBlockParam` content blocks | `assistant.tool_calls[] {id, function:{name, arguments}}`; `arguments` serialized from the `JsonElement` to a JSON string |
| Tool results | `ToolResultBlockParam` on a **user**-role message | One `{role:"tool", tool_call_id, content}` message **per result**. Mapper detects `LlmMessage.ToolResults` and fans out; `IsError` results prefix content with `"Error: "` (no `is_error` on the wire) |
| Tool call input | `JsonElement` (parsed by SDK) | `JsonDocument.Parse(arguments)` — guard malformed JSON with a structured tool-level error, not an exception |
| Stop reason | `end_turn`/`tool_use`/`max_tokens` | `finish_reason`: `stop`→`EndTurn`, `tool_calls`→`ToolUse`, `length`→`MaxTokens`; also treat a non-empty `tool_calls` array as `ToolUse` regardless of `finish_reason` (defensive — some providers report `stop`) |
| Usage | `input_tokens`/`output_tokens`/cache fields | `prompt_tokens`/`completion_tokens`/`prompt_tokens_details.cached_tokens` → existing `LlmUsage` fields; `usage.cost` → `LlmUsage.EstimatedCost` (finally populated with real billed USD) |
| Prompt caching | `CacheControlEphemeral()` on system block | `cache_control: {type:"ephemeral"}` on the system message's content part (as a `ContentPart` list). Honoured when OpenRouter routes to Anthropic models; harmless elsewhere |

### 1.3 `OpenRouterLlmClient : ILlmClient`

Modelled on pike-assistant's `OpenRouterClient` but keeping this bot's retry posture:

- Typed `HttpClient` (`AddHttpClient<ILlmClient, OpenRouterLlmClient>`): `BaseAddress` from options (ensure trailing slash), `Authorization: Bearer` from options (config-only key — no DB key rotation here), optional `HTTP-Referer`/`X-Title` headers.
- `POST chat/completions` with `JsonContent.Create(request, options: OpenRouterJson.Options)`.
- Error contract (port verbatim from pike-assistant — these are hard-won): non-2xx throws with status + parsed `error.message` + per-status hint (401 key, 402 credits, 404 bad model slug, 429 rate limit); **error object on a 200 throws**; no usable choice throws; bodies truncated before logging.
- Retry: keep the existing exponential-backoff loop (`MaxRetries`, `RetryBaseDelayMs`, per-attempt timeout via linked CTS) but key transient detection on **real status codes** (429, 5xx, timeout) instead of today's string-matching on exception messages — one of the two latent bugs this migration fixes.
- `SupportsPromptCaching => true` (pass-through; see risks).
- When `request.Tools` is non-empty, always send `provider: { require_parameters: true }` — pike-assistant's production bug #64: without it, `openrouter/auto`-style routing can land on a provider without native function calling and the model emits tool-call-shaped text into the visible reply.

### 1.4 Fix the two latent bugs while in here

1. **Temperature is silently dropped** today (`LlmRequest.Temperature` never reaches `MessageCreateParams`). Map it onto the wire request.
2. **String-based transient-error detection** — replaced by status-code checks (1.3).

### 1.5 Tests (all new code testable without wiring changes)

- `OpenRouterMessageMapperTests` — replaces `AnthropicMessageMapperTests.cs` (the only test file importing SDK types). Cover: system-message placement + cache_control, tool-result fan-out to `role:"tool"` messages, error-result prefixing, assistant `tool_calls` round-trip, arguments string↔`JsonElement`, finish-reason mapping incl. the defensive tool_calls-with-stop case, usage/cost mapping.
- `OpenRouterWireTests` — port pike-assistant's contract pins: snake_case serialization, `WhenWritingNull` omission, `function.parameters` passthrough, `provider.require_parameters`, error body on 200.
- `OpenRouterLlmClientTests` — stub `HttpMessageHandler` (pike-assistant's `StubHandler` pattern): retry on 429/5xx, no retry on 400/401, per-status error messages, timeout, error-on-200. This closes the existing coverage gap (there was never a test for `AnthropicLlmClient`).
- `AgentRunnerTests` (861 lines, mocks `ILlmClient`) — **must pass unchanged**; this is the regression harness for the loop.

## Phase 2 — Cutover (config, DI, model slugs, cost)

### 2.1 `OpenRouterOptions` (replaces `AnthropicOptions`)

`src/DiscordBot.Core/Configuration/OpenRouterOptions.cs`, section `"OpenRouter"`:

| Key | Default |
| --- | --- |
| `OpenRouter:ApiKey` | `""` (user secrets / `OpenRouter__ApiKey`) |
| `OpenRouter:BaseUrl` | `https://openrouter.ai/api/v1/` |
| `OpenRouter:DefaultModel` | `anthropic/claude-sonnet-4` |
| `OpenRouter:MaxRetries` | `3` |
| `OpenRouter:TimeoutSeconds` | `300` |
| `OpenRouter:RetryBaseDelayMs` | `1000` |
| `OpenRouter:EnablePromptCachingByDefault` | `true` |
| `OpenRouter:AppUrl` / `AppTitle` | `null` / `"DiscordBot"` (attribution headers) |

### 2.2 DI + key gating

- `AssistantServiceExtensions.AddAssistant`: bind `OpenRouterOptions`, gate on `OpenRouter:ApiKey`, drop the `AnthropicClient` singleton, register the typed `HttpClient`.
- `DmAssistantServiceExtensions.AddDmAssistant`: same gate change (reads `Anthropic:ApiKey` today at line ~66).
- Three total `Anthropic:ApiKey` read sites move to `OpenRouter:ApiKey`; the "no key ⇒ LLM features disabled, migrations still run" behavior is preserved.
- Remove `<PackageReference Include="Anthropic" ...>` from `DiscordBot.Infrastructure.csproj`; delete the `Anthropic/` folder.

### 2.3 Model slugs

`claude-sonnet-4-20250514` → OpenRouter slug (`anthropic/claude-sonnet-4`) in five places: `OpenRouterOptions.DefaultModel` (was `AnthropicOptions`), `AssistantOptions.Model`, `DmAssistantOptions.Model`, `FeatureRequestsOptions.RequirementsGatheringModel`, `appsettings.json` → `DmAssistant:Model`.

### 2.4 Cost accounting

`AssistantOptions`/`DmAssistantOptions` hard-code Claude Sonnet per-million pricing into `CalculateCost`. OpenRouter returns real billed USD in `usage.cost`:

- Prefer `LlmUsage.EstimatedCost` (now populated) when present; fall back to the configured per-million rates when null (**null means "not reported", never 0**).
- The configured rates stay as fallback but stop pretending to be authoritative; note in each options class that they only apply when OpenRouter omits cost.
- `AssistantUsageMetrics` / `AssistantInteractionLog` persistence and the `AssistantMetrics.cshtml` page need no schema change — values just get more accurate. Verify cached-token columns still populate when routed to Anthropic models (see risks).

## Phase 3 — Docs, deploy files, consent copy

- **Consent/privacy (user-facing, not optional):** `Pages/Account/Privacy.cshtml` and `Commands/ConsentModule.cs` name Anthropic/Claude as the data processor. Message content now flows to OpenRouter *and* the routed provider — update the copy. (Pike-assistant bumped a consent version stamp for this; this bot has no version stamp on consent, so a copy update is the minimum. Flag for the user whether re-consent is wanted.)
- Deploy/config: `.env.example` (`Anthropic__ApiKey` → `OpenRouter__ApiKey`; leave `ANTHROPIC_API_KEY` — that's the Mogwai CLI), `docs/deployment/.env.production.example`, `docker-compose*.yml`, `README.md`.
- `CLAUDE.md` secrets list (`Anthropic:ApiKey` → `OpenRouter:ApiKey`), `CLAUDE-REFERENCE.md` options table row.
- `.claude/agents/ai-assistant.md` — repo maintenance rule requires updating the agent definition in the same PR (it names `AnthropicLlmClient`/`AnthropicMessageMapper`/`AnthropicOptions` and the key-gating gotcha).
- `docs/specs/llm-abstraction-architecture.md` — replace the "Anthropic Implementation" section; its "Adding a New LLM Provider" section already sketches exactly this seam.
- `docs/articles/ai-assistant.md` (already stale — cites `Claude:ApiKey` and an old SDK), `configuration-guide.md`, `environment-configuration.md`, deployment articles, `docs/architecture/*` and `docs/requirements/*` mentions.
- Leave alone: `docs/articles/mogwai.md`, `docs/features/mogwai-claude-code-integration.md`.

---

## Delivery strategy

Pike-assistant's process lesson: isolate mechanical diffs from judgement-heavy ones, then cut over in **one release** (running both providers side-by-side needs two keys and two consent stories — not worth it). Here:

- **PR 1 (or commits 1–n):** Phase 1 — additive only, nothing wired, full test suite green including untouched `AgentRunnerTests`.
- **PR 2 (or the cutover commit):** Phases 2 + 3 together — DI switch, SDK removal, config, docs, consent copy.

Verification before cutover merge: `dotnet build` + `dotnet test`; a smoke run against real OpenRouter with a low-cost model exercising a tool-calling conversation (the `AgentRunner` loop end-to-end), confirming tool round-trips, usage/cost fields, and cached-token reporting.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| **Silent cache regression** — a broken prefix answers correctly at ~10× input price (pike-assistant's biggest cost risk) | Keep `cache_control` on the system message; verify `cached_tokens` > 0 on the second call of a smoke conversation; metrics page already displays cached tokens, so drift is visible |
| Provider without function calling via routing | `provider.require_parameters: true` on every tools-bearing request |
| `finish_reason` variance across providers | Defensive mapping: non-empty `tool_calls` ⇒ `ToolUse` |
| Non-Anthropic models ignore `cache_control` | Accepted: cached tokens read 0, cost falls back to real `usage.cost`; caching only ever applied to Anthropic-family models anyway |
| Malformed `arguments` JSON from a provider | Parse failures become a structured tool error result fed back to the model, not an exception |
| Cost fields drift on non-Anthropic models | Prefer `usage.cost` (billed truth) over computed per-million estimates |
| Extra network hop / OpenRouter outage | Existing retry loop; status-code-based transient detection |

## Decisions taken (flag if you want these changed)

1. **Config-only API key** — no DB-stored key/rotation (pike-assistant has an admin-managed encrypted key; this bot has no equivalent admin surface, and user secrets/env is its existing pattern).
2. **No streaming** — `ILlmClient` stays buffered-only; Discord replies are single messages. Pike-assistant's separate `IOpenRouterStreamingClient` interface pattern is noted for a future follow-up.
3. **No reasoning/effort parameter** — omitted entirely, leaving provider defaults. If a routed model turns thinking on by default and latency/cost matters, add `reasoning: {enabled:false}` later (pike-assistant sends this explicitly on forced-tool calls).
4. **Default model stays Claude Sonnet** (via OpenRouter slug) — behavior-preserving cutover; model experimentation is a config change afterwards.
5. **`AssistantOptions`/`DmAssistantOptions`/`FeatureRequestsOptions` keep their `Model` keys** — only the values change to slugs. No per-guild model selection in this pass.
