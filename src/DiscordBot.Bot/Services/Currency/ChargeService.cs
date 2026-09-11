using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services;

namespace DiscordBot.Bot.Services.Currency;

/// <summary>
/// Hold, commit, release, refund: the charge seam from the currency spec. Priced features talk to
/// this and to nothing else in the currency system.
/// <para>
/// The balance rules themselves belong to <see cref="IWalletService"/>; this service adds the
/// price lookup, the exemption check, and the reservation that stops two concurrent uses from
/// both passing with funds for one.
/// </para>
/// </summary>
public class ChargeService : IChargeService
{
    private readonly ICurrencyService _currencyService;
    private readonly ICurrencyRepository _currencies;
    private readonly IWalletRepository _wallets;
    private readonly IWalletService _walletService;
    private readonly ILedgerRepository _ledger;
    private readonly IChargeHoldStore _holds;
    private readonly IGuildMemberService _members;
    private readonly ILogger<ChargeService> _logger;

    public ChargeService(
        ICurrencyService currencyService,
        ICurrencyRepository currencies,
        IWalletRepository wallets,
        IWalletService walletService,
        ILedgerRepository ledger,
        IChargeHoldStore holds,
        IGuildMemberService members,
        ILogger<ChargeService> logger)
    {
        _currencyService = currencyService;
        _currencies = currencies;
        _wallets = wallets;
        _walletService = walletService;
        _ledger = ledger;
        _holds = holds;
        _members = members;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ChargeHoldResult> TryHoldAsync(
        ulong userId,
        ulong guildId,
        string featureKey,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(featureKey) || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            // A caller that cannot name the feature or key the attempt gets the free path rather
            // than a refusal: an unpriced feature is the default everywhere.
            _logger.LogWarning(
                "Charge hold asked for with a blank feature key or idempotency key in guild {GuildId}", guildId);
            return ChargeHoldResult.Free();
        }

        // 1. No active price for this feature here means the feature is free, as it was before it
        //    could be priced at all.
        var price = await _currencyService.GetActivePriceAsync(featureKey, guildId, cancellationToken);
        if (price == null)
        {
            return ChargeHoldResult.Free();
        }

        // 2. An exempt role pays nothing.
        if (price.ExemptRoleIds.Count > 0 && await HasExemptRoleAsync(guildId, userId, price.ExemptRoleIds, cancellationToken))
        {
            _logger.LogDebug(
                "User {UserId} is exempt from the price of {FeatureKey} in guild {GuildId}", userId, featureKey, guildId);
            return ChargeHoldResult.Free();
        }

        var currency = await _currencies.GetAsync(price.CurrencyId, cancellationToken);
        if (currency == null)
        {
            // A price pointing at a currency that no longer exists cannot be charged, and a
            // priced feature is not free by accident.
            _logger.LogWarning(
                "Price on {FeatureKey} in guild {GuildId} names currency {CurrencyId}, which does not exist",
                featureKey, guildId, price.CurrencyId);
            return ChargeHoldResult.Refused(ChargeHoldStatus.NoWallet, price.Amount, 0, null);
        }

        if (!currency.IsActive)
        {
            return ChargeHoldResult.Refused(ChargeHoldStatus.CurrencyInactive, price.Amount, 0, currency.Symbol);
        }

        // 3. No wallet is the same as a balance of zero, so nothing is created until there is
        //    something to write.
        var wallet = await _wallets.GetAsync(price.CurrencyId, userId, cancellationToken);
        var balance = wallet?.CachedBalance ?? 0;

        if (balance < 0)
        {
            return ChargeHoldResult.Refused(ChargeHoldStatus.InDebt, price.Amount, balance, currency.Symbol);
        }

        if (wallet == null || balance < price.Amount)
        {
            return ChargeHoldResult.Refused(ChargeHoldStatus.InsufficientFunds, price.Amount, balance, currency.Symbol);
        }

        // 4 and 5. Available balance is the cached balance less the user's own open holds, and the
        //    store checks it again as it writes so two concurrent plays cannot both pass.
        var hold = _holds.TryCreate(
            new ChargeHold
            {
                WalletId = wallet.Id,
                CurrencyId = currency.Id,
                UserId = userId,
                GuildId = guildId,
                FeatureKey = featureKey,
                Amount = price.Amount,
                IdempotencyKey = idempotencyKey
            },
            balance);

        if (hold == null)
        {
            return ChargeHoldResult.Refused(
                ChargeHoldStatus.InsufficientFunds,
                price.Amount,
                balance - _holds.SumOpenHolds(wallet.Id),
                currency.Symbol);
        }

        return new ChargeHoldResult
        {
            Status = ChargeHoldStatus.Held,
            HoldId = hold.Id,
            Price = price.Amount,
            Balance = balance,
            CurrencySymbol = currency.Symbol
        };
    }

    /// <inheritdoc />
    public async Task<ChargeCommitResult> CommitAsync(Guid holdId, CancellationToken cancellationToken = default)
    {
        var hold = _holds.Get(holdId);
        if (hold == null)
        {
            // The hold aged out of the store entirely. Nothing was written, so nothing is owed.
            _logger.LogWarning("Commit asked for hold {HoldId}, which is no longer in the store", holdId);
            return ChargeCommitResult.Failed(CurrencyErrors.HoldNotFound);
        }

        if (hold.CommittedTransactionId.HasValue)
        {
            // Already committed. Hand back the row that was written rather than writing a second.
            var existing = await _ledger.GetByIdAsync(hold.CommittedTransactionId.Value, cancellationToken);
            return new ChargeCommitResult
            {
                Success = existing != null,
                Error = existing == null ? CurrencyErrors.ReferenceNotFound : null,
                Transaction = existing?.ToDto(),
                Balance = existing?.BalanceAfter ?? 0,
                WasDuplicate = true
            };
        }

        // The spend rules live in the wallet service; an expired reservation does not change them,
        // so a slow feature still pays for what it used.
        var spend = await _walletService.SpendAsync(
            hold.CurrencyId,
            hold.UserId,
            hold.Amount,
            hold.FeatureKey,
            hold.IdempotencyKey,
            hold.UserId,
            cancellationToken);

        if (!spend.Success)
        {
            // The funds went somewhere else between the hold and the commit. The reservation is no
            // longer useful to anyone.
            _holds.Release(holdId);
            return ChargeCommitResult.Failed(spend.Error ?? CurrencyErrors.InsufficientFunds);
        }

        _holds.Complete(holdId, spend.Transaction!.Id);

        _logger.LogInformation(
            "Charged user {UserId} {Amount} for {FeatureKey} in guild {GuildId}",
            hold.UserId, hold.Amount, hold.FeatureKey, hold.GuildId);

        return new ChargeCommitResult
        {
            Success = true,
            Transaction = spend.Transaction,
            Balance = spend.Balance,
            WasDuplicate = spend.WasDuplicate
        };
    }

    /// <inheritdoc />
    public Task ReleaseAsync(Guid holdId, CancellationToken cancellationToken = default)
    {
        _holds.Release(holdId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<RefundResult> RefundAsync(
        long spendTransactionId,
        string reason,
        ulong? actorId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return RefundResult.Failed(CurrencyErrors.ReasonRequired);
        }

        var spend = await _ledger.GetByIdAsync(spendTransactionId, cancellationToken);
        if (spend == null)
        {
            return RefundResult.Failed(CurrencyErrors.ReferenceNotFound);
        }

        if (spend.Type != LedgerTransactionType.Spend)
        {
            return RefundResult.Failed(CurrencyErrors.NotRefundable);
        }

        var wallet = await _wallets.GetAsync(spend.WalletId, cancellationToken);
        if (wallet == null)
        {
            return RefundResult.Failed(CurrencyErrors.WalletNotFound);
        }

        var currency = await _currencies.GetAsync(wallet.CurrencyId, cancellationToken);
        if (currency == null)
        {
            return RefundResult.Failed(CurrencyErrors.CurrencyNotFound);
        }

        if (!currency.IsActive)
        {
            return RefundResult.Failed(CurrencyErrors.CurrencyInactive);
        }

        // The key is derived from the spend, so a second refund of the same spend is refused by
        // the ledger's idempotency invariant rather than by a lookup that could race.
        var appended = await _ledger.AppendAsync(
            new LedgerTransaction
            {
                WalletId = wallet.Id,
                Type = LedgerTransactionType.Refund,
                Source = LedgerSource.Manual,
                Amount = Math.Abs(spend.Amount),
                Reason = reason.Trim(),
                FeatureKey = spend.FeatureKey,
                IdempotencyKey = $"refund:{spendTransactionId}",
                ReferenceTransactionId = spendTransactionId,
                ActorId = actorId
            },
            cancellationToken);

        if (appended.WasDuplicate)
        {
            return RefundResult.Failed(CurrencyErrors.AlreadyRefunded);
        }

        _logger.LogInformation(
            "Refunded {Amount} on wallet {WalletId} for spend {SpendId}: {Reason}",
            appended.Transaction.Amount, wallet.Id, spendTransactionId, reason);

        return new RefundResult { Success = true, Transaction = appended.Transaction.ToDto() };
    }

    /// <summary>
    /// Whether the user holds any of the roles the price exempts. Roles come from the synced guild
    /// member record, the same source the portal reads.
    /// </summary>
    private async Task<bool> HasExemptRoleAsync(
        ulong guildId,
        ulong userId,
        IReadOnlyList<ulong> exemptRoleIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var member = await _members.GetMemberAsync(guildId, userId, cancellationToken);
            return member != null && member.RoleIds.Any(exemptRoleIds.Contains);
        }
        catch (Exception ex)
        {
            // An exemption that cannot be read is not an exemption: the user pays, which is the
            // safe direction for the ledger.
            _logger.LogError(ex, "Could not read roles for user {UserId} in guild {GuildId}", userId, guildId);
            return false;
        }
    }
}
