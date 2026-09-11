using DiscordBot.Core.Entities;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// What the ledger did with an append. <see cref="Transaction"/> is the row that now exists for the
/// idempotency key, whether this call wrote it or an earlier one did.
/// </summary>
/// <param name="Transaction">The row stored under the caller's idempotency key.</param>
/// <param name="WasDuplicate">
/// True when the key already existed, so nothing was written and no balance moved.
/// </param>
public record LedgerAppendResult(LedgerTransaction Transaction, bool WasDuplicate);

/// <summary>
/// What the ledger did with a linked pair of rows, such as the two halves of a transfer.
/// </summary>
/// <param name="Debit">The debit row, written first.</param>
/// <param name="Credit">The credit row, referencing the debit.</param>
/// <param name="WasDuplicate">
/// True when the keys already existed, so nothing was written and no balance moved.
/// </param>
public record LedgerAppendPairResult(LedgerTransaction Debit, LedgerTransaction Credit, bool WasDuplicate);
