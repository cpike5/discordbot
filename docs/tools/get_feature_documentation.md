# `get_feature_documentation`

**Status**: shipped · **Surfaces**: Guild, DM · **Category**: Documentation · **Default**: on
**Implementation**: `src/DiscordBot.Infrastructure/Services/LLM/Providers/DocumentationToolProvider.cs`
**Mutation**: *read-only*

## Purpose

Answers "how do I use X" in one call by returning a feature's whole documentation page from
`docs/articles/`, so the assistant explains a feature from the shipped docs rather than from memory.
It is the first tool to reach for on a feature question: `search_commands` and `get_command_details`
describe one command each, and a feature is usually several commands plus the settings page that
configures them.

Still one of the hand-written `IToolProvider` tools rather than an `IAgentTool` — it shares the
provider with `search_commands`, `get_command_details` and `list_features`. This page exists because
the tool was last touched by the path-containment fix (F13), which is the rule in
[`README.md`](./README.md): a page is written when its tool changes.

## Dependencies

- `Assistant:Tools:DocumentationBasePath` — the directory it may read, default `docs/articles`, and
  the only directory it may read. A relative value resolves against the process working directory.
- `Assistant:BaseUrl` or `Application:BaseUrl` — substituted into `{BASE_URL}` in the page, with
  `{GUILD_ID}` from `ToolContext.GuildId`, so links in the answer point at the caller's own portal.
  On the DM surface `DmDocumentationToolProvider` copies the conversation's active guild into
  `GuildId` first; with no base URL or no guild the placeholders are left as they are.

## Model-facing description

> Retrieves comprehensive documentation for a bot feature including ALL related commands,
> configuration options, usage instructions, and examples. This is the BEST tool for 'how do I use X'
> questions - use it FIRST before search_commands. Feature names: soundboard, rat-watch, tts, vox,
> reminder, member-directory, moderation, welcome, scheduled-messages, consent, privacy, commands,
> settings, audio, performance, audit.

Changing this costs a prompt-cache prefix, so it is quoted here to make a change visible in review.

## Input

| Property | Type | Required | Notes |
| --- | --- | --- | --- |
| `feature_name` | string | yes | A feature name or alias from `FeatureDocumentationMap` (case-insensitive, 28 aliases over 15 pages) — or, for a page with no alias, the file's own name without `.md`. An unmapped name must match `^[a-z0-9][a-z0-9-]*$`; anything else is refused, and refused indistinguishably from absent. See [Containment](#containment). |

## Results

### Success

```json
{
  "feature": "soundboard",
  "content": "# Soundboard\n...",
  "available": true,
  "last_updated": "2026-03-11T18:02:44.1234567Z"
}
```

`feature` echoes the name as the model asked for it, not the file that answered. `content` is the
whole page — the single largest result any tool here returns, and the reason §1.2 of the
implementation spec proposes a `section` argument.

### `failed_result` — the document will not be read

```json
{
  "available": false,
  "error": "Documentation for feature 'welcome-system' not found. Available features can be listed using list_features tool."
}
```

One shape, three causes: no such page, a name outside the allow-list, or a path that resolved outside
the base directory. It is the same payload by design — see [Containment](#containment) — and it
carries both a top-level `error` string and `available: false`, either of which is enough for
[`ToolOutcomes.Classify`](../architecture/patterns.md#agent-tool-authoring) to count the call as a
`failed_result`.

### `error` — the call itself was malformed

```
Error: Missing required parameter: feature_name
Error: Parameter feature_name cannot be empty
```

A failed `ToolExecutionResult`, not a payload, which is the older convention this provider still
follows for a missing argument. A page that exists but cannot be read — a permission error mid-read —
comes back the same way, as `Error: Error reading documentation: …`.

## Containment

`feature_name` is model input, and the model's input is a Discord message from any user in any guild
where the assistant is enabled. Two layers stand between that string and the filesystem:

1. **An allow-list on the unmapped fallback.** A name not in `FeatureDocumentationMap` is lower-cased,
   trimmed and matched against `^[a-z0-9][a-z0-9-]*$` before it becomes `<name>.md`. A separator, a
   dot, a leading `~`, a null byte — none of them are ever turned into a path. Mapped names skip this
   because they resolve to a constant in the table.
2. **Resolve-and-verify.** The base directory and the candidate are both resolved with
   `Path.GetFullPath`, and a result that is not under the base directory is refused. The allow-list
   already catches everything the model can send, so this is the layer that holds if a
   `FeatureDocumentationMap` entry is ever written wrongly.

It is ordinary containment, not a filesystem audit: a symlink *inside* `docs/articles` that points
out of it is still followed.

**The refusal is byte-identical to the not-found answer**, and deliberately so — a distinct
"rejected" message tells whoever wrote the message behind the model's call that their probe was
understood. What says so instead is a `Warning` log line naming the feature name, where an operator
reads it and a prober does not.

`DmDocumentationToolProvider` delegates to this provider, so the DM surface inherits all of it.

## Notes

- **The feature map is an alias table, not a catalogue.** Several names point at one page
  (`tts`, `tts-support`, `text-to-speech`), `moderation` points at `authorization-policies.md`, and a
  page with no alias is still reachable by its own file name through the allow-listed fallback. A new
  `docs/articles/` page needs a map entry only if it wants an alias.
- **`list_features` is the companion, and the two lists are maintained separately**: a hand-written
  13 features with categories and portal URLs, against 15 mapped pages here plus every other page in
  `docs/articles/` reachable by its own file name. A feature in one and not the other is a
  maintenance slip rather than a rule.
- The error messages name `list_features` as the way out of a miss, which is the only reason the
  model reliably recovers from a wrong guess at a feature name.
