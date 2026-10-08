# Implementation Plan — Mod-log Channel Feed and Multi-turn Guild Assistant in Threads

> **Status:** Part A PR 1 and PR 2 shipped to `feature/mod-log-and-threaded-assistant`; the rest proposed
> **Date:** 2026-10-08
> **Source:** items 2 and 23 of `enhancement-candidates-2026-10.md`
> **Baseline:** v1.5.1-dev

Two independent features, two independent sections. Each section is written so it can be handed to
an agent on its own, lists its PRs in order, and names the files it touches. Neither depends on the
other; the mod-log feed is the smaller and should go first.

---

## Part A — Mod-log channel feed

### A.1 Goal

A guild admin picks one text channel. From then on, every moderation case (warn, kick, ban, unban,
mute, note), every flagged event the automod raises, and every automatic action the automod takes
is posted to that channel as an embed, with the same review buttons the automod alert already has
and a link to the portal. Moderators get a feed in Discord; the portal remains the record.

### A.2 What exists

- **A name-based alert already ships.** `AutoModerationHandler.SendModAlertAsync`
  (`src/DiscordBot.Bot/Handlers/AutoModerationHandler.cs:357`) looks for a text channel whose name
  contains `mod-log` or `mod-alert` and posts a severity-coloured embed with Dismiss, Acknowledge
  and Take Action buttons (`automod:dismiss:*` etc., handled by `FlaggedEventComponentModule`). It
  fires only for flagged events that do not auto-action. Nobody configures it and nothing documents
  it. This plan replaces the heuristic with a configured channel and widens what is posted.
- **One choke point for cases.** All eight case creators (`ModerationActionRunner` ×5,
  `ModerationActionModule`, `ModerationCasesController`, the wallet fine in `WalletModule` and
  `WalletsController`) go through `IModerationService.CreateCaseAsync`
  (`src/DiscordBot.Bot/Services/Moderation/ModerationService.cs:32`). Hooking there covers every
  path, including the portal and the currency fine.
- **Per-guild moderation config** is `GuildModerationConfig` (one row per guild, three JSON blobs
  for spam, content filter and raid rules), read and written through `IGuildModerationConfigService`
  (`Bot/Services/Guild/GuildModerationConfigService.cs`), edited on
  `Pages/Guilds/ModerationSettings/Index` (an Overview tab with `OnPostSaveOverviewAsync` taking
  `OverviewUpdateDto` from `ViewModels/Pages/ModerationSettingsViewModel.cs:78`) and over
  `PUT api/guilds/{guildId}/moderation/config` (`ModerationConfigController.UpdateConfig`).
- **Sending to a channel** has two precedents: `ScheduledMessageService` (line 427,
  `_client.GetChannel(id) as IMessageChannel`) and `WelcomeService` (line 272). Fire-and-forget
  work goes through `IBackgroundTaskRunner`, as `AudioModerationLogService` does.
- **Portal link base.** `ApplicationOptions.BaseUrl` (default `https://localhost:5001`) is the
  portal's public address; the assistant prompt already builds portal links from it.
- **The requirements doc asked for this.** `docs/requirements/moderation-system.md:354` lists
  "Discord channel alerts (post to mod-log channel)" as a notification channel.

### A.3 Design

#### Configuration

Three new columns on `GuildModerationConfig`:

| Column | Type | Default | Meaning |
| --- | --- | --- | --- |
| `ModLogChannelId` | `ulong?` | null | The feed channel. Null means the feed is off |
| `ModLogEvents` | `int` (flags enum `ModLogEventKinds`) | `Cases \| FlaggedEvents \| AutoActions` | Which kinds post |
| `ModLogUpdatedAt` | not needed; `UpdatedAt` already exists | | |

```csharp
[Flags]
public enum ModLogEventKinds
{
    None = 0,
    Cases = 1,          // every ModerationCase created, any CaseType
    FlaggedEvents = 2,  // automod flagged an event and did not act
    AutoActions = 4     // automod acted (delete, mute, kick, ban)
}
```

Columns rather than a fourth JSON blob: the channel id is queried on every case, it is a single
value, and a column is what every other per-guild channel setting is (`WelcomeChannelId`,
`NotXGuildSettings.OutputChannelId`).

`GuildModerationConfigDto` gains the same two properties. `ModerationCase` gains
`ModLogMessageId (ulong?)` so a later reason edit can update the posted embed (Phase 3).

#### Service

Interface in Core, because `ModerationService` (Bot) and `AutoModerationHandler` (Bot) call it and
tests mock it:

```csharp
// Core/Interfaces/IModLogNotifier.cs
public interface IModLogNotifier
{
    /// Posts a case to the guild's mod-log channel, if one is configured and Cases is enabled.
    /// Never throws; returns the posted message id or null.
    Task<ulong?> CaseCreatedAsync(ModerationCaseDto moderationCase, CancellationToken ct = default);
    Task FlaggedEventAsync(FlaggedEventDto flaggedEvent, ModLogFlaggedContext context, CancellationToken ct = default);
    Task AutoActionAsync(FlaggedEventDto flaggedEvent, AutoAction action, bool succeeded, CancellationToken ct = default);
}
```

`ModLogFlaggedContext` is a small record carrying what the current alert embed shows from the
Discord message (content excerpt, jump link) so the notifier does not need a `SocketMessage`.

Implementation `ModLogNotifier` in `Bot/Services/Moderation/`, scoped, depending on
`DiscordSocketClient`, `IGuildModerationConfigService`, `IOptions<ApplicationOptions>`,
`ILogger`. Its job per call:

1. Read config (the service already caches). No channel, or the kind is not enabled → return.
2. Resolve `_client.GetGuild(guildId)?.GetTextChannel(channelId)`. Missing → log a warning once
   per guild per hour (an `IMemoryCache` key) and return.
3. Build the embed and components with `ModLogEmbeds`, a static helper in `Bot/Helpers/`:
   `ForCase(ModerationCaseDto, baseUrl)`, `ForFlaggedEvent(...)`, `ForAutoAction(...)`. Static and
   pure so the tests assert on fields without a Discord client. The flagged-event embed and buttons
   move here from `AutoModerationHandler.BuildAlertEmbed` / `BuildAlertComponents` unchanged.
4. `SendMessageAsync`. A `Discord.Net.HttpException` with 403 is logged at Warning with the
   channel named, not rethrown.

The notifier itself is called through `IBackgroundTaskRunner.Run(...)` by its callers, so a slow or
failing Discord call never delays the `/warn` reply or the portal response. The one exception is
`CaseCreatedAsync` when Phase 3 lands and the message id is stored: then `ModerationService`
awaits it after the case row is saved and before returning, still inside its own try/catch.

Embed content (case): colour by `CaseType` (Warn gold, Mute orange, Kick orange, Ban red, Unban
green, Note blue); title `Case #{n} · {Type}`; fields User (`<@id>` + id), Moderator, Reason (or
"No reason given"), Duration and Expires (`<t:unix:R>`) when set, Context (jump link when
`ContextChannelId`/`ContextMessageId` are set); footer with the case id; a link button
`View in portal` to `{BaseUrl}/Guilds/{guildId}/Members/Moderation?userId={target}` (the existing
member moderation page). Cases created by automod carry the bot as moderator; the embed says
"Auto-moderation" instead of a mention.

#### Hook points

| Where | Change |
| --- | --- |
| `ModerationService.CreateCaseAsync` | After the repository add and audit, `_backgroundTaskRunner.Run(() => _modLog.CaseCreatedAsync(dto))`. One line; every caller is covered |
| `AutoModerationHandler.HandleDetectionResultAsync` | Replace `SendModAlertAsync(...)` with `_modLog.FlaggedEventAsync(...)` resolved from the scope it already opens. Delete `SendModAlertAsync`, `BuildAlertEmbed`, `BuildAlertComponents` |
| `AutoModerationHandler.ExecuteAutoActionAsync` | After each `case` branch, `_modLog.AutoActionAsync(flaggedEvent, action, succeeded)` |

The name heuristic (`mod-log` / `mod-alert`) is removed, not kept as a fallback. A guild that relied
on it unknowingly gets a one-time hint instead: on the Overview tab, when `ModLogChannelId` is null
and a channel with that name exists, the channel select is pre-highlighted with "Looks like
#mod-log — use it?" (a view-model flag, no behaviour). Document the removal in the release note.

#### Settings UI

`Pages/Guilds/ModerationSettings/Index`, Overview tab:

- A **Mod-log channel** `_FormSelect` fed by the same channel list the Assistant Settings page
  builds (`ChannelSelectItem`, with `IsMissing` for a saved channel the bot can no longer see). Add
  "Off" as the first option.
- Three `_FormToggle`s: Cases, Flagged events, Automatic actions. Disabled while the channel is Off.
- A **Send test message** button posting `?handler=TestModLog`, which calls the notifier with a
  synthetic case and reports success or the 403 reason as a toast.
- A **permission check** on load: if the bot lacks `SendMessages` or `EmbedLinks` in the chosen
  channel, an inline `_Alert` says so. Computed in the page model from
  `guild.CurrentUser.GetPermissions(channel)`.

`OverviewUpdateDto` gains `ModLogChannelId (string?)` and `ModLogEvents (int?)`. The page JS for
the Overview tab (`moderation-settings.js` or wherever `saveOverview` lives) sends them. Snowflakes
stay strings end to end.

`ModerationConfigController.UpdateConfig` accepts the two new DTO fields automatically since it
binds `GuildModerationConfigDto`; validate that the channel, when set, belongs to the guild
(`GuildModerationConfigService.UpdateConfigAsync` already validates the blobs; add the check there).

### A.4 PRs

**PR 1 — Data, service, case feed** (M)

- `Core/Enums/ModLogEventKinds.cs`; columns on `GuildModerationConfig` and `GuildModerationConfigDto`;
  `GuildModerationConfigConfiguration` mapping; both migrations (`AddModLogChannel`).
- `IModLogNotifier`, `ModLogNotifier`, `ModLogEmbeds` (case embed only), DI line in the moderation
  service extension.
- Hook in `ModerationService.CreateCaseAsync`.
- Settings: Overview tab channel select, toggles, DTO, controller validation. No test button yet.
- Tests: `ModLogEmbedsTests` (one per `CaseType`, reason fallback, expiry field present only when
  set, link button URL); `ModLogNotifierTests` with a mocked config service (off → no send;
  channel missing → warning logged once); `ModerationServiceTests` asserting the notifier is
  invoked after create; page model test for the overview save round-trip; route smoke test
  unchanged (page already exists).
- Docs: `data-model.md` (new columns), `feature-map.md` § Direct Moderation Actions,
  `service-catalog.md` § Moderation, `api-endpoints.md` (DTO fields),
  `docs/articles/moderation` user doc section "Mod-log channel", `.claude/agents/moderation-safety.md`.

**PR 2 — Flagged events and automod actions** (S)

- Move `BuildAlertEmbed`/`BuildAlertComponents` into `ModLogEmbeds.ForFlaggedEvent`; add
  `ForAutoAction`; delete `SendModAlertAsync` and the name heuristic.
- `AutoModerationHandler` calls the notifier for both kinds.
- The "Looks like #mod-log" hint on the Overview tab.
- Tests: handler tests already cover detection paths; add assertions that the notifier is called
  with the right kind and not called when the kind is disabled.
- Release note: the name heuristic is gone; set the channel in Moderation Settings.

**PR 3 — Polish** (S, optional)

- `ModerationCase.ModLogMessageId`; `UpdateCaseReasonAsync` edits the posted embed.
- Send-test-message handler and the permission check alert.
- A daily admin `UserNotification` when the feed has failed more than N times in 24 h (uses
  `INotificationService`; the counter is the same `IMemoryCache` key as the hourly warning).

### A.5 Risks and decisions

- **Discord rate limits.** A purge creates one case, not one per message, and automod actions are
  already per-event. A raid can produce a burst of flagged events; the notifier runs on the
  background runner and Discord.NET queues on 429, so the bot itself is not stalled. Acceptable.
- **Privacy.** The case embed shows the target's mention and the reason; the flagged-event embed
  shows a message excerpt (as the current alert does). The channel is admin-chosen and is expected
  to be moderator-only; the settings copy says so. No message content is stored anywhere new.
- **Automod cases.** Automod warns are "not implemented" today (`ExecuteAutoActionAsync`,
  `AutoAction.Warn` branch); the feed posts the *action* row regardless, so the feed is complete
  even where the case is not. Creating real cases for automod actions is a separate change.
- **Why not reuse `IAuditLogService`?** The audit log is the portal's record of who did what; the
  feed is a Discord view of moderation outcomes. Different consumers, different retention, and the
  audit log has no channel to post to. The feed does not replace an audit row and never writes one.

---

## Part B — Multi-turn guild assistant in threads

### B.1 Goal

Today a guild member mentions the bot, gets one answer, and cannot follow up: the prompt itself
says "You see one message at a time with no conversation history... Do not ask follow-up
questions." With this change, a guild can opt in to **thread mode**: the first mention opens a public
thread off the member's message, the answer goes in the thread, and anyone who posts in that thread
continues the conversation without mentioning the bot. The thread is the conversation key. Skills
loaded in the thread stay loaded, so a guild skill costs one round per conversation rather than one
per question, which removes the reason `docs/agents/skills/guild/` ships empty.

### B.2 What exists

| Piece | Where | Relevant detail |
| --- | --- | --- |
| Entry point | `Bot/Handlers/AssistantMessageHandler.HandleMessageReceivedAsync` | Ignores bots and DMs, requires a mention of the bot, strips it, runs the gates (global setting, guild enabled, channel allowed, consent, rate limit), calls `IAssistantService.AskQuestionAsync`, replies with `MessageReference`. Decides `CallerCanMutate` from Discord permissions, deliberately in this layer |
| Service | `Infrastructure/Services/AssistantService.AskQuestionAsync(guildId, channelId, userId, messageId, question, callerCanMutate)` | Re-checks the gates, builds the context via `IGuildAssistantContextFactory`, rate-limits by `{guildId}:{userId}`, runs `IAssistantMessagePipeline`, calls `context.RecordUsageAsync` |
| Context | `Infrastructure/Services/LLM/GuildAssistantContext` | `ConversationHistory` is always empty; `Skills` is a fresh session with nothing pre-activated; `RecordUsageAsync` writes `AssistantUsageMetrics` (daily) and `AssistantInteractionLog` (per question) and records the ledger row |
| Factory | `GuildAssistantContextFactory.CreateAsync` | Resolves model, narrows the registry with `FilteredToolRegistry` from `IToolAccessResolver`, builds the skill session with `preActivatedKeys: null` |
| The multi-turn precedent | `DmAssistantContextFactory` / `DmAssistantContext` | Loads the last `MaxConversationMessages` (20) rows of `DmConversationMessage` as `LlmMessage`s; saves the user and assistant turn on success and trims; replays `IDmSkillActivationStore.Get(userId)` into `ISkillSessionFactory.CreateAsync(...)` and stores `skills.Activated` afterwards. `SkillRoster.Append` puts loaded skills' instructions back into the system prompt because the tool result that carried them is not in the window |
| Pipeline | `AssistantMessagePipeline.RunAsync` | Passes `context.ConversationHistory` to `AgentContext.ConversationHistory` when non-empty. Nothing to change |
| Chunking | `DmAssistantMessageHandler` lines 141–195 | Splits a long reply on newlines into ≤2000-char messages or uploads `response.md`. Private to that handler |
| Settings | `AssistantGuildSettings` (IsEnabled, AllowedChannelIds JSON, RateLimitOverride, EnabledTools JSON); page `Pages/Guilds/AssistantSettings` | One row per guild; the page is a form with `_FormToggle`, a channel multi-select and the tool checklist |
| Prompt | `docs/agents/assistant-agent.md` | Rendered by `PromptTemplate` with `{{GUILD_ID}}`/`{{BASE_URL}}`; states the no-history rule |
| Retention | `Bot/Services/LLM/AssistantInteractionLogRetentionService` | Daily sweep of three tables by configured days |
| GDPR | `UserPurgeService`, `UserDataExportService` | Both enumerate the assistant tables per user |
| Evals | `tests/DiscordBot.Evals` (`EvalHarness`, `SkillEvals`, `MemoryToolEvals`) | Real loop, real model, throwaway SQLite; assert on tools called and rows written |
| Discord.NET 3.20.1 | `ITextChannel.CreateThreadAsync(name, ThreadType, ThreadArchiveDuration, IMessage, ...)`; `SocketThreadChannel` with `ParentChannel` and `Owner` | A thread the bot creates has the bot as owner. Messages in threads arrive on `MessageReceived` under the existing `GuildMessages` intent; no new intent |

### B.3 Design

#### Modes

`AssistantGuildSettings.ConversationMode` (`int`, enum `AssistantConversationMode`):

| Value | Behaviour |
| --- | --- |
| `SingleReply = 0` (default) | Exactly today. Opt-in keeps existing guilds' cost unchanged |
| `Thread = 1` | A mention in a text channel opens a public thread from the member's message and answers there. Messages in a bot-owned assistant thread are turns in that conversation, no mention needed |

A mention *inside* an existing assistant thread is a turn, with the mention stripped. A mention
inside a thread the bot did not create (someone else's thread) behaves as `SingleReply` does today:
one answer, no new thread (Discord cannot nest threads).

#### Data

Two new entities and two new columns.

```
AssistantThread
  ThreadId        ulong   PK (the Discord thread id)
  GuildId         ulong   FK Guild, indexed
  ParentChannelId ulong
  StarterUserId   ulong
  CreatedAt       DateTime
  LastActivityAt  DateTime  indexed (retention)
  TurnCount       int
  Status          int  (enum AssistantThreadStatus: Active, Closed)
  ActiveSkills    string  JSON array of skill keys, default "[]"

AssistantThreadMessage
  Id        long   PK
  ThreadId  ulong  FK AssistantThread (cascade), indexed with Timestamp
  UserId    ulong  indexed (GDPR)
  Role      string ("user" | "assistant")
  Content   string
  Timestamp DateTime
```

`AssistantInteractionLog.ThreadId (ulong?)` so the metrics page can count conversations and a
question can be traced to its thread. `AssistantGuildSettings.ConversationMode (int)`.

Skill activations live on the thread row rather than in `IMemoryCache` as the DM store does: a
thread can be picked up days later and after a restart, and a cache entry would silently drop the
instructions while the tools came back. `ActiveSkills` is read and written where the DM factory reads
and writes its store, so the engine sees the same `preActivatedKeys` shape.

Repositories `IAssistantThreadRepository` (get, add, touch, close, delete-older-than) and
`IAssistantThreadMessageRepository` (recent-by-thread, add, trim-to-window, delete-by-user,
delete-by-thread-older-than), in Core with Infrastructure implementations, same shape as
`IDmConversationMessageRepository`.

#### Request shape

`AskQuestionAsync` has six positional parameters already. Add a request record rather than a
seventh:

```csharp
public sealed record GuildAssistantRequest(
    ulong GuildId,
    ulong ChannelId,        // the channel the member wrote in: the thread id for a turn
    ulong? ParentChannelId, // the text channel the thread hangs off; null outside a thread
    ulong? ThreadId,        // non-null for a thread turn
    ulong UserId,
    ulong MessageId,
    string Question,
    bool CallerCanMutate);

Task<AssistantResponseResult> AskQuestionAsync(GuildAssistantRequest request, CancellationToken ct = default);
```

The existing overload stays as a one-line forwarder (`ThreadId = null`, `ParentChannelId = null`)
so current tests compile. `IGuildAssistantContextFactory.CreateAsync` takes the record plus the
rate limit. `IsAllowedInChannelAsync` is checked against `ParentChannelId ?? ChannelId`: the
allowed-channel list is about where conversations may start, and a thread inherits its parent.

#### Context changes

`GuildAssistantContextFactory.CreateAsync`:

1. When `request.ThreadId` is set, load the thread row (if missing, treat as `SingleReply`: the
   row was retained away or the bot restarted mid-create) and the last
   `Assistant:Threads:MaxConversationMessages` (default 20) messages as `LlmMessage`s, oldest first.
2. Build the skill session with `preActivatedKeys` from `ActiveSkills` instead of null.
3. Hand the context the thread id and the two repositories.

`GuildAssistantContext.RecordUsageAsync`, on a thread turn and `result.Success`: append the user
and assistant messages, trim to the window, set `ActiveSkills = skills.Activated keys`,
`LastActivityAt = now`, `TurnCount++`, and stamp `ThreadId` on the interaction log row. Failures
here are caught and logged, as every other write in that method is.

`BuildSystemPromptAsync` renders a new variable `{{CONVERSATION_MODE}}` into the prompt (below).
The rest of the prompt is byte-identical between modes so the cached prefix is shared up to that
line; the thread variant and the single variant are two cache prefixes per guild, which is the
same count the allow-list already produces.

`FormatUserMessageAsync` keeps the `{GUILD_ID}\n{GUILD_NAME}\n---\n{msg}` shape on every turn.
Changing it would mean two formats in one history.

#### Handler changes

`AssistantMessageHandler.HandleMessageReceivedAsync` gets one decision up front, pulled into a
pure static so it can be unit-tested without a socket client:

```csharp
// Bot/Services/LLM/AssistantTriggerRules.cs
public static AssistantTrigger Classify(
    bool authorIsBot, bool isGuildChannel, bool mentionsBot,
    bool isThread, bool threadOwnedByBot, bool threadIsKnownAssistantThread,
    AssistantConversationMode mode)
// -> Ignore | NewQuestion | NewThreadQuestion | ThreadTurn
```

- `ThreadTurn` when the message is in a `SocketThreadChannel` whose `Owner` is the bot **and**
  `IAssistantThreadRepository` has the row. Both checks: ownership is cheap and filters every other
  thread before a database read; the row is what proves it is an assistant thread and not, say, a
  thread the bot made for some future feature.
- `NewThreadQuestion` when mentioned in a non-thread guild text channel and the guild's mode is
  `Thread`.
- `NewQuestion` otherwise when mentioned (including inside a thread the bot does not own).

Flow for `NewThreadQuestion`:

1. Run the gates as today (consent, rate limit...). Consent is still per author.
2. `question = ExtractQuestion(content)`; thread name = first 90 characters of the question (Discord
   caps at 100), falling back to `"Assistant · {displayName}"`.
3. `thread = await textChannel.CreateThreadAsync(name, ThreadType.PublicThread,
   ThreadArchiveDuration.OneDay, message)`. On `HttpException` 403 (bot lacks Create Public Threads
   or Send Messages in Threads), log at Warning once per channel per hour and **fall back to
   `NewQuestion`**: the member still gets an answer in the channel. Never fail silently.
4. Insert the `AssistantThread` row before asking the model, so a second message in the thread that
   arrives while the first answer is being generated is classified as a turn (it will then wait on
   nothing; see "Concurrency").
5. `AskQuestionAsync(request with ThreadId = thread.Id, ChannelId = thread.Id, ParentChannelId =
   textChannel.Id)`; send the reply in the thread with the typing indicator on the thread.

Flow for `ThreadTurn`: same gates against the parent channel; question is the whole content with
any bot mention stripped; reply in the thread with `MessageReference` to the member's message.

Replies use a shared `DiscordReplyChunker` (new, `Bot/Helpers/`), extracted verbatim from
`DmAssistantMessageHandler`'s split-or-attach logic so both handlers behave the same. The DM handler
is changed only to call it. `MaxResponseLength` stays the cap on what the model is allowed to say;
the chunker is for the rare reply that still exceeds 2000 characters after tool output.

Consent prompts inside a thread are sent once per user per thread (an `IMemoryCache` key with the
thread id and user id, one-hour TTL), so a non-consenting bystander does not get the embed on
every message they post in a public thread.

#### Closing a thread

- **Turn cap.** `Assistant:Threads:MaxTurnsPerThread` (default 40). On reaching it the bot replies
  "This conversation has reached its limit. Mention me in the channel to start a new one.", sets
  `Status = Closed`, and later messages in that thread are ignored. Bounds the cost of one thread.
- **Archive.** Discord auto-archives after `OneDay` of inactivity. A message in an archived thread
  un-archives it, and the bot continues if the row is still Active and within retention. No
  handling needed.
- **`/assistant end`** is deliberately not in scope. Archive and the cap cover it.

#### Concurrency

Two members posting in the same thread at once produce two runs with the same seeded history; each
appends its own pair of messages. The second answer will not see the first. Accept this for the
first release (the DM assistant has the same property and one owner), but serialise writes with a
per-thread `SemaphoreSlim` in `AssistantMessageHandler` (the Per-Guild Locking pattern, keyed by
thread id, entries dropped when the thread is closed) so the two runs at least execute one after the
other and the second sees the first's turn. Note in the handler why.

#### Prompt

`docs/agents/assistant-agent.md`, section "How messages arrive", becomes:

```
{{CONVERSATION_MODE}}
```

rendered from one of two fragments kept in the same file under the template's variable, or, more
simply, two short paragraphs selected by the context:

- Single: the current text ("You see one message at a time with no conversation history... Do not
  ask follow-up questions.").
- Thread: "You are in a thread with one or more members. Earlier turns are in your history; later
  messages may come from different people, each shown with the same header. You may ask one short
  clarifying question when the request is ambiguous. Keep answers as short as before."

`PromptTemplate` renders `{{variable}}` substitutions, so the context passes the chosen paragraph
as the variable's value. Prompt-injection guidance (never quote injection phrases) applies.

#### Settings UI

`Pages/Guilds/AssistantSettings`:

- A `_RadioCardGroup` **Conversation mode** with two cards: "Single reply" (today) and "Threads"
  (description: opens a thread per question so members can follow up; the bot needs Create Public
  Threads and Send Messages in Threads in the allowed channels).
- Under it, when Threads is chosen, a read-only list of allowed channels where the bot lacks those
  permissions (computed in the page model from `guild.CurrentUser.GetPermissions(channel)`), as an
  inline `_Alert`. Saving is still allowed; the handler falls back per channel.

`InputModel.ConversationMode` binds the enum; `AssistantGuildSettingsService` saves it and
invalidates whatever it caches today.

#### Metrics and retention

- `Pages/Guilds/AssistantMetrics`: a **Conversations** tile (distinct `ThreadId` in the window) and
  an **Average turns per conversation** figure next to the existing question count, both read by
  `IAssistantTelemetryReader` from the interaction log. Phase 3.
- `AssistantInteractionLogRetentionService` sweeps `AssistantThread` rows (and their messages, by
  cascade) whose `LastActivityAt` is older than `Assistant:Threads:HistoryRetentionDays` (default
  30), on the same daily cadence. `0` disables, like the others.
- `UserPurgeService` deletes a user's `AssistantThreadMessage` rows and anonymises
  `StarterUserId` on threads they started (set to 0; the thread belongs to the guild, the text was
  theirs). `UserDataExportService` includes their thread messages. Ships in PR 1 with the tables.

#### Options

`Assistant:Threads` section, new class `AssistantThreadOptions` on `AssistantOptions.Threads`:

| Key | Default | Meaning |
| --- | --- | --- |
| `MaxConversationMessages` | 20 | History window seeded per turn (same meaning as the DM option) |
| `MaxTurnsPerThread` | 40 | Closes the thread when reached |
| `HistoryRetentionDays` | 30 | Retention sweep for threads and their messages |
| `AutoArchiveMinutes` | 1440 | Mapped to the nearest `ThreadArchiveDuration` (60, 1440, 4320, 10080) |

Documented in `docs/articles/configuration-guide.md`.

### B.4 PRs

**PR 1 — Data, settings, context (no Discord behaviour change)** (M)

- Entities `AssistantThread`, `AssistantThreadMessage`; enums `AssistantConversationMode`,
  `AssistantThreadStatus`; columns on `AssistantGuildSettings` and `AssistantInteractionLog`; EF
  configurations; both migrations (`AddAssistantThreads`); `data-model.md`.
- Repositories and their Infrastructure implementations; registered in the assistant service
  extension (ungated, like the telemetry reader, so the settings page works without an API key).
- `AssistantThreadOptions`; `configuration-guide.md`.
- `GuildAssistantRequest`; the new `AskQuestionAsync` and `CreateAsync` overloads; old overloads
  forward.
- Factory seeds history and replays skills when `ThreadId` is set; context records the turn, trims,
  stores skills, stamps `ThreadId` on the log row; `{{CONVERSATION_MODE}}` rendering.
- Settings page: the mode radio group and save path.
- Retention sweep and GDPR purge/export.
- Tests: `GuildAssistantContextFactoryTests` (no thread → empty history and no pre-activation;
  thread → history in order and bounded by the window; skills replayed from `ActiveSkills`);
  `GuildAssistantContextTests` for `RecordUsageAsync` (turn appended, trimmed, skills written,
  `ThreadId` on the log, failures swallowed); `AssistantContextSkillTests` extended for the replay;
  repository tests on Postgres; retention service test; purge/export tests extended; settings page
  save round-trip; `ToolContractTests` and `SkillContractTests` unchanged and still green.
- After this PR nothing user-visible changes: no handler creates threads yet.

**PR 2 — Threads in Discord** (M)

- `AssistantTriggerRules` and its tests (every combination in the table above).
- `DiscordReplyChunker` extracted from `DmAssistantMessageHandler`; that handler calls it; tests
  moved with it.
- `AssistantMessageHandler`: classification, thread creation with the 403 fallback, thread turns,
  per-thread lock, once-per-thread consent prompt, turn cap and close.
- Prompt file: the two "How messages arrive" paragraphs.
- Settings page: the permission warning list.
- `docs/articles/ai-assistant.md`: "Thread mode" section (what members see, who can continue, the
  cap, the permissions the bot needs); `feature-map.md` § AI Assistant and § DM Assistant (the
  sentence explaining why `guild/` ships empty now says threads change that);
  `docs/agents/skills/README.md` cost note updated; `.claude/agents/ai-assistant.md`.
- Evals (`tests/DiscordBot.Evals/ThreadConversationEvals.cs`, skipped without a key): (1) two turns
  through the real factory with a seeded `AssistantThread`: turn 2's `AgentContext` history
  contains turn 1 (assert on rows, not on reply text); (2) a skill loaded in turn 1 is advertised in
  turn 2 with no `load_skill` call in turn 2's tool list. The harness runs on SQLite, so the SQLite
  migration from PR 1 is exercised here even though nothing else uses it.
- Manual verification in offline mode is not possible (threads need Discord); verify against
  `Discord:TestGuildId` and record the steps in the PR.

**PR 3 — Metrics and the first guild skill** (S)

- Conversations tile and average turns on `AssistantMetrics`; `IAssistantTelemetryReader` methods;
  tests.
- `docs/agents/skills/guild/` gets its first file only when there is a tool group worth hiding;
  the obvious candidate is the Rat Watch trio (three always-on tools that most questions never
  need). Measure with the Prompt Surface panel before and after; that number is the justification
  and goes in the PR.

### B.5 Risks and decisions

- **Cost.** Thread mode is opt-in per guild and the window, cap and retention are all configurable.
  A 20-message window at ~150 tokens per message adds ~3,000 input tokens to a late turn; with a
  pinned model and prompt caching the prefix is shared, the history is not. The metrics page shows
  the per-guild cost either way.
- **Public threads expose the conversation to the channel.** That is the point (a member's follow-ups
  help the next member), and it is what the consent copy already describes for the channel reply.
  Private threads would hide the conversation from moderators; not offered.
- **Who may continue.** Anyone with consent, rate-limited per user as today. Restricting to the
  starter would make a public thread a confusing place for everyone else; the cap and the per-user
  rate limit bound abuse.
- **The `Owner` check.** A thread's owner is whoever created it; the bot creates assistant threads,
  so `Owner.Id == _client.CurrentUser.Id` is a reliable first filter. Discord does not let a user
  transfer thread ownership.
- **Tools and `ChannelId`.** `ToolContext.ChannelId` becomes the thread id on a turn. No shipped
  guild tool reads it (they use `GuildId` and `UserId`); a future tool that posts to "the current
  channel" would post in the thread, which is right.
- **Why not reuse `DmConversationMessage`?** It is keyed by user, has no guild or thread, and its
  repository trims per user. Sharing it would couple the owner-only surface to guild data and its
  GDPR path. Two small tables are cheaper than a nullable-column compromise.
- **Why not a slash command to start a conversation?** A mention is how members already reach the
  assistant; keeping one entry point keeps the consent and rate-limit story unchanged.
