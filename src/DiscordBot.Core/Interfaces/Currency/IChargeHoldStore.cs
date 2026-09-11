using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Where open holds live. The store is in-process and holds are lost on restart, which is safe:
/// a lost hold means a free play, never a double charge, because the ledger is only written at
/// commit and the idempotency key blocks duplicates.
/// </summary>
public interface IChargeHoldStore
{
    /// <summary>
    /// Reserves an amount against a wallet when the available balance covers it. The check and the
    /// write happen together, so two concurrent holds cannot both pass with funds for one.
    /// </summary>
    /// <param name="hold">
    /// The hold to store. Its <c>CreatedAt</c> and <c>ExpiresAt</c> are set by the store.
    /// </param>
    /// <param name="cachedBalance">The wallet's balance as the ledger knows it.</param>
    /// <returns>The stored hold, or null when the available balance does not cover the amount.</returns>
    ChargeHold? TryCreate(ChargeHold hold, long cachedBalance);

    /// <summary>Gets a hold by id, or null once it has aged out of the store.</summary>
    ChargeHold? Get(Guid holdId);

    /// <summary>
    /// Sums the amounts still reserved on a wallet. Expired and committed holds count for nothing.
    /// </summary>
    long SumOpenHolds(Guid walletId);

    /// <summary>
    /// Records that a hold became a <c>Spend</c> row. The hold stops reserving funds and a repeated
    /// commit can find the row it already wrote.
    /// </summary>
    void Complete(Guid holdId, long transactionId);

    /// <summary>Drops a hold. Idempotent.</summary>
    void Release(Guid holdId);
}
