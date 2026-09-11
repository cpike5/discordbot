using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Minting is the only source of units, so it is the only operation with its own authority list.
/// This service is the authority check in front of <c>IWalletService.MintAsync</c>, which owns the
/// balance half.
/// </summary>
public interface IMintService
{
    /// <summary>
    /// Creates units and credits them to a wallet, after checking that the actor may mint this
    /// currency. A null <paramref name="actorId"/> is the system principal, which mints only when
    /// the currency carries a <see cref="MintPrincipalType.System"/> grant.
    /// </summary>
    /// <param name="currencyId">Currency to mint.</param>
    /// <param name="toUserId">Discord user snowflake ID receiving the units.</param>
    /// <param name="amount">Units to create. Must be greater than zero.</param>
    /// <param name="reason">Required free text; it is what the audit log and the ledger record.</param>
    /// <param name="source">Where the mint came from.</param>
    /// <param name="idempotencyKey">Caller-supplied key. A repeat writes nothing.</param>
    /// <param name="actorId">Discord user minting, or null for the system principal.</param>
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
    /// Whether a user may mint a currency, by their own id or by any of their Discord roles. Used
    /// to decide whether to offer minting at all, on the command and in the portal.
    /// </summary>
    Task<bool> CanMintAsync(
        Guid currencyId,
        ulong userId,
        IReadOnlyCollection<ulong> userRoleIds,
        CancellationToken cancellationToken = default);
}
