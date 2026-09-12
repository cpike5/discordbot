# `load_skill`

**Status**: shipped · **Surfaces**: Guild, DM · **Category**: Skills · **Default**: on
**Implementation**: `src/DiscordBot.Infrastructure/Services/LLM/Tools/LoadSkillTool.cs`
**Mutation**: read-only

## Purpose

Loads one skill: a bundle of instructions, and with them the tools those instructions are about. It
is how a rare, heavy tool group stops costing its schemas on every request — the model reads a
one-line summary of each skill in its prompt and pays for the rest only when a request needs it.

The tool itself does very little. It records the activation on the run's session and returns the
instructions; the loop reads that session after the round and re-composes the advertised tool array,
so the skill's tools arrive on the **next** round rather than this one.

See [`docs/agents/skills/README.md`](../agents/skills/README.md) for writing a skill, and
[`patterns.md` § Agent Skills](../architecture/patterns.md#agent-skills) for the mechanism.

## Dependencies

None. It reads the `SkillSession` out of `ToolContext.Items`, which the context factory put there;
it touches no service and no database.

## Model-facing description

> Loads a skill: detailed instructions for one kind of request, plus the tools those instructions
> use. Use it when the skills list in your instructions names one that fits what you were asked; the
> result is the instructions themselves. Load only what the request needs.

## Input

| Property | Type | Required | Notes |
| --- | --- | --- | --- |
| `key` | string | yes | A key from the roster in the system prompt. Matched case-insensitively. |

## Results

### Success

```json
{
  "skill": "moderation",
  "instructions": "**Guild context first.** All three tools need a guild…",
  "tools_now_available": ["get_moderation_cases", "get_user_mod_history", "search_audit_logs"]
}
```

`tools_now_available` is omitted when the skill names no tools, or when this surface advertises none
of the ones it names — the list is narrowed to what the run's registry actually holds before the run
starts, so it never promises something the model cannot then call.

Re-loading an already active skill succeeds and adds `"already_loaded": true`. That is not an error:
a model loading it again wants to re-read the instructions.

### `failed_result` — no such skill

```json
{ "found": false, "error": "There is no skill called 'moderatoin'. Available skills: analytics, moderation." }
```

The available keys are named in the error so the model's next attempt is informed rather than another
guess.

### `failed_result` — no skills on this surface

```json
{ "found": false, "error": "No skills are available in this conversation. Answer with the tools you already have." }
```

Reachable only from a stale prompt-cache prefix: the loop stops advertising this tool entirely when a
surface has no skill files, so a surface with an empty directory pays nothing for it.

### `failed_result` — no key

```json
{ "error": "Missing required parameter: key" }
```

## Notes

- **It can never widen reach.** `SkillToolSet.Compose` builds the advertised set from the run's
  registry and only subtracts. On the guild surface that registry is a `FilteredToolRegistry` over
  the guild's allow-list, so a skill naming a tool a guild has turned off cannot turn it back on.
- **Loading costs a prompt-cache write.** The tool array serializes at position 0 of the request, so
  re-composing it invalidates the cached prefix for the rest of that run. Expect a cache-miss spike
  after a `load_skill` on the metrics page; it is the mechanism working.
- On the DM surface the activation is replayed into the next turn, so a skill is paid for once. The
  guild assistant is single-turn and pays every time — which is why `docs/agents/skills/guild/` ships
  empty.
