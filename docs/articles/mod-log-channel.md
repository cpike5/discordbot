---
uid: mod-log-channel
title: Mod-log Channel
description: Post every moderation case to a Discord channel so moderators have a feed without opening the portal
---

# Mod-log Channel

The mod-log channel is a Discord-side feed of what the portal already records. Pick one text
channel per server and the bot posts an embed there for every moderation case, whatever created it
(a slash command such as `/warn` or `/ban`, the member moderation page in the portal, or a currency
fine), for every event auto-moderation flags for review, and for every action auto-moderation takes
on its own. The portal stays the record; the feed is a view of it.

## Setting it up

1. Open **Moderation Settings** for the server (`/Guilds/ModerationSettings/{guildId}`) and stay on
   the **Overview** tab.
2. Under **Mod-log channel**, choose a channel. Pick one only moderators can read: the embed names
   the member and shows the reason.
3. Tick what to post: **Moderation cases**, **Flagged events** (auto-moderation raised something
   High or Critical and did nothing on its own), **Automatic actions** (auto-moderation deleted a
   message, or muted, kicked or banned someone).
4. Save the overview.

If the server already has a channel named like `#mod-log` or `#mod-alerts` and nothing is saved, the
page offers it with a **Use it** button. Until this setting existed the bot posted automod alerts to
a channel by that name without being told to; it no longer does, so a server that relied on that
needs to choose the channel here once.

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

## What a flagged event or automatic action looks like

One post per event. When auto-moderation acted, the post is the action; otherwise it is the event.
Low and Medium events that needed no action stay in the portal's flagged-events list only, because
they would flood the channel.

| Part | Content |
| --- | --- |
| Title | `Auto-mod flagged: Spam`, or `Auto-mod muted: Spam` for an action, coloured by severity (low blue, medium gold, high orange, critical red; grey when the action failed) |
| Action | For an action only: what was done, and "failed; see the bot log" when Discord refused it |
| Description, User, Severity, Channel | From the flagged event |
| Message | The text that tripped the rule, cut to fit |
| Account created, Joined | For a join event: relative Discord timestamps |
| Buttons | **Dismiss**, **Acknowledge**, **Take Action**: the same review flow as the portal's flagged-events page, answered by `FlaggedEventComponentModule` |

## Where it lives

| Piece | Location |
| --- | --- |
| Setting | `GuildModerationConfig.ModLogChannelId` and `ModLogEvents` (a `ModLogEventKinds` flags value) |
| Contract | `IModLogNotifier` (`Core/Interfaces`): `CaseCreatedAsync`, `FlaggedEventAsync`, `AutoActionAsync`; `ModLogFlaggedContext` carries the message text and account age as plain values |
| Delivery | `ModLogNotifier` (`Bot/Services/Moderation`): reads the setting, resolves the channel, posts |
| Embed | `ModLogEmbeds` (`Bot/Helpers`): pure functions over the case DTO, covered by `ModLogEmbedsTests` |
| Hooks | `ModerationService.CreateCaseAsync` queues the case post on `IBackgroundTaskRunner` after the case is saved, so a slow Discord never delays a moderator's reply; `AutoModerationHandler.HandleDetectionResultAsync` posts the event or the action after it runs |
| Validation | `ModLogSettings` (`Bot/Helpers`), shared by the settings page and `PUT api/guilds/{guildId}/moderation-config` |

A failure anywhere in delivery is logged and swallowed. The feed never writes an audit row and
never replaces one.
