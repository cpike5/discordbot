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
- **Services:** `UserManagementService` (995 lines), `ConsentService` (567 lines), `VerificationService`, `VerificationCleanupService`, `UserPurgeService`, `UserDataExportService` (762 lines), `DiscordUserInfoService`, `UserDiscordGuildService`
- **Commands:** `PrivacyModule`, `VerifyAccountModule`, `ConsentModule`
- **Repos:** `UserRepository`, `UserConsentRepository`

### User Activity
- **Entities:** `UserActivityLog`, `UserActivityEvent`
- **Enums:** `ConsentType`, `ActivityEventType`
- **Handlers:** `ActivityEventTrackingHandler`, `MemberEventHandler`

### Pages
- **Account:** Login, ExternalLogin, Profile, Privacy, LinkDiscord, Logout, Lockout, AccessDenied
- **Admin:** `Admin/Users/` (Index, Create, Edit, Details), `Admin/UserPurge.cshtml`
- **Guild:** `Guilds/Members/` (Index, Moderation)

### Key Flows
- **Discord OAuth:** External login → callback → account linking → token storage
- **Verification:** Discord ↔ web account linking via `VerificationCode`
- **Data export:** `UserDataExportService` generates GDPR-compliant data packages
- **User purge:** `UserPurgeService` removes all user data across ALL tables — cascading delete

## Gotchas

- **Very large services:** UserManagementService (995), UserDataExportService (762), ConsentService (567) — search for specific methods
- **OAuth secrets in User Secrets:** `Discord:OAuth:ClientId`, `Discord:OAuth:ClientSecret` — never commit
- **OAuth redirect URI** must match environment exactly (`https://localhost:5001/signin-discord` for dev)
- **User purge is destructive and cascading** — removes data from ALL tables; ensure confirmation workflow
- **Consent is per-type** — different `ConsentType` values for different data collection categories
- **Role hierarchy enforced in authorization policies** — higher roles inherit lower role permissions
- **SameSite cookie policy** affects Discord OAuth — see commit history for redirect loop fix
