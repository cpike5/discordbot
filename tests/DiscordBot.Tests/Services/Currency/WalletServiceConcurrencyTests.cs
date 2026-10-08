using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Infrastructure.Services;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// The balance rules under contention. Each operation runs on its own thread with its own context,
/// and so its own connection, on one PostgreSQL database. A gate in front of the ledger holds every
/// operation until all of them have passed <see cref="WalletService"/>'s balance pre-check, so the
/// race the wallet lock has to settle happens on every run rather than by luck.
/// </summary>
public class WalletServiceConcurrencyTests
{
    private const ulong Payer = 111UL;
    private const ulong Payee = 222UL;
    private const ulong Moderator = 999UL;

    [Fact]
    public void SpendAsync_TwoConcurrentSpendsOfTheFullBalance_ExactlyOneSucceeds()
    {
        using var database = TestDbContextFactory.CreateDatabase();
        var (currencyId, walletId) = Seed(database, balance: 100);
        var gate = new ArrivalGate(2);
        var results = new SpendResult[2];

        ConcurrencyTestHelper.RunOnDedicatedThreads(2, index =>
        {
            using var db = database.CreateContext();
            var service = NewService(db, gate);

#pragma warning disable xUnit1031 // Dedicated thread, not a pool thread: blocking is the point.
            results[index] = service
                .SpendAsync(currencyId, Payer, 100, "feature", $"spend-{index}", Payer)
                .GetAwaiter()
                .GetResult();
#pragma warning restore xUnit1031
        });

        results.Count(r => r.Success).Should().Be(1, "only one spend of the full balance fits in it");
        results.Single(r => !r.Success).Error.Should().Be(CurrencyErrors.InsufficientFunds);
        results.Single(r => !r.Success).Balance.Should().Be(0, "the refusal reports the balance read under the lock");

        AssertWallet(database, walletId, expectedBalance: 0, expectedRows: 2);
    }

    [Fact]
    public void TransferAsync_TwoConcurrentTransfersOfTheFullBalance_ExactlyOneSucceedsAndNoHalfPairIsLeft()
    {
        using var database = TestDbContextFactory.CreateDatabase();
        var (currencyId, senderWalletId) = Seed(database, balance: 100);
        var gate = new ArrivalGate(2);
        var results = new TransferResult[2];

        ConcurrencyTestHelper.RunOnDedicatedThreads(2, index =>
        {
            using var db = database.CreateContext();
            var service = NewService(db, gate);

#pragma warning disable xUnit1031 // Dedicated thread, not a pool thread: blocking is the point.
            results[index] = service
                .TransferAsync(currencyId, Payer, Payee, 100, null, $"pay-{index}")
                .GetAwaiter()
                .GetResult();
#pragma warning restore xUnit1031
        });

        results.Count(r => r.Success).Should().Be(1);
        results.Single(r => !r.Success).Error.Should().Be(CurrencyErrors.InsufficientFunds);

        AssertWallet(database, senderWalletId, expectedBalance: 0, expectedRows: 2);

        using var verify = database.CreateContext();
        var payee = verify.Wallets.AsNoTracking().Single(w => w.CurrencyId == currencyId && w.UserId == Payee);
        var payeeRows = verify.LedgerTransactions.AsNoTracking().Where(t => t.WalletId == payee.Id).ToList();
        payeeRows.Should().ContainSingle("the refused transfer's credit half must have rolled back with its debit");
        payee.CachedBalance.Should().Be(100);
    }

    /// <summary>
    /// A fine may still take a wallet into debt, down to the currency's floor, even when a spend
    /// lands between the fine's balance read and its write. Whichever order the lock picks, the
    /// wallet ends on the floor and never below it.
    /// </summary>
    [Fact]
    public void FineAsync_RacingASpend_StillReachesTheDebtFloorAndNeverPassesIt()
    {
        using var database = TestDbContextFactory.CreateDatabase();
        var (currencyId, walletId) = Seed(database, balance: 100, allowNegative: true, debtFloor: -50);
        var gate = new ArrivalGate(2);
        SpendResult? spend = null;
        FineResult? fine = null;

        ConcurrencyTestHelper.RunOnDedicatedThreads(2, index =>
        {
            using var db = database.CreateContext();
            var service = NewService(db, gate);

#pragma warning disable xUnit1031 // Dedicated thread, not a pool thread: blocking is the point.
            if (index == 0)
            {
                spend = service.SpendAsync(currencyId, Payer, 100, "feature", "spend", Payer).GetAwaiter().GetResult();
            }
            else
            {
                fine = service.FineAsync(currencyId, Payer, 150, "spam", Moderator, null).GetAwaiter().GetResult();
            }
#pragma warning restore xUnit1031
        });

        fine!.Success.Should().BeTrue("a fine is never refused for want of funds; it is clamped");

        using var verify = database.CreateContext();
        var wallet = verify.Wallets.AsNoTracking().Single(w => w.Id == walletId);
        var rows = verify.LedgerTransactions.AsNoTracking().Where(t => t.WalletId == walletId).ToList();

        wallet.CachedBalance.Should().Be(-50, "the fine takes the wallet to the floor whichever write lands first");
        wallet.CachedBalance.Should().Be(rows.Sum(r => r.Amount));
        rows.Should().OnlyContain(r => r.BalanceAfter >= -50);

        if (spend!.Success)
        {
            fine.ClampedAmount.Should().Be(50, "after the spend only 50 of room was left above the floor");
        }
        else
        {
            spend.Error.Should().Be(CurrencyErrors.InDebt);
            fine.ClampedAmount.Should().BeNull();
        }
    }

    private static WalletService NewService(BotDbContext db, ArrivalGate gate)
    {
        var auditBuilder = new Mock<IAuditLogBuilder> { DefaultValue = DefaultValue.Mock };
        auditBuilder.SetReturnsDefault(auditBuilder.Object);
        auditBuilder.Setup(b => b.LogAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var auditLog = new Mock<IAuditLogService>();
        auditLog.Setup(a => a.CreateBuilder()).Returns(auditBuilder.Object);

        return new WalletService(
            new CurrencyRepository(db, NullLogger<CurrencyRepository>.Instance, NullLogger<Repository<Currency>>.Instance),
            new WalletRepository(db, NullLogger<WalletRepository>.Instance, NullLogger<Repository<Wallet>>.Instance),
            new GatedLedger(new LedgerRepository(db, NullLogger<LedgerRepository>.Instance), gate),
            auditLog.Object,
            NullLogger<WalletService>.Instance);
    }

    private static (Guid CurrencyId, Guid WalletId) Seed(
        TestDatabase database,
        long balance,
        bool allowNegative = false,
        long? debtFloor = null)
    {
        using var setup = database.CreateContext();

        var currency = new Currency
        {
            Id = Guid.NewGuid(),
            Scope = CurrencyScope.Guild,
            GuildId = 1001UL,
            Name = "Coins",
            Symbol = "C",
            IsActive = true,
            IsTransferable = true,
            AllowNegative = allowNegative,
            DebtFloor = debtFloor,
            CreatedById = 1UL,
            CreatedAt = DateTime.UtcNow
        };

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            CurrencyId = currency.Id,
            UserId = Payer,
            CachedBalance = balance,
            CreatedAt = DateTime.UtcNow
        };

        setup.Currencies.Add(currency);
        setup.Wallets.Add(wallet);
        setup.LedgerTransactions.Add(new LedgerTransaction
        {
            WalletId = wallet.Id,
            Type = LedgerTransactionType.Mint,
            Source = LedgerSource.Manual,
            Amount = balance,
            BalanceAfter = balance,
            Reason = "seed",
            IdempotencyKey = "seed",
            CreatedAt = DateTime.UtcNow
        });
        setup.SaveChanges();

        return (currency.Id, wallet.Id);
    }

    private static void AssertWallet(TestDatabase database, Guid walletId, long expectedBalance, int expectedRows)
    {
        using var verify = database.CreateContext();
        var wallet = verify.Wallets.AsNoTracking().Single(w => w.Id == walletId);
        var rows = verify.LedgerTransactions.AsNoTracking().Where(t => t.WalletId == walletId).ToList();

        rows.Should().HaveCount(expectedRows, "the seed plus exactly one debit");
        wallet.CachedBalance.Should().Be(expectedBalance);
        wallet.CachedBalance.Should().Be(rows.Sum(r => r.Amount), "the cache must equal the sum of the rows");
        rows.Should().OnlyContain(r => r.BalanceAfter >= 0, "no debit may take the wallet below zero");
    }

    /// <summary>
    /// Releases every participant once all of them have arrived. Waits asynchronously, so a
    /// continuation parked here never blocks a pool thread.
    /// </summary>
    private sealed class ArrivalGate
    {
        private readonly int _participants;
        private readonly TaskCompletionSource _open = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public ArrivalGate(int participants) => _participants = participants;

        public async Task ArriveAsync()
        {
            if (Interlocked.Increment(ref _arrived) >= _participants)
            {
                _open.TrySetResult();
            }

            await _open.Task.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A ledger that waits at the gate before every write. By the time any write starts, every
    /// operation has already read its balance and passed the service's pre-check.
    /// </summary>
    private sealed class GatedLedger : ILedgerRepository
    {
        private readonly ILedgerRepository _inner;
        private readonly ArrivalGate _gate;

        public GatedLedger(ILedgerRepository inner, ArrivalGate gate)
        {
            _inner = inner;
            _gate = gate;
        }

        public async Task<LedgerAppendResult> AppendAsync(
            LedgerTransaction row,
            long? minBalanceAfter = null,
            CancellationToken cancellationToken = default)
        {
            await _gate.ArriveAsync().ConfigureAwait(false);
            return await _inner.AppendAsync(row, minBalanceAfter, cancellationToken).ConfigureAwait(false);
        }

        public async Task<LedgerAppendPairResult> AppendPairAsync(
            LedgerTransaction debit,
            LedgerTransaction credit,
            long? debitMinBalanceAfter = null,
            CancellationToken cancellationToken = default)
        {
            await _gate.ArriveAsync().ConfigureAwait(false);
            return await _inner.AppendPairAsync(debit, credit, debitMinBalanceAfter, cancellationToken).ConfigureAwait(false);
        }

        public Task<LedgerTransaction?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
            _inner.GetByIdAsync(id, cancellationToken);

        public Task<LedgerTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) =>
            _inner.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);

        public Task<(IReadOnlyList<LedgerTransaction> Items, int TotalCount)> GetForWalletAsync(
            Guid walletId, int page, int pageSize, CancellationToken cancellationToken = default) =>
            _inner.GetForWalletAsync(walletId, page, pageSize, cancellationToken);

        public Task<long> SumForWalletAsync(Guid walletId, CancellationToken cancellationToken = default) =>
            _inner.SumForWalletAsync(walletId, cancellationToken);

        public Task<IReadOnlyDictionary<Guid, long>> SumByWalletForCurrencyAsync(
            Guid currencyId, CancellationToken cancellationToken = default) =>
            _inner.SumByWalletForCurrencyAsync(currencyId, cancellationToken);
    }
}
