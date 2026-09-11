using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

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
/// <em>allowed</em> — balance floors, debt, transferability — is <c>IWalletService</c>'s job.
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
    public async Task<LedgerAppendResult> AppendAsync(LedgerTransaction row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        RequireIdempotencyKey(row);

        // Cheap pre-check outside the transaction. The authoritative one happens inside it.
        var alreadyWritten = await GetByIdempotencyKeyAsync(row.IdempotencyKey, cancellationToken);
        if (alreadyWritten != null)
        {
            return new LedgerAppendResult(alreadyWritten, true);
        }

        var owned = await BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await AppendCoreAsync(row, cancellationToken);
            await CommitAsync(owned, cancellationToken);
            return result;
        }
        catch (DbUpdateException ex)
        {
            var duplicate = await RecoverFromFailedWriteAsync(ex, owned, new[] { row }, cancellationToken);
            return new LedgerAppendResult(duplicate.Single(), true);
        }
        catch
        {
            await RollbackAsync(owned, cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<LedgerAppendPairResult> AppendPairAsync(
        LedgerTransaction debit,
        LedgerTransaction credit,
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

        var owned = await BeginTransactionAsync(cancellationToken);
        try
        {
            // Lock both wallets up front, lowest id first. Two transfers running in opposite
            // directions between the same pair would otherwise be able to deadlock on Postgres.
            foreach (var walletId in new[] { debit.WalletId, credit.WalletId }.Distinct().OrderBy(id => id))
            {
                await LockWalletAsync(walletId, cancellationToken);
            }

            var debitResult = await AppendCoreAsync(debit, cancellationToken);

            // Link forward now that the debit has an id, then back once the credit has one. Both
            // writes happen before the transaction commits, so no reader ever sees a half-linked
            // pair and the "rows are never updated" rule holds for everything outside it.
            if (!debitResult.WasDuplicate)
            {
                credit.ReferenceTransactionId = debitResult.Transaction.Id;
            }

            var creditResult = await AppendCoreAsync(credit, cancellationToken);

            if (!debitResult.WasDuplicate)
            {
                debitResult.Transaction.ReferenceTransactionId = creditResult.Transaction.Id;
                await _context.SaveChangesAsync(cancellationToken);
            }

            await CommitAsync(owned, cancellationToken);

            return new LedgerAppendPairResult(
                debitResult.Transaction,
                creditResult.Transaction,
                debitResult.WasDuplicate && creditResult.WasDuplicate);
        }
        catch (DbUpdateException ex)
        {
            var recovered = await RecoverFromFailedWriteAsync(ex, owned, new[] { debit, credit }, cancellationToken);
            return new LedgerAppendPairResult(recovered[0], recovered[1], true);
        }
        catch
        {
            await RollbackAsync(owned, cancellationToken);
            throw;
        }
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
    /// Writes one row inside an already-open transaction: idempotency check, wallet lock, balance
    /// bookkeeping, insert.
    /// </summary>
    private async Task<LedgerAppendResult> AppendCoreAsync(LedgerTransaction row, CancellationToken cancellationToken)
    {
        var existing = await GetByIdempotencyKeyAsync(row.IdempotencyKey, cancellationToken);
        if (existing != null)
        {
            _logger.LogDebug(
                "Ledger append skipped: idempotency key {Key} already written as transaction {TransactionId}",
                row.IdempotencyKey, existing.Id);
            return new LedgerAppendResult(existing, true);
        }

        await LockWalletAsync(row.WalletId, cancellationToken);

        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Id == row.WalletId, cancellationToken)
            ?? throw new InvalidOperationException($"Cannot append to wallet {row.WalletId}: it does not exist.");

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

        return new LedgerAppendResult(row, false);
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
                throw exception;
            }

            recovered.Add(existing);
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
    /// Starts a transaction unless one is already running, in which case the caller above us owns
    /// it and this returns null.
    /// </summary>
    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return _context.Database.CurrentTransaction != null
            ? null
            : await _context.Database.BeginTransactionAsync(cancellationToken);
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

        await owned.RollbackAsync(cancellationToken);
        await owned.DisposeAsync();
    }
}
