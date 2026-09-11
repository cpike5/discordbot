using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Currency;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Tests for the in-memory hold store on its own: what it reserves, what it stops reserving, and
/// what happens when several threads reach for the same balance at once.
/// </summary>
public class ChargeHoldStoreTests : IDisposable
{
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());

    private ChargeHoldStore CreateStore(int holdExpirySeconds = 120)
    {
        var cache = new InstrumentedMemoryCache(
            _memoryCache,
            NullLogger<InstrumentedMemoryCache>.Instance,
            Options.Create(new PerformanceMetricsOptions()));

        return new ChargeHoldStore(
            cache,
            Options.Create(new CurrencyOptions { HoldExpirySeconds = holdExpirySeconds }),
            NullLogger<ChargeHoldStore>.Instance);
    }

    private static ChargeHold Hold(Guid walletId, long amount) => new()
    {
        WalletId = walletId,
        CurrencyId = Guid.NewGuid(),
        UserId = 111UL,
        GuildId = 1001UL,
        FeatureKey = "soundboard:airhorn",
        Amount = amount,
        IdempotencyKey = Guid.NewGuid().ToString("N")
    };

    [Fact]
    public void TryCreate_WithinTheBalance_ReservesTheAmount()
    {
        var store = CreateStore();
        var wallet = Guid.NewGuid();

        var hold = store.TryCreate(Hold(wallet, 5), cachedBalance: 10);

        hold.Should().NotBeNull();
        hold!.ExpiresAt.Should().BeAfter(hold.CreatedAt);
        store.SumOpenHolds(wallet).Should().Be(5);
    }

    [Fact]
    public void TryCreate_BeyondTheAvailableBalance_IsRefused()
    {
        var store = CreateStore();
        var wallet = Guid.NewGuid();

        store.TryCreate(Hold(wallet, 8), cachedBalance: 10);
        var second = store.TryCreate(Hold(wallet, 5), cachedBalance: 10);

        second.Should().BeNull();
        store.SumOpenHolds(wallet).Should().Be(8);
    }

    [Fact]
    public void Complete_StopsTheHoldReservingFunds()
    {
        var store = CreateStore();
        var wallet = Guid.NewGuid();
        var hold = store.TryCreate(Hold(wallet, 5), cachedBalance: 10)!;

        store.Complete(hold.Id, transactionId: 42);

        store.SumOpenHolds(wallet).Should().Be(0);
        store.Get(hold.Id)!.CommittedTransactionId.Should().Be(42);
    }

    [Fact]
    public void Release_DropsTheHold()
    {
        var store = CreateStore();
        var wallet = Guid.NewGuid();
        var hold = store.TryCreate(Hold(wallet, 5), cachedBalance: 10)!;

        store.Release(hold.Id);

        store.Get(hold.Id).Should().BeNull();
        store.SumOpenHolds(wallet).Should().Be(0);
    }

    [Fact]
    public void AnExpiredHold_FreesItsFundsButStaysReadable()
    {
        var store = CreateStore(holdExpirySeconds: 0);
        var wallet = Guid.NewGuid();

        var hold = store.TryCreate(Hold(wallet, 5), cachedBalance: 5)!;

        store.SumOpenHolds(wallet).Should().Be(0);
        store.Get(hold.Id).Should().NotBeNull("an expired hold must still be committable");
    }

    [Fact]
    public void TryCreate_FromManyThreadsAtOnce_ReservesTheBalanceOnlyOnce()
    {
        var store = CreateStore();
        var wallet = Guid.NewGuid();
        var granted = 0;

        // Ten threads, one coin: the store's check and write happen together, so exactly one
        // of them may hold it.
        ConcurrencyTestHelper.RunOnDedicatedThreads(10, _ =>
        {
            if (store.TryCreate(Hold(wallet, 1), cachedBalance: 1) != null)
            {
                Interlocked.Increment(ref granted);
            }
        });

        granted.Should().Be(1);
        store.SumOpenHolds(wallet).Should().Be(1);
    }

    public void Dispose() => _memoryCache.Dispose();
}
