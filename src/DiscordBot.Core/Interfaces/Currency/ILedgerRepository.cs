using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// The single write path into the currency system. Everything that moves a balance goes through
/// <see cref="AppendAsync"/> or <see cref="AppendPairAsync"/>; nothing else writes
/// <see cref="Wallet.CachedBalance"/>.
/// </summary>
public interface ILedgerRepository
{
    /// <summary>
    /// Appends one row. In a single transaction it checks the idempotency key, locks the wallet row
    /// where the provider supports it, sets <see cref="LedgerTransaction.BalanceAfter"/>, writes the
    /// row, and updates <see cref="Wallet.CachedBalance"/>.
    /// <para>
    /// A duplicate idempotency key writes nothing and returns the existing row, so a retry is free.
    /// </para>
    /// </summary>
    /// <param name="row">
    /// The row to write. <see cref="LedgerTransaction.BalanceAfter"/> is set by the repository and
    /// <see cref="LedgerTransaction.CreatedAt"/> is stamped when left at default.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<LedgerAppendResult> AppendAsync(LedgerTransaction row, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends two linked rows in one transaction, setting each row's
    /// <see cref="LedgerTransaction.ReferenceTransactionId"/> to the other. Used for a transfer, so
    /// the two halves can never exist apart.
    /// </summary>
    /// <param name="debit">The row that leaves a wallet. Written first.</param>
    /// <param name="credit">The row that enters a wallet.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<LedgerAppendPairResult> AppendPairAsync(
        LedgerTransaction debit,
        LedgerTransaction credit,
        CancellationToken cancellationToken = default);

    /// <summary>Gets one row by id, or null.</summary>
    Task<LedgerTransaction?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Gets the row stored under an idempotency key, or null.</summary>
    Task<LedgerTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Gets one page of a wallet's history, newest first, with the total row count.</summary>
    Task<(IReadOnlyList<LedgerTransaction> Items, int TotalCount)> GetForWalletAsync(
        Guid walletId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Sums a wallet's rows. The reconcile check compares this against the cached balance.</summary>
    Task<long> SumForWalletAsync(Guid walletId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sums every wallet's rows in one currency, keyed by wallet id. Wallets with no rows are
    /// absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, long>> SumByWalletForCurrencyAsync(
        Guid currencyId,
        CancellationToken cancellationToken = default);
}
