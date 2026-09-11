#!/bin/bash
# PostToolUse hook: after `gh pr create`/`gh pr edit`, strip any Claude Code
# signature (Generated with/by line, Co-Authored-By: Claude, Claude-Session
# link, or bare claude.ai/code/session_ link) from the resulting PR body.
set -uo pipefail

input=$(cat)

command=$(echo "$input" | jq -r '.tool_input.command // empty' 2>/dev/null)
if ! echo "$command" | grep -qE 'gh pr (create|edit)'; then
  exit 0
fi

pr_number=$(echo "$input" | grep -oE '/pull/[0-9]+' | head -1 | grep -oE '[0-9]+')
if [ -z "$pr_number" ]; then
  exit 0
fi

repo=$(gh repo view --json nameWithOwner -q .nameWithOwner 2>/dev/null)
if [ -z "$repo" ]; then
  exit 0
fi

body=$(gh pr view "$pr_number" --json body -q .body 2>/dev/null)
if [ -z "$body" ]; then
  exit 0
fi

new_body=$(printf '%s' "$body" | python3 "$CLAUDE_PROJECT_DIR/.claude/hooks/strip_pr_signature.py")

if [ "$body" != "$new_body" ]; then
  gh api "repos/$repo/pulls/$pr_number" -X PATCH -f body="$new_body" >/dev/null 2>&1
fi

exit 0
