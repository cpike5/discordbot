# Test Coverage Gaps & Recommended Minimum Cases

**Date:** 2026-04-01
**Current state:** 3,216 tests across 182 files. Existing tests are high quality (xUnit, Moq, FluentAssertions, AAA pattern, strong isolation). The gap is breadth, not depth.

---

## 1. Command Modules (1 of 31 tested)

**Why it matters:** These are the primary user-facing interaction points. Validation logic, guard clauses, and service delegation all live here.

**Pattern:** All modules inherit `InteractionModuleBase<SocketInteractionContext>`, use constructor injection, and delegate to services. Testable surface area is the validation/guard logic and correct service delegation. Discord's sealed `SocketInteractionContext` requires a wrapper or abstraction to mock.

### Recommended minimum cases

#### ReminderModule (IReminderService, ITimeParsingService, IOptions\<ReminderOptions>)
- `CreateReminder_ValidInput_DelegatesToReminderService`
- `CreateReminder_ExceedsPendingLimit_ReturnsErrorEmbed`
- `CreateReminder_InvalidTimeFormat_ReturnsParseError`
- `ListReminders_NoReminders_ReturnsEmptyMessage`
- `DeleteReminder_NonexistentId_ReturnsNotFound`

#### ModerationActionModule (IModerationService)
- `WarnUser_TargetIsBot_ReturnsError`
- `WarnUser_TargetIsSelf_ReturnsError`
- `WarnUser_ValidTarget_DelegatesToModerationService`
- `BanUser_MissingPermissions_ReturnsPermissionError`
- `WarnUser_CreatesModalWithCorrectFields`

#### WatchlistModule (IWatchlistService, IInteractionStateService)
- `Add_TargetIsBot_ReturnsError`
- `Add_AlreadyOnWatchlist_ReturnsDuplicateError`
- `Add_ValidUser_DelegatesToWatchlistService`
- `Remove_NotOnWatchlist_ReturnsNotFoundError`
- `List_EmptyWatchlist_ReturnsEmptyEmbed`

#### ScheduleModule (IScheduledMessageService, ITimeParsingService)
- `Create_ValidInput_DelegatesToScheduledMessageService`
- `Create_InvalidCron_ReturnsParseError`
- `List_NoScheduledMessages_ReturnsEmptyMessage`
- `Delete_NonexistentId_ReturnsNotFound`
- `Edit_ValidChanges_UpdatesViaService`

#### SoundboardModule (ISoundService, ISoundFileService)
- `Play_SoundNotFound_ReturnsError`
- `Play_NotInVoiceChannel_ReturnsError`
- `Upload_ExceedsMaxSize_ReturnsError`
- `List_ReturnsPaginatedSounds`
- `Delete_ValidSound_DelegatesToService`

#### Additional modules needing at least 2-3 cases each
- `AdminModule` — permission checks, admin-only operations
- `TtsModule` — TTS-disabled guild check, valid/invalid input
- `VoxModule` — clip not found, not in voice channel
- `VoiceModule` — join/leave/move delegation
- `ModNoteModule` — CRUD delegation, permission checks
- `ModTagModule` — CRUD delegation, duplicate tag check
- `RatWatchModule` — enable/disable, report submission
- `WelcomeModule` — config get/set delegation
- `ConsentModule` — already tested, verify coverage is adequate
- `NotXCommandModule` — toggle, configuration
- `VerifyAccountModule` — code validation, expiry check
- `PrivacyModule` — data export request, purge confirmation

---

## 2. Event Handlers (3 of 11 tested)

**Why it matters:** Handlers process every incoming Discord event. Bugs here affect all users silently.

**Pattern:** Thin async methods with guard clauses (ignore bots, check guild context) then delegate to services. Testable via mocking injected services and verifying delegation.

### Recommended minimum cases

#### AutoModerationHandler (ISpamDetectionService, IContentFilterService, IRaidDetectionService)
- `HandleMessage_FromBot_ReturnsEarlyWithoutProcessing`
- `HandleMessage_FromDm_ReturnsEarlyWithoutProcessing`
- `HandleMessage_ValidMessage_DelegatesToSpamDetection`
- `HandleMessage_ValidMessage_DelegatesToContentFilter`
- `HandleMessage_SpamDetected_TakesAction`
- `HandleMessage_FilterTriggered_LogsFlaggedEvent`

#### VoiceStateHandler (IAudioService, IAudioNotifier)
- `HandleVoiceUpdate_BotDisconnected_CleansUpAudioService`
- `HandleVoiceUpdate_UserJoinsChannel_NotifiesIfBotConnected`
- `HandleVoiceUpdate_UserLeavesChannel_NotifiesIfBotConnected`
- `HandleVoiceUpdate_ChannelEmpty_TriggersAutoLeave`

#### AssistantMessageHandler (IServiceScopeFactory, AssistantOptions)
- `HandleMessage_NotMentioned_ReturnsEarly`
- `HandleMessage_FromBot_ReturnsEarly`
- `HandleMessage_BotMentioned_DelegatesToAssistantService`
- `HandleMessage_AssistantDisabled_ReturnsEarly`

#### DmAssistantMessageHandler
- `HandleMessage_NotDm_ReturnsEarly`
- `HandleMessage_FromBot_ReturnsEarly`
- `HandleMessage_ValidDm_DelegatesToDmAssistantService`
- `HandleMessage_NoConsent_ReturnsConsentPrompt`

#### NotXMessageHandler
- `HandleMessage_NotXDisabledForGuild_ReturnsEarly`
- `HandleMessage_ContainsTrigger_DelegatesToNotXService`
- `HandleMessage_NoTrigger_ReturnsEarly`

#### ActivityEventTrackingHandler
- `HandleMessage_ValidGuildMessage_RecordsActivity`
- `HandleMessage_FromBot_Skipped`
- `HandleReaction_RecordsActivity`

#### VoiceStateHandler (already above), FeatureRequestDmHandler, DiscordApiTracingHandler
- 2-3 cases each covering happy path and primary guard clause

---

## 3. API Controllers (14 of 31 tested)

**Why it matters:** These serve the web portal and are the external API surface. Authorization and input validation bugs here are security-relevant.

### Recommended minimum cases

#### AlertsController
- `GetAlerts_ReturnsPagedResults`
- `GetAlert_NotFound_Returns404`
- `AcknowledgeAlert_ValidId_UpdatesStatus`

#### AnalyticsController
- `GetGuildAnalytics_ValidGuild_ReturnsSummary`
- `GetGuildAnalytics_Unauthorized_Returns403`
- `GetEngagementData_DateRange_FiltersCorrectly`

#### AudioController
- `GetAudioSettings_ValidGuild_ReturnsConfig`
- `UpdateAudioSettings_ValidInput_Persists`
- `GetPlaybackLog_ReturnsPaginatedResults`

#### SoundsController
- `GetSounds_ReturnsPaginatedList`
- `UploadSound_ValidFile_ReturnsCreated`
- `UploadSound_InvalidMimeType_Returns400`
- `DeleteSound_NotFound_Returns404`

#### WatchlistController
- `GetWatchlist_ValidGuild_ReturnsList`
- `AddToWatchlist_DuplicateUser_Returns409`
- `RemoveFromWatchlist_NotFound_Returns404`

#### Additional controllers needing 2-4 cases each
- `BulkPurgeController` — authorization, valid/invalid channel
- `FlaggedEventsController` — list, resolve, filter by status
- `ModTagsController` — CRUD operations
- `ModerationConfigController` — get/update guild config
- `NotificationsController` — list, mark read, mark all read
- `UserModerationController` — history lookup, permission checks
- `UserPreferencesController` — get/update preferences
- `PortalSoundboardController` — OAuth context, sound list
- `PortalVoxController` — OAuth context, clip list
- `CommandsApiController` — command metadata list
- `PreviewController` — embed preview rendering

---

## 4. Data Repositories (16 of 53 tested)

**Why it matters:** Data integrity. Incorrect queries cause silent data loss or corruption.

**Pattern:** All repositories use `BotDbContext` with `TestDbContextFactory` (in-memory SQLite). Existing repository tests are the template.

### Recommended minimum cases

#### ModerationCaseRepository (high priority — moderation data)
- `CreateAsync_ValidCase_PersistsAndReturnsId`
- `GetByIdAsync_ExistingCase_ReturnsCase`
- `GetByIdAsync_NonExistent_ReturnsNull`
- `GetByGuildAsync_ReturnsFilteredResults`
- `GetByUserAsync_ReturnsUserCases`
- `UpdateAsync_SetsModifiedFields`

#### WatchlistRepository
- `AddAsync_ValidEntry_Persists`
- `GetByGuildAsync_ReturnsList`
- `RemoveAsync_ExistingEntry_Removes`
- `IsOnWatchlistAsync_WhenPresent_ReturnsTrue`
- `IsOnWatchlistAsync_WhenAbsent_ReturnsFalse`

#### FlaggedEventRepository
- `CreateAsync_PersistsEvent`
- `GetByGuildAsync_FiltersByStatus`
- `ResolveAsync_SetsResolvedFields`
- `GetPendingCountAsync_ReturnsCorrectCount`

#### SoundRepository + SoundCategoryRepository
- `GetByGuildAsync_ReturnsSounds`
- `GetByIdAsync_IncludesCategory`
- `SearchAsync_MatchesNamePartial`
- `DeleteAsync_RemovesSound`
- `GetCategoriesAsync_ReturnsSorted`

#### NotificationRepository
- `CreateAsync_PersistsNotification`
- `GetByUserAsync_ReturnsPaginated`
- `MarkReadAsync_UpdatesFlag`
- `GetUnreadCountAsync_ReturnsCorrectCount`

#### FeatureRequestRepository
- `CreateAsync_PersistsRequest`
- `GetByGuildAsync_ReturnsFiltered`
- `UpdateStatusAsync_ChangesStatus`
- `GetByIdAsync_IncludesVotes`

#### Additional repositories needing 3-4 cases each
- `ModNoteRepository` — CRUD + GetByUser
- `ModTagRepository` — CRUD + GetByGuild
- `GuildAudioSettingsRepository` — GetOrCreate pattern
- `GuildTtsSettingsRepository` — GetOrCreate pattern
- `GuildModerationConfigRepository` — GetOrCreate pattern
- `GuildRatWatchSettingsRepository` — GetOrCreate pattern
- `PerformanceAlertRepository` — Create + GetActive + Acknowledge
- `RatRecordRepository` — Create + GetByGuild + GetByUser
- `RatWatchRepository` — Active watch queries
- `SettingsRepository` — Get/Set key-value pairs
- `ThemeRepository` — CRUD
- `All DM-related repositories` (6 repos) — basic CRUD per repo

---

## 5. Services (58 of ~104 tested)

**Why it matters:** Business logic lives here. Many untested services handle moderation, content filtering, and security-critical operations.

### Recommended minimum cases

#### ContentFilterService (IContentFilterService) — SECURITY CRITICAL
- `AnalyzeMessage_NoFilters_ReturnsClean`
- `AnalyzeMessage_MatchesRegexFilter_ReturnsFlagged`
- `AnalyzeMessage_CaseInsensitiveMatch_ReturnsFlagged`
- `AnalyzeMessage_CacheExpired_RefreshesFilters`
- `AnalyzeMessage_InvalidRegex_HandlesGracefully`

#### RaidDetectionService — SECURITY CRITICAL
- `AnalyzeJoin_NormalRate_ReturnsNoThreat`
- `AnalyzeJoin_BurstJoins_DetectsRaid`
- `AnalyzeJoin_SimilarAccountNames_FlagsSuspicious`
- `Reset_ClearsState`

#### SpamDetectionService — SECURITY CRITICAL
- `AnalyzeMessage_NormalMessage_ReturnsClean`
- `AnalyzeMessage_RepeatedMessages_DetectsSpam`
- `AnalyzeMessage_MassMentions_DetectsSpam`
- `AnalyzeMessage_RapidFire_DetectsSpam`

#### ModerationService (IModerationCaseRepository)
- `CreateCaseAsync_ValidInput_PersistsCase`
- `CreateCaseAsync_SetsCreatedTimestamp`
- `GetCaseAsync_ExistingId_ReturnsCase`
- `GetCaseAsync_NonExistentId_ReturnsNull`
- `GetCasesForUserAsync_ReturnsSortedList`

#### NotificationService
- `CreateForUserAsync_ValidInput_PersistsAndBroadcasts`
- `CreateForUserAsync_InvalidUser_ThrowsOrReturnsNull`
- `MarkReadAsync_UpdatesNotification`
- `GetUnreadCountAsync_ReturnsCorrectCount`

#### DmAssistantService
- `HandleMessageAsync_ValidInput_CallsLlmClient`
- `HandleMessageAsync_NoConsent_ReturnsError`
- `HandleMessageAsync_RateLimited_ReturnsError`
- `HandleMessageAsync_LlmError_HandlesGracefully`

#### FeatureRequestService
- `CreateAsync_ValidRequest_Persists`
- `GetByGuildAsync_ReturnsPaginated`
- `UpdateStatusAsync_NotifiesUser`
- `VoteAsync_DuplicateVote_ReturnsError`

#### WatchlistService
- `AddAsync_ValidUser_PersistsEntry`
- `AddAsync_DuplicateUser_ReturnsError`
- `RemoveAsync_ExistingUser_Removes`
- `IsOnWatchlistAsync_DelegatesToRepository`

#### Additional services needing 2-4 cases each
- `BulkPurgeService` — execute purge, audit logging
- `VoxService` / `VoxConcatenationService` — clip lookup, concatenation
- `NotXService` — trigger detection, response generation
- `RatWatchExecutionService` — execution cycle, heartbeat
- `SoundboardOrchestrationService` — play flow, queue management
- `MemberSyncService` — sync execution, error handling
- `UserDataExportService` — GDPR export assembly
- `InputValidationService` — boundary inputs, injection attempts
- `ModNoteService` / `ModTagService` — CRUD delegation
- `VoiceAutoLeaveService` — timeout trigger, channel check
- `AlertMonitoringService` — threshold breach detection
- `PerformanceAlertService` — alert creation, deduplication

---

## 6. AI/LLM Integration (6 of 47 tested)

**Why it matters:** LLM integration is a core feature. Tool providers execute real actions (moderation, data queries) based on AI decisions. Bugs here could cause unintended moderation actions.

### Recommended minimum cases

#### ~~OpenRouterLlmClient (ILlmClient) — CRITICAL~~ — COVERED
Closed by the OpenRouter migration. `OpenRouterLlmClientTests` covers request construction, retry
on transient statuses, no-retry on permanent ones, per-status error messages, error objects
returned on a 200, empty choices, unparseable bodies, cancellation, and the capability flags.
`OpenRouterMessageMapperTests` and `OpenRouterWireTests` cover the DTO mapping and the wire
contract. (The predecessor `AnthropicLlmClient` never had a test.)

#### ConversationToolProvider (IDmToolProvider)
- `GetTools_ReturnsExpectedToolDefinitions`
- `ExecuteToolAsync_ClearConversation_DelegatesToRepository`
- `ExecuteToolAsync_UnknownTool_ReturnsError`
- `ExecuteToolAsync_NullArguments_HandlesGracefully`

#### BotManagementToolProvider (IDmToolProvider)
- `GetTools_ReturnsExpectedToolDefinitions`
- `ExecuteToolAsync_ListGuilds_ReturnsGuildList`
- `ExecuteToolAsync_SetActiveGuild_CachesSelection`
- `ExecuteToolAsync_GetBotHealth_ReturnsMetrics`
- `ExecuteToolAsync_SearchAuditLogs_DelegatesToService`
- `ExecuteToolAsync_UnknownTool_ReturnsError`

#### MemoryToolProvider
- `GetTools_ReturnsExpectedToolDefinitions`
- `ExecuteToolAsync_SaveNote_Persists`
- `ExecuteToolAsync_SearchNotes_ReturnsMatches`
- `ExecuteToolAsync_DeleteNote_Removes`

#### RatWatchToolProvider / RatWatchTools
- `GetTools_ReturnsExpectedToolDefinitions`
- `ExecuteToolAsync_GetActiveWatches_ReturnsData`
- `ExecuteToolAsync_CreateWatch_DelegatesToService`

#### DmModerationToolProvider — SECURITY CRITICAL
- `GetTools_ReturnsExpectedToolDefinitions`
- `ExecuteToolAsync_WarnUser_DelegatesToModerationService`
- `ExecuteToolAsync_MissingGuildContext_ReturnsError`
- `ExecuteToolAsync_InsufficientPermissions_ReturnsError`

#### WebFetchToolProvider
- `GetTools_ReturnsExpectedToolDefinitions`
- `ExecuteToolAsync_ValidUrl_ReturnsFetchedContent`
- `ExecuteToolAsync_InvalidUrl_ReturnsError`
- `ExecuteToolAsync_Timeout_HandlesGracefully`

#### CodeExecutionToolProvider, ClaudeCodeToolProvider, DmAnalyticsToolProvider, DmDocumentationToolProvider
- 2-3 cases each: `GetTools` returns definitions, happy path execution, error handling

---

## 7. Autocomplete Handlers (0 of 8 tested)

**Why it matters:** Broken autocomplete degrades UX for every command that uses it.

**Pattern:** All implement `AutocompleteHandler` with `GenerateSuggestionsAsync`. Simple filtering logic but good candidates for Theory tests.

### Recommended minimum cases per handler

Each of the 8 handlers needs:
- `GenerateSuggestions_NullInput_ReturnsAllResults` (or empty)
- `GenerateSuggestions_PartialMatch_ReturnsFiltered`
- `GenerateSuggestions_NoMatch_ReturnsEmpty`
- `GenerateSuggestions_ExceedsMax25_ReturnsTruncated`

#### Handlers
- `SoundAutocompleteHandler`
- `VoxClipAutocompleteHandler`
- `VoiceAutocompleteHandler`
- `ReminderAutocompleteHandler`
- `ModTagAutocompleteHandler`
- `UserModTagAutocompleteHandler`
- `StylePresetAutocompleteHandler`
- `FilterAutocompleteHandler`

---

## 8. Preconditions (6 of 12 tested)

### Recommended minimum cases per attribute

Each untested precondition needs:
- `CheckRequirements_ConditionMet_ReturnsSuccess`
- `CheckRequirements_ConditionNotMet_ReturnsError`
- `CheckRequirements_NullContext_HandlesGracefully`

#### Untested
- `RequireBanMembersAttribute`
- `RequireKickMembersAttribute`
- `RequireModerationEnabledAttribute`
- `RequireModeratorAttribute`
- `RequireRatWatchEnabledAttribute`
- `RequireTtsEnabledAttribute`

---

## 9. Razor Page Models (11 of 73 tested)

**Why it matters:** Page models contain authorization checks, data loading, and form processing. Authorization bugs = access control failures.

### Recommended minimum cases (high-priority pages)

#### Admin pages
- `Admin/Settings` — OnGetAsync loads settings, OnPostAsync saves
- `Admin/Users/Index` — OnGetAsync returns paginated users
- `Admin/Users/Edit` — OnPostAsync validates, updates user
- `Admin/BulkPurge` — OnPostAsync validates channel, executes purge
- `Admin/Performance/Index` — OnGetAsync loads dashboard data

#### Guild feature pages
- `Guilds/Soundboard/Index` — OnGetAsync loads guild sounds
- `Guilds/TextToSpeech/Index` — OnGetAsync loads TTS config
- `Guilds/Members/Index` — OnGetAsync loads member list
- `Guilds/ScheduledMessages/Create` — OnPostAsync validates input
- `Guilds/FlaggedEvents/Index` — OnGetAsync loads events with filters

#### Each page needs at minimum
- `OnGetAsync_ValidGuild_ReturnsPage`
- `OnGetAsync_UnauthorizedUser_Returns403`
- `OnPostAsync_ValidInput_Redirects` (for form pages)
- `OnPostAsync_InvalidModelState_ReturnsPage` (for form pages)

---

## 10. Configuration/Options (2 of 37 tested)

**Low priority** unless options classes contain validation logic or computed properties.

### Recommended: test only options with custom validation
- `OpenRouterOptions` — ApiKey required validation
- `AzureSpeechOptions` — SubscriptionKey/Region validation
- `DiscordOAuthOptions` — ClientId/ClientSecret validation
- `AutoModerationOptions` — threshold range validation
- `RatWatchOptions` — interval/threshold defaults
- Any options class with `IValidateOptions<T>` implementation

---

## 11. Core Entities (3 of 62 tested)

**Low priority** for simple POCO/record types. Test only entities with behavior.

### Recommended: test only entities with methods/computed properties
- Any entity with a method beyond get/set
- Any entity with a computed property
- Any entity with custom equality/comparison logic

---

## Priority Matrix

| Priority | Area | Est. New Tests | Impact |
|----------|------|---------------|--------|
| P0 | Security services (ContentFilter, RaidDetection, SpamDetection) | ~15 | Prevents security regressions |
| P0 | LLM tool providers (DmModeration, BotManagement) | ~25 | Prevents unintended AI actions |
| P1 | Event handlers (AutoMod, VoiceState, Assistant) | ~25 | Core event processing |
| P1 | Moderation repositories + services | ~20 | Data integrity for mod system |
| P1 | Command modules (Moderation, Watchlist, Reminder) | ~25 | User-facing validation |
| P2 | Remaining API controllers | ~35 | Portal stability |
| P2 | Autocomplete handlers | ~32 | UX quality |
| P2 | Remaining command modules | ~30 | User-facing validation |
| P2 | Remaining repositories | ~40 | Data integrity |
| P3 | Preconditions | ~18 | Guard correctness |
| P3 | Razor page models | ~40 | Portal page behavior |
| P3 | Configuration options | ~10 | Settings validation |
| P3 | Core entities | ~5 | Only those with behavior |

**Total estimated new tests: ~320**

---

## Structural Recommendations

1. **Test project splitting** — Separate `DiscordBot.Tests.Unit` and `DiscordBot.Tests.Integration` for selective CI runs.
2. **Add `xunit.runner.json`** — Configure parallelization and collection behavior.
3. **Coverage gate in CI** — Use coverlet to enforce a minimum threshold (start at current baseline, ratchet up).
4. **Discord context abstraction** — Create a testable wrapper around `SocketInteractionContext` to unblock command module testing without fighting sealed classes.
5. **Tool provider test base class** — All LLM tool providers share the same `GetTools`/`ExecuteToolAsync` pattern; a shared base test fixture would reduce boilerplate.
