# Virtual Currency — Implementation Plan

**Status:** Proposed
**Date:** 2026-09-11
**Spec:** `docs/specs/virtual-currency-spec.md`

This plan turns the spec into ordered PRs, each small enough to review on its
own and green on its own. The spec is authoritative on behaviour; this
document is authoritative on file placement and sequencing.

## What exists today

| Concern | Current state | Source |
| --- | --- | --- |
| Soundboard play | `ISoundboardOrchestrationService.PlaySoundAsync(guildId, soundId, userId, queueEnabled, filter)` returns `SoundPlayResult`. Called by `SoundboardModule` and `PortalSoundboardPlaybackController`. Enabled checks happen first, then voice connection, then playback. | `Bot/Services/SoundboardOrchestrationService.cs:243` |
| Moderation cases | `ModerationCase` with `CaseType` { Warn, Kick, Ban, Mute, Note, Unban }, created through `IModerationService`. | `Core/Entities/ModerationCase.cs` |
| Audit log | `IAuditLogService.LogAsync(AuditLogCreateDto)`; `AuditLogAction` enum ends at `LlmModelDisabled = 25`; `AuditLogCategory` has `User`, `Configuration`. | `Core/Enums/` |
| Roles | `Roles.SuperAdmin`, `Roles.Admin`, `Roles.Moderator` constants; guild access through `GuildAccessRequirement`. Discord-side: `RequireAdminAttribute`, `RequireModeratorAttribute`, `RequireOwnerAttribute` preconditions. | `Core/Authorization/Roles.cs`, `Bot/Preconditions/` |
| Feature gates | `RequireRatWatchEnabledAttribute` pattern, plus `CommandModuleConfiguration` for per-guild module toggles. | `Bot/Preconditions/` |
| Cache | `IInstrumentedCache` over `IMemoryCache` with metrics by key prefix. | `Core/Interfaces/IInstrumentedCache.cs` |
| Component state | `ComponentIdBuilder` + `IInteractionStateService`, handlers in `*ComponentModule`. | `Bot/Commands/` |
| Background services | `MonitoredBackgroundService` base with health reporting. | `Bot/Services/MonitoredBackgroundService.cs` |
| DI | One `Add<Feature>()` extension per stream in `Bot/Extensions/`, called from `Program.cs`. | `Bot/Extensions/RatWatchServiceExtensions.cs` as the model |
| Entity config | One `IEntityTypeConfiguration` per entity in `Infrastructure/Data/Configurations/`, migrations under `Migrations/Sqlite` and `Migrations/Postgresql`. | |
| Options | Plain class with `SectionName` const in `Core/Configuration/`, bound in the feature's extension method. | |

## Ordering

Five PRs. Each leaves `main` shippable with the feature partially present but
harmless: nothing is priced until PR 4, and nothing is visible until PR 3.

| PR | Delivers | Depends on |
| --- | --- | --- |
| 1 | Core model, repositories, migrations, ledger and wallet services, unit tests | — |
| 2 | Charge service, mint service, fines, audit actions, options | 1 |
| 3 | Discord commands and component module | 2 |
| 4 | Soundboard integration | 2 |
| 5 | Portal pages and REST controllers, docs updates | 2 (3 and 4 can land in either order) |

PR 3 and PR 4 are independent and can be developed in parallel once PR 2 is
merged.

## PR 1 — Model and ledger

**Core**

- `Enums/CurrencyScope.cs`, `LedgerTransactionType.cs`, `LedgerSource.cs`,
  `MintPrincipalType.cs`, `IncomeInterval.cs`.
- `Entities/Currency/Currency.cs`, `Wallet.cs`, `LedgerTransaction.cs`,
  `MintAuthority.cs`, `PriceEntry.cs`. Columns per the spec.
- `DTOs/Currency/`: `CurrencyDto`, `WalletDto`, `LedgerTransactionDto`,
  `PriceEntryDto`, and the result records `MintResult`, `TransferResult`,
  `FineResult`, `AdjustmentResult`, each `{ bool Success, string? Error,
  LedgerTransactionDto? Transaction, long? ClampedAmount }` as applicable.
- `Interfaces/Currency/ICurrencyRepository.cs`, `IWalletRepository.cs`,
  `ILedgerRepository.cs`, `IPriceRepository.cs`, `IMintAuthorityRepository.cs`.
  `ILedgerRepository.AppendAsync(LedgerTransaction row)` is the single write
  path: it loads the wallet with a row lock where the provider supports one,
  checks the idempotency key, writes the row, updates `CachedBalance` and
  `BalanceAfter`, and commits. Returns the existing row on a duplicate key
  instead of throwing.
- `Interfaces/Currency/ICurrencyService.cs`, `IWalletService.cs` (signatures in
  the spec).

**Infrastructure**

- `Data/Configurations/Currency*Configuration.cs` for the five entities.
  Unique indexes: `Wallets(CurrencyId, UserId)`,
  `LedgerTransactions(IdempotencyKey)`, `PriceEntries(FeatureKey, GuildId)`,
  `Currencies(Scope, GuildId, Name)`. `ExemptRoleIds` stored as JSON string
  with a value converter, matching how `Guild.Settings` is stored.
- `DbSet`s on `BotDbContext`.
- Migrations `AddVirtualCurrency` for both `SqliteBotDbContext` and
  `PostgresBotDbContext`. Note in the PR that the Postgres migration is not
  test-covered.
- `Data/Repositories/Currency/*Repository.cs`.
- `Services/Currency/CurrencyService.cs`, `WalletService.cs`. Balance rules
  from the spec live in `WalletService`; the repository only enforces the
  idempotency invariant.

**Tests** (`tests/DiscordBot.Tests/Services/Currency/`)

- Ledger: append updates cache and `BalanceAfter`; duplicate key returns the
  first row and writes nothing; concurrent appends on one wallet through
  `ConcurrencyTestHelper` leave cache equal to the sum of rows.
- Wallet rules: spend below balance refused; spend at exactly balance allowed;
  spend while negative refused with `InDebt`; transfer writes two linked rows;
  fine clamps to zero without `AllowNegative`; fine clamps to floor with it;
  mint from debt lands on the right number; every operation refused on an
  inactive currency.
- Adjustment requires a reference and a reason.

**Docs**: `data-model.md` gains section "19. Virtual Currency".

## PR 2 — Charge, mint, fines, options

**Core**

- `Configuration/CurrencyOptions.cs`.
- `Interfaces/Currency/IChargeService.cs`, `IMintService.cs`,
  `IChargeHoldStore.cs`.
- `DTOs/Currency/ChargeHoldResult.cs` `{ ChargeHoldStatus Status, Guid? HoldId,
  long Price, long Balance, string? CurrencySymbol }`, `ChargeCommitResult`,
  `RefundResult`.
- `AuditLogAction` additions 26–35 in the order listed in the spec.

**Bot** (`Services/Currency/`)

- `ChargeHoldStore`: `IInstrumentedCache` with prefix `currency:hold:` and a
  per-wallet index so open holds on a wallet can be summed. Expiry from
  options.
- `ChargeService`: the seven steps in the spec. Available balance = cached
  minus open holds. Commit writes through `ILedgerRepository.AppendAsync`
  with the hold's key, then drops the hold.
- `MintService`: authority check (user id, any of the user's role ids, or
  `System` when `actorId` is null), then append with `Type = Mint`.
- `WalletService.FineAsync` and `AdjustAsync` get their audit calls here
  rather than in PR 1 so PR 1 stays free of `IAuditLogService`.
- `Extensions/CurrencyServiceExtensions.cs` with `AddCurrency(configuration)`:
  binds options, registers repositories (scoped), services (scoped), hold
  store (singleton). Called from `Program.cs` only when `Currency:Enabled`.

**Tests**

- Hold: no price → `Free`; exempt role → `Free`; two holds for one balance,
  second refused; expired hold still commits; release writes nothing; commit
  twice writes once.
- Mint: unauthorized user refused; role authority accepted; system principal
  accepted with null actor.
- Refund references the spend and cannot refund twice.

**Docs**: `configuration-guide.md` gains the `Currency` section;
`service-catalog.md` lists the services.

## PR 3 — Discord commands

**Bot**

- `Preconditions/RequireCurrencyEnabledAttribute.cs`.
- `Commands/WalletModule.cs`: `/wallet balance`, `history`, `pay`, `mint`,
  `fine`; `Commands/CurrencyModule.cs`: `/currency create`, `list`. Currency
  option is an autocomplete over currencies visible in the guild, via the
  existing `AutocompleteController` pattern for the bot side.
- `Commands/WalletComponentModule.cs`: pay confirmation (`wallet:pay:confirm`
  / `cancel`), history pagination (`wallet:history:page`). State in
  `IInteractionStateService`.
- Register the module in `CommandModuleConfiguration` seeding so guilds can
  disable it.
- Embeds: balance card, history table, fine receipt showing any clamp.
- Rate limit `pay` with `RateLimitAttribute` using
  `Currency:MaxTransferPerMinute`.

**Tests**: module tests in the style of the existing Rat Watch module tests,
covering the refusal messages and the confirmation flow.

**Docs**: `feature-map.md` gets a Virtual Currency section;
`docs/articles/virtual-currency.md` user guide; `docs/toc.yml` and
`docs/index.md` entries.

## PR 4 — Soundboard integration

**Core**

- `SoundPlayResult` gains `ChargeHoldStatus? ChargeStatus`, `long? Price`,
  `long? Balance`, `string? CurrencySymbol`.

**Bot**

- `SoundboardOrchestrationService.PlaySoundAsync`: hold after the guild audio
  enabled check and the voice connection check, commit on accepted playback,
  release on every failure return. Wrap the existing body so the release is
  in a `catch` and the failure branches, not sprinkled through them.
- `SoundboardModule` and `PortalSoundboardPlaybackController` render the
  refusal from the new fields. The portal soundboard page shows the price
  badge on priced sounds (data from PR 5's price lookup; until then the badge
  code path is present but no prices exist).
- `IChargeService` is injected as optional (`IChargeService?`) so the
  soundboard still constructs when `Currency:Enabled` is false.

**Tests**: free sound unchanged; priced sound with funds commits once; priced
sound refused before voice work starts; playback failure releases the hold.

**Docs**: `docs/articles/soundboard*.md` note on pricing; audio-voice agent
definition mentions the charge hook.

## PR 5 — Portal and API

**Bot**

- Pages under `Pages/Guilds/Currency/`: `Index`, `Details`, `Prices`, all
  inheriting `GuildPageModelBase`; `Pages/Admin/Currency/Index` for globals.
- Controllers `CurrenciesController`, `WalletsController`,
  `PricesController` on `ApiControllerBase`.

| Method | Route | Who |
| --- | --- | --- |
| GET | `/api/guilds/{guildId}/currencies` | guild member |
| POST | `/api/guilds/{guildId}/currencies` | guild admin |
| PUT | `/api/currencies/{id}` | owner of scope |
| POST | `/api/currencies/{id}/deactivate` | owner of scope |
| GET/POST/DELETE | `/api/currencies/{id}/mint-authorities` | owner of scope |
| GET | `/api/currencies/{id}/wallets?sort=balance&debtorsOnly=` | admin or moderator |
| GET | `/api/wallets/{id}/ledger?page=` | admin or moderator, or the wallet's owner |
| POST | `/api/currencies/{id}/mint` | mint authority |
| POST | `/api/currencies/{id}/fine` | moderator |
| POST | `/api/ledger/{txId}/adjust` | admin |
| GET | `/api/currencies/{id}/reconcile` | admin. Returns wallets whose cache differs from the row sum. |
| GET/PUT/DELETE | `/api/guilds/{guildId}/prices/{featureKey}` | guild admin |
| GET | `/api/admin/currencies` | SuperAdmin |
| POST | `/api/admin/currencies` | SuperAdmin |

- Page scripts in `wwwroot/js/currency/`. Discord ids emitted as strings.
- Prices page lists the guild's sounds from `ISoundService` with a price input
  and exempt-role multi-select from the guild's roles.

**Tests**: controller tests for authorization on each route; page model tests
for the price save validation (guild currency may only price its own guild).

**Docs**: `ui-inventory.md`, `api-endpoints.md`, `web-ui-portal` and
`data-infrastructure` agent definitions.

## Cross-cutting notes

- **Provider coverage.** The test suite runs SQLite only. Each migration PR
  says so and asks for a manual Postgres `database update` before release.
- **Row locking.** SQLite serializes writes anyway. For Postgres,
  `ILedgerRepository.AppendAsync` uses `SELECT ... FOR UPDATE` on the wallet
  row through a raw query so concurrent appends cannot both read the same
  cached balance. Keep this inside the repository; nothing above it knows.
- **Retention.** Ledger rows are not subject to any retention job. They are
  the record. If size becomes a concern, that is a future decision.
- **Feature flag.** With `Currency:Enabled = false` the extension method is
  never called, the optional `IChargeService?` in the soundboard is null, and
  every play is free. This is the rollback path if something misbehaves in
  production.
- **Lessons learned.** After PR 4 or 5, add
  `docs/lessons-learned/virtual-currency.md` if anything fought back,
  especially around holds and concurrency.
