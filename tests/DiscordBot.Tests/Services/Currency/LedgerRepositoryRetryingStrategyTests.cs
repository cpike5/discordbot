using System.Data.Common;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Runs the ledger's write path on a context configured the way the application configures it:
/// Npgsql with <c>EnableRetryOnFailure</c>. The shared test contexts do not retry, which is how a
/// "does not support user-initiated transactions" failure on mint, fine and adjust got past the
/// suite.
/// </summary>
public class LedgerRepositoryRetryingStrategyTests
{
    /// <summary>
    /// Mirrors the Npgsql block in <c>ServiceCollectionExtensions.AddInfrastructure</c>. Keep the
    /// two in step.
    /// </summary>
    private static DbContextOptions<PostgresBotDbContext> RetryingOptions(
        TestDatabase database,
        params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<PostgresBotDbContext>()
            .UseNpgsql(database.ConnectionString, npgsql =>
            {
                npgsql.MigrationsAssembly("DiscordBot.Infrastructure");
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            });

        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        return builder.Options;
    }

    private static async Task<(Guid CurrencyId, Wallet Sender, Wallet Recipient)> SeedAsync(TestDatabase database)
    {
        // Seeded through a plain context: only the ledger write path is under test.
        await using var seed = database.CreateContext();

        var currency = new Currency
        {
            Id = Guid.NewGuid(),
            Scope = CurrencyScope.Guild,
            GuildId = 1001UL,
            Name = "Coins",
            Symbol = "C",
            IsTransferable = true,
            IsActive = true,
            CreatedById = 9000UL,
            CreatedAt = DateTime.UtcNow
        };
        var sender = new Wallet { Id = Guid.NewGuid(), CurrencyId = currency.Id, UserId = 1UL, CreatedAt = DateTime.UtcNow };
        var recipient = new Wallet { Id = Guid.NewGuid(), CurrencyId = currency.Id, UserId = 2UL, CreatedAt = DateTime.UtcNow };

        seed.Currencies.Add(currency);
        seed.Wallets.AddRange(sender, recipient);
        await seed.SaveChangesAsync();

        return (currency.Id, sender, recipient);
    }

    private static LedgerRepository NewRepository(BotDbContext context) =>
        new(context, NullLogger<LedgerRepository>.Instance);

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

    [Fact]
    public async Task AppendAsync_WithRetryOnFailureConfigured_WritesExactlyOneRow()
    {
        using var database = TestDbContextFactory.CreateDatabase();
        var (_, sender, _) = await SeedAsync(database);

        await using var context = new PostgresBotDbContext(RetryingOptions(database));
        var ledger = NewRepository(context);

        var result = await ledger.AppendAsync(NewRow(sender.Id, 100, "mint:1"));

        result.WasDuplicate.Should().BeFalse();
        result.Transaction.BalanceAfter.Should().Be(100);

        await using var verify = database.CreateContext();
        (await verify.LedgerTransactions.CountAsync(t => t.WalletId == sender.Id)).Should().Be(1);
        (await verify.Wallets.SingleAsync(w => w.Id == sender.Id)).CachedBalance.Should().Be(100);
    }

    [Fact]
    public async Task AppendPairAsync_WithRetryOnFailureConfigured_WritesExactlyOneRowPerSide()
    {
        using var database = TestDbContextFactory.CreateDatabase();
        var (_, sender, recipient) = await SeedAsync(database);

        await using var context = new PostgresBotDbContext(RetryingOptions(database));
        var ledger = NewRepository(context);
        await ledger.AppendAsync(NewRow(sender.Id, 50, "seed"));

        var pair = await ledger.AppendPairAsync(
            NewRow(sender.Id, -20, "pay:out", LedgerTransactionType.TransferOut),
            NewRow(recipient.Id, 20, "pay:in", LedgerTransactionType.TransferIn));

        pair.WasDuplicate.Should().BeFalse();
        pair.Debit.ReferenceTransactionId.Should().Be(pair.Credit.Id);
        pair.Credit.ReferenceTransactionId.Should().Be(pair.Debit.Id);

        await using var verify = database.CreateContext();
        (await verify.LedgerTransactions.CountAsync(t => t.IdempotencyKey == "pay:out")).Should().Be(1);
        (await verify.LedgerTransactions.CountAsync(t => t.IdempotencyKey == "pay:in")).Should().Be(1);
        (await verify.Wallets.SingleAsync(w => w.Id == sender.Id)).CachedBalance.Should().Be(30);
        (await verify.Wallets.SingleAsync(w => w.Id == recipient.Id)).CachedBalance.Should().Be(20);
    }

    [Fact]
    public async Task AppendAsync_WhenTheFirstCommitFailsTransiently_RetriesTheWholeUnitWithoutDoubleAppending()
    {
        using var database = TestDbContextFactory.CreateDatabase();
        var (_, sender, _) = await SeedAsync(database);

        var failer = new FailFirstCommitInterceptor();
        await using var context = new PostgresBotDbContext(RetryingOptions(database, failer));
        var ledger = NewRepository(context);

        var result = await ledger.AppendAsync(NewRow(sender.Id, 100, "mint:retry"));

        failer.Commits.Should().Be(2, "the first commit fails and the retry's commit succeeds");
        result.Transaction.Id.Should().BeGreaterThan(0);
        result.Transaction.BalanceAfter.Should().Be(100);

        await using var verify = database.CreateContext();
        (await verify.LedgerTransactions.CountAsync(t => t.WalletId == sender.Id)).Should().Be(1);
        (await verify.Wallets.SingleAsync(w => w.Id == sender.Id)).CachedBalance.Should().Be(100);
    }

    [Fact]
    public async Task AppendPairAsync_WhenTheFirstCommitFailsTransiently_RetriesTheWholeUnitWithoutDoubleAppending()
    {
        using var database = TestDbContextFactory.CreateDatabase();
        var (_, sender, recipient) = await SeedAsync(database);

        await using (var seedContext = new PostgresBotDbContext(RetryingOptions(database)))
        {
            await NewRepository(seedContext).AppendAsync(NewRow(sender.Id, 50, "seed"));
        }

        var failer = new FailFirstCommitInterceptor();
        await using var context = new PostgresBotDbContext(RetryingOptions(database, failer));
        var ledger = NewRepository(context);

        var pair = await ledger.AppendPairAsync(
            NewRow(sender.Id, -20, "pay:out", LedgerTransactionType.TransferOut),
            NewRow(recipient.Id, 20, "pay:in", LedgerTransactionType.TransferIn));

        failer.Commits.Should().Be(2, "the first commit fails and the retry's commit succeeds");
        pair.Debit.ReferenceTransactionId.Should().Be(pair.Credit.Id);
        pair.Credit.ReferenceTransactionId.Should().Be(pair.Debit.Id);

        await using var verify = database.CreateContext();
        (await verify.LedgerTransactions.CountAsync(t => t.IdempotencyKey == "pay:out")).Should().Be(1);
        (await verify.LedgerTransactions.CountAsync(t => t.IdempotencyKey == "pay:in")).Should().Be(1);
        (await verify.Wallets.SingleAsync(w => w.Id == sender.Id)).CachedBalance.Should().Be(30);
        (await verify.Wallets.SingleAsync(w => w.Id == recipient.Id)).CachedBalance.Should().Be(20);
    }

    [Fact]
    public async Task AppendAsync_WhenTheCommitSucceededButItsOutcomeIsLost_ReportsTheRetryAsItsOwnWrite()
    {
        using var database = TestDbContextFactory.CreateDatabase();
        var (_, sender, _) = await SeedAsync(database);

        var failer = new LoseFirstCommitOutcomeInterceptor();
        await using var context = new PostgresBotDbContext(RetryingOptions(database, failer));
        var ledger = NewRepository(context);

        var result = await ledger.AppendAsync(NewRow(sender.Id, 100, "refund:1"));

        failer.Commits.Should().Be(2, "the first commit lands but reports failure, so the unit runs again");
        result.WasDuplicate.Should().BeFalse("the rows the retry found are this call's own earlier write");
        result.Transaction.IdempotencyKey.Should().Be("refund:1");
        result.Transaction.BalanceAfter.Should().Be(100);

        await using var verify = database.CreateContext();
        (await verify.LedgerTransactions.CountAsync(t => t.WalletId == sender.Id)).Should().Be(1);
        (await verify.Wallets.SingleAsync(w => w.Id == sender.Id)).CachedBalance.Should().Be(100);
    }

    [Fact]
    public async Task AppendPairAsync_WhenTheCommitSucceededButItsOutcomeIsLost_ReportsTheRetryAsItsOwnWrite()
    {
        using var database = TestDbContextFactory.CreateDatabase();
        var (_, sender, recipient) = await SeedAsync(database);

        await using (var seedContext = new PostgresBotDbContext(RetryingOptions(database)))
        {
            await NewRepository(seedContext).AppendAsync(NewRow(sender.Id, 50, "seed"));
        }

        var failer = new LoseFirstCommitOutcomeInterceptor();
        await using var context = new PostgresBotDbContext(RetryingOptions(database, failer));
        var ledger = NewRepository(context);

        var pair = await ledger.AppendPairAsync(
            NewRow(sender.Id, -20, "pay:out", LedgerTransactionType.TransferOut),
            NewRow(recipient.Id, 20, "pay:in", LedgerTransactionType.TransferIn));

        failer.Commits.Should().Be(2);
        pair.WasDuplicate.Should().BeFalse("the rows the retry found are this call's own earlier write");
        pair.Debit.ReferenceTransactionId.Should().Be(pair.Credit.Id);
        pair.Credit.ReferenceTransactionId.Should().Be(pair.Debit.Id);

        await using var verify = database.CreateContext();
        (await verify.LedgerTransactions.CountAsync(t => t.IdempotencyKey == "pay:out")).Should().Be(1);
        (await verify.LedgerTransactions.CountAsync(t => t.IdempotencyKey == "pay:in")).Should().Be(1);
        (await verify.Wallets.SingleAsync(w => w.Id == sender.Id)).CachedBalance.Should().Be(30);
        (await verify.Wallets.SingleAsync(w => w.Id == recipient.Id)).CachedBalance.Should().Be(20);
    }

    [Fact]
    public async Task AppendAsync_WhenTheKeyWasWrittenByAnEarlierCall_StillReportsADuplicate()
    {
        using var database = TestDbContextFactory.CreateDatabase();
        var (_, sender, _) = await SeedAsync(database);

        await using var context = new PostgresBotDbContext(RetryingOptions(database));
        var ledger = NewRepository(context);
        await ledger.AppendAsync(NewRow(sender.Id, 100, "refund:2"));

        var again = await ledger.AppendAsync(NewRow(sender.Id, 100, "refund:2"));

        again.WasDuplicate.Should().BeTrue();
    }

    /// <summary>
    /// Lets the first commit land, then fails it with the kind of error the Npgsql retry strategy
    /// treats as transient: the commit happened, but the caller is told it did not.
    /// </summary>
    private sealed class LoseFirstCommitOutcomeInterceptor : DbTransactionInterceptor
    {
        private int _commits;

        /// <summary>Commits attempted so far, failed and successful.</summary>
        public int Commits => _commits;

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _commits);
            return ValueTask.FromResult(result);
        }

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (_commits == 1)
            {
                throw new NpgsqlException("simulated lost commit outcome", new IOException("simulated"));
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Fails the first commit with the kind of error the Npgsql retry strategy treats as transient
    /// (an <see cref="NpgsqlException"/> caused by an I/O failure), before anything is committed.
    /// </summary>
    private sealed class FailFirstCommitInterceptor : DbTransactionInterceptor
    {
        private int _commits;

        /// <summary>Commits attempted so far, failed and successful.</summary>
        public int Commits => _commits;

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _commits) == 1)
            {
                throw new NpgsqlException("simulated transient failure", new IOException("simulated"));
            }

            return ValueTask.FromResult(result);
        }
    }
}
