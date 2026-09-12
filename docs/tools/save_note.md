# `save_note`

**Status**: shipped · **Surfaces**: DM · **Category**: Memory · **Default**: on
**Implementation**: `src/DiscordBot.Infrastructure/Services/LLM/Tools/SaveNoteTool.cs`
**Mutation**: `"save notes"`

## Purpose

Writes one durable note for the caller, so something said in one DM conversation is still known in
the next. The DM assistant's history is a sliding window; this is the only thing that survives it.

Reach for it when the user asks to be remembered, states a preference, or gives context they clearly
expect to be retained — not to summarise a conversation for its own sake. Notes are private per user
and are never read by another.

## Dependencies

`IDmAssistantNoteRepository` (`DmAssistantNotes` table). Nothing else — no Discord client, no guild.

## Model-facing description

> Saves a personal note for the user that persists across conversations. Use this when the user asks
> you to remember something, states a preference, or shares important context they want retained.
> Notes are private to each user.

## Input

| Property | Type | Required | Notes |
| --- | --- | --- | --- |
| `content` | string | yes | What to remember. 4,096 characters, matching the column. |
| `tag` | string | no | A category — `preference`, `fact`, `context`, `todo`. Free text; nothing validates it, and `list_notes` filters on it. |

## Results

### Success

```json
{ "success": true, "note_id": 41, "message": "Note saved successfully." }
```

`note_id` is what `get_note` and `delete_note` take, so a model that saves and then wants to undo has
the id without another lookup.

### `failed_result` — no content

```json
{ "error": "Missing required parameter: content" }
```

### `failed_result` — content too long

```json
{ "error": "Note content exceeds maximum length of 4096 characters." }
```

The model can act on this one: shorten it and try again.

### `forbidden` — the caller may not write

```json
{
  "forbidden": true,
  "message": "The user isn't allowed to save notes. Don't retry — tell them plainly, and mention that a server administrator can do it for them."
}
```

Returned by `AgentToolProvider` **before the tool is entered**, when `ToolContext.CanMutate` is
false. The tool itself has no check and needs none: declaring `Mutation` is the guard.

## Notes

- `UpdatedAt` is set to `CreatedAt` on save. Nothing updates a note in place today — a correction is
  a new note, and a stale one is deleted.
- There is no de-duplication. Two saves of the same sentence are two rows; the loop's duplicate-call
  guard is what stops the model doing that within one run.
