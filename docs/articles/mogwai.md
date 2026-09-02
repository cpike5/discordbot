---
uid: mogwai
title: Mogwai — Claude Code CLI Integration
description: Owner-only DM assistant that delegates coding tasks to Claude Code CLI running in Docker
---

# Mogwai — Claude Code CLI Integration

Mogwai is a separate bot instance that runs in Docker and extends the DM assistant with Claude Code CLI capabilities. The bot owner can DM it with coding tasks — file editing, debugging, project analysis — and Haiku decides whether to answer directly or delegate to Claude Code.

## Overview

- **Owner-only access** — Only the Discord application owner can use the assistant. All other DMs receive a placeholder response.
- **Haiku as triage** — `claude-haiku` handles the conversation loop. Simple questions are answered directly. Coding tasks are delegated via the `run_claude_code` tool.
- **Claude Code as a tool** — `ClaudeCodeToolProvider` implements `IDmToolProvider` and registers `run_claude_code` and `get_claude_code_status` as tools into the existing `IAgentRunner` / `IToolRegistry` pipeline. No new handlers or services.
- **Docker as sandbox** — The container provides OS-level isolation. `--dangerously-skip-permissions` is safe to enable inside the container because the container itself limits the blast radius.
- **Session continuity** — Claude Code session IDs are tracked per-user in memory. Follow-up messages automatically resume the previous session via `--resume`.
- **Response chunking** — Long responses (from Claude Code output) are split on newline boundaries into ≤2000-character chunks, or uploaded as a `.md` file attachment when they exceed 8000 characters.

---

## Prerequisites

### Host Machine

| Requirement | Notes |
|-------------|-------|
| Docker Engine 24.0+ | Required to build and run `Dockerfile.mogwai` |
| Separate Discord bot token | Mogwai runs as its own bot application — it must not share a token with the main bot |
| `ANTHROPIC_API_KEY` | Required for both Haiku (DM assistant) and Claude Code CLI inside the container |

### Claude Code CLI (inside container)

The `Dockerfile.mogwai` installs all CLI dependencies. You do not need to install anything on the host beyond Docker.

**What the image installs on top of the base bot image:**
- Node.js 22 LTS
- `@anthropic-ai/claude-code` (npm global install)
- Playwright + Chromium (for web browsing tasks)
- `git` (for worktree isolation within the container)

> **Image size**: Expect ~1.5–2 GB due to Chromium and Node.js dependencies.

---

## Configuration

### `Mogwai` appsettings Section

```json
{
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
}
```

### Property Reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | bool | `false` | Master toggle. When `false`, `ClaudeCodeToolProvider` registers no tools. Zero impact on other bot instances. |
| `ClaudeCliPath` | string | `"claude"` | Path to the `claude` binary. Use the default when the binary is on `PATH` (as it will be inside the container). |
| `WorkingDirectory` | string | `"."` | Working directory passed to Claude Code. In Docker, set to `/workspace` to give access to the mounted project. |
| `AllowedTools` | string | `"Bash,Read,Glob,Grep,Write,Edit"` | Comma-separated tool whitelist passed as `--allowedTools` to the CLI. |
| `MaxBudgetUsd` | decimal | `5.00` | Per-invocation spend cap passed as `--max-budget-usd`. |
| `MaxTurns` | int | `10` | Maximum agentic turns per invocation passed as `--max-turns`. |
| `TimeoutSeconds` | int | `300` | Wall-clock timeout before the process is killed (5 minutes). |
| `MaxOutputLength` | int | `50000` | Output characters beyond this limit are truncated before returning to Haiku. |
| `AppendSystemPrompt` | string? | `null` | Optional extra instructions passed as `--append-system-prompt`. |
| `UseBareMode` | bool | `true` | Passes `--bare` to skip hooks, skills, and MCP configuration. |
| `SkipPermissions` | bool | `false` | Passes `--dangerously-skip-permissions`. Set to `true` in `docker-compose.mogwai.yml` via environment override. |

### Environment Variable Override

All `appsettings.json` values can be overridden with environment variables using `__` as the separator:

```env
Mogwai__Enabled=true
Mogwai__WorkingDirectory=/workspace
Mogwai__SkipPermissions=true
```

---

## Architecture

### ClaudeCodeToolProvider

`ClaudeCodeToolProvider` implements `IDmToolProvider` and is registered into the DI container as a scoped `IDmToolProvider`. The existing `IToolRegistry` picks it up automatically — no changes to `AgentRunner` or `DmAssistantService`.

**Tool: `run_claude_code`**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `prompt` | string | Yes | The task or question for Claude Code |
| `continue_session` | bool | No (default: `true`) | Resume the user's previous Claude Code session |
| `working_directory` | string | No | Override the default working directory for this invocation |

**Tool: `get_claude_code_status`**

No parameters. Returns the current session state, last known cost, and whether a session is active for the calling user.

### Process Spawning

`ClaudeCodeToolProvider` builds and executes:

```
claude -p "{prompt}" \
  --output-format json \
  --allowedTools "Bash,Read,Glob,Grep,Write,Edit" \
  --max-budget-usd 5.00 \
  --max-turns 10 \
  --bare \
  [--resume {sessionId}] \
  [--append-system-prompt "..."] \
  [--dangerously-skip-permissions]
```

The process output is JSON: `{ result, session_id, duration_ms, total_cost_usd, is_error }`.

### Session Tracking

Session IDs are stored in a `ConcurrentDictionary<ulong, ClaudeCodeSession>` (userId to session). Sessions are **in-memory only** — they do not survive a container restart. This is intentional; no database entities or migrations are required.

A second `ConcurrentDictionary<ulong, bool>` prevents concurrent invocations per user. If a user sends a second message while Claude Code is still running, the provider returns an error response immediately.

### Response Chunking

`DmAssistantMessageHandler` applies the following chunking strategy before sending:

| Response length | Behavior |
|-----------------|----------|
| ≤ 2000 characters | Single Discord message (existing behavior) |
| 2001–8000 characters | Split on newline boundaries into ≤ 2000-character chunks, sent sequentially |
| > 8000 characters | Uploaded as a `.md` file attachment via `SendFileAsync` |

---

## Docker Setup

Mogwai uses a **separate Dockerfile and compose file** so that the main bot images remain lean (~500 MB vs ~2 GB).

### Building and Running

```bash
# Build the Mogwai image
docker compose -f docker-compose.mogwai.yml build

# Start Mogwai
docker compose -f docker-compose.mogwai.yml up -d

# View logs
docker compose -f docker-compose.mogwai.yml logs -f mogwai
```

### `.env.mogwai`

Create a `.env.mogwai` file (never commit this) with the Mogwai-specific bot token and API keys:

```env
Discord__Token=your-mogwai-bot-token-here
OpenRouter__ApiKey=your-openrouter-api-key-here
ANTHROPIC_API_KEY=your-anthropic-api-key-here
```

> Both are required, and they are **two different keys for two different services**. The .NET DM
> assistant makes its LLM calls through OpenRouter and reads `OpenRouter__ApiKey`; the `claude` CLI
> process inside the container talks to Anthropic directly and reads `ANTHROPIC_API_KEY`.

### `docker-compose.mogwai.yml` Overview

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

### Volume Mounts

| Volume | Container Path | Purpose |
|--------|---------------|---------|
| `mogwai-data` | `/app/data` | SQLite database and DM conversation history |
| `./` (host repo root) | `/workspace` | Project directory exposed to Claude Code |

The `/workspace` mount is **read-write** by default so Claude Code can edit files. Restrict to `:ro` if you only want read access.

---

## Security

### Owner-Only Access

Mogwai inherits the DM assistant's owner check (`GetApplicationInfoAsync()`). The bot owner is identified via the Discord API — no configuration-based user ID is needed. Non-owner DMs receive a placeholder response.

### Container Sandbox

`--dangerously-skip-permissions` is enabled inside the container via the `Mogwai__SkipPermissions=true` environment variable. This is safe because:

- The container has no access to the host filesystem beyond the explicit `/workspace` mount.
- The mounted directory is the project repository that you are explicitly granting access to.
- Container network is isolated from host services unless ports are explicitly mapped.

### API Key Handling

- `OpenRouter__ApiKey` is injected via `.env.mogwai` and is never committed to version control.
- The `claude` CLI reads `ANTHROPIC_API_KEY` from the process environment — no browser-based authentication is needed inside the container.

---

## Troubleshooting

### `claude: command not found` inside container

The `claude` CLI failed to install during the image build. Rebuild the image and check for npm errors:

```bash
docker compose -f docker-compose.mogwai.yml build --no-cache
docker compose -f docker-compose.mogwai.yml logs mogwai | grep -i npm
```

Verify the binary exists after a successful build:

```bash
docker compose -f docker-compose.mogwai.yml exec mogwai claude --version
```

### Claude Code invocation times out

The default timeout is 300 seconds. For tasks that need longer, increase `Mogwai__TimeoutSeconds` in `docker-compose.mogwai.yml`. Check `MaxTurns` as well — a high turn count with complex tasks can exceed the timeout.

### `ANTHROPIC_API_KEY` not found

The .NET process and the `claude` CLI each need their own key. Confirm your `.env.mogwai` contains both variables:

```env
OpenRouter__ApiKey=sk-or-v1-...
ANTHROPIC_API_KEY=sk-ant-...
```

If they differ, the CLI will fail even if the .NET DM assistant is working.

### Container starts but DM assistant is not responding

1. Verify `DmAssistant__Enabled=true` is set in the compose file environment.
2. Confirm `Discord__Token` in `.env.mogwai` is a **different token** from the main bot. Sharing a token between two bot processes will cause one to be kicked offline.
3. Check logs: `docker compose -f docker-compose.mogwai.yml logs mogwai`

### Container won't start (image build fails)

Playwright/Chromium installation can fail if the base image is missing system packages. The `Dockerfile.mogwai` runs `npx playwright install --with-deps chromium` which handles dependencies, but this requires internet access at build time. Ensure the build machine can reach `registry.npmjs.org` and Playwright's CDN.

---

## Verification Checklist

1. `docker compose -f docker-compose.mogwai.yml build` completes without errors
2. `docker compose -f docker-compose.mogwai.yml up -d` starts the container
3. `docker exec mogwai claude --version` returns a version string
4. `docker exec mogwai npx playwright --version` returns a version string
5. DM the Mogwai bot with a simple question — Haiku answers directly (no Claude Code invocation)
6. DM the Mogwai bot with a coding task — Haiku delegates to `run_claude_code`
7. JSON output from the CLI is parsed and the response is chunked/sent correctly
8. Send a follow-up coding message — `--resume` uses the stored session ID
9. Confirm timeout kills the process after the configured seconds
10. Send two rapid messages — confirm the second is rejected with a concurrency error

---

## Related Documentation

- [AI Assistant](ai-assistant.md) — Guild-based AI assistant (different feature)
- [DM Assistant Requirements](../requirements/dm-assistant-requirements.md) — DM assistant foundation
- [Docker Deployment Guide](docker-deployment.md) — Main bot Docker setup
- [Agent Prompt](../../docs/agents/dm-owner-agent.md) — Owner DM assistant system prompt
- [Service Catalog](../architecture/service-catalog.md) — `ClaudeCodeToolProvider` entry
