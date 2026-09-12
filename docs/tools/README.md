# Tool specifications

One page per agent tool: what it is for, what it takes, and **every** shape it can return. The
audience is whoever is about to change a tool, and the model's behaviour is the thing being
specified — a tool's contract is its description, its schema and its result shapes, and all three are
read by something that cannot ask a follow-up question.

This directory replaces [`specs/archive/assistant-tool-catalog.md`](../specs/archive/assistant-tool-catalog.md),
which described a design rather than what shipped.

## What is here

| Tool | Surfaces | Writes? |
| --- | --- | --- |
| [`save_note`](./save_note.md) | DM | yes |
| [`search_notes`](./search_notes.md) | DM | no |
| [`get_note`](./get_note.md) | DM | no |
| [`list_notes`](./list_notes.md) | DM | no |
| [`delete_note`](./delete_note.md) | DM | yes |
| [`load_skill`](./load_skill.md) | Guild, DM | no |
| [`get_feature_documentation`](./get_feature_documentation.md) | Guild, DM | no |

The other 22 tools are still the hand-written `IToolProvider` kind and have no page yet.

## When to write one

**When you touch the tool, not before.** A page per tool written in one sitting is 29 pages nobody
reads and nobody keeps true; a page written while changing the thing it describes is accurate for
the same reason the change is. Backfill the ones being changed, and write one for every new tool.

The set above is the tools converted to `IAgentTool` in Phases 4 and 5, plus
`get_feature_documentation`, which is still a hand-written provider tool and got its page when the
path-containment fix (F13) touched it. Being converted is not what earns a page; being changed is.

## The template

Copy this. Every heading earns its place; the result shapes earn it twice, because they are the part
that is impossible to reconstruct from the code without reading every branch.

````markdown
# `tool_name`

**Status**: shipped · **Surfaces**: DM · **Category**: Memory · **Default**: on
**Implementation**: `src/DiscordBot.Infrastructure/Services/LLM/Tools/ToolNameTool.cs`
**Mutation**: `"phrase that follows: isn't allowed to"` — or *read-only*

## Purpose

One paragraph: what question this answers for the model, and when it should reach for it rather than
for a neighbouring tool.

## Dependencies

What it needs to work — services, repositories, a Discord client, configuration.

## Model-facing description

> The exact string from `Definition.Description`, verbatim.

Changing this costs a prompt-cache prefix, so it is quoted here to make a change visible in review.

## Input

| Property | Type | Required | Notes |
| --- | --- | --- | --- |
| `example` | string | yes | What it means, and what happens when it is odd. |

## Results

### Success

```json
{ "example": "value" }
```

### `failed_result` — what went wrong

```json
{ "error": "Missing required parameter: example" }
```

One subsection per expected failure, each marked `failed_result` when
[`ToolOutcomes.Classify`](../architecture/patterns.md#agent-tool-authoring) counts it as one — a
top-level `error` string, or a top-level `success` / `available` / `found` flag that is false. A
failure reported any other way is invisible in the traces and on the metrics page, and expected
failures are the majority of what is worth seeing there.

## Notes

Anything a future reader would otherwise have to discover: limits, ordering, how it behaves on an
empty store.
````

## The `failed_result` convention, once

`ToolResults.Error` and `ToolResults.NotFound` return a **successful** `ToolExecutionResult` carrying
an explanation, because that is what the model needs to read — `CreateError` prepends `Error: ` on
the wire, which reads as a malfunction to retry around rather than an answer to relay. The cost is
that such failures are invisible to anything checking `ToolExecutionResult.Success`, which is why
`ToolOutcomes.Classify` reads the payload instead. Writing results through `ToolResults` satisfies
the convention by construction; hand-rolling one means following it deliberately.

Reserve `ToolResults.Failed` for a genuine malfunction — something the model can do nothing useful
with.
