using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Wallet reads and the balance rules from the currency spec. Every write goes out through
/// <see cref="ILedgerRepository"/>, which owns the idempotency and cached-balance invariants; this
/// service owns what is <em>allowed</em>:
/// <list type="bullet">
/// <item>Mint, TransferIn, and Refund always succeed on an active currency, including out of debt.</item>
/// <item>Spend and TransferOut need the balance to cover the amount and never take it below zero;
/// a wallet already below zero is refused with <see cref="CurrencyErrors.InDebt"/>.</item>
/// <item>Fine stops at zero, or at the currency's debt floor when it allows debt, and reports the
/// clamp.</item>
/// <item>Adjustment may move either way but must reference an existing row and carry a reason.</item>
/// <item>A deactivated currency refuses everything except reading.</item>
/// </list>
/// </summary>
public interface IWalletService
{
    /// <summary>Gets a user's wallet in one currency, or null when they hold none.</summary>
    Task<WalletDto?> GetWalletAsync(Guid currencyId, ulong userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user's wallets, narrowed to the currencies visible in a guild when one is given.
    /// </summary>
    Task<IReadOnlyList<WalletDto>> GetWalletsForUserAsync(ulong userId, ulong? guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates units and credits them to a wallet. The mint authority check lives in
    /// <c>IMintService</c>; this is the balance half of the operation.
    /// </summary>
    /// <param name="currencyId">Currency to mint.</param>
    /// <param name="toUserId">Discord user snowflake ID receiving the units.</param>
    /// <param name="amount">Units to create. Must be greater than zero.</param>
    /// <param name="reason">Required free text.</param>
    /// <param name="source">Where the mint came from.</param>
    /// <param name="idempotencyKey">Caller-supplied key. A repeat writes nothing.</param>
    /// <param name="actorId">Discord user who caused it, or null for the system principal.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<MintResult> MintAsync(
        Guid currencyId,
        ulong toUserId,
        long amount,
        string reason,
        LedgerSource source,
        string idempotencyKey,
        ulong? actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Debits a wallet for a priced feature. Refused when the wallet is in debt or cannot cover the
    /// amount; a spend never creates debt.
    /// </summary>
    /// <param name="currencyId">Currency to charge.</param>
    /// <param name="userId">Discord user snowflake ID paying.</param>
    /// <param name="amount">Units to take. Must be greater than zero.</param>
    /// <param name="featureKey">The feature being paid for.</param>
    /// <param name="idempotencyKey">Caller-supplied key. A repeat writes nothing.</param>
    /// <param name="actorId">Discord user who caused it, usually the payer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SpendResult> SpendAsync(
        Guid currencyId,
        ulong userId,
        long amount,
        string featureKey,
        string idempotencyKey,
        ulong? actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves units between two users. Writes a <c>TransferOut</c> and a <c>TransferIn</c> row in one
    /// transaction, each referencing the other, with the caller's key suffixed <c>:out</c> and
    /// <c>:in</c>.
    /// </summary>
    Task<TransferResult> TransferAsync(
        Guid currencyId,
        ulong fromUserId,
        ulong toUserId,
        long amount,
        string? note,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes units from a user as a moderation action. Guild currencies only. Stops at zero, or at
    /// the debt floor when the currency allows debt, and reports the clamp.
    /// </summary>
    Task<FineResult> FineAsync(
        Guid currencyId,
        ulong userId,
        long amount,
        string reason,
        ulong moderatorId,
        Guid? moderationCaseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Corrects a mistake by writing a signed row against the wallet of an existing transaction.
    /// </summary>
    /// <param name="referenceTransactionId">The row being corrected. Must exist.</param>
    /// <param name="amount">Signed amount. May move either direction, but not zero.</param>
    /// <param name="reason">Required free text.</param>
    /// <param name="actorId">Discord user making the correction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AdjustmentResult> AdjustAsync(
        long referenceTransactionId,
        long amount,
        string reason,
        ulong actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Gets one page of a wallet's history, newest first.</summary>
    Task<PagedResult<LedgerTransactionDto>> GetHistoryAsync(
        Guid walletId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
