using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// The seam every priced feature uses. A feature asks for a hold, does its work, then commits or
/// releases; nothing reaches the ledger until the commit.
/// <para>
/// A feature with no active price is <see cref="ChargeHoldStatus.Free"/> and creates no hold, so a
/// guild that has priced nothing behaves exactly as it did before the currency feature existed.
/// </para>
/// </summary>
public interface IChargeService
{
    /// <summary>
    /// Looks up the price of a feature in a guild and reserves it against the user's wallet.
    /// </summary>
    /// <param name="userId">Discord user snowflake ID paying.</param>
    /// <param name="guildId">Guild the feature is being used in.</param>
    /// <param name="featureKey">The priced feature, shaped <c>{area}:{identifier}</c>.</param>
    /// <param name="idempotencyKey">
    /// Key the eventual <c>Spend</c> row carries. Must be unique per attempt, so a retried commit
    /// writes once and two separate uses are both charged.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="ChargeHoldStatus.Free"/> when nothing is owed, <see cref="ChargeHoldStatus.Held"/>
    /// with a hold id when the price is reserved, or a refusal the caller renders to the user.
    /// </returns>
    Task<ChargeHoldResult> TryHoldAsync(
        ulong userId,
        ulong guildId,
        string featureKey,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the <c>Spend</c> row for a hold and drops the reservation. Idempotent: committing the
    /// same hold twice returns the first row and writes nothing.
    /// <para>
    /// A hold whose reservation has expired still commits. Expiry only frees the funds for
    /// concurrent holds; it does not punish a feature for being slow.
    /// </para>
    /// </summary>
    Task<ChargeCommitResult> CommitAsync(Guid holdId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops a hold without writing anything. Idempotent, and safe to call on a hold that has
    /// already expired or been committed.
    /// </summary>
    Task ReleaseAsync(Guid holdId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverses a committed spend by writing a <c>Refund</c> row that references it. A spend can
    /// only be refunded once.
    /// </summary>
    /// <param name="spendTransactionId">The <c>Spend</c> row to reverse.</param>
    /// <param name="reason">Why it is being reversed.</param>
    /// <param name="actorId">Discord user who caused the refund, or null for the system.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<RefundResult> RefundAsync(
        long spendTransactionId,
        string reason,
        ulong? actorId,
        CancellationToken cancellationToken = default);
}
