# Mogwai: Claude Code CLI Integration via DM Assistant

## Overview

Mogwai is a third Discord bot instance running in Docker on a local PC that invokes Claude Code CLI through Discord DMs. It provides an OpenClaw/Claude Code Channels-like experience integrated into the existing bot infrastructure.

**Architecture**: Claude Code as a DM Tool Provider — not a separate service. The existing DM assistant with Haiku as its model sees a `run_claude_code` tool and decides when to delegate. No separate routing layer, no new handler, no new service. The AgentRunner's agentic loop handles it naturally.

**Docker as sandbox** — Mogwai runs in a custom Docker image (`Dockerfile.mogwai`) that extends the base bot image with Node.js, `claude` CLI, and Playwright + Chromium. Claude Code runs with `--dangerously-skip-permissions` since the container itself is the sandbox.

## Why This Works

- Haiku is fast/cheap for triage — it answers simple questions directly, delegates coding to the tool
- The system prompt instructs Haiku on when to use Claude Code
- Follows the exact `CodeExecutionToolProvider` pattern (process spawn, timeout, kill tree)
- Zero changes to AgentRunner, ToolRegistry, or DmAssistantService
- Container provides OS-level sandboxing — no host filesystem access beyond mounted volumes

## New Files

### 1. `src/DiscordBot.Core/Configuration/MogwaiOptions.cs`

Configuration class, section name `"Mogwai"`:

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Enabled` | bool | false | Master toggle — no tools registered when false |
| `ClaudeCliPath` | string | `"claude"` | Path to `claude` binary |
| `WorkingDirectory` | string | `"."` | Working directory for Claude Code sessions |
| `AllowedTools` | string | `"Bash,Read,Glob,Grep,Write,Edit"` | `--allowedTools` whitelist |
| `MaxBudgetUsd` | decimal | `5.00` | `--max-budget-usd` per invocation |
| `MaxTurns` | int | `10` | `--max-turns` per invocation |
| `TimeoutSeconds` | int | `300` | Process timeout (5 min) |
| `MaxOutputLength` | int | `50000` | Truncate CLI output beyond this |
| `AppendSystemPrompt` | string? | null | `--append-system-prompt` extra instructions |
| `UseBareMode` | bool | true | `--bare` flag (skip hooks/skills/MCP) |
| `SkipPermissions` | bool | false | `--dangerously-skip-permissions` (safe inside Docker) |

### 2. `src/DiscordBot.Infrastructure/Services/LLM/Implementations/ClaudeCodeTools.cs`

Static tool definitions (follows `CodeExecutionTools` pattern):

**Tool: `run_claude_code`**
- `prompt` (string, required) — the task/question for Claude Code
- `continue_session` (bool, optional, default true) — resume previous session
- `working_directory` (string, optional) — override default working dir

**Tool: `get_claude_code_status`**
- No parameters — returns session state, last cost, whether a session is active

### 3. `src/DiscordBot.Infrastructure/Services/LLM/Providers/ClaudeCodeToolProvider.cs`

Core implementation, implements `IDmToolProvider`:

- **Process spawning**: Builds `claude -p "{prompt}" --output-format json --allowedTools "..." --max-budget-usd N --max-turns N [--bare] [--resume sessionId] [--append-system-prompt "..."]`
- **Session tracking**: `ConcurrentDictionary<ulong, ClaudeCodeSession>` (userId → {sessionId, cumulativeCost, lastUsed}). In-memory only — sessions are ephemeral.
- **Concurrency guard**: `ConcurrentDictionary<ulong, bool>` tracks in-progress executions. Returns error if user tries concurrent invocations.
- **Output parsing**: Deserializes JSON response `{ result, session_id, duration_ms, total_cost_usd, is_error }`
- **Timeout**: Same linked CancellationTokenSource + KillProcessTree pattern as CodeExecutionToolProvider
- **CLI not found**: Catches Win32Exception, returns helpful error

## Modified Files

### 4. `src/DiscordBot.Bot/Extensions/DmAssistantServiceExtensions.cs`

- Add `services.Configure<MogwaiOptions>(configuration.GetSection("Mogwai"));`
- Add `services.AddScoped<IDmToolProvider, ClaudeCodeToolProvider>();`

### 5. `src/DiscordBot.Bot/Handlers/DmAssistantMessageHandler.cs`

Replace single `SendMessageAsync(response.Response)` with chunked sending:
- ≤2000 chars: single message (current behavior)
- ≤8000 chars: split on newline boundaries into ≤2000 char chunks, send sequentially
- \>8000 chars: upload as `.md` file attachment via `SendFileAsync`

Extract to private helper `SendResponseAsync(IMessageChannel channel, string response)`.

### 6. `src/DiscordBot.Core/Configuration/DmAssistantOptions.cs`

- Increase `MaxResponseLength` default from 1800 to 50000 since the handler now handles Discord's limit via chunking.

### 7. `docs/agents/dm-owner-agent.md`

Add Claude Code tool section documenting `run_claude_code` and `get_claude_code_status` tools, including guidance on when to use Claude Code vs answering directly.

### 8. `src/DiscordBot.Bot/appsettings.json`

Add Mogwai section (disabled by default):
```json
"Mogwai": {
  "Enabled": false,
  "ClaudeCliPath": "claude",
  "WorkingDirectory": ".",
  "AllowedTools": "Bash,Read,Glob,Grep,Write,Edit",
  "MaxBudgetUsd": 5.00,
  "MaxTurns": 10,
  "TimeoutSeconds": 300,
  "MaxOutputLength": 50000,
  "UseBareMode": true
}
```

## Docker Files

### 9. `Dockerfile.mogwai`

Separate Dockerfile extending the bot with Mogwai dependencies. Reuses the same build stage as the main Dockerfile, adds to the runtime stage:

- Node.js 22 LTS (for claude CLI)
- Claude Code CLI (`npm install -g @anthropic-ai/claude-code`)
- Playwright + Chromium (`npx playwright install --with-deps chromium`)
- git (for worktree isolation within container)
- All existing runtime deps (ffmpeg, python3, libsodium, libopus)

Image will be ~1.5-2GB. `ANTHROPIC_API_KEY` passed via env var (no browser auth needed).

### 10. `docker-compose.mogwai.yml`

Standalone compose file for Mogwai:

```yaml
services:
  mogwai:
    build:
      context: .
      dockerfile: Dockerfile.mogwai
    env_file: .env.mogwai
    environment:
      Mogwai__Enabled: "true"
      Mogwai__WorkingDirectory: /workspace
      Mogwai__SkipPermissions: "true"
      DmAssistant__Enabled: "true"
      DmAssistant__Model: "claude-haiku-4-5-20251001"
      ANTHROPIC_API_KEY: ${ANTHROPIC_API_KEY}
    volumes:
      - mogwai-data:/app/data
      - ./:/workspace:rw
    ports:
      - "7310:5000"
    restart: unless-stopped
```

## Implementation Phases

1. **Phase 1 — Configuration & Tool Definitions**: `MogwaiOptions.cs` + `ClaudeCodeTools.cs` + appsettings (no behavior change)
2. **Phase 2 — Tool Provider & DI**: `ClaudeCodeToolProvider.cs` + DI registration
3. **Phase 3 — Response Chunking**: DmAssistantMessageHandler chunking + MaxResponseLength adjustment
4. **Phase 4 — System Prompt**: Update `dm-owner-agent.md` with Claude Code tool guidance
5. **Phase 5 — Docker**: `Dockerfile.mogwai` + `docker-compose.mogwai.yml`

## Key Design Decisions

- **No new DB entities/migrations** — session IDs are in-memory, ephemeral
- **No new handler** — reuses existing DmAssistantMessageHandler
- **No new service** — reuses existing DmAssistantService + AgentRunner
- **Haiku model** — configured via existing `DmAssistantOptions.Model`
- **Disabled by default** — `MogwaiOptions.Enabled = false` means zero impact on other bot instances
- **Concurrency guard** — prevents overlapping Claude Code invocations per user
- **Separate Dockerfile** — keeps other bot images lean (~500MB vs ~2GB)
- **Docker = sandbox** — `--dangerously-skip-permissions` is safe; container limits blast radius
- **API key auth** — no browser needed inside container

## Verification

1. Build: `docker compose -f docker-compose.mogwai.yml build`
2. Run: `docker compose -f docker-compose.mogwai.yml up -d`
3. Verify `claude --version` works inside container
4. Verify Playwright: `docker exec mogwai npx playwright --version`
5. DM with simple question → Haiku answers directly (no Claude Code)
6. DM coding task → Haiku delegates to `run_claude_code` tool
7. Verify JSON output parsed, response chunked/sent correctly
8. Follow-up coding message → verify `--resume` uses stored session ID
9. Verify timeout kills process after configured seconds
10. Verify budget limit passed through to CLI
11. Test concurrent message rejection
