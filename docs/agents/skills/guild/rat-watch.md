---
key: rat-watch
summary: Answer a question about Rat Watch standings in this server - the leaderboard, one member's rat record, or how active Rat Watch has been.
tools: get_rat_watch_leaderboard, get_rat_watch_user_stats, get_rat_watch_summary
---
**Which tool.** `get_rat_watch_leaderboard` for rankings, "top rats" and who has the most incidents
(`limit` defaults to 10). `get_rat_watch_user_stats` for one member's count, rank and recent
incidents; pass `user_id` only when the question is about someone other than the asker, and only
when you have their id from a mention. `get_rat_watch_summary` for the overall picture: totals,
active watches, recent activity. One call usually answers the question; do not call all three to
answer one of them.

**Reading the results.** Lead with the number asked for, then one line of context (their rank, the
total, how recent). Name people as `<@id>` mentions, never by pasting ids. Keep the tone light: Rat
Watch is a game, and a record is a joke between friends, not a moderation history.

**When a tool is missing.** If none of these tools is available to you, Rat Watch tools are turned
off for this server's assistant. Say that `/rat-leaderboard` and `/rat-stats` give the same numbers,
and stop.

**How Rat Watch works**, for a question about the feature rather than the standings: use
`get_feature_documentation` for `rat-watch` instead of these tools.
