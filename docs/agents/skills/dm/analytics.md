---
key: analytics
summary: Report a server's activity, growth, or slash-command usage and performance.
tools: get_server_activity_summary, get_command_analytics
---
**Guild context first.** Both tools need an active guild or an explicit `guild_id`. If neither is
set, list the guilds and ask which one. Name the server and the period in your answer — an activity
number with no period attached is not an answer.

**Which tool.**

- `get_server_activity_summary` — member counts, message volume, active members and growth over a
  period. Use it for how busy a server is, whether it is growing, and who is carrying the activity.
- `get_command_analytics` — the bot's own slash commands: usage counts and response-time
  performance. Use it for which features are actually used, and for whether something is slow.

**Reading the results.**

- Lead with the number that answers the question, then the one or two figures that give it meaning.
  "Roughly 4,100 messages last week, down about a fifth from the week before" beats a table.
- Say what changed and, where the data supports it, what it suggests: a growth figure that is all
  from one day, activity concentrated in a handful of members, a command whose response time is out
  of line with the rest.
- Do not present a small difference as a trend, and do not extrapolate from one period. If a figure
  looks wrong rather than interesting, say that instead of explaining it.
- Never paste the raw JSON. The owner asked a question, not for a dump.

**Cost.** These are the heavier queries the bot runs. Ask for one period and answer from it rather
than calling repeatedly to build a series by hand.
