# `delete_note`

**Status**: shipped · **Surfaces**: DM · **Category**: Memory · **Default**: on
**Implementation**: `src/DiscordBot.Infrastructure/Services/LLM/Tools/DeleteNoteTool.cs`
**Mutation**: `"delete notes"`

## Purpose

Removes one note permanently. There is no update path for a note, so a correction is a `save_note`
followed by this — which is worth knowing when reading a conversation that does both.

## Dependencies

`IDmAssistantNoteRepository`.

## Model-facing description

> Deletes a specific note by its ID. Use this when the user wants to remove previously saved
> information. This action cannot be undone.

## Input

| Property | Type | Required | Notes |
| --- | --- | --- | --- |
| `note_id` | integer | yes | Read with `ToolInput.GetLong`, so a quoted number works. |

## Results

### Success

```json
{ "success": true, "message": "Note 41 deleted successfully." }
```

### `failed_result` — no such note

```json
{ "found": false, "error": "Note with ID 41 not found or already deleted." }
```

The wording covers both, on purpose: the delete is scoped to the caller, so a note that never
existed, one already gone, and one belonging to someone else are the same answer.

### `failed_result` — no id

```json
{ "error": "Missing required parameter: note_id" }
```

### `forbidden` — the caller may not write

```json
{
  "forbidden": true,
  "message": "The user isn't allowed to delete notes. Don't retry — tell them plainly, and mention that a server administrator can do it for them."
}
```

Applied by `AgentToolProvider` before the tool runs, from the `Mutation` declaration above. The
refusal comes first, so a forbidden delete never half-runs.

## Notes

Deletion is a hard delete. There is no soft-delete column and no audit row for a note, which is the
right trade for a private scratchpad and the wrong one for anything with a retention obligation —
worth remembering before this pattern is copied.
