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
- **Services:** `UserManagementService` (995 lines), `ConsentService` (567 lines), `VerificationService`, `VerificationCleanupService`, `UserPurgeService`, `UserDataExportService` (762 lines), `DiscordUserInfoService`, `UserDiscordGuildService`, `DiscordLinkService` (`Services/Account/IDiscordLinkService` — wraps unlink/refresh/verification for `Blazor/Pages/Account/LinkDiscord.razor.cs`, `docs/plans/blazor-port-plan.md` §5 Phase 4 cluster 4c)
- **Commands:** `PrivacyModule`, `VerifyAccountModule`, `ConsentModule`
- **Repos:** `UserRepository`, `UserConsentRepository`

### User Activity
- **Entities:** `UserActivityLog`, `UserActivityEvent`
- **Enums:** `ConsentType`, `ActivityEventType`
- **Handlers:** `ActivityEventTrackingHandler`, `MemberEventHandler`

### Pages
- **Account (Blazor, all of it — Phase 4 clusters 4a/4c):** `Blazor/Pages/Account/{Profile,AccessDenied,Lockout,Login,LinkDiscord,Privacy}.razor` (Profile/AccessDenied/Lockout from cluster 4a; Login/LinkDiscord/Privacy from cluster 4c). Every legacy `Pages/Account/*.cshtml(.cs)` is deleted, including `{Login,ExternalLogin,Logout}.cshtml.cs` — the email/password flow lives in `Services/Account/IPasswordSignInService`, the Discord OAuth callback flow in `Services/Account/IExternalLoginHandler`, the LinkDiscord mutations (unlink, refresh, verification) in `Services/Account/IDiscordLinkService`, and sign-in/sign-out/the Discord challenge are three `[AllowAnonymous]` minimal-API endpoints in `Extensions/AccountEndpointExtensions.cs` (`POST /Account/Logout`, `POST /Account/PerformExternalLogin`, `GET /Account/ExternalLogin/Callback`). Route strings are `Extensions/AccountRoutes` constants. LinkDiscord/Privacy each dispatch their same-page mutations through one named `<EditForm>` per bound-model group rather than one per action (Privacy splits "Delete My Data" into its own second form, separate from consent/export, so Enter in the confirmation box can't implicitly submit a consent toggle) — see "Static-SSR account pages" in `docs/architecture/patterns.md`.
- **Admin:** `Blazor/Pages/Admin/Users/` (Index, Create, Edit, Details) — ported off `Pages/Admin/Users/*.cshtml` in the Blazor port (`docs/plans/blazor-port-plan.md` §5 Phase 4 cluster 4a); `Admin/UserPurge.cshtml` is still a Razor Page
- **Guild:** `Guilds/Members/` (Index, Moderation)

### Key Flows
- **Discord OAuth:** `Login.razor`'s Discord button (or `LinkDiscord.razor`'s "Link Discord Account" form) → `POST /Account/PerformExternalLogin` challenge → `/signin-discord` middleware callback (unchanged) → `GET /Account/ExternalLogin/Callback` → `IExternalLoginHandler` (sign-in existing login, or link/create by Discord id / email / brand-new) → token storage
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
