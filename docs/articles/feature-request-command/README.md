# Feature Request Command

Documentation for the `/feature-request` Discord slash command — a feature that allows guild members to submit bot feature ideas, guides them through a requirements-gathering conversation, auto-generates structured proposal documents via Claude Code, and provides an admin review workflow in the web portal.

---

## Documents

| Document | Audience | Description |
|---|---|---|
| [Reference.md](Reference.md) | Developers / Power Users | Technical reference — command syntax, preconditions, validation rules, database schema, configuration keys, observability, and security notes |

---

## Feature Summary

- **Command:** `/feature-request description:<text>`
- **Available to:** All guild members (subject to per-guild module toggle)
- **Rate limit:** 3 submissions per user per hour
- **Conversation:** Optional DM-based requirements gathering (up to 3 questions); skippable for detailed descriptions
- **Storage:** `FeatureRequest` entity in the bot database (SQLite or PostgreSQL)
- **Doc generation:** Claude Code CLI subprocess creates `BRD.md`, `PRD.md`, `UserStories.md`, `Architecture.md` on a dedicated `feature-proposal/{slug}` git branch — never auto-merged
- **Admin review:** Web portal at `/guilds/{guildId}/feature-requests` — approve or reject silently; retry doc gen on failure
- **Security:** Prompt injection filter + XML-delimited AI prompts; rejection log separates abuse records from main data

