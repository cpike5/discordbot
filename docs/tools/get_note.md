# `get_note`

**Status**: shipped · **Surfaces**: DM · **Category**: Memory · **Default**: on
**Implementation**: `src/DiscordBot.Infrastructure/Services/LLM/Tools/GetNoteTool.cs`
**Mutation**: read-only

## Purpose

Reads one note in full, by id. The other two read tools already return whole notes, so this is
narrower than it looks: it is for an id the model has from an earlier `save_note` or from the user,
not a step on the way to a search.

## Dependencies

`IDmAssistantNoteRepository`.

## Model-facing description

> Retrieves a specific note by its ID. Use this when you need the full content of a particular note.

## Input

| Property | Type | Required | Notes |
| --- | --- | --- | --- |
| `note_id` | integer | yes | Read with `ToolInput.GetLong`, so a quoted number works — models send those routinely. |

## Results

### Success

```json
{
  "id": 41,
  "content": "Deploys go out on Thursdays.",
  "tag": "process",
  "created_at": "2026-02-01T18:04:11.0000000Z",
  "updated_at": "2026-02-01T18:04:11.0000000Z"
}
```

### `failed_result` — no such note

```json
{ "found": false, "error": "Note with ID 41 not found." }
```

Also what a note belonging to **another user** returns: the lookup is scoped to the caller, so
another user's note is indistinguishable from one that does not exist. That is the intended answer,
not a leak to close.

### `failed_result` — no id

```json
{ "error": "Missing required parameter: note_id" }
```

## Notes

The not-found answer is final. A model that retries it is burning rounds, which is what the loop's
duplicate-call guard is there to absorb.
