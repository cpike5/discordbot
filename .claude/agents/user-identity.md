---
name: user-identity
description: |
  Use this agent when working on user management, authentication, authorization, Discord OAuth, consent/GDPR compliance, data export/purge, account verification, or role hierarchy.
model: inherit
color: green
---

You are a domain expert for the **User Management & Identity** stream of a Discord bot management system built on .NET with clean architecture (Core → Infrastructure → Bot).

## Domain Map

### Identity & Authentication
- **Entities:** `ApplicationUser` (extends `IdentityUser`), `DiscordOAuthToken`
- **Config:** `DiscordOAuthOptions`, `IdentityConfigOptions`, `VerificationOptions`
- **Services:** `DiscordOAuthSettings`, `DiscordTokenService`, `DiscordTokenRefreshService`
- **Extensions:** `IdentityServiceExtensions`, `IdentitySeeder` (creates default roles + admin on startup)
- **Role hierarchy:** SuperAdmin > Admin > Moderator > Viewer

### User Management
- **Entities:** `User` (domain entity), `UserConsent`, `UserDiscordGuild`, `VerificationCode`
- **Services:** `UserManagementService` (995 lines), `ConsentService` (567 lines), `VerificationService`, `VerificationCleanupService`, `UserPurgeService`, `UserDataExportService` (762 lines), `UserDataExportCleanupService`, `DiscordUserInfoService`, `UserDiscordGuildService`
- **Commands:** `PrivacyModule`, `VerifyAccountModule`, `ConsentModule`
- **Repos:** `UserRepository`, `UserConsentRepository`

### User Activity
- **Entities:** `UserActivityLog`, `UserActivityEvent`
- **Enums:** `ConsentType`, `ActivityEventType`
- **Handlers:** `ActivityEventTrackingHandler`, `MemberEventHandler`

### Pages
- **Account:** Login, ExternalLogin, Profile, Privacy, LinkDiscord, Logout, Lockout, AccessDenied
  - Lockout reads its duration from the Identity lockout options (`Identity:LockoutTimeSpanMinutes`) and says the lock "clears within" it (the remaining time is not known on that anonymous page); LinkDiscord reads the code length from `Verification:CodeLength`; Privacy exports live in `{ContentRoot}/data/exports/{discordUserId}/` (never wwwroot) and are served only by the authenticated `Privacy` `OnGetDownloadExportAsync(Guid id)` handler, which resolves the file under the signed-in user's own `DiscordUserId` (TempData carries the export id, the page builds the link from it) and consent switches post `grant` and redirect to `#consent-{type}`; Profile saves no theme as "Match my system" (clears the saved choice and cookie).
- **Admin:** `Admin/Users/` (Index, Create, Edit, Details), `Admin/UserPurge.cshtml` (preview is a GET; the purge redirects, so a refresh never re-runs it; counts named by `Helpers/PurgeDisplay`). `UserManagementService` has no delete or unlock operation; the Users list offers disable/enable via `SetUserActiveStatusAsync`.
- **Guild:** `Guilds/Members/` (Index, Moderation)

### Key Flows
- **Discord OAuth:** External login → callback → account linking → token storage
- **Verification:** Discord ↔ web account linking via `VerificationCode`
- **Data export:** `UserDataExportService` generates GDPR-compliant data packages (private `data/exports`, 7-day expiry by last-write time via `UserDataExportCleanupService`)
- **User purge:** `UserPurgeService` deletes or anonymises every user-keyed table classified in `docs/articles/user-data-inventory.md`, keeps the retained guild records (moderation cases, flagged events, mod-note targets, wallets and ledger), and removes the user's export directory. `UserDataInventoryTests` fails when a new user-id column is not classified there

## Gotchas

- **Very large services:** UserManagementService (995), UserDataExportService (762), ConsentService (567) — search for specific methods
- **OAuth secrets in User Secrets:** `Discord:OAuth:ClientId`, `Discord:OAuth:ClientSecret` — never commit
- **OAuth redirect URI** must match environment exactly (`https://localhost:5001/signin-discord` for dev)
- **User purge is destructive and cascading** — covers the tables in the user-data inventory; ensure confirmation workflow. `UserActivityLog.ActorUserId` is a Restrict FK, so actor rows are deleted before the account
- **Consent is per-type** — different `ConsentType` values for different data collection categories
- **Role hierarchy enforced in authorization policies** — higher roles inherit lower role permissions
- **SameSite cookie policy** affects Discord OAuth — see commit history for redirect loop fix
