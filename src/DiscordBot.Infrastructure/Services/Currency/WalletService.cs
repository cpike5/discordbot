using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services;

/// <summary>
/// Wallet reads plus the balance rules from the currency spec. Every write leaves through
/// <see cref="ILedgerRepository"/>, which owns the idempotency key and the cached balance; this
/// service decides what is allowed before handing a row over.
/// </summary>
public class WalletService : IWalletService
{
    private readonly ICurrencyRepository _currencies;
    private readonly IWalletRepository _wallets;
    private readonly ILedgerRepository _ledger;
    private readonly ILogger<WalletService> _logger;

    public WalletService(
        ICurrencyRepository currencies,
        IWalletRepository wallets,
        ILedgerRepository ledger,
        ILogger<WalletService> logger)
    {
        _currencies = currencies;
        _wallets = wallets;
        _ledger = ledger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WalletDto?> GetWalletAsync(Guid currencyId, ulong userId, CancellationToken cancellationToken = default)
    {
        var wallet = await _wallets.GetAsync(currencyId, userId, cancellationToken);
        return wallet?.ToDto();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WalletDto>> GetWalletsForUserAsync(ulong userId, ulong? guildId, CancellationToken cancellationToken = default)
    {
        var wallets = await _wallets.GetForUserAsync(userId, guildId, cancellationToken);
        return wallets.Select(w => w.ToDto()).ToList();
    }

    /// <inheritdoc />
    public async Task<MintResult> MintAsync(
        Guid currencyId,
        ulong toUserId,
        long amount,
        string reason,
        LedgerSource source,
        string idempotencyKey,
        ulong? actorId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return MintResult.Failed(CurrencyErrors.IdempotencyKeyRequired);
        }

        if (amount <= 0)
        {
            return MintResult.Failed(CurrencyErrors.InvalidAmount);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return MintResult.Failed(CurrencyErrors.ReasonRequired);
        }

        var currency = await _currencies.GetAsync(currencyId, cancellationToken);
        if (currency == null)
        {
            return MintResult.Failed(CurrencyErrors.CurrencyNotFound);
        }

        if (!currency.IsActive)
        {
            return MintResult.Failed(CurrencyErrors.CurrencyInactive);
        }

        // A mint moves the balance up from anywhere, debt included, so there is nothing else to
        // check: it is the only source of units.
        var wallet = await _wallets.GetOrCreateAsync(currencyId, toUserId, cancellationToken);

        var appended = await _ledger.AppendAsync(
            new LedgerTransaction
            {
                WalletId = wallet.Id,
                Type = LedgerTransactionType.Mint,
                Source = source,
                Amount = amount,
                Reason = reason.Trim(),
                IdempotencyKey = idempotencyKey,
                ActorId = actorId
            },
            cancellationToken);

        _logger.LogInformation(
            "Minted {Amount} of currency {CurrencyId} to user {UserId} (source {Source}, duplicate {Duplicate})",
            amount, currencyId, toUserId, source, appended.WasDuplicate);

        return new MintResult
        {
            Success = true,
            Transaction = appended.Transaction.ToDto(),
            WasDuplicate = appended.WasDuplicate
        };
    }

    /// <inheritdoc />
    public async Task<SpendResult> SpendAsync(
        Guid currencyId,
        ulong userId,
        long amount,
        string featureKey,
        string idempotencyKey,
        ulong? actorId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return SpendResult.Failed(CurrencyErrors.IdempotencyKeyRequired);
        }

        if (amount <= 0)
        {
            return SpendResult.Failed(CurrencyErrors.InvalidAmount);
        }

        var currency = await _currencies.GetAsync(currencyId, cancellationToken);
        if (currency == null)
        {
            return SpendResult.Failed(CurrencyErrors.CurrencyNotFound);
        }

        if (!currency.IsActive)
        {
            return SpendResult.Failed(CurrencyErrors.CurrencyInactive);
        }

        // No wallet is the same as a balance of zero.
        var existing = await _wallets.GetAsync(currencyId, userId, cancellationToken);
        var balance = existing?.CachedBalance ?? 0;

        // A wallet in debt is locked out of priced features until it is back above zero, and a
        // spend never creates debt.
        if (balance < 0)
        {
            return SpendResult.Failed(CurrencyErrors.InDebt, balance);
        }

        if (balance < amount)
        {
            return SpendResult.Failed(CurrencyErrors.InsufficientFunds, balance);
        }

        var wallet = existing ?? await _wallets.GetOrCreateAsync(currencyId, userId, cancellationToken);

        var appended = await _ledger.AppendAsync(
            new LedgerTransaction
            {
                WalletId = wallet.Id,
                Type = LedgerTransactionType.Spend,
                Source = LedgerSource.Manual,
                Amount = -amount,
                FeatureKey = featureKey,
                IdempotencyKey = idempotencyKey,
                ActorId = actorId
            },
            cancellationToken);

        return new SpendResult
        {
            Success = true,
            Transaction = appended.Transaction.ToDto(),
            Balance = appended.Transaction.BalanceAfter,
            WasDuplicate = appended.WasDuplicate
        };
    }

    /// <inheritdoc />
    public async Task<TransferResult> TransferAsync(
        Guid currencyId,
        ulong fromUserId,
        ulong toUserId,
        long amount,
        string? note,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return TransferResult.Failed(CurrencyErrors.IdempotencyKeyRequired);
        }

        if (amount <= 0)
        {
            return TransferResult.Failed(CurrencyErrors.InvalidAmount);
        }

        if (fromUserId == toUserId)
        {
            return TransferResult.Failed(CurrencyErrors.SelfTransfer);
        }

        var currency = await _currencies.GetAsync(currencyId, cancellationToken);
        if (currency == null)
        {
            return TransferResult.Failed(CurrencyErrors.CurrencyNotFound);
        }

        if (!currency.IsActive)
        {
            return TransferResult.Failed(CurrencyErrors.CurrencyInactive);
        }

        if (!currency.IsTransferable)
        {
            return TransferResult.Failed(CurrencyErrors.TransfersDisabled);
        }

        var sender = await _wallets.GetAsync(currencyId, fromUserId, cancellationToken);
        var senderBalance = sender?.CachedBalance ?? 0;

        // TransferOut follows the spend rules: never below zero, and refused outright while in debt.
        if (senderBalance < 0)
        {
            return TransferResult.Failed(CurrencyErrors.InDebt, senderBalance);
        }

        if (senderBalance < amount)
        {
            return TransferResult.Failed(CurrencyErrors.InsufficientFunds, senderBalance);
        }

        sender ??= await _wallets.GetOrCreateAsync(currencyId, fromUserId, cancellationToken);
        var recipient = await _wallets.GetOrCreateAsync(currencyId, toUserId, cancellationToken);

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        var pair = await _ledger.AppendPairAsync(
            new LedgerTransaction
            {
                WalletId = sender.Id,
                Type = LedgerTransactionType.TransferOut,
                Source = LedgerSource.Manual,
                Amount = -amount,
                Reason = trimmedNote,
                IdempotencyKey = $"{idempotencyKey}:out",
                ActorId = fromUserId
            },
            new LedgerTransaction
            {
                WalletId = recipient.Id,
                Type = LedgerTransactionType.TransferIn,
                Source = LedgerSource.Manual,
                Amount = amount,
                Reason = trimmedNote,
                IdempotencyKey = $"{idempotencyKey}:in",
                ActorId = fromUserId
            },
            cancellationToken);

        _logger.LogInformation(
            "Transferred {Amount} of currency {CurrencyId} from {FromUserId} to {ToUserId} (duplicate {Duplicate})",
            amount, currencyId, fromUserId, toUserId, pair.WasDuplicate);

        return new TransferResult
        {
            Success = true,
            Transaction = pair.Debit.ToDto(),
            CounterpartTransaction = pair.Credit.ToDto(),
            SenderBalance = pair.Debit.BalanceAfter,
            WasDuplicate = pair.WasDuplicate
        };
    }

    /// <inheritdoc />
    public async Task<FineResult> FineAsync(
        Guid currencyId,
        ulong userId,
        long amount,
        string reason,
        ulong moderatorId,
        Guid? moderationCaseId,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            return FineResult.Failed(CurrencyErrors.InvalidAmount);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return FineResult.Failed(CurrencyErrors.ReasonRequired);
        }

        var currency = await _currencies.GetAsync(currencyId, cancellationToken);
        if (currency == null)
        {
            return FineResult.Failed(CurrencyErrors.CurrencyNotFound);
        }

        if (!currency.IsActive)
        {
            return FineResult.Failed(CurrencyErrors.CurrencyInactive);
        }

        // Fines are a guild moderation action. Nobody fines in bot credit.
        if (currency.Scope != CurrencyScope.Guild)
        {
            return FineResult.Failed(CurrencyErrors.FineRequiresGuildCurrency);
        }

        var wallet = await _wallets.GetOrCreateAsync(currencyId, userId, cancellationToken);
        var balance = wallet.CachedBalance;

        // A fine stops at zero, or at the currency's debt floor when it allows debt. Either way
        // the clamped amount is still written, so the ledger shows what actually happened.
        var floor = currency.AllowNegative ? currency.DebtFloor ?? 0 : 0;
        var room = Math.Max(0, balance - floor);
        var effective = Math.Min(amount, room);
        var clamped = effective == amount ? (long?)null : effective;

        var appended = await _ledger.AppendAsync(
            new LedgerTransaction
            {
                WalletId = wallet.Id,
                Type = LedgerTransactionType.Fine,
                Source = LedgerSource.Manual,
                Amount = -effective,
                Reason = reason.Trim(),
                IdempotencyKey = BuildFineKey(currencyId, userId, moderatorId),
                ModerationCaseId = moderationCaseId,
                ActorId = moderatorId
            },
            cancellationToken);

        _logger.LogInformation(
            "Fined user {UserId} {Amount} of currency {CurrencyId} by moderator {ModeratorId} (clamped to {Effective})",
            userId, amount, currencyId, moderatorId, effective);

        return new FineResult
        {
            Success = true,
            Transaction = appended.Transaction.ToDto(),
            ClampedAmount = clamped,
            WasDuplicate = appended.WasDuplicate
        };
    }

    /// <inheritdoc />
    public async Task<AdjustmentResult> AdjustAsync(
        long referenceTransactionId,
        long amount,
        string reason,
        ulong actorId,
        CancellationToken cancellationToken = default)
    {
        if (amount == 0)
        {
            return AdjustmentResult.Failed(CurrencyErrors.InvalidAmount);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return AdjustmentResult.Failed(CurrencyErrors.ReasonRequired);
        }

        var reference = await _ledger.GetByIdAsync(referenceTransactionId, cancellationToken);
        if (reference == null)
        {
            return AdjustmentResult.Failed(CurrencyErrors.ReferenceNotFound);
        }

        var wallet = await _wallets.GetAsync(reference.WalletId, cancellationToken);
        if (wallet == null)
        {
            return AdjustmentResult.Failed(CurrencyErrors.WalletNotFound);
        }

        var currency = await _currencies.GetAsync(wallet.CurrencyId, cancellationToken);
        if (currency == null)
        {
            return AdjustmentResult.Failed(CurrencyErrors.CurrencyNotFound);
        }

        if (!currency.IsActive)
        {
            return AdjustmentResult.Failed(CurrencyErrors.CurrencyInactive);
        }

        // An adjustment is the correction of record: it may move either direction and is not
        // clamped, because the mistake it fixes may itself have been a clamp.
        var appended = await _ledger.AppendAsync(
            new LedgerTransaction
            {
                WalletId = wallet.Id,
                Type = LedgerTransactionType.Adjustment,
                Source = LedgerSource.Manual,
                Amount = amount,
                Reason = reason.Trim(),
                IdempotencyKey = $"adjust:{referenceTransactionId}:{Guid.NewGuid():N}",
                ReferenceTransactionId = referenceTransactionId,
                ActorId = actorId
            },
            cancellationToken);

        _logger.LogWarning(
            "Adjusted transaction {ReferenceId} by {Amount} on wallet {WalletId} by actor {ActorId}: {Reason}",
            referenceTransactionId, amount, wallet.Id, actorId, reason);

        return new AdjustmentResult
        {
            Success = true,
            Transaction = appended.Transaction.ToDto(),
            WasDuplicate = appended.WasDuplicate
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<LedgerTransactionDto>> GetHistoryAsync(
        Guid walletId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var (items, totalCount) = await _ledger.GetForWalletAsync(walletId, page, pageSize, cancellationToken);

        return new PagedResult<LedgerTransactionDto>
        {
            Items = items.Select(t => t.ToDto()).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Fines have no caller-supplied key: a moderator issuing the same fine twice means it twice.
    /// The key is still unique so the ledger's single write path stays intact.
    /// </summary>
    private static string BuildFineKey(Guid currencyId, ulong userId, ulong moderatorId) =>
        $"fine:{currencyId:N}:{userId}:{moderatorId}:{Guid.NewGuid():N}";
}
