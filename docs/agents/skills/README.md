# Agent skills

A **skill** is a bundle of instructions — and, optionally, the tools those instructions are about —
that an assistant loads on demand instead of carrying in every request. The model sees a one-line
summary of each skill; it pays for the rest only when it decides a request needs one.

This directory is not itself scanned. Each assistant surface reads one subdirectory:

| Surface | Directory | Configuration key |
| --- | --- | --- |
| DM assistant | `dm/` | `DmAssistant:SkillsPath` |
| Guild assistant | `guild/` | `Assistant:Tools:SkillsPath` |

`guild/` ships empty on purpose — see the cost note below. A missing or empty directory is not an
error; it is a surface with no skills, and it costs nothing (the `load_skill` tool is not advertised
there either).

Two directories rather than a `surfaces:` field in each file, because the two surfaces want
different wording anyway: they advertise different tools, and one of them is multi-turn.

## The file format

One `.md` file per skill, with a small front-matter block:

```markdown
---
key: moderation
summary: Look up moderation cases, a member's moderation history, or the portal's audit log for a server.
tools: get_moderation_cases, get_user_mod_history, search_audit_logs
---
**Guild context first.** All three tools need a guild …
```

- **`summary`** is required. It is the only part of the skill that is paid for on every request, and
  it is the entire basis for the model's decision to load. Write it as the condition that should
  trigger the skill, not as a description of the file. A file without one is logged and ignored —
  a skill nothing can decide to load is worse than no skill.
- **`key`** is optional; it defaults to the file name. It is what the model passes to `load_skill`,
  and it appears in the roster, so changing one costs a prompt-cache prefix.
- **`tools`** is optional, comma-separated. A skill with no tools is legitimate: a body of standing
  orders for a kind of request is something a tools-only mechanism cannot express.
- Everything under the closing `---` is the instructions, returned verbatim as the loader's result.

## What loading actually does

1. The tools any skill names are **held back** from the advertised tool array. They cost nothing
   until a skill that names them is loaded.
2. `load_skill` returns the instructions and records the activation.
3. After that round, the loop re-composes the tool array: the skill's tools are now advertised.

Three things follow from that, and they are the whole of what you need to know to write one well:

- **A tool named by *any* skill is hidden until one of the skills naming it is loaded.** Do not put
  a tool the assistant needs on most requests behind a skill.
- **A skill can never widen reach.** Its tool list is intersected with what the surface advertises
  and, on the guild surface, with that guild's allow-list. A name the surface does not have is
  dropped silently, and the model is never told about it.
- **Loading costs a round trip and a prompt-cache write.** On the DM assistant that is paid once:
  the activation is replayed on the next turn, so the tools are advertised from the first call and
  the instructions are already in the prompt. The guild assistant is single-turn, so there it is
  paid *every time* — which is why `guild/` ships empty and should only ever hold rare, heavy
  capabilities.

## Writing one

- Keep the body under a few thousand characters. It arrives as a tool result, and a tool result is
  capped (`Assistant:Tools:MaxToolResultChars`, default 8000) — a longer body is truncated and the
  model reads a fragment.
- Say which tool answers which kind of question, and what *not* to use each for. That is the part a
  tool description cannot carry, and the reason a skill beats a longer description.
- Say how to read the results: what to lead with, what to summarise, what not to paste.
- Edits are picked up without a restart (within the prompt cache's five minutes). Adding or removing
  a file is picked up on the same terms.
