---
uid: mod-log-channel
title: Mod-log Channel
description: Post every moderation case to a Discord channel so moderators have a feed without opening the portal
---

# Mod-log Channel

The mod-log channel is a Discord-side feed of what the portal already records. Pick one text
channel per server and the bot posts an embed there for every moderation case, whatever created it:
a slash command (`/warn`, `/kick`, `/ban`, `/unban`, `/mute`), the member moderation page in the
portal, or a currency fine. The portal stays the record; the feed is a view of it.

## Setting it up

1. Open **Moderation Settings** for the server (`/Guilds/ModerationSettings/{guildId}`) and stay on
   the **Overview** tab.
2. Under **Mod-log channel**, choose a channel. Pick one only moderators can read: the embed names
   the member and shows the reason.
3. Tick what to post. **Moderation cases** is the only kind that posts today; **Flagged events** and
   **Automatic actions** are stored now and start posting when the auto-moderation feed lands.
4. Save the overview.

The bot needs **Send Messages** and **Embed Links** in the channel. If it lacks them, nothing is
posted and a warning is logged once an hour, not once per case.

Choosing **Off (no channel)** turns the feed off. Applying a protection level (Relaxed, Moderate,
Strict) replaces the moderation rules but leaves the channel and its kinds as they were.

## What a case looks like

| Part | Content |
| --- | --- |
| Title | `Case #12 · Warn`, coloured by type: warn gold, mute and kick orange, ban red, unban green, note blue |
| User | A mention plus the raw id, so the row still identifies someone who has left |
| Moderator | A mention, or "Auto-moderation" when the bot created the case |
| Reason | The reason, or "No reason given" |
| Duration and Expires | Only for a temporary mute or ban; Expires is a relative Discord timestamp |
| Context | A jump link to the message the action was taken on and a short quote of it, when the case carries one |
| Button | **View in portal**: the member's moderation page, with the case, their other cases, notes and tags |

## Where it lives

| Piece | Location |
| --- | --- |
| Setting | `GuildModerationConfig.ModLogChannelId` and `ModLogEvents` (a `ModLogEventKinds` flags value) |
| Contract | `IModLogNotifier` (`Core/Interfaces`), one method per kind of event |
| Delivery | `ModLogNotifier` (`Bot/Services/Moderation`): reads the setting, resolves the channel, posts |
| Embed | `ModLogEmbeds` (`Bot/Helpers`): pure functions over the case DTO, covered by `ModLogEmbedsTests` |
| Hook | `ModerationService.CreateCaseAsync` queues the post on `IBackgroundTaskRunner` after the case is saved, so a slow Discord never delays a moderator's reply |
| Validation | `ModLogSettings` (`Bot/Helpers`), shared by the settings page and `PUT api/guilds/{guildId}/moderation-config` |

A failure anywhere in delivery is logged and swallowed. The feed never writes an audit row and
never replaces one.
