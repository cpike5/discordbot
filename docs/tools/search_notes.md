# `search_notes`

**Status**: shipped · **Surfaces**: DM · **Category**: Memory · **Default**: on
**Implementation**: `src/DiscordBot.Infrastructure/Services/LLM/Tools/SearchNotesTool.cs`
**Mutation**: read-only

## Purpose

Keyword recall over the caller's saved notes. This is the tool for "do I have anything about X" —
`list_notes` is for browsing, `get_note` is for reading one already known by id.

The match is a substring over both content and tag, so a tag name works as a query without the model
having to know that tags exist.

## Dependencies

`IDmAssistantNoteRepository`.

## Model-facing description

> Searches the user's saved notes by keyword. Matches against both note content and tags. Use this to
> recall previously saved information about the user.

## Input

| Property | Type | Required | Notes |
| --- | --- | --- | --- |
| `query` | string | yes | Substring matched against content and tag. |
| `limit` | integer | no | 1–50, default 10. Out-of-range values are clamped rather than refused. |

## Results

### Success

```json
{
  "results": [
    {
      "id": 41,
      "content": "Deploys go out on Thursdays.",
      "tag": "process",
      "created_at": "2026-02-01T18:04:11.0000000Z",
      "updated_at": "2026-02-01T18:04:11.0000000Z"
    }
  ],
  "total_results": 1
}
```

`tag` is omitted rather than null when a note has none — `ToolJson.Compact` drops nulls, so an
untagged note costs nothing to report.

A search that matches nothing is a **success with an empty list**, not a failure: an empty
`results` array with `total_results: 0` is the true answer, and `ToolOutcomes.Classify` counts it as
`ok`. Only a malformed call is a failure here.

### `failed_result` — no query

```json
{ "error": "Missing required parameter: query" }
```

## Notes

- `total_results` is the length of what was returned, not the number of matches in the store. At
  `limit` it means "at least this many"; the model has no way to tell, which is a known rough edge
  rather than a designed one.
- There is no ranking. Results come back in the repository's order.
