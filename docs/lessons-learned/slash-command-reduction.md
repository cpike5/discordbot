# Lessons Learned: Slash command reduction

**Date:** 2026-11-19
**Related:** PR #2000, `docs/specs/slash-command-reduction-followups.md`

---

## Summary

The series cut top-level slash commands from 51 to about 18: PR #2000 removed portal-covered and
low-value commands, then follow-up workstreams removed the feature-request DM flow, added portal
actions for the gaps, and merged near-duplicate commands. Most of it was deletion. These are the
parts that fought back.

## A modal handler inside a `[Group]` module gets the group prefix

Moving `/rat-clear`, `/rat-stats` and `/rat-leaderboard` into a `/rat` group changed more than
the command names. Discord.NET prefixes the custom ID of component and modal handlers declared in
a `[Group]` module with the group name, so the Rat Watch modal's `ratwatch:*` ID no longer routed.
The modal handler had to leave the grouped module and go where the IDs are not rewritten.

## Context menus cannot live in grouped modules

Context menu commands are not subcommands, so a module with `[Group]` cannot host one. The Rat
Watch context menu moved to `RatWatchContextMenuModule`. Splitting it out created a second module
that the Commands tab would list, and `DefaultModules` would need a row for it.

## Companion module mapping

`SlashCommandRegistrationService.ResolveCompanionParentModule` maps a companion module
(`*ComponentModule`, or `RatWatchContextMenuModule`) to the module whose toggle governs it.
PR #2000 deleted the explicit map because nothing needed it; the Rat Watch split needed one again.
Keep a small explicit entry per companion that does not follow the naming convention, and cover
it with a registration test so one toggle keeps controlling the whole feature.

## Snowflake IDs in DTOs for JavaScript

The watchlist UI posts and reads user IDs from page JavaScript. A `ulong` serialized as a JSON
number rounds its last digits in the browser and every lookup fails. DTOs that cross into
JavaScript carry `[JsonNumberHandling(WriteAsString | AllowReadingFromString)]` on the ID
properties (see `WatchlistDtos.cs`, `GuildMemberDto.cs`). Reading from a string alone is not
enough for output the page renders.

## Orphans are found by caller, not by name

After a deletion this large, the services the removed modules injected were mostly still alive:
the portal, controllers, the assistant's moderation tool provider and background handlers
used them. Only `IInvestigationService` (its single caller was `/investigate`), its profile DTO and
two mod-tag autocomplete handlers had no remaining caller. Grep each candidate for callers before
deleting; the interface list a removed module injected is a candidate list, not a delete list.

## Parallel workstreams touch the same docs

Workstreams B, C and D ran in parallel and each edited `feature-map.md`, `service-catalog.md`,
`README.md` and the per-feature articles. Merges conflicted in the same tables, and a doc fixed
in one branch was reintroduced stale by another. The audit that closes the series (workstream E)
is cheap because it runs last; keep it last, and keep "formerly `/x`" notes where a command was
renamed so readers who search for the old name land on the new one.
