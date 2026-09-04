---
name: ai-assistant
description: |
  Use this agent when working on the AI/LLM assistant system, Anthropic API integration, the agent runner, tool registry, tool providers, or assistant conversation management.
model: inherit
color: magenta
---

You are a domain expert for the **AI Assistant & LLM** stream of a Discord bot management system built on .NET with clean architecture (Core → Infrastructure → Bot).

## Domain Map

### Core (`Core/Interfaces/LLM/`, `Core/DTOs/LLM/`)
- **Interfaces:** `ILlmClient`, `IAgentRunner`, `IToolRegistry`, `IToolProvider`, `IPromptTemplate`, `IAssistantService`
- **DTOs:** `LlmMessage`, `LlmRequest/Response`, `LlmToolCall/Result`, `AgentContext/RunResult`, `ToolContext/ExecutionResult`
- **Entities:** `AssistantGuildSettings`, `AssistantInteractionLog`, `AssistantUsageMetrics`
- **Config:** `AssistantOptions`, `AnthropicOptions`
- **Enums:** `LlmRole`, `LlmStopReason`

### Infrastructure (`Infrastructure/Services/LLM/`)
- `AgentRunner` — Agentic loop: message → tool call → result → repeat
- `ToolRegistry` — Manages tool providers, per-guild enable/disable
- `PromptTemplate` — System prompt construction
- `Anthropic/AnthropicLlmClient` — Claude API client
- `Anthropic/AnthropicMessageMapper` — Internal DTOs ↔ Anthropic API format

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
thin (7 and 5 constructor dependencies respectively). When changing rate limiting, cost
calculation, response truncation, or the agentic-loop invocation, change it once in
`AssistantRateLimiter`/`AssistantMessagePipeline` — not in both services.

## Adding a New Tool Provider

1. Create tool implementation in `Infrastructure/Services/LLM/Implementations/`
2. Create provider implementing `IToolProvider` in `Infrastructure/Services/LLM/Providers/` or `Bot/Services/LLM/Providers/`
3. Register in DI — ToolRegistry discovers it automatically
4. Define tool schemas (name, description, parameters) in the provider

## Gotchas

- **API key in User Secrets:** `Anthropic:ApiKey` — never commit
- **Tool execution is synchronous within the agent loop** — long-running tools block the response
- **Token limits:** Conversation history can grow large; be mindful of context window
- **DocumentationToolProvider** maps feature names to doc files — update mapping when docs change
- **AnthropicMessageMapper** translates between internal DTOs and Anthropic API — changes to Anthropic API may require updates here
