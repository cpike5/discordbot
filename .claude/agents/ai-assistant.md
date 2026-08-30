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
- **Interfaces:** `ILlmClient`, `IAgentRunner`, `IToolRegistry`, `IToolProvider`, `IPromptTemplate`, `IAssistantService`
- **DTOs:** `LlmMessage`, `LlmRequest/Response`, `LlmToolCall/Result`, `AgentContext/RunResult`, `ToolContext/ExecutionResult`
- **Entities:** `AssistantGuildSettings`, `AssistantInteractionLog`, `AssistantUsageMetrics`
- **Config:** `AssistantOptions`, `OpenRouterOptions`
- **Enums:** `LlmRole`, `LlmStopReason`

### Infrastructure (`Infrastructure/Services/LLM/`)
- `AgentRunner` — Agentic loop: message → tool call → result → repeat
- `ToolRegistry` — Manages tool providers, per-guild enable/disable
- `PromptTemplate` — System prompt construction
- `OpenRouter/OpenRouterLlmClient` — OpenRouter API client (owned typed `HttpClient`, no SDK)
- `OpenRouter/OpenRouterMessageMapper` — Internal DTOs ↔ OpenRouter (OpenAI-compatible) format
- `OpenRouter/ChatCompletionRequest`, `OpenRouter/ChatCompletionResponse` — Owned wire records

### Tool Providers
- `Providers/DocumentationToolProvider` — Maps 13 features to doc files
- `Bot/Services/LLM/Providers/UserGuildInfoToolProvider` — User profiles, guild info, roles
- `Bot/Services/LLM/Providers/RatWatchToolProvider` — Rat Watch leaderboards, stats
- Implementations in `Implementations/DocumentationTools`, `RatWatchTools`, `UserGuildInfoTools`

### Bot Layer
- `Services/AssistantService` — High-level orchestration
- `Handlers/AssistantMessageHandler` — Discord message handler
- `Pages/Guilds/AssistantSettings.cshtml` — Per-guild config
- `Pages/Guilds/AssistantMetrics.cshtml` — Usage metrics dashboard
- **Repos:** `AssistantGuildSettingsRepository`, `AssistantInteractionLogRepository`, `AssistantUsageMetricsRepository`

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
- **Cost:** OpenRouter reports real billed `usage.cost`, which wins over the configured per-million rates; those rates are only a fallback for responses that report no cost.
