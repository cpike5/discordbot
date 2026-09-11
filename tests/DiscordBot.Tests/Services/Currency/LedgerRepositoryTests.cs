using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Tests for the single write path into the currency system: that an append moves the cached
/// balance in step with the row it writes, that a repeated idempotency key writes nothing, and that
/// concurrent appends on one wallet cannot lose an update.
/// </summary>
public class LedgerRepositoryTests
{
    [Fact]
    public async Task AppendAsync_StampsBalanceAfterAndMovesCachedBalance()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        var wallet = await context.Wallets.GetOrCreateAsync(currency.Id, 42UL);

        var first = await context.Ledger.AppendAsync(NewRow(wallet.Id, 100, "key-1"));
        var second = await context.Ledger.AppendAsync(NewRow(wallet.Id, -30, "key-2"));

        first.WasDuplicate.Should().BeFalse();
        first.Transaction.BalanceAfter.Should().Be(100);
        second.Transaction.BalanceAfter.Should().Be(70);

        var reloaded = await context.Wallets.GetAsync(currency.Id, 42UL);
        reloaded!.CachedBalance.Should().Be(70);
        (await context.Ledger.SumForWalletAsync(wallet.Id)).Should().Be(70);
    }

    [Fact]
    public async Task AppendAsync_StampsCreatedAtWhenTheCallerLeavesItUnset()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        var wallet = await context.Wallets.GetOrCreateAsync(currency.Id, 42UL);

        var before = DateTime.UtcNow.AddSeconds(-1);
        var result = await context.Ledger.AppendAsync(NewRow(wallet.Id, 5, "key-1"));

        result.Transaction.CreatedAt.Should().BeAfter(before);
    }

    [Fact]
    public async Task AppendAsync_DuplicateIdempotencyKey_ReturnsTheExistingRowAndWritesNothing()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        var wallet = await context.Wallets.GetOrCreateAsync(currency.Id, 42UL);

        var first = await context.Ledger.AppendAsync(NewRow(wallet.Id, 100, "same-key"));
        var repeat = await context.Ledger.AppendAsync(NewRow(wallet.Id, 100, "same-key"));

        repeat.WasDuplicate.Should().BeTrue();
        repeat.Transaction.Id.Should().Be(first.Transaction.Id);

        var rows = await context.Db.LedgerTransactions.AsNoTracking().ToListAsync();
        rows.Should().ContainSingle("a repeated idempotency key must not write a second row");

        var reloaded = await context.Wallets.GetAsync(currency.Id, 42UL);
        reloaded!.CachedBalance.Should().Be(100, "the balance must not move twice for one key");
    }

    [Fact]
    public async Task AppendAsync_WithoutAnIdempotencyKey_Throws()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        var wallet = await context.Wallets.GetOrCreateAsync(currency.Id, 42UL);

        var act = () => context.Ledger.AppendAsync(NewRow(wallet.Id, 10, string.Empty));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AppendAsync_ForAWalletThatDoesNotExist_Throws()
    {
        using var context = new CurrencyTestContext();

        var act = () => context.Ledger.AppendAsync(NewRow(Guid.NewGuid(), 10, "key-1"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AppendPairAsync_WritesBothRowsPointingAtEachOther()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        var sender = await context.Wallets.GetOrCreateAsync(currency.Id, 1UL);
        var recipient = await context.Wallets.GetOrCreateAsync(currency.Id, 2UL);
        await context.Ledger.AppendAsync(NewRow(sender.Id, 50, "seed"));

        var pair = await context.Ledger.AppendPairAsync(
            NewRow(sender.Id, -20, "pay:out", LedgerTransactionType.TransferOut),
            NewRow(recipient.Id, 20, "pay:in", LedgerTransactionType.TransferIn));

        pair.Debit.ReferenceTransactionId.Should().Be(pair.Credit.Id);
        pair.Credit.ReferenceTransactionId.Should().Be(pair.Debit.Id);
        pair.Debit.BalanceAfter.Should().Be(30);
        pair.Credit.BalanceAfter.Should().Be(20);
    }

    [Fact]
    public async Task AppendPairAsync_RepeatedKeys_ReturnTheExistingPairAndWriteNothing()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        var sender = await context.Wallets.GetOrCreateAsync(currency.Id, 1UL);
        var recipient = await context.Wallets.GetOrCreateAsync(currency.Id, 2UL);
        await context.Ledger.AppendAsync(NewRow(sender.Id, 50, "seed"));

        var first = await context.Ledger.AppendPairAsync(
            NewRow(sender.Id, -20, "pay:out", LedgerTransactionType.TransferOut),
            NewRow(recipient.Id, 20, "pay:in", LedgerTransactionType.TransferIn));

        var repeat = await context.Ledger.AppendPairAsync(
            NewRow(sender.Id, -20, "pay:out", LedgerTransactionType.TransferOut),
            NewRow(recipient.Id, 20, "pay:in", LedgerTransactionType.TransferIn));

        repeat.WasDuplicate.Should().BeTrue();
        repeat.Debit.Id.Should().Be(first.Debit.Id);
        repeat.Credit.Id.Should().Be(first.Credit.Id);

        (await context.BalanceOfAsync(currency.Id, 1UL)).Should().Be(30);
        (await context.BalanceOfAsync(currency.Id, 2UL)).Should().Be(20);
    }

    [Fact]
    public async Task GetForWalletAsync_PagesNewestFirst()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        var wallet = await context.Wallets.GetOrCreateAsync(currency.Id, 42UL);

        for (var i = 1; i <= 5; i++)
        {
            await context.Ledger.AppendAsync(NewRow(wallet.Id, i, $"key-{i}"));
        }

        var (items, total) = await context.Ledger.GetForWalletAsync(wallet.Id, page: 1, pageSize: 2);

        total.Should().Be(5);
        items.Should().HaveCount(2);
        items[0].IdempotencyKey.Should().Be("key-5");
    }

    /// <summary>
    /// The invariant the cached balance exists for: whatever order concurrent appends land in, the
    /// cache must equal the sum of the rows. Each thread gets its own context on a shared,
    /// file-backed database, because the <c>:memory:</c> one lives inside a single connection.
    /// </summary>
    [Fact]
    public void AppendAsync_ConcurrentAppendsOnOneWallet_LeaveTheCacheEqualToTheSumOfRows()
    {
        const int threadCount = 8;
        const int amountPerThread = 10;

        using var database = TestDbContextFactory.CreateSharedDatabase();

        Guid currencyId;
        Guid walletId;

        using (var setup = database.CreateContext())
        {
            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Scope = CurrencyScope.Guild,
                GuildId = 1001UL,
                Name = "Coins",
                Symbol = "C",
                IsActive = true,
                CreatedById = 1UL,
                CreatedAt = DateTime.UtcNow
            };

            var wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                CurrencyId = currency.Id,
                UserId = 42UL,
                CachedBalance = 0,
                CreatedAt = DateTime.UtcNow
            };

            setup.Currencies.Add(currency);
            setup.Wallets.Add(wallet);
            setup.SaveChanges();

            currencyId = currency.Id;
            walletId = wallet.Id;
        }

        // Dedicated threads, not the pool: a barrier across pool threads starves unrelated
        // background-service tests running in parallel.
        ConcurrencyTestHelper.RunOnDedicatedThreads(threadCount, index =>
        {
            using var db = database.CreateContext();
            var ledger = new LedgerRepository(db, NullLogger<LedgerRepository>.Instance);

            // Blocking is the point: this runs on a dedicated thread, not a pool thread, and the
            // threads must genuinely overlap inside AppendAsync.
#pragma warning disable xUnit1031
            ledger.AppendAsync(NewRow(walletId, amountPerThread, $"concurrent-{index}"))
                .GetAwaiter()
                .GetResult();
#pragma warning restore xUnit1031
        });

        using var verify = database.CreateContext();
        var finalWallet = verify.Wallets.AsNoTracking().Single(w => w.Id == walletId);
        var rows = verify.LedgerTransactions.AsNoTracking().Where(t => t.WalletId == walletId).ToList();

        rows.Should().HaveCount(threadCount);
        finalWallet.CachedBalance.Should().Be(threadCount * amountPerThread);
        finalWallet.CachedBalance.Should().Be(rows.Sum(r => r.Amount));

        // Every row saw a different balance, so no append read a stale cached balance.
        rows.Select(r => r.BalanceAfter).Should().OnlyHaveUniqueItems();
        rows.Select(r => r.BalanceAfter).OrderBy(b => b)
            .Should().BeEquivalentTo(Enumerable.Range(1, threadCount).Select(i => (long)(i * amountPerThread)));

        currencyId.Should().NotBeEmpty();
    }

    private static LedgerTransaction NewRow(
        Guid walletId,
        long amount,
        string idempotencyKey,
        LedgerTransactionType type = LedgerTransactionType.Mint) => new()
    {
        WalletId = walletId,
        Type = type,
        Source = LedgerSource.Manual,
        Amount = amount,
        Reason = "test",
        IdempotencyKey = idempotencyKey
    };
}
