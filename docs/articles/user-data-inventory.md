# User Data Inventory

This page lists every database column that holds a Discord user ID or a web account ID. For each
column it says what the user purge does with it and whether the data export includes it.

- **Purge:** `UserPurgeService` runs from `/privacy delete-data`, the Privacy page and **Admin > User Purge**.
- **Export:** `UserDataExportService` runs from `/privacy export-data` and the Privacy page.

`UserDataInventoryTests` (in `tests/DiscordBot.Tests/Services/`) reads the EF Core model and fails
when a user-id column is missing from its list. It also fails when this table and its list differ. A
new entity with a user-id column must therefore get a purge decision and an export decision, a row
here, and an entry in the test.

**Last updated:** 2026-10-08 (review finding H10, decision D5)

## Purge actions

| Action | Meaning |
| --- | --- |
| **Purged** | The purge deletes the row and reports a count. |
| **Anonymised** | The row stays. The purge overwrites the user ID with `0` or `null` and reports an `_Anonymized` count. |
| **Retained** | The row stays as it is. The reason is in the Notes column. |
| **Cascade** | The database deletes the row when the purge deletes its parent. No separate count is reported. |

**Retained — awaiting owner decision** marks moderation and currency records that are kept for now.
They are guild records, and the ledger is append-only. A retention rule for them is still open.

## Which columns count as user IDs

The test looks at properties of type `ulong`, `ulong?` or `string`. It selects a property when its name:

- ends in `UserId` (for example `UserId`, `DiscordUserId`, `ApplicationUserId`, `TargetUserId`, `SubmittedByUserId`),
- is `AuthorId`, `ActorId`, `VoterId`, `PrincipalId` or `TargetId`, or
- ends in `By` or `ById` (for example `CreatedBy`, `LastModifiedBy`, `UploadedById`).

The test also adds the primary keys of `User` and `ApplicationUser`. The rule is broad on purpose. A
false match costs one row here; a missed column is data the purge leaves behind.

## Inventory

| Entity | Column | Purge | Exported | Notes |
| --- | --- | --- | --- | --- |
| `User` | `Id` | Purged | Yes | The Discord user record. Its deletion cascades to the tables marked "also cascades from User" below. |
| `ApplicationUser` | `Id` | Purged | Yes | The linked web account, deleted through `UserManager`. |
| `ApplicationUser` | `DiscordUserId` | Purged | Yes | How the purge finds the linked web account. |
| `UserConsent` | `DiscordUserId` | Purged | Yes | Also cascades from User. |
| `GuildMember` | `UserId` | Purged | Yes | Also cascades from User. |
| `UserGuildAccess` | `ApplicationUserId` | Purged | Yes | Portal access grants of the account. |
| `UserDiscordGuild` | `ApplicationUserId` | Purged | Yes | Guilds captured at Discord sign-in. |
| `DiscordOAuthToken` | `ApplicationUserId` | Purged | No | Encrypted tokens. These are secrets, so the export leaves them out. |
| `DiscordOAuthToken` | `DiscordUserId` | Purged | No | Same rows as above. |
| `VerificationCode` | `DiscordUserId` | Purged | No | Short-lived link codes. `VerificationCleanupService` also deletes them when they expire. |
| `VerificationCode` | `ApplicationUserId` | Cascade | No | Cascades from ApplicationUser. |
| `IdentityUserClaim` | `UserId` | Cascade | No | ASP.NET Identity table. Cascades from ApplicationUser. |
| `IdentityUserLogin` | `UserId` | Cascade | No | ASP.NET Identity table. Cascades from ApplicationUser. |
| `IdentityUserRole` | `UserId` | Cascade | No | ASP.NET Identity table. Cascades from ApplicationUser. |
| `IdentityUserToken` | `UserId` | Cascade | No | ASP.NET Identity table. Cascades from ApplicationUser. |
| `UserNotification` | `UserId` | Purged | Yes | Portal notifications. They also cascade from ApplicationUser; the purge deletes them first so it can report a count. |
| `UserActivityLog` | `ActorUserId` | Purged | Yes | Portal actions the account took. The foreign key is Restrict, so these rows must go before the account. Before H10, purging a former admin with such rows failed. |
| `UserActivityLog` | `TargetUserId` | Anonymised | Yes | Actions another admin took on the account. The admin's record stays. The target and `Details` (which holds the target's email and names) are set to null. |
| `MessageLog` | `AuthorId` | Purged | Yes | Also cascades from User. |
| `CommandLog` | `UserId` | Purged | Yes | Also cascades from User. |
| `UserActivityEvent` | `UserId` | Purged | Yes | Raw message, reaction and voice events. |
| `MemberActivitySnapshot` | `UserId` | Purged | Yes | Hourly and daily totals per member. Also cascades from User. |
| `SoundPlayLog` | `UserId` | Purged | Yes | |
| `AudioPlaybackLog` | `UserId` | Purged | Yes | |
| `UserSoundFavorite` | `UserId` | Purged | Yes | |
| `TtsMessage` | `UserId` | Purged | Yes | |
| `TtsMessageHistory` | `UserId` | Purged | Yes | Text-to-speech messages played from the portal. |
| `VoxMessageHistory` | `UserId` | Purged | Yes | |
| `UserTtsPreset` | `UserId` | Purged | Yes | |
| `UserPreference` | `UserId` | Purged | Yes | |
| `Sound` | `UploadedById` | Retained | No | Guild content. The sound belongs to the guild. The column is nullable, so it could be anonymised later. |
| `LlmUsageRecord` | `UserId` | Purged | Yes | |
| `AssistantInteractionLog` | `UserId` | Purged | Yes | Also cascades from User. |
| `DmAssistantInteractionLog` | `UserId` | Purged | Yes | Also cascades from User. |
| `DmAssistantUsageMetrics` | `UserId` | Purged | Yes | Also cascades from User. |
| `DmConversationMessage` | `UserId` | Purged | Yes | DM conversation with the assistant. Also cascades from User. |
| `DmAssistantNote` | `UserId` | Purged | Yes | Notes the DM assistant keeps about the user. Also cascades from User. |
| `Reminder` | `UserId` | Purged | Yes | |
| `RatVote` | `VoterUserId` | Purged | Yes | |
| `RatRecord` | `UserId` | Anonymised | Yes | Set to 0. The guild's Rat Watch tallies stay. |
| `RatWatch` | `AccusedUserId` | Anonymised | Yes | Set to 0. Other users' votes hang off the watch. |
| `RatWatch` | `InitiatorUserId` | Anonymised | Yes | Set to 0. |
| `FeatureRequest` | `SubmittedByUserId` | Anonymised | Yes | Set to 0. The request belongs to the guild's backlog, and the admin's review and generated docs are on the same row. Title and description are the user's own text and stay. |
| `FeatureRequest` | `ReviewedByUserId` | Retained | No | The reviewing admin's attribution on another user's request. |
| `FeatureRequestRejection` | `UserId` | Purged | Yes | Requests the screening step declined. |
| `ModNote` | `AuthorUserId` | Purged | Yes | Notes the user wrote as a moderator. |
| `ModNote` | `TargetUserId` | Retained — awaiting owner decision | No | Guild moderation record about the user. |
| `UserModTag` | `UserId` | Purged | Yes | |
| `UserModTag` | `AppliedByUserId` | Retained | No | Moderator attribution on another user's tag. |
| `Watchlist` | `UserId` | Purged | Yes | |
| `Watchlist` | `AddedByUserId` | Retained | No | Moderator attribution on another user's entry. |
| `ModerationCase` | `TargetUserId` | Retained — awaiting owner decision | No | Guild moderation record (warns, mutes, kicks, bans). |
| `ModerationCase` | `ModeratorUserId` | Retained — awaiting owner decision | No | Who acted. Part of the same record. |
| `FlaggedEvent` | `UserId` | Retained — awaiting owner decision | No | Auto-moderation detection. Moderation cases can point at it. |
| `FlaggedEvent` | `ReviewedByUserId` | Retained — awaiting owner decision | No | Reviewer attribution on the same record. |
| `Wallet` | `UserId` | Retained — awaiting owner decision | Yes | The ledger hangs off the wallet and is append-only. The export includes the user's wallets with all their transactions. |
| `LedgerTransaction` | `ActorId` | Retained — awaiting owner decision | No | Who caused a transaction (for example a moderator's fine) on any wallet. The user's own wallet transactions are exported through `Wallet`. |
| `Currency` | `CreatedById` | Retained | No | Admin attribution on guild configuration. |
| `MintAuthority` | `GrantedById` | Retained | No | Admin attribution on guild configuration. |
| `MintAuthority` | `PrincipalId` | Retained | No | A user or role allowed to mint. Guild configuration. |
| `PriceEntry` | `UpdatedById` | Retained | No | Admin attribution on guild configuration. |
| `UserGuildAccess` | `GrantedByUserId` | Retained | No | Admin attribution on another account's grant. |
| `AuditLog` | `ActorId` | Retained | No | Security audit trail. The purge writes its own entry with `[PURGED]` in place of the user ID. |
| `AuditLog` | `TargetId` | Retained | No | Security audit trail. |
| `ApplicationSetting` | `LastModifiedBy` | Retained | No | Admin attribution on a global setting. |
| `CommandModuleConfiguration` | `LastModifiedBy` | Retained | No | Admin attribution on a global setting. |
| `ScheduledMessage` | `CreatedBy` | Retained | No | Admin attribution on guild configuration. |
| `PerformanceAlertConfig` | `UpdatedBy` | Retained | No | Admin attribution on a global setting. |
| `PerformanceIncident` | `AcknowledgedBy` | Retained | No | Admin attribution on an operational record. |
| `LlmModel` | `EnabledBy` | Retained | No | Admin attribution on a global setting. |

## Files outside the database

The purge also deletes the user's export archives in `{ContentRoot}/data/exports/{discordUserId}/`.
Export archives are deleted 7 days after they are written.

## Open decisions

- **Moderation and currency records.** Retention period and lawful basis for `ModerationCase`,
  `FlaggedEvent`, `ModNote` targets, `Wallet` and `LedgerTransaction`.
- **Right of access to moderation records.** The export leaves out moderation cases, flagged events
  and notes about the user. Including them, or recording an exemption, is a decision for the owner.
- **Free text in retained rows.** An anonymised feature request keeps its title and description.
  Moderation records keep their reasons and message context.
