# Virtual Currency

A ledger-backed virtual currency for a guild. Someone with permission creates a
currency, mints units into members' wallets, and members spend, send, or lose
them. Every balance change is a row in an append-only ledger, so history and
audits always agree with the balance.

This page covers the Discord commands. The design is in
`docs/specs/virtual-currency-spec.md`; configuration keys are in
[Configuration Guide](configuration-guide.md).

## Turning it on

The feature is registered only when `Currency:Enabled` is true (the default). An
administrator can also switch it off at runtime with the bot setting
`Features:CurrencyEnabled`. With either off, the commands refuse rather than
half-work, and nothing that could be priced is ever charged.

Per-guild, the `WalletModule` and `CurrencyModule` command modules can be
disabled like any other module in the command module configuration.

## Commands

### `/currency create`

Creates a currency for this server. Requires the Discord **Administrator**
permission. The creator becomes the currency's first mint authority, because a
currency nobody can mint has no way to exist.

| Option | Meaning |
| --- | --- |
| `name` | What it is called. Unique within the server. |
| `symbol` | Emoji or short text shown next to amounts, e.g. 🪙. |
| `transferable` | Whether members can pay each other. Default yes. |
| `allow-negative` | Whether fines may push a balance below zero. Default no. |
| `debt-floor` | The most negative balance a fine may reach, e.g. `-100`. Defaults to `Currency:DefaultDebtFloor` when debt is allowed. |

Global currencies (including bot credit) are created in the portal, not here.

### `/currency list`

Lists the currencies usable in this server: the server's own plus every active
global one, with their symbol and rules.

### `/wallet balance [currency]`

Shows your balances. With one currency in the server there is no picker; with
several, the `currency` option narrows it to one. A currency you have never held
shows as zero. A balance below zero is flagged, along with the reminder that
priced features stay locked until it is back above zero.

### `/wallet history [currency]`

Shows your ledger for one currency, `Currency:HistoryPageSize` rows at a time
(default 10), with **Previous** and **Next** buttons. Each row shows the type,
the signed amount, the reason or feature key, and the balance after it. Only the
person who ran the command can use the buttons, and the view expires after
15 minutes.

### `/wallet pay <user> <amount> [currency] [note]`

Sends currency to another member. The command shows a confirmation first, and
only the **Send** button writes anything. Rate limited per user by
`Currency:MaxTransferPerMinute` (default 5 per minute).

Refused when the currency is not transferable, when you are paying yourself or a
bot, when your balance does not cover the amount, or when you are in debt. A
transfer writes two linked ledger rows, one on each wallet, in a single
transaction — under an idempotency key created with the confirmation, so a
double click pays once.

### `/wallet mint <user> <amount> [currency] [reason]`

Creates units and gives them to a member. Only a **mint authority** on that
currency may do this: the creator, anyone granted authority in the portal, or a
holder of a granted role. A mint moves a balance up from anywhere, debt
included, which is how somebody gets bailed out. Every mint is audited with its
actor and reason.

### `/wallet fine <user> <amount> <reason> [currency] [open-case]`

Fines a member. Requires moderator permission (ManageMessages or a Moderator
role). Guild currencies only — nobody is fined in bot credit.

A fine is the only operation that can push a balance below zero, and only on a
currency created with `allow-negative`. It is clamped: at zero on a currency
that does not allow debt, or at the currency's debt floor on one that does. The
clamped amount is what gets written, and the receipt says so. You cannot fine
yourself, a bot, or (as a non-administrator) an administrator.

With `open-case` set, a `Note` moderation case is opened with the fine's reason
and linked to the ledger row, so the case history and the ledger agree.

## Debt

A wallet below zero is locked out of priced features and cannot send currency.
Nothing else changes: free commands keep working. The way out is a gift from
another member or a mint — spending can never create debt.

## Priced features

A feature costs currency when there is an active price entry for its feature key
in this guild. Nothing is priced by default, so every feature stays free until
someone sets a price.

| Feature | Feature key | What it costs |
| --- | --- | --- |
| Soundboard playback | `soundboard:{soundId}` | One price per sound, per guild |

Charging a feature is always hold → play → commit. The price is reserved before
the work starts, so two plays cannot both go through on funds for one, and it
only reaches the ledger once the work is accepted. A user who cannot pay is told
what it costs and what they have, and nothing is written.

For the soundboard specifically — where the price is charged, what a refusal
looks like in Discord and in the portal, and how the price badge is rendered —
see [Soundboard](soundboard.md#pricing-sounds).

Members holding one of a price entry's exempt roles pay nothing. A wallet in
debt is refused regardless of the price.

The admin page for setting prices ships with the portal currency pages.
