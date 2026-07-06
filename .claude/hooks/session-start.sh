#!/bin/bash
# SessionStart hook — provision the .NET 8 toolchain for Claude Code on the web.
#
# Why this is needed:
#   The remote web environment ships without the .NET SDK, and the official
#   dotnet-install CDN (builds.dotnet.microsoft.com) is blocked by the egress
#   policy (HTTP 403). Ubuntu 24.04's own archive, however, ships
#   `dotnet-sdk-10.0` and is reachable — so we install from there.
#
# Behaviour:
#   - Runs only in the remote (web) environment.
#   - Idempotent: skips installs that are already present.
#   - Synchronous: the session waits until the toolchain is ready, so the agent
#     never races ahead of `dotnet build` / `dotnet test`.
#   - Verbose tool output goes to a log; only concise status reaches the session.
set -euo pipefail

# Only provision in the remote web environment (no-op locally).
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

LOG="/tmp/session-start-hook.log"
: > "$LOG"
log() { echo "[session-start] $*"; }

export DEBIAN_FRONTEND=noninteractive

# --- .NET 8 SDK (required) -------------------------------------------------
if command -v dotnet >/dev/null 2>&1; then
  log "dotnet already present ($(dotnet --version))"
else
  log "Installing dotnet-sdk-10.0 from the Ubuntu archive (see $LOG)..."
  if ! sudo apt-get install -y --no-install-recommends dotnet-sdk-10.0 >>"$LOG" 2>&1; then
    log "First attempt failed; refreshing package lists and retrying..."
    sudo apt-get update >>"$LOG" 2>&1
    sudo apt-get install -y --no-install-recommends dotnet-sdk-10.0 >>"$LOG" 2>&1
  fi
  log "Installed dotnet $(dotnet --version)"
fi

# --- Node.js + Tailwind (best-effort) --------------------------------------
# Node is only needed to recompile Tailwind CSS. The .csproj skips the Tailwind
# build when the `SkipTailwind` MSBuild property is set, so if Node is
# unavailable we persist SkipTailwind=true and C#/Razor builds still work.
NPM_OK=true
if ! command -v npm >/dev/null 2>&1; then
  log "Installing Node.js/npm for Tailwind (best-effort, see $LOG)..."
  if ! sudo apt-get install -y --no-install-recommends nodejs npm >>"$LOG" 2>&1; then
    NPM_OK=false
    log "WARN: Node.js install failed — CSS rebuilds unavailable this session."
  fi
fi
if [ "$NPM_OK" = "true" ] && command -v npm >/dev/null 2>&1; then
  log "Installing npm packages for Tailwind..."
  if ! ( cd "$CLAUDE_PROJECT_DIR/src/DiscordBot.Bot" && npm install --no-audit --no-fund >>"$LOG" 2>&1 ); then
    NPM_OK=false
    log "WARN: npm install failed — CSS rebuilds unavailable this session."
  fi
else
  NPM_OK=false
fi

# --- Warm the NuGet cache (snapshotted into the cached container) ----------
log "Restoring NuGet packages (see $LOG)..."
dotnet restore "$CLAUDE_PROJECT_DIR/DiscordBot.sln" >>"$LOG" 2>&1
log "NuGet restore complete."

# --- Persist environment for the session -----------------------------------
{
  echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
  echo 'export DOTNET_NOLOGO=1'
  # Without Node, tell MSBuild to skip the Tailwind/npm build targets so that
  # `dotnet build` / `dotnet test` succeed (CSS just isn't regenerated).
  if [ "$NPM_OK" != "true" ]; then
    echo 'export SkipTailwind=true'
  fi
} >> "$CLAUDE_ENV_FILE"

if [ "$NPM_OK" = "true" ]; then
  log "Toolchain ready: .NET + Node. Build with: dotnet build"
else
  log "Toolchain ready: .NET only. Build with: dotnet build (Tailwind auto-skipped)."
fi
