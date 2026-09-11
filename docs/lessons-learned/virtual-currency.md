# Virtual currency

Notes from building the ledger-backed currency system across the five PRs in
`docs/plans/virtual-currency-implementation-plan.md`. The parts that fought back were the
ones the plan predicted — holds and concurrency — plus two the plan did not: a locking
clause that has to be written once per provider, and a feature flag that turned out to be
two switches.

## The cached balance is only as good as the lock under it

`Wallet.CachedBalance` is a denormalisation of the wallet's ledger rows, so every write
goes through `ILedgerRepository.AppendAsync` and nothing else touches that column. The
invariant is easy to state — cache equals the sum of the rows — and easy to break: two
appends that read the same balance both stamp the same `BalanceAfter`, and the wallet ends
up one row short of its own history with no error anywhere.

The lock that stops it had to be written twice, because the two providers fail differently:

- **PostgreSQL** takes the wallet row's write lock with `SELECT ... FROM "Wallets" WHERE
  "Id" = ... FOR UPDATE`, sent through `FromSqlInterpolated` and materialised immediately.
  The "immediately" is load-bearing. Composing anything onto a `FromSql` query lets EF wrap
  it in a subquery, and `FOR UPDATE` is only legal in some shapes: PostgreSQL 16 answers
  `FOR UPDATE is not allowed with DISTINCT clause`, `... with aggregate functions`, and
  `FOR UPDATE cannot be applied to the nullable side of an outer join`. The shapes it does
  accept stop being obvious about which rows get locked. So the query is `ToListAsync`'d
  with no `Where` after it, and the result thrown away.
- **SQLite** has no row locks at all. A deferred transaction starts as a reader, and the
  upgrade to a writer fails outright — `SQLITE_BUSY`, which `busy_timeout` does not wait
  out — if another connection took the write lock in between. `LockWalletAsync` therefore
  promotes the transaction *before* reading the balance, with an `ExecuteUpdateAsync` that
  sets `CachedBalance` to itself. A no-op write is the only portable way to say "I intend
  to write" to SQLite.

A transfer locks both wallets up front, lowest id first. Without the ordering, two
transfers running in opposite directions between the same pair deadlock on Postgres.

The other half of the invariant is the unique index on `IdempotencyKey`. Rather than
asking "has this been written?" and trusting the answer, the repository lets the insert
fail and treats a `DbUpdateException` whose keys turn out to be present as the duplicate it
almost certainly was. Two details make that work: roll the transaction back *and* call
`ChangeTracker.Clear()` before re-reading, or the re-read is served from the failed
attempt's tracked entities; and rethrow when the keys are still absent, because then the
write failed for some other reason entirely.

**Rules.** One write path per denormalised column, and no exceptions to it. A uniqueness
check that is not a database constraint is a race with extra steps. Provider-specific SQL
belongs inside the repository, where the layers above it cannot know or care.

## Testing concurrency needed a different database

`TestDbContextFactory` builds SQLite in-memory databases, and an in-memory SQLite database
lives inside a single connection: every context shares it, so two "concurrent" writers are
never actually concurrent and the test proves nothing. PR 1 added
`TestDbContextFactory.CreateSharedDatabase()` for this: a temporary file-backed database in
WAL mode, one keep-alive connection holding the file and the pragma open, `DefaultTimeout`
set so `sqlite3_busy_timeout` makes a blocked writer wait instead of throwing, and a
`Dispose` that clears the connection pool before deleting the `.db`, `-wal` and `-shm`
files.

The threads come from `ConcurrencyTestHelper.RunOnDedicatedThreads`, not the thread pool,
for the reason in `flaky-tests-thread-pool-starvation.md`: threads that block on each other
inside `Parallel.For` or `Task.Run` starve the background-service tests running in parallel
with them, and the failures land somewhere else entirely.

The assertion worth copying is not "the final balance is right" — that passes by luck about
half the time — but "every row saw a different `BalanceAfter`". A stale read shows up as a
duplicate, immediately.

**Rule.** A concurrency test that shares one connection, or one thread pool, tests nothing.
Assert on the intermediate values, not just the total.

## The Postgres half had no test coverage, so it was run by hand

The suite is SQLite-only, which means the `FOR UPDATE` branch above — the whole reason the
Postgres path exists — is never executed by CI. That is not a gap tests can close cheaply
(CI has no Postgres service), so it is closed by running it before release and writing down
what was run.

Done on 2026-09-11 against PostgreSQL 16.13, from an empty database:

```bash
dotnet ef database update --project src/DiscordBot.Infrastructure \
  --startup-project src/DiscordBot.Bot --context PostgresBotDbContext \
  --connection "Host=localhost;Database=discordbot;Username=discordbot;Password=..."
```

All 16 migrations applied cleanly, `AddVirtualCurrency` last. The five tables came out with
the columns and indexes the spec asks for: `IX_LedgerTransactions_IdempotencyKey` unique,
`IX_Wallets_CurrencyId_UserId` unique, `IX_PriceEntries_FeatureKey_GuildId` unique,
`ExemptRoleIds` as `text` holding JSON, and every snowflake as `bigint`.

Then `LedgerRepository` itself, driven from a throwaway console project against the same
server, one context per thread on dedicated threads:

| Check | Result |
| --- | --- |
| 8 concurrent appends on one wallet | 8 rows, cache 80 = sum of rows, `BalanceAfter` 10..80 with no repeats |
| Same idempotency key twice | One row; the second call returns the first row |
| 4 threads racing on one idempotency key | One row, no exceptions — the losers recover through the duplicate path |
| 8 transfers alternating direction between two wallets | No deadlock, both halves linked, cache equals sum on both wallets |
| `CreatedAt` into `timestamp without time zone` | Round-trips, with `Npgsql.EnableLegacyTimestampBehavior` set as `Program.cs` sets it |

**Rule.** A provider-specific branch that CI cannot reach gets a manual run and a note like
this one before the release that carries it. "Not test-covered" in a PR description is a
promise to someone, and this is where it gets kept.

## A hold has two clocks

The spec asks for something that sounds contradictory: a hold expires after
`Currency:HoldExpirySeconds` so it stops reserving funds, but an expired hold still commits
so a slow feature is not punished for being slow. Implement that with one clock — a cache
entry whose TTL is the expiry — and a sound that takes longer than two minutes to start
loses its hold, the commit finds nothing, and the play is free.

So `ChargeHoldStore` keeps two: `ChargeHold.ExpiresAt` is when the hold stops counting
against the wallet's available balance, while the cache entry lives ten times that (five
minutes minimum) so `CommitAsync` can still find it. A committed hold stays cached too,
with a transaction id on it, so a retried commit returns the row it already wrote instead
of writing a second.

Available balance is `CachedBalance` minus the wallet's open holds, and the sum and the
write happen under one lock inside the store. Checking availability in the service and
then storing the hold would let two plays priced at the user's last coin both pass. The
commit still goes through `IWalletService.SpendAsync` and its balance rules: the hold is a
reservation, not a promise, and the funds may have moved.

Holds are in-process and lost on restart. That direction is deliberate: a lost hold is a
free play, never a double charge, because nothing is written until commit and the
idempotency key blocks a duplicate. It also means the store is per-instance — two instances
of the bot would each hold the same coin. Not a problem today; a real one the day this runs
more than once.

**Rule.** When a lifetime means two things, store two of them. When a check and a write
have to agree, do them under one lock, in the thing that owns the data.

## Release belongs in `finally`

`PlaySoundAsync` has about ten ways out — audio disabled globally, disabled for the guild,
not in voice, sound missing, file missing, playback throwing — and a hold that is taken
before them is leaked by every one that forgets to release it. Rather than a
`ReleaseAsync` in each branch, the method sets `chargeSettled` when the commit lands and
releases in a `finally` on everything else. The release passes
`CancellationToken.None` deliberately: a cancelled play still has to give the money back.

Commit happens when the sound is *accepted for playback*, not when it finishes. Anything
later means a skip refunds by accident, and the failure mode is worse than it looks: a
commit that fails after audio is already going out is a charge that got away, not a failed
play, so the result reports the play as successful with no price rather than claiming a
cost nobody paid.

**Rule.** A reservation taken in a method with many exits is released in `finally`, with a
flag for the one path that settled it. Cleanup does not take the caller's cancellation
token.

## One feature, two switches, and an optional dependency

`Currency:Enabled` decides whether `AddCurrency` is called at all. With it off nothing is
registered, which is the rollback path — and which makes `IChargeService` an *optional*
constructor argument for every consumer, a nullable field, and a null check on every use.
Everything that can see a currency type has to construct without one, which is what the
activation test in PR 5 is for: build the container with the feature off and resolve every
page and controller.

`Features:CurrencyEnabled` is the other switch, read from `ISettingsService` at runtime so
an administrator can stop charging without a restart. Six places read it.

None of them could be turned off. The key had no entry in `SettingDefinitions`, so it never
appeared on the Settings page's Features tab, while `virtual-currency.md` told
administrators to switch it off "in Settings". It defaults to on, so nothing misbehaved —
the switch simply was not reachable without writing the row by hand. Added in the follow-up
that produced this note.

**Rule.** A runtime switch is not a switch until it is in `SettingDefinitions`. Reading a
setting key that nothing defines is a feature nobody can use, and it fails silently in the
direction that looks fine.

## Things that had to move to be testable

- **Discord.NET command modules cannot be unit tested** — `SocketInteractionContext` and
  friends are sealed. So everything worth asserting on left the modules for
  `CurrencyFormatting`: which currency a command resolved to, the refusal wording, the
  embeds, the fine-clamp line. The modules became thin enough that their remaining risk is
  wiring.
- **An attribute argument has to be a constant**, so `[RateLimit(5)]` cannot read
  `Currency:MaxTransferPerMinute`. `RateLimitAttribute` grew a `protected virtual GetLimit(
  IServiceProvider)` and `RateLimitTransfersAttribute` overrides it to read the options.
  Worth knowing before writing a configurable precondition.
- **The `GuildAccess` policy needs a `guildId` in the route**, and half the currency routes
  are keyed by currency id instead. `ICurrencyAccessService` resolves the scope from the
  currency itself and returns a level (`Administer` / `Moderate` / `Read` / `None`). A
  currency the caller cannot see answers 404, not 403, so ids cannot be probed. The detail
  page asks the same seam for its button flags, so what the UI offers and what the API
  allows cannot drift apart.

## Snowflakes as strings, balances as numbers

The usual gotcha, with a line in it worth drawing. Every `ulong` on a currency DTO that
reaches a page script carries `[JsonNumberHandling(WriteAsString | AllowReadingFromString)]`
— user ids, guild ids, role ids, mint principal ids — because JSON numbers past
`Number.MAX_SAFE_INTEGER` lose their last digits and every lookup then fails. Balances are
`long` too and stay numbers: they are arithmetic, they are nowhere near the limit, and the
scripts compare and format them.

## Left open

- **Global prices are not constrained by an index.** `IX_PriceEntries_FeatureKey_GuildId`
  is unique, but `GuildId` is null for a price that applies everywhere, and both SQLite and
  PostgreSQL treat nulls as distinct — so the index does not stop two rows for the same
  global feature key. `CurrencyService.SetPriceAsync` reads before it writes, which is
  enough for the portal but not against a simultaneous double save. A filtered unique index
  per provider, or a sentinel `GuildId` of 0, would close it; neither is worth a migration
  until a global price exists.
- **Nothing reconciles on a timer.** The spec asks for the cache-versus-rows check to be
  logged as a warning daily; the plan never scheduled it, so what shipped is the endpoint
  and the button on the currency detail page. A `MonitoredBackgroundService` that walks
  active currencies and warns on a non-empty result is the missing piece.
- **Income, awards, and refunds have no caller.** `LedgerSource.Income` / `Award`, the
  `System` mint principal, and `IChargeService.RefundAsync` are all built and tested but
  reachable only from a future feature. They are hooks, not dead code — the spec's "future
  consumers" section says what each is for.
