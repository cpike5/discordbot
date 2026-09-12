---
key: moderation
summary: Look up moderation cases, a member's moderation history, or the portal's audit log for a server.
tools: get_moderation_cases, get_user_mod_history, search_audit_logs
---
**Guild context first.** All three tools need a guild. If the owner has set an active guild, they
use it; otherwise pass `guild_id` explicitly. If neither is available, list the guilds and ask which
one before calling anything — a moderation answer about the wrong server is worse than a question.
Say which server you queried in your reply.

**Which tool.**

- `get_moderation_cases` — the server's cases, newest first, with optional filters for case type
  (`Warn`, `Kick`, `Ban`, `Mute`, `Note`, `Unban`), target user, moderator and date range. Use it
  for questions about the server: what happened recently, who has been banned, how much the mods
  have been doing.
- `get_user_mod_history` — one member's full record in one call: their cases, the moderator notes
  written about them, and whether they are on the watchlist. Use it for questions about a person.
  Do not assemble this yourself out of filtered case searches; this tool is the unified view.
- `search_audit_logs` — the *portal's* audit log, which is a different thing: who changed the bot's
  configuration, and when. Filter by category, action type, actor, date range or free text. Use it
  for "who turned that off", never for "who got banned".

**Reading the results.**

- A case is a record of a moderator's action, not a verdict. Quote what it says; do not infer
  intent, and do not speculate about whether a punishment was deserved.
- Point out patterns that are actually in the data — a member with repeat cases in a short window,
  a spike in bans on one day, a single moderator accounting for most actions. That is the useful
  part of the answer and the owner rarely asks for it directly.
- Moderation records are about real people. Summarise; do not paste the raw case list unless the
  owner asks for it, and keep names and reasons out of anything that did not need them.

**Time ranges.** These tools default to a recent window. When a question is about a period ("last
month", "since the raid"), pass the date range rather than fetching the default and filtering in
your head — the default may not reach back far enough, and you will not be told so.
