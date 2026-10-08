namespace DiscordBot.Core.Exceptions;

/// <summary>
/// Thrown by the ledger when a row would leave its wallet below the minimum balance the caller
/// asked for. The check runs under the wallet lock, so <see cref="Balance"/> is the balance the
/// row would actually have been applied to. Nothing was written: the transaction, and for a
/// transfer both halves of the pair, rolled back.
/// </summary>
public class LedgerFloorException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="LedgerFloorException"/> class.</summary>
    public LedgerFloorException(Guid walletId, long balance, long amount, long minBalanceAfter)
        : base($"Appending {amount} to wallet {walletId} at balance {balance} would go below the minimum of {minBalanceAfter}.")
    {
        WalletId = walletId;
        Balance = balance;
        Amount = amount;
        MinBalanceAfter = minBalanceAfter;
    }

    /// <summary>Gets the wallet the row was for.</summary>
    public Guid WalletId { get; }

    /// <summary>Gets the wallet's balance read under the lock.</summary>
    public long Balance { get; }

    /// <summary>Gets the signed amount of the refused row.</summary>
    public long Amount { get; }

    /// <summary>Gets the minimum balance the caller asked for.</summary>
    public long MinBalanceAfter { get; }
}
