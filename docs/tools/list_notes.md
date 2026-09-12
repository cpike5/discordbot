# `list_notes`

**Status**: shipped · **Surfaces**: DM · **Category**: Memory · **Default**: on
**Implementation**: `src/DiscordBot.Infrastructure/Services/LLM/Tools/ListNotesTool.cs`
**Mutation**: read-only

## Purpose

Browses the caller's notes, most recently updated first, optionally within one tag. This is the tool
for "what do you remember about me" — `search_notes` is for a specific subject.

It is the only memory tool with no required argument, which makes it the cheapest thing for the model
to reach for. That is deliberate: the alternative to a cheap browse is a model inventing what it
thinks it remembers.

## Dependencies

`IDmAssistantNoteRepository`.

## Model-facing description

> Lists the user's saved notes, optionally filtered by tag. Returns notes sorted by most recently
> updated. Use this to browse all saved notes or notes in a specific category.

## Input

| Property | Type | Required | Notes |
| --- | --- | --- | --- |
| `tag` | string | no | Exact tag filter. Omit for everything. |
| `limit` | integer | no | 1–50, default 20. Clamped, not refused. |

## Results

### Success

```json
{
  "notes": [
    {
      "id": 41,
      "content": "Deploys go out on Thursdays.",
      "tag": "process",
      "created_at": "2026-02-01T18:04:11.0000000Z",
      "updated_at": "2026-02-01T18:04:11.0000000Z"
    }
  ],
  "total_count": 1,
  "filter_tag": "process"
}
```

`filter_tag` is echoed so the model can tell "no notes" from "no notes *with that tag*" without
having to remember what it asked. It is omitted when no tag was passed.

An empty store is a success with an empty `notes` array. There is no failure shape for "nothing
saved yet" — that is an answer, and the model should say it plainly.

## Notes

This tool has no required arguments, so it is the one memory tool that cannot fail on input. Every
other failure is a malfunction and reaches the model as one.
