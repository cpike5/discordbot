# Agent prompts and skills

This directory holds the assistant's prompts, read by the cpike.Agent framework's markdown prompt
store. The Bot csproj copies `prompts/` to `Prompts/` in the build output (`Prompts:Directory=Prompts`,
`Prompts:SkillsDirectory=Prompts/skills`, `Prompts:DefaultAgent=bot`), and edits are hot-reloaded.

| File | Mode | What it is |
| --- | --- | --- |
| `prompts/bot.md` | `bot` | The base persona: one neutral line, since every mode carries its own identity and layers on it. The framework rejects a prompt file with no body, so it cannot be empty. |
| `prompts/guild.md` | `guild` | The guild assistant (mentions in a server). |
| `prompts/dm.md` | `dm` | The owner's DM assistant. Front matter `skills: analytics, moderation`; uses `{{skills}}` for the roster. |
| `prompts/feature-requests.md` | `feature-requests` | The feature-request gathering agent (was `Templates/feature-request-gathering-prompt.md`). |
| `prompts/skills/dm/` | | The DM assistant's skills. |

A mode's tool list is not written in its file: `CatalogPromptStore` fills it from `ToolCatalog`
(`Core/Models/Llm/`), withholds tools only a skill brings, adds `load_skill` only when the mode has a
`skills:` line, and renders `{{BASE_URL}}`. Per-turn text (such as the guild portal links) is not in
these files either: it arrives as a turn-context prompt part, so the system prompt stays cacheable.

# Skills

A **skill** is a bundle of instructions — and, optionally, the tools those instructions are about —
that an assistant loads on demand instead of carrying in every request. The model sees a one-line
summary of each skill; it pays for the rest only when it decides a request needs one.

A mode opts in with a `skills:` line in its front matter and a `{{skills}}` token where the roster
should appear. The guild mode has none: it is single-turn, so a skill there would cost a round on
every use. A mode with no `skills:` line costs nothing and does not advertise `load_skill`.

## The file format

One `.md` file per skill in `prompts/skills/<surface>/`, with a small front-matter block:

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
  trigger the skill, not as a description of the file. A skill nothing can decide to load is worse
  than no skill.
- **`key`** is what the model passes to `load_skill` and what the mode's `skills:` line lists; it
  defaults to the file name. It appears in the roster, so changing one costs a prompt-cache prefix.
- **`tools`** is optional, comma-separated. A skill with no tools is legitimate: a body of standing
  orders for a kind of request is something a tools-only mechanism cannot express.
- Everything under the closing `---` is the instructions, returned as `load_skill`'s result.

## What loading actually does

1. The tools any skill names are **held back** from the advertised tool array. They cost nothing
   until a skill that names them is loaded.
2. `load_skill` (a framework built-in) returns the instructions and records the activation.
3. After that round, the skill's tools are advertised.

Three things follow from that:

- **A tool named by *any* skill is hidden until one of the skills naming it is loaded.** Do not put
  a tool the assistant needs on most requests behind a skill.
- **A skill can never widen reach.** On the guild surface a guild's allow-list becomes the run's
  `DisabledTools`, which a skill cannot override. A name the surface does not have is dropped, and the
  model is never told about it.
- **Loading costs a round trip and a prompt-cache write.** On the DM assistant that is paid once: the
  loaded keys are stored (`IDmSkillActivationStore`) and preloaded on the next turn
  (`AgentRunRequest.PreloadedSkills`), so the tools are advertised from the first call and the
  instructions are already in the prompt. A single-turn surface pays *every time*, so it should only
  ever hold rare, heavy capabilities.

## Writing one

- Keep the body under a few thousand characters. It arrives as a tool result, and a tool result is
  capped (`Assistant:Tools:MaxToolResultChars`, default 8000) — a longer body is truncated and the
  model reads a fragment.
- Say which tool answers which kind of question, and what *not* to use each for. That is the part a
  tool description cannot carry, and the reason a skill beats a longer description.
- Say how to read the results: what to lead with, what to summarise, what not to paste.
- Every tool it names must exist and be advertised on that surface; `SkillContractTests` checks this.
- Edits are picked up without a restart (hot reload). To add a skill to a mode that has none, also
  add the `skills:` line and `{{skills}}` token to the mode file.
