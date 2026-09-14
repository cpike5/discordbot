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
- **Account (Blazor, Phase 4 cluster 4c):** `Blazor/Pages/Account/Login.razor` (+ `.razor.cs`, static SSR, `EmptyLayout`) is now sign-in — `Pages/Account/{Login,ExternalLogin,Logout}.cshtml(.cs)` are deleted. The email/password flow lives in `Services/Account/IPasswordSignInService`, the Discord OAuth callback flow in `Services/Account/IExternalLoginHandler`, and both are fronted by three `[AllowAnonymous]` minimal-API endpoints in `Extensions/AccountEndpointExtensions.cs` (`POST /Account/Logout`, `POST /Account/PerformExternalLogin` — the Discord challenge, shared by `Login.razor`'s Discord button and `LinkDiscord.cshtml.cs`'s "Link Discord" action — and `GET /Account/ExternalLogin/Callback`). Route strings are `Extensions/AccountRoutes` constants. `Profile`, `Privacy`, `LinkDiscord`, `Lockout`, `AccessDenied` are unchanged from cluster 4a/this cluster's neighbors — `Blazor/Pages/Account/{Profile,Lockout,AccessDenied}.razor` (Blazor) and `Pages/Account/{Privacy,LinkDiscord}.cshtml` (still Razor Pages).
- **Account:** `Blazor/Pages/Account/{Profile,AccessDenied,Lockout,LinkDiscord,Privacy}.razor` — ported off the matching `.cshtml` in the Blazor port (`docs/plans/blazor-port-plan.md` §5 Phase 4 clusters 4a/4c; LinkDiscord/Privacy in 4c). `Login`, `ExternalLogin`, `Logout` are being ported concurrently (also cluster 4c) alongside minimal-API `PerformExternalLogin`/`Logout` endpoints.
- **Admin:** `Blazor/Pages/Admin/Users/` (Index, Create, Edit, Details) — ported off `Pages/Admin/Users/*.cshtml` in the Blazor port (`docs/plans/blazor-port-plan.md` §5 Phase 4 cluster 4a); `Admin/UserPurge.cshtml` is still a Razor Page
- **Guild:** `Guilds/Members/` (Index, Moderation)

### Key Flows
- **Discord OAuth:** `Login.razor`'s Discord button (or `LinkDiscord.cshtml.cs`'s "Link Discord") → `POST /Account/PerformExternalLogin` challenge → `/signin-discord` middleware callback (unchanged) → `GET /Account/ExternalLogin/Callback` → `IExternalLoginHandler` (sign-in existing login, or link/create by Discord id / email / brand-new) → token storage
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
