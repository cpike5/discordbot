using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services.Currency;

/// <summary>
/// In-process hold store over <see cref="IInstrumentedCache"/>, keyed under
/// <c>currency:hold:</c> so the cache dashboard counts holds as their own prefix.
/// <para>
/// Two things live in the cache: a hold by its id, and a per-wallet index of hold ids so open
/// holds on one wallet can be summed without enumerating the cache. Reads prune the index, so an
/// entry that aged out costs nothing.
/// </para>
/// <para>
/// Entries outlive their reservation. <see cref="ChargeHold.ExpiresAt"/> is when a hold stops
/// counting against the wallet; the cache keeps it around for longer so a feature that took longer
/// than the expiry can still commit what it reserved.
/// </para>
/// </summary>
public class ChargeHoldStore : IChargeHoldStore
{
    /// <summary>Cache key prefix, shared by both entry kinds so metrics group them together.</summary>
    public const string KeyPrefix = "currency:hold:";

    /// <summary>How many times the reservation window an entry stays cached for.</summary>
    private const int RetentionMultiplier = 10;

    /// <summary>Floor on the cache lifetime, so a short expiry still leaves room to commit.</summary>
    private static readonly TimeSpan MinimumRetention = TimeSpan.FromMinutes(5);

    private readonly IInstrumentedCache _cache;
    private readonly CurrencyOptions _options;
    private readonly ILogger<ChargeHoldStore> _logger;

    /// <summary>
    /// Serializes the sum-then-store sequence. Without it two plays priced at the last coin could
    /// both read the same available balance and both be held.
    /// </summary>
    private readonly object _gate = new();

    public ChargeHoldStore(
        IInstrumentedCache cache,
        IOptions<CurrencyOptions> options,
        ILogger<ChargeHoldStore> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public ChargeHold? TryCreate(ChargeHold hold, long cachedBalance)
    {
        var now = DateTime.UtcNow;

        var stored = hold with
        {
            CreatedAt = now,
            ExpiresAt = now.AddSeconds(Math.Max(0, _options.HoldExpirySeconds)),
            CommittedTransactionId = null
        };

        lock (_gate)
        {
            var open = SumOpenHoldsLocked(hold.WalletId, now, out var liveHoldIds);
            var available = cachedBalance - open;

            if (available < hold.Amount)
            {
                _logger.LogDebug(
                    "Hold refused on wallet {WalletId}: {Available} available ({Balance} cached less {Open} held) against {Amount}",
                    hold.WalletId, available, cachedBalance, open, hold.Amount);
                return null;
            }

            liveHoldIds.Add(stored.Id);
            _cache.Set(HoldKey(stored.Id), stored, Retention);
            _cache.Set(IndexKey(stored.WalletId), liveHoldIds, Retention);
        }

        _logger.LogDebug(
            "Held {Amount} on wallet {WalletId} for {FeatureKey} until {ExpiresAt:O}",
            stored.Amount, stored.WalletId, stored.FeatureKey, stored.ExpiresAt);

        return stored;
    }

    /// <inheritdoc />
    public ChargeHold? Get(Guid holdId) =>
        _cache.TryGetValue<ChargeHold>(HoldKey(holdId), out var hold) ? hold : null;

    /// <inheritdoc />
    public long SumOpenHolds(Guid walletId)
    {
        lock (_gate)
        {
            return SumOpenHoldsLocked(walletId, DateTime.UtcNow, out _);
        }
    }

    /// <inheritdoc />
    public void Complete(Guid holdId, long transactionId)
    {
        lock (_gate)
        {
            if (!_cache.TryGetValue<ChargeHold>(HoldKey(holdId), out var hold) || hold == null)
            {
                return;
            }

            // The hold stays cached so a retried commit finds the row it already wrote, but with a
            // transaction id on it, it reserves nothing.
            _cache.Set(HoldKey(holdId), hold with { CommittedTransactionId = transactionId }, Retention);
        }
    }

    /// <inheritdoc />
    public void Release(Guid holdId)
    {
        lock (_gate)
        {
            if (!_cache.TryGetValue<ChargeHold>(HoldKey(holdId), out var hold) || hold == null)
            {
                return;
            }

            _cache.Remove(HoldKey(holdId));

            if (_cache.TryGetValue<List<Guid>>(IndexKey(hold.WalletId), out var index) && index != null)
            {
                index.Remove(holdId);
                _cache.Set(IndexKey(hold.WalletId), index, Retention);
            }
        }
    }

    /// <summary>
    /// Sums the amounts still reserved on a wallet and hands back the pruned index, so a caller
    /// that is about to add a hold does not walk the list twice.
    /// </summary>
    private long SumOpenHoldsLocked(Guid walletId, DateTime now, out List<Guid> liveHoldIds)
    {
        liveHoldIds = new List<Guid>();

        if (!_cache.TryGetValue<List<Guid>>(IndexKey(walletId), out var index) || index == null)
        {
            return 0;
        }

        var total = 0L;

        foreach (var id in index)
        {
            if (!_cache.TryGetValue<ChargeHold>(HoldKey(id), out var hold) || hold == null)
            {
                continue;
            }

            // A committed hold is already in the ledger and an expired one has given its funds
            // back, but both stay in the index while their cache entry lives.
            liveHoldIds.Add(id);

            if (hold.CommittedTransactionId.HasValue || hold.IsExpired(now))
            {
                continue;
            }

            total += hold.Amount;
        }

        return total;
    }

    private TimeSpan Retention
    {
        get
        {
            var window = TimeSpan.FromSeconds(Math.Max(0, _options.HoldExpirySeconds) * RetentionMultiplier);
            return window > MinimumRetention ? window : MinimumRetention;
        }
    }

    private static string HoldKey(Guid holdId) => $"{KeyPrefix}{holdId:N}";

    private static string IndexKey(Guid walletId) => $"{KeyPrefix}wallet:{walletId:N}";
}
