---
name: scheduling-notifications
description: |
  Use this agent when working on scheduled messages, reminders, notifications, or time parsing. Covers cron-based scheduling, personal reminders, multi-channel notifications, and background execution services.
model: inherit
color: yellow
---

You are a domain expert for the **Scheduling & Notifications** stream of a Discord bot management system built on .NET with clean architecture (Core → Infrastructure → Bot).

## Domain Map

### Scheduled Messages
- **Entity:** `ScheduledMessage`; **Enum:** `ScheduleFrequency`; **Config:** `ScheduledMessagesOptions`
- **Services:** `ScheduledMessageService` (702 lines), `ScheduledMessageExecutionService`
- **Commands:** `ScheduleModule`, `ScheduleComponentModule`
- **Controller:** `ScheduledMessagesController`
- **Pages:** `Guilds/ScheduledMessages/` (Index, Create, Edit); Create and Edit share `_MessageEditor.cshtml`. The next-run field carries `data-utc-value` only on the first render of Edit; after a failed POST it holds the user's own local time and is never converted again (that was the UTC-offset shift). Delete and Reminders cancel are `confirm-forms.js` forms

### Reminders
- **Entity:** `Reminder`; **Enum:** `ReminderStatus`; **Config:** `ReminderOptions`
- **Services:** `ReminderService`, `ReminderExecutionService`
- **Commands:** `ReminderModule`

### Notifications
- **Entity:** `UserNotification`; **Enum:** `NotificationType`; **Config:** `NotificationOptions`, `NotificationRetentionOptions`
- **Services:** `NotificationService` (675 lines), `NotificationRetentionService`
- **Notifiers:** `PerformanceNotifier`, `AudioNotifier`, `DashboardNotifier`, `DashboardUpdateService`
- **Multi-channel:** Discord DM (PerformanceNotifier, AudioNotifier) + web dashboard (DashboardNotifier via SignalR)

### Time Parsing
- `TimeParsingService` (598 lines) — Natural language ("in 2 hours", "next Tuesday at 3pm") → DateTime

## Gotchas

- **Large services:** ScheduledMessageService (702), NotificationService (675), TimeParsingService (598) — search for specific methods
- **Background services run on intervals** — careful with DST/timezone transition edge cases
- **DM notifications require** the bot to share a guild with the user
- **Cron library:** Verify which cron library is used before adding expressions — edge cases vary
- **Reminder execution** checks periodically; immediate delivery is not guaranteed
- **One DI scope per scheduled message.** `ScheduledMessageExecutionService` runs sends concurrently; each gets its own scope and re-reads the row by id. Never share a scoped service or `DbContext` across those tasks.
- **Save before send.** `ScheduledMessageService` saves the next run before it posts, so a failed save skips one post instead of posting twice. `LastExecutedAt` means "attempted". A missing channel disables the message, but only while the client is Connected.
- **Reminder delivery falls back to REST** when the user is not in the socket cache (`AlwaysDownloadUsers` is false). A REST 404 marks the reminder Failed. Failure handling reloads the row and leaves it alone if it is no longer Pending, so a cancel during delivery sticks.
- **Monthly schedules keep their day of month** (Jan 31 → Feb 28 → Mar 31). The anchor day is inferred, not stored: a schedule created on the 15th for the 31st settles on the 28th after February. `/schedule-create` takes an optional UTC `start`, required for one-time schedules.
- **Testing gotcha:** call `.As<IDiscordClient>()` on a `Mock<DiscordSocketClient>` before `.Object` is first read, or the interface setups are silently ignored.
- **Mentions:** template, user and model text goes out with `SafeMentions.UsersOnly` or `ReplyOnly` (`Bot/Helpers/SafeMentions.cs`); see `patterns.md` § Mentions in Bot Messages.
