# Virtual Currency — Feature Specification

**Status:** Proposed
**Date:** 2026-09-11
**Implementation plan:** `docs/plans/virtual-currency-implementation-plan.md`

## Summary

A ledger-backed virtual currency system. Authorized people create currencies
scoped to one guild or to the whole bot, mint units into user wallets, and
price bot features so that using them spends currency. Users hold one wallet per
currency, can pay each other, and can be fined by moderators into debt.

The first priced feature is soundboard playback, so a guild can charge for its
long or obnoxious sounds. The system is built so that later features (image and
video generation over OpenRouter, premium TTS voices, a weekly income job) plug
into the same two seams: **charge** and **mint**. Those later features are
described here only as consumers; their own design is out of scope.

## Goals

1. **Currencies.** Guild admins create currencies for their guild. The bot
   owner creates global currencies. Each currency carries its own rules.
2. **Wallets and a ledger.** Every balance change is an append-only ledger row
   with a type, a reason, an actor, and an idempotency key. Wallet balance is a
   cached sum that can always be rebuilt from the rows.
3. **Minting.** Only principals on a currency's authority list can create
   units. The list may include a system principal for background jobs.
4. **Transfers.** Users can send currency to each other when the currency
   allows it.
5. **Pricing.** Any feature can be priced by a feature key. Unpriced features
   stay free, so nothing changes for existing guilds until an admin sets a
   price.
6. **Charging.** A single hold / commit / release service that priced features
   call. Nothing is written to the ledger until commit.
7. **Fines and debt.** Moderators can fine users. A fine is the only operation
   that can push a balance below zero, and only on a currency that allows it.
   Users in debt cannot use priced features until they are back above zero.
8. **Bot credit.** The bot owner's global currency that will back real spend.
   It is an ordinary currency with transfers and debt turned off, not a
   special entity.

## Non-goals (this project)

- Image, video, or any OpenRouter media generation.
- Automatic earning (weekly income, activity rewards). Groundwork only; see
  [Future consumers](#future-consumers).
- Shops, items, inventories. "Buy a thing" is a spend with a reason string.
- Exchange between currencies.
- Any real-money purchase path.

## Terms

| Term | Meaning |
| --- | --- |
| Currency | A named unit of account with scope and rules. |
| Wallet | One user's balance in one currency. |
| Ledger row | One immutable balance change on one wallet. |
| Mint | Creating new units. The only source of currency. |
| Hold | A reservation against a wallet that has not yet been written to the ledger. |
| Fine | A moderator-initiated debit that may cross zero. |
| Feature key | A string naming a priceable action, e.g. `soundboard:{soundId}`. |
| Bot credit | The SuperAdmin-owned global currency intended to gate paid features. |

## Data model

All amounts are `long` in whole units. No decimals.

### Currency

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | Guid PK | |
| `Scope` | enum `CurrencyScope` { Global = 0, Guild = 1 } | |
| `GuildId` | ulong? | Required when `Scope = Guild`, null otherwise. |
| `Name` | string(64) | Unique within its scope (per guild, or among globals). |
| `Symbol` | string(16) | Emoji or short text shown next to amounts. |
| `IsTransferable` | bool | User-to-user transfers allowed. Default true for guild, false for global. |
| `AllowNegative` | bool | Fines may push a balance below zero. Default false. |
| `DebtFloor` | long? | Most negative balance a fine may reach. Required when `AllowNegative`. Stored negative, e.g. `-100`. |
| `IncomeAmount` | long? | **Groundwork only.** Not read by anything in this project. |
| `IncomeInterval` | enum `IncomeInterval`? { Daily, Weekly, Monthly } | **Groundwork only.** |
| `IsActive` | bool | Deactivated currencies freeze: no mint, spend, transfer, or fine. History stays readable. |
| `CreatedById` | ulong | Discord user id of the creator. |
| `CreatedAt` | DateTime | |

Currencies are never deleted.

### Wallet

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | Guid PK | |
| `CurrencyId` | Guid FK | |
| `UserId` | ulong | Discord user id. |
| `CachedBalance` | long | Sum of the wallet's ledger rows. Updated in the same transaction as each row. |
| `CreatedAt` | DateTime | |

Unique index on (`CurrencyId`, `UserId`). A wallet is created on first credit.

### LedgerTransaction

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | long PK | |
| `WalletId` | Guid FK | |
| `Type` | enum `LedgerTransactionType` | `Mint`, `TransferIn`, `TransferOut`, `Spend`, `Refund`, `Fine`, `Adjustment` |
| `Source` | enum `LedgerSource` | `Manual`, `Income`, `Award`, `System`. Meaningful for `Mint`; `Manual` otherwise. |
| `Amount` | long | Signed. Positive for credits, negative for debits. |
| `BalanceAfter` | long | Wallet balance after this row. Lets history render without summing. |
| `Reason` | string(512)? | Free text. Required for `Mint`, `Fine`, `Adjustment`. |
| `FeatureKey` | string(128)? | Set on `Spend` and `Refund`. |
| `IdempotencyKey` | string(128) | Unique. Callers supply it; the ledger refuses a duplicate and returns the existing row. |
| `ReferenceTransactionId` | long? | The row this one relates to: the other side of a transfer, the spend a refund reverses, the row an adjustment corrects. Required for `TransferIn`, `TransferOut`, `Refund`, `Adjustment`. |
| `ModerationCaseId` | Guid? | Set on `Fine` when a mod case exists. |
| `ActorId` | ulong? | Discord user who caused the row. Null for the system principal. |
| `CorrelationId` | string? | Same as the audit log correlation id when one exists. |
| `CreatedAt` | DateTime | |

Rows are never updated or deleted. Indexes on (`WalletId`, `CreatedAt`) and
`IdempotencyKey` (unique).

### MintAuthority

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | Guid PK | |
| `CurrencyId` | Guid FK | |
| `PrincipalType` | enum `MintPrincipalType` { User, Role, System } | |
| `PrincipalId` | ulong? | User id or Discord role id. Null for `System`. |
| `GrantedById` | ulong | |
| `GrantedAt` | DateTime | |

The creator is added as a `User` authority on creation. `System` exists so a
future income job can mint without a fake user.

### PriceEntry

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | Guid PK | |
| `FeatureKey` | string(128) | e.g. `soundboard:3fa85f64-...`. Conventions below. |
| `GuildId` | ulong? | Null means the price applies everywhere the feature is used. |
| `CurrencyId` | Guid FK | May be a guild currency or a global one. |
| `Amount` | long | Must be > 0. |
| `ExemptRoleIds` | ulong[] | Stored as JSON. Members holding any of these roles pay nothing. |
| `IsActive` | bool | |
| `UpdatedById` | ulong | |
| `UpdatedAt` | DateTime | |

Unique index on (`FeatureKey`, `GuildId`). At most one active price per
feature per guild, so a feature costs one currency, not a menu.

**Feature key conventions.** Keys are `{area}:{identifier}`. This project
defines `soundboard:{soundId}`. Future areas reserve `media:image`,
`media:video`, `tts:voice:{voiceId}`. A guild-scoped currency may only price
features in its own guild; the service enforces this on save.

## Rules

### Who may do what

| Action | Global currency | Guild currency |
| --- | --- | --- |
| Create | SuperAdmin | Guild admin (`Admin` portal role with guild access, or Discord Administrator in the guild) |
| Edit rules / deactivate | SuperAdmin | Guild admin |
| Manage mint authorities | SuperAdmin | Guild admin |
| Mint | Listed authority | Listed authority |
| Set prices for a guild's features | Guild admin (may pick either currency kind) | Guild admin |
| Fine | Moderator in that guild, only in guild currencies, and only in the guild the currency belongs to | Moderator in that guild |
| Fine in bot credit | Nobody. Bot credit has `AllowNegative = false`, and only SuperAdmin may debit it via `Adjustment`. | n/a |
| Transfer | Any wallet holder when `IsTransferable` | Same |
| Adjustment | SuperAdmin | Guild admin |

### Balance rules

Let `b` be the wallet balance before the operation.

- **Mint, TransferIn, Refund** always succeed on an active currency. They move
  the balance up from anywhere, including out of debt.
- **Spend, TransferOut** require `b >= amount`. They never take a balance
  below zero. A wallet at `b < 0` is refused with reason `InDebt`.
- **Fine** requires `AllowNegative` on the currency or it stops at zero (the
  fine is reduced to `b` and the response says so). With `AllowNegative`, the
  result may not go below `DebtFloor`; the fine is clamped and the response
  says so.
- **Adjustment** may move either direction but must reference an existing row
  and carry a reason. It is the only way to correct a mistake. It is audited.
- Deactivated currency: every operation except reading is refused with reason
  `CurrencyInactive`.

### Charging

`IChargeService` is the seam every priced feature uses.

```csharp
public interface IChargeService
{
    /// Looks up the price for the feature in the guild and reserves it.
    /// Returns Free when no active price exists or the user is exempt.
    Task<ChargeHoldResult> TryHoldAsync(
        ulong userId, ulong guildId, string featureKey, string idempotencyKey,
        CancellationToken ct = default);

    /// Writes the Spend row and releases the hold. Idempotent by holdId.
    Task<ChargeCommitResult> CommitAsync(Guid holdId, CancellationToken ct = default);

    /// Drops the hold without writing anything. Idempotent by holdId.
    Task ReleaseAsync(Guid holdId, CancellationToken ct = default);

    /// Reverses a committed spend. Writes a Refund row referencing it.
    Task<RefundResult> RefundAsync(long spendTransactionId, string reason, ulong? actorId,
        CancellationToken ct = default);
}

public enum ChargeHoldStatus { Free, Held, InsufficientFunds, InDebt, CurrencyInactive, NoWallet }
```

Behaviour:

1. Look up the active `PriceEntry` for (`featureKey`, `guildId`), then
   (`featureKey`, null). No entry → `Free`, no hold created.
2. If the user holds an exempt role in the guild → `Free`.
3. Load or infer the wallet. No wallet is the same as balance zero.
4. Compute available balance = `CachedBalance` minus the sum of the user's
   open holds on that wallet. Apply the balance rules above.
5. Create a hold in the hold store with an expiry of
   `Currency:HoldExpirySeconds` (default 120).
6. `CommitAsync` writes the `Spend` row using the hold's idempotency key,
   updates `CachedBalance`, and drops the hold, all in one database
   transaction. A hold that has already expired but whose idempotency key has
   not been written still commits, so a slow feature is not punished for
   being slow; expiry only frees the reservation for concurrent holds.
7. `ReleaseAsync` drops the hold. Nothing is written.

The hold store is in-process (`IMemoryCache` via `IInstrumentedCache`, prefix
`currency:hold:`). Holds are lost on restart. That is acceptable: a lost hold
means a free play, never a double charge, because the ledger is only written at
commit and the idempotency key blocks duplicates.

### Minting and transfers

```csharp
public interface IMintService
{
    Task<MintResult> MintAsync(Guid currencyId, ulong toUserId, long amount, string reason,
        LedgerSource source, string idempotencyKey, ulong? actorId, CancellationToken ct = default);
    Task<bool> CanMintAsync(Guid currencyId, ulong userId, IReadOnlyCollection<ulong> userRoleIds,
        CancellationToken ct = default);
}

public interface IWalletService
{
    Task<WalletDto?> GetWalletAsync(Guid currencyId, ulong userId, CancellationToken ct = default);
    Task<IReadOnlyList<WalletDto>> GetWalletsForUserAsync(ulong userId, ulong? guildId, CancellationToken ct = default);
    Task<TransferResult> TransferAsync(Guid currencyId, ulong fromUserId, ulong toUserId, long amount,
        string? note, string idempotencyKey, CancellationToken ct = default);
    Task<FineResult> FineAsync(Guid currencyId, ulong userId, long amount, string reason,
        ulong moderatorId, Guid? moderationCaseId, CancellationToken ct = default);
    Task<AdjustmentResult> AdjustAsync(long referenceTransactionId, long amount, string reason,
        ulong actorId, CancellationToken ct = default);
    Task<PagedResult<LedgerTransactionDto>> GetHistoryAsync(Guid walletId, int page, int pageSize,
        CancellationToken ct = default);
}
```

- A transfer writes two rows, `TransferOut` then `TransferIn`, each
  referencing the other, in one transaction. The idempotency key is the
  caller's; the in-row key is suffixed `:out` / `:in`.
- Transfers are rate limited per user with the existing `RateLimitAttribute`
  on the command (default 5 per minute) and refuse self-transfer.
- A fine that clamps to zero or to the floor still writes one row for the
  clamped amount and reports the clamp.
- Fines are guild-currency only. The service refuses a fine on a global
  currency.

### Integration with moderation

`FineAsync` accepts an optional `ModerationCaseId`. The `/fine` command and the
portal fine action offer a checkbox "also open a mod case". When set, the
service creates a `ModerationCase` of type `Note` with the fine reason through
the existing `IModerationService`, then writes the fine row linking to it. The
existing mod case timeline gains nothing new in this project; the link is there
so the ledger and the case history agree.

### Audit logging

Through the existing `IAuditLogService` with category `Configuration` for
currency and price changes and `User` for balance actions. New
`AuditLogAction` values: `CurrencyCreated`, `CurrencyUpdated`,
`CurrencyDeactivated`, `MintAuthorityGranted`, `MintAuthorityRevoked`,
`CurrencyMinted`, `CurrencyFined`, `CurrencyAdjusted`, `PriceSet`,
`PriceRemoved`. Spends and transfers are not audited; the ledger is their
record.

## User-facing surface

### Discord commands

All under `/wallet` except the moderation and admin ones. Guild-scoped
commands see guild currencies for that guild plus all active global currencies.

| Command | Who | Does |
| --- | --- | --- |
| `/wallet balance [currency]` | anyone | Shows balances. One currency in the guild → no picker. |
| `/wallet history [currency]` | anyone | Last 10 rows, paginated by component buttons. |
| `/wallet pay <user> <amount> [currency] [note]` | anyone | Transfer. Confirmation button before it sends. |
| `/wallet mint <user> <amount> [currency] [reason]` | mint authority | Mint with `Source = Manual`. |
| `/wallet fine <user> <amount> [currency] <reason> [open-case]` | moderator | Fine. Shows the clamp if one applied. |
| `/currency create <name> <symbol> [transferable] [allow-negative] [debt-floor]` | guild admin | Guild currency. |
| `/currency list` | anyone | Currencies visible in this guild with symbol and rules. |

Global currency creation and bot-credit management are portal only.

Command modules follow the existing pattern: `WalletModule` for slash commands,
`WalletComponentModule` for the confirmation and pagination buttons, ids built
with `ComponentIdBuilder`, state in `IInteractionStateService`. Commands are
gated by a new `RequireCurrencyEnabledAttribute` mirroring
`RequireRatWatchEnabledAttribute`, and the module is registered in the command
module configuration so guilds can turn it off.

When a priced soundboard play is refused, the soundboard command replies with
the price, the user's balance, and the currency symbol, e.g.
"This sound costs 5 🪙. You have 2 🪙." A user in debt sees
"You owe 40 🪙. Priced sounds are locked until you're back above zero."

### Portal

| Route | Who | Content |
| --- | --- | --- |
| `/Guilds/{guildId}/Currency` | guild admin | List guild currencies and visible globals; create; edit rules; deactivate; manage mint authorities. |
| `/Guilds/{guildId}/Currency/{currencyId}` | guild admin, moderators read-only | Wallets in this currency sorted by balance; debtors highlighted; mint, fine, adjust actions; ledger with filters. |
| `/Guilds/{guildId}/Currency/Prices` | guild admin | Price entries for this guild. Soundboard sounds appear as a list with a price column and exempt-role picker. |
| `/Admin/Currency` | SuperAdmin | Global currencies. Create bot credit here. Same wallet and ledger views across all users. |
| Guild Soundboard page | existing | Shows the price next to a priced sound. |

REST controllers back these pages following `ApiControllerBase`:
`CurrenciesController`, `WalletsController`, `PricesController`. Endpoints are
listed in the implementation plan and go into `api-endpoints.md`.

### Soundboard integration

`SoundboardOrchestrationService.PlaySoundAsync` gains one step after the
existing enabled and voice-connected checks and before playback starts:

1. `TryHoldAsync(userId, guildId, $"soundboard:{soundId}", key)` where
   `key = $"sound:{guildId}:{soundId}:{userId}:{Guid.NewGuid()}"`.
2. `Free` → continue unchanged.
3. `Held` → continue; on successful enqueue or start, `CommitAsync`; on any
   failure path that returns an unsuccessful `SoundPlayResult`, `ReleaseAsync`.
4. Any other status → return `SoundPlayResult { Success = false, ErrorMessage
   = <price message>, ChargeStatus = status }` so both the slash command and
   the portal playback controller can render the refusal.

Commit happens when the sound is accepted for playback, not when it finishes.
A sound that is skipped by another user mid-play is still paid for.

## Configuration

New options class `CurrencyOptions`, section `Currency`:

| Key | Default | Meaning |
| --- | --- | --- |
| `Currency:Enabled` | `true` | Registers the feature. Off hides commands and pages. |
| `Currency:HoldExpirySeconds` | `120` | How long a hold reserves funds. |
| `Currency:MaxTransferPerMinute` | `5` | Rate limit on `/wallet pay`. |
| `Currency:DefaultDebtFloor` | `-100` | Pre-filled when a guild admin enables debt on a currency. |
| `Currency:HistoryPageSize` | `10` | Rows per page in `/wallet history`. |

## Security and abuse

- Mint is the only source of units, and every mint has an actor and a reason.
  The audit log and the ledger together answer "where did this come from".
- Transfers off on bot credit stops resale and stops a mint mistake from
  spreading.
- Idempotency keys stop double spends from retries, double mints from a
  restarted job, and double refunds.
- Balance checks use available balance (cached minus open holds) so two
  concurrent plays cannot both pass with funds for one.
- `CachedBalance` and the ledger row are written in one transaction. A
  reconcile query (sum of rows vs cache) is exposed on the admin page and
  logged as a warning by a daily check inside the existing retention or
  metrics background service pattern.
- Fines require moderator permission in the guild and are audited. Moderators
  cannot fine themselves. Admins cannot be fined by moderators (role hierarchy
  check through the existing guild member service).
- Discord ids are rendered as strings in every page script.

## Future consumers

Each of these is a later project. They are listed so this project builds the
hooks they need and nothing more.

- **Image and video generation.** A media service charges
  `media:image` / `media:video` in bot credit through `IChargeService`:
  hold before the OpenRouter call, commit on a returned asset, release on
  failure. Needs nothing beyond the feature key convention and refund support.
- **Premium TTS voices.** Price entries on `tts:voice:{id}`. The TTS service
  calls the same hold / commit / release.
- **Weekly income.** A `MonitoredBackgroundService` that reads currencies
  with `IncomeAmount` set, mints to each eligible member with
  `Source = Income` and idempotency key `income:{currencyId}:{userId}:{period}`.
  Eligibility rules are that project's design. The `System` mint principal and
  the idempotency key are the hooks.
- **Awards.** A mod command that mints with `Source = Award`, the mirror of
  a fine. Trivial once minting exists; left out only to keep this PR small.

## Decisions

| Decision | Choice | Why |
| --- | --- | --- |
| Balance storage | Append-only ledger plus cached balance | Refunds, audits, and history need the rows; the cache keeps reads cheap. |
| Units | Whole `long` | No rounding disputes. |
| Bot credit | An ordinary global currency with transfers and debt off | One code path; the flags are the only difference. |
| Who mints globals | SuperAdmin only | Otherwise it is not a cost control for the OpenRouter bill. |
| Debt | Per-currency flag with a floor; fines only | Punitive, not a credit line. Spending can never create debt. |
| In debt | Priced actions locked, free features unchanged | The lock is the point; a full ban would be a moderation action, not a wallet state. |
| Repayment | Gifts, mints, and future income | No earning path in this project; guild members can bail each other out. |
| Deletion | Deactivate only | Balances and history stay readable. |
| Price shape | One active price per feature per guild | Avoids a menu of currencies per action. |
| Hold store | In-memory with expiry | A lost hold is a free play, never a double charge. |
| Commit timing for sounds | On accepted playback | Simple and hard to game. |
| Exempt roles | Per price entry | Admins spamming the obnoxious sound for free is the intended joke. |

## Open questions

None blocking. Two things to decide during implementation, defaulting as shown:

1. Whether `/wallet pay` needs a per-guild daily cap in addition to the rate
   limit. Default: no.
2. Whether the debtors list on the portal should also post a periodic
   "wall of shame" embed to a channel. Default: no; that is a scheduled message
   an admin can set up later.
