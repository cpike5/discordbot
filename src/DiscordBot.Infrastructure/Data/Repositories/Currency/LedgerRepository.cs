using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Exceptions;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Runtime.ExceptionServices;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// The single write path for currency balances.
/// <para>
/// Every append runs in one transaction that checks the idempotency key, takes the wallet row's
/// lock, stamps <see cref="LedgerTransaction.BalanceAfter"/>, writes the row, and moves
/// <see cref="Wallet.CachedBalance"/>. Nothing else in the system writes that column, which is what
/// keeps the cache equal to the sum of the rows.
/// </para>
/// <para>
/// The repository enforces the idempotency and bookkeeping invariants only. Whether an operation is
/// <em>allowed</em> — balance floors, debt, transferability — is <c>IWalletService</c>'s job. The
/// one exception is the minimum balance a caller passes in: the service picks the floor, and the
/// repository checks it under the wallet lock, because only there is the balance current.
/// </para>
/// </summary>
public class LedgerRepository : ILedgerRepository
{
    private readonly BotDbContext _context;
    private readonly ILogger<LedgerRepository> _logger;

    public LedgerRepository(BotDbContext context, ILogger<LedgerRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LedgerAppendResult> AppendAsync(
        LedgerTransaction row,
        long? minBalanceAfter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        RequireIdempotencyKey(row);

        // Cheap pre-check outside the transaction. The authoritative one happens inside it.
        var alreadyWritten = await GetByIdempotencyKeyAsync(row.IdempotencyKey, cancellationToken);
        if (alreadyWritten != null)
        {
            return new LedgerAppendResult(alreadyWritten, true);
        }

        return await ExecuteAtomicallyAsync(
            new[] { row },
            async (owned, isRetry, ct) =>
            {
                try
                {
                    var written = await AppendCoreAsync(row, minBalanceAfter, ct);
                    await CommitAsync(owned, ct);
                    return new LedgerAppendResult(written.Transaction, written.Existed && !isRetry);
                }
                catch (DbUpdateException ex)
                {
                    var duplicate = await RecoverFromFailedWriteAsync(ex, owned, new[] { row }, ct);
                    return new LedgerAppendResult(duplicate.Single(), true);
                }
                catch
                {
                    await RollbackAsync(owned, ct);
                    throw;
                }
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LedgerAppendPairResult> AppendPairAsync(
        LedgerTransaction debit,
        LedgerTransaction credit,
        long? debitMinBalanceAfter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(debit);
        ArgumentNullException.ThrowIfNull(credit);
        RequireIdempotencyKey(debit);
        RequireIdempotencyKey(credit);

        var existingDebit = await GetByIdempotencyKeyAsync(debit.IdempotencyKey, cancellationToken);
        var existingCredit = await GetByIdempotencyKeyAsync(credit.IdempotencyKey, cancellationToken);
        if (existingDebit != null && existingCredit != null)
        {
            return new LedgerAppendPairResult(existingDebit, existingCredit, true);
        }

        return await ExecuteAtomicallyAsync(
            new[] { debit, credit },
            async (owned, isRetry, ct) =>
            {
                try
                {
                    // Lock both wallets up front, lowest id first. Two transfers running in opposite
                    // directions between the same pair would otherwise be able to deadlock on Postgres.
                    foreach (var walletId in new[] { debit.WalletId, credit.WalletId }.Distinct().OrderBy(id => id))
                    {
                        await LockWalletAsync(walletId, ct);
                    }

                    // A refused debit throws before anything is written, and the catch below rolls the
                    // transaction back, so the credit half is never written on its own.
                    var debitResult = await AppendCoreAsync(debit, debitMinBalanceAfter, ct);

                    // Link forward now that the debit has an id, then back once the credit has one. Both
                    // writes happen before the transaction commits, so no reader ever sees a half-linked
                    // pair and the "rows are never updated" rule holds for everything outside it.
                    if (!debitResult.Existed)
                    {
                        credit.ReferenceTransactionId = debitResult.Transaction.Id;
                    }

                    var creditResult = await AppendCoreAsync(credit, null, ct);

                    if (!debitResult.Existed)
                    {
                        debitResult.Transaction.ReferenceTransactionId = creditResult.Transaction.Id;
                        await _context.SaveChangesAsync(ct);
                    }

                    await CommitAsync(owned, ct);

                    return new LedgerAppendPairResult(
                        debitResult.Transaction,
                        creditResult.Transaction,
                        !isRetry && debitResult.Existed && creditResult.Existed);
                }
                catch (DbUpdateException ex)
                {
                    var recovered = await RecoverFromFailedWriteAsync(ex, owned, new[] { debit, credit }, ct);
                    return new LedgerAppendPairResult(recovered[0], recovered[1], true);
                }
                catch
                {
                    await RollbackAsync(owned, ct);
                    throw;
                }
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LedgerTransaction?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.LedgerTransactions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LedgerTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        return await _context.LedgerTransactions
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<LedgerTransaction> Items, int TotalCount)> GetForWalletAsync(
        Guid walletId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.LedgerTransactions.AsNoTracking().Where(t => t.WalletId == walletId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<long> SumForWalletAsync(Guid walletId, CancellationToken cancellationToken = default)
    {
        return await _context.LedgerTransactions
            .AsNoTracking()
            .Where(t => t.WalletId == walletId)
            .SumAsync(t => (long?)t.Amount, cancellationToken) ?? 0L;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, long>> SumByWalletForCurrencyAsync(
        Guid currencyId,
        CancellationToken cancellationToken = default)
    {
        var sums = await _context.LedgerTransactions
            .AsNoTracking()
            .Where(t => _context.Wallets.Any(w => w.Id == t.WalletId && w.CurrencyId == currencyId))
            .GroupBy(t => t.WalletId)
            .Select(g => new { WalletId = g.Key, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken);

        return sums.ToDictionary(s => s.WalletId, s => s.Total);
    }

    /// <summary>
    /// Writes one row inside an already-open transaction: idempotency check, wallet lock, floor
    /// check against <paramref name="minBalanceAfter"/>, balance bookkeeping, insert. <c>Existed</c> is true when the idempotency key was already written and
    /// nothing was inserted; whether that counts as a duplicate is the caller's call, because on a
    /// retry the rows found are this call's own earlier write.
    /// </summary>
    private async Task<(LedgerTransaction Transaction, bool Existed)> AppendCoreAsync(
        LedgerTransaction row,
        long? minBalanceAfter,
        CancellationToken cancellationToken)
    {
        var existing = await GetByIdempotencyKeyAsync(row.IdempotencyKey, cancellationToken);
        if (existing != null)
        {
            _logger.LogDebug(
                "Ledger append skipped: idempotency key {Key} already written as transaction {TransactionId}",
                row.IdempotencyKey, existing.Id);
            return (existing, true);
        }

        await LockWalletAsync(row.WalletId, cancellationToken);

        // The caller has usually read this wallet already (the service's balance pre-check), and a
        // tracking query hands back that instance as it was, not as the database has it now. Reload
        // it, so the balance below is the one under the lock and not one a concurrent append has
        // since moved.
        var wallet = _context.Wallets.Local.FirstOrDefault(w => w.Id == row.WalletId);
        if (wallet != null)
        {
            await _context.Entry(wallet).ReloadAsync(cancellationToken);
        }
        else
        {
            wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Id == row.WalletId, cancellationToken);
        }

        if (wallet == null || _context.Entry(wallet).State == EntityState.Detached)
        {
            throw new InvalidOperationException($"Cannot append to wallet {row.WalletId}: it does not exist.");
        }

        // The pre-check a service makes before calling in can be overtaken by another append; this
        // is the check that holds. Thrown before anything is written, so the caller's catch only has
        // to roll back.
        if (minBalanceAfter.HasValue && wallet.CachedBalance + row.Amount < minBalanceAfter.Value)
        {
            throw new LedgerFloorException(row.WalletId, wallet.CachedBalance, row.Amount, minBalanceAfter.Value);
        }

        row.BalanceAfter = wallet.CachedBalance + row.Amount;
        wallet.CachedBalance = row.BalanceAfter;

        if (row.CreatedAt == default)
        {
            row.CreatedAt = DateTime.UtcNow;
        }

        await _context.LedgerTransactions.AddAsync(row, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Appended {Type} of {Amount} to wallet {WalletId}; balance now {Balance}",
            row.Type, row.Amount, row.WalletId, row.BalanceAfter);

        return (row, false);
    }

    /// <summary>
    /// Takes the wallet row's write lock for the rest of the transaction, so two appends can never
    /// both read the same cached balance.
    /// </summary>
    private async Task LockWalletAsync(Guid walletId, CancellationToken cancellationToken)
    {
        if (_context.Database.IsNpgsql())
        {
            // Held until commit. ToListAsync with no further composition sends the SQL verbatim,
            // which matters: composing over a FromSql query lets EF wrap it in shapes Postgres
            // refuses a locking clause in (DISTINCT, an aggregate, the nullable side of an outer
            // join), and the ones it accepts no longer say which rows get locked.
            await _context.Wallets
                .FromSqlInterpolated($"SELECT * FROM \"Wallets\" WHERE \"Id\" = {walletId} FOR UPDATE")
                .ToListAsync(cancellationToken);
            return;
        }

        // SQLite has no row locks, so instead promote the transaction to a write transaction
        // before reading the balance. Without this both appends would read, then try to upgrade,
        // and one of them would fail on a lock it cannot wait for.
        await _context.Wallets
            .Where(w => w.Id == walletId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(w => w.CachedBalance, w => w.CachedBalance), cancellationToken);
    }

    /// <summary>
    /// Turns a failed write into the duplicate it almost certainly was: rolls back, resets the
    /// change tracker, and re-reads the rows by idempotency key. Rethrows when the keys are still
    /// absent, because then the failure was something else.
    /// </summary>
    private async Task<IReadOnlyList<LedgerTransaction>> RecoverFromFailedWriteAsync(
        DbUpdateException exception,
        IDbContextTransaction? ownedTransaction,
        IReadOnlyList<LedgerTransaction> rows,
        CancellationToken cancellationToken)
    {
        await RollbackAsync(ownedTransaction, cancellationToken);

        // The rolled-back balance change and the never-inserted rows are still tracked; drop the
        // lot so the re-read below sees the database, not our failed attempt.
        _context.ChangeTracker.Clear();

        var recovered = new List<LedgerTransaction>(rows.Count);
        foreach (var row in rows)
        {
            var existing = await GetByIdempotencyKeyAsync(row.IdempotencyKey, cancellationToken);
            if (existing == null)
            {
                // Rethrow with the original stack trace
                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            recovered.Add(existing!);
        }

        _logger.LogDebug(
            "Ledger write lost a race on idempotency key(s) {Keys}; returning the rows that won",
            string.Join(", ", rows.Select(r => r.IdempotencyKey)));

        return recovered;
    }

    private static void RequireIdempotencyKey(LedgerTransaction row)
    {
        if (string.IsNullOrWhiteSpace(row.IdempotencyKey))
        {
            throw new ArgumentException("A ledger row must carry an idempotency key.", nameof(row));
        }
    }

    /// <summary>
    /// Runs one append unit (begin, work, commit) so that a retrying execution strategy can repeat it
    /// as a whole.
    /// <para>
    /// The application configures Npgsql with <c>EnableRetryOnFailure</c>, and a retrying strategy
    /// refuses a transaction the caller opens itself unless the whole transaction runs inside
    /// <see cref="IExecutionStrategy.ExecuteAsync{TResult}(Func{CancellationToken, Task{TResult}}, CancellationToken)"/>.
    /// Providers that do not retry (SQLite) get a pass-through strategy, so this costs them nothing.
    /// </para>
    /// <para>
    /// Every attempt after the first starts clean: the change tracker is cleared (it still holds the
    /// failed attempt's wallet change and inserted rows) and the caller's rows are put back as they
    /// arrived (no generated id, no stamped balance, no wallet navigation, original reference). The idempotency check inside <see cref="AppendCoreAsync"/> then makes the retry safe
    /// even when the failure was a commit whose outcome was unknown: if the first attempt did
    /// commit, the retry finds its rows and returns them instead of writing twice. Those rows are
    /// this call's own write, so a retry reports <c>WasDuplicate = false</c>; reporting a duplicate
    /// would tell a refund that it had already happened although this call is what moved the money.
    /// </para>
    /// <para>
    /// When a transaction is already running the caller above us owns it, and the strategy that
    /// wraps it, so the unit runs as-is with a null transaction handle.
    /// </para>
    /// </summary>
    private async Task<T> ExecuteAtomicallyAsync<T>(
        IReadOnlyList<LedgerTransaction> rows,
        Func<IDbContextTransaction?, bool, CancellationToken, Task<T>> unit,
        CancellationToken cancellationToken)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return await unit(null, false, cancellationToken);
        }

        var originalReferences = rows.Select(r => r.ReferenceTransactionId).ToArray();
        var attempt = 0;

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            var isRetry = attempt++ > 0;
            if (isRetry)
            {
                _context.ChangeTracker.Clear();
                for (var i = 0; i < rows.Count; i++)
                {
                    rows[i].Id = 0;
                    rows[i].BalanceAfter = 0;

                    // Fix-up pointed this at the wallet instance the failed attempt tracked, with
                    // its balance already moved. Re-adding the row would attach that stale copy.
                    rows[i].Wallet = null;
                    rows[i].ReferenceTransactionId = originalReferences[i];
                }
            }

            var owned = await _context.Database.BeginTransactionAsync(ct);
            return await unit(owned, isRetry, ct);
        }, cancellationToken);
    }

    private static async Task CommitAsync(IDbContextTransaction? owned, CancellationToken cancellationToken)
    {
        if (owned != null)
        {
            await owned.CommitAsync(cancellationToken);
            await owned.DisposeAsync();
        }
    }

    private static async Task RollbackAsync(IDbContextTransaction? owned, CancellationToken cancellationToken)
    {
        if (owned == null)
        {
            return;
        }

        try
        {
            await owned.RollbackAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Data.Common.DbException)
        {
            // The caller is already handling the failure that sent us here. A transaction that is
            // already complete (a commit that landed but whose outcome was lost) or whose connection
            // is gone cannot be rolled back, and that must not replace the original exception: the
            // retrying execution strategy has to see it to decide whether to run the unit again.
        }
        finally
        {
            await owned.DisposeAsync();
        }
    }
}
