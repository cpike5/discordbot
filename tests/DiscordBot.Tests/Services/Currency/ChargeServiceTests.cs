using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Tests for the charge seam: what a priced feature is told, what a hold reserves, and what
/// reaches the ledger. Nothing is written until a commit, and a commit writes once.
/// </summary>
public class ChargeServiceTests
{
    private const ulong Guild = 1001UL;
    private const ulong Player = 111UL;
    private const ulong ExemptRole = 555UL;
    private const string Feature = "soundboard:airhorn";

    // Free paths: the default everywhere until an admin sets a price.

    [Fact]
    public async Task TryHoldAsync_WithNoPrice_IsFreeAndHoldsNothing()
    {
        using var context = new CurrencyTestContext();
        await context.SeedCurrencyAsync();

        var result = await context.ChargeService.TryHoldAsync(Player, Guild, Feature, "key-1");

        result.Status.Should().Be(ChargeHoldStatus.Free);
        result.HoldId.Should().BeNull();
        result.Price.Should().Be(0);
    }

    [Fact]
    public async Task TryHoldAsync_WithAnExemptRole_IsFree()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedPriceAsync(currency.Id, Feature, 5, Guild, true, ExemptRole);
        context.SetMemberRoles(Guild, Player, ExemptRole);

        var result = await context.ChargeService.TryHoldAsync(Player, Guild, Feature, "key-1");

        result.Status.Should().Be(ChargeHoldStatus.Free);
        result.HoldId.Should().BeNull();
    }

    [Fact]
    public async Task TryHoldAsync_WithoutTheExemptRole_StillCharges()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedPriceAsync(currency.Id, Feature, 5, Guild, true, ExemptRole);
        context.SetMemberRoles(Guild, Player, 777UL);
        await context.SeedBalanceAsync(currency.Id, Player, 10);

        var result = await context.ChargeService.TryHoldAsync(Player, Guild, Feature, "key-1");

        result.Status.Should().Be(ChargeHoldStatus.Held);
        result.Price.Should().Be(5);
    }

    // Refusals: rendered by the caller, so each carries the price, the balance, and the symbol.

    [Fact]
    public async Task TryHoldAsync_WithoutTheFunds_IsRefusedWithInsufficientFunds()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedPriceAsync(currency.Id, Feature, 5);
        await context.SeedBalanceAsync(currency.Id, Player, 2);

        var result = await context.ChargeService.TryHoldAsync(Player, Guild, Feature, "key-1");

        result.Status.Should().Be(ChargeHoldStatus.InsufficientFunds);
        result.Price.Should().Be(5);
        result.Balance.Should().Be(2);
        result.CurrencySymbol.Should().Be("C");
        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task TryHoldAsync_WhileInDebt_IsRefusedWithInDebt()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync(allowNegative: true, debtFloor: -100);
        await context.SeedPriceAsync(currency.Id, Feature, 5);
        await context.SeedBalanceAsync(currency.Id, Player, -40);

        var result = await context.ChargeService.TryHoldAsync(Player, Guild, Feature, "key-1");

        result.Status.Should().Be(ChargeHoldStatus.InDebt);
        result.Balance.Should().Be(-40);
    }

    [Fact]
    public async Task TryHoldAsync_OnADeactivatedCurrency_IsRefused()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedPriceAsync(currency.Id, Feature, 5);
        await context.SeedBalanceAsync(currency.Id, Player, 100);

        currency.IsActive = false;
        await context.Currencies.UpdateAsync(currency);

        var result = await context.ChargeService.TryHoldAsync(Player, Guild, Feature, "key-1");

        result.Status.Should().Be(ChargeHoldStatus.CurrencyInactive);
    }

    // Holds reserve funds so two concurrent uses cannot both pass with funds for one.

    [Fact]
    public async Task TryHoldAsync_TwiceOnOneBalance_RefusesTheSecond()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedPriceAsync(currency.Id, Feature, 5);
        await context.SeedBalanceAsync(currency.Id, Player, 5);

        var first = await context.ChargeService.TryHoldAsync(Player, Guild, Feature, "key-1");
        var second = await context.ChargeService.TryHoldAsync(Player, Guild, Feature, "key-2");

        first.Status.Should().Be(ChargeHoldStatus.Held);
        second.Status.Should().Be(ChargeHoldStatus.InsufficientFunds);
        second.Balance.Should().Be(0, "the first hold has reserved the whole balance");
    }

    [Fact]
    public async Task ReleaseAsync_WritesNothingAndFreesTheReservation()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedPriceAsync(currency.Id, Feature, 5);
        await context.SeedBalanceAsync(currency.Id, Player, 5);

        var held = await context.ChargeService.TryHoldAsync(Player, Guild, Feature, "key-1");
        await context.ChargeService.ReleaseAsync(held.HoldId!.Value);

        (await context.BalanceOfAsync(currency.Id, Player)).Should().Be(5);
        (await context.Db.LedgerTransactions.CountAsync(t => t.Type == LedgerTransactionType.Spend)).Should().Be(0);

        var again = await context.ChargeService.TryHoldAsync(Player, Guild, Feature, "key-2");
        again.Status.Should().Be(ChargeHoldStatus.Held);
    }

    [Fact]
    public async Task ReleaseAsync_OnAnUnknownHold_DoesNothing()
    {
        using var context = new CurrencyTestContext();

        var release = async () => await context.ChargeService.ReleaseAsync(Guid.NewGuid());

        await release.Should().NotThrowAsync();
    }

    // Commit is the only thing that writes, and it writes once.

    [Fact]
    public async Task CommitAsync_WritesOneSpendRowAndMovesTheBalance()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedPriceAsync(currency.Id, Feature, 5);
        await context.SeedBalanceAsync(currency.Id, Player, 12);

        var held = await context.ChargeService.TryHoldAsync(Player, Guild, Feature, "key-1");
        var commit = await context.ChargeService.CommitAsync(held.HoldId!.Value);

        commit.Success.Should().BeTrue();
        commit.Transaction!.Type.Should().Be(LedgerTransactionType.Spend);
        commit.Transaction.Amount.Should().Be(-5);
        commit.Transaction.FeatureKey.Should().Be(Feature);
        commit.Transaction.IdempotencyKey.Should().Be("key-1");
        commit.Balance.Should().Be(7);
        (await context.BalanceOfAsync(currency.Id, Player)).Should().Be(7);
    }

    [Fact]
    public async Task CommitAsync_Twice_WritesOnce()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedPriceAsync(currency.Id, Feature, 5);
        await context.SeedBalanceAsync(currency.Id, Player, 12);

        var held = await context.ChargeService.TryHoldAsync(Player, Guild, Feature, "key-1");
        var first = await context.ChargeService.CommitAsync(held.HoldId!.Value);
        var second = await context.ChargeService.CommitAsync(held.HoldId!.Value);

        second.Success.Should().BeTrue();
        second.WasDuplicate.Should().BeTrue();
        second.Transaction!.Id.Should().Be(first.Transaction!.Id);
        (await context.BalanceOfAsync(currency.Id, Player)).Should().Be(7);
        (await context.Db.LedgerTransactions.CountAsync(t => t.Type == LedgerTransactionType.Spend)).Should().Be(1);
    }

    [Fact]
    public async Task CommitAsync_AfterTheHoldExpired_StillCommits()
    {
        // A zero window expires every hold the moment it is created; the entry stays in the store
        // long enough for a slow feature to commit what it reserved.
        using var context = new CurrencyTestContext(holdExpirySeconds: 0);
        var currency = await context.SeedCurrencyAsync();
        await context.SeedPriceAsync(currency.Id, Feature, 5);
        await context.SeedBalanceAsync(currency.Id, Player, 12);

        var held = await context.ChargeService.TryHoldAsync(Player, Guild, Feature, "key-1");
        var commit = await context.ChargeService.CommitAsync(held.HoldId!.Value);

        commit.Success.Should().BeTrue();
        (await context.BalanceOfAsync(currency.Id, Player)).Should().Be(7);
    }

    [Fact]
    public async Task TryHoldAsync_AfterAnEarlierHoldExpired_ReservesTheFundsAgain()
    {
        using var context = new CurrencyTestContext(holdExpirySeconds: 0);
        var currency = await context.SeedCurrencyAsync();
        await context.SeedPriceAsync(currency.Id, Feature, 5);
        await context.SeedBalanceAsync(currency.Id, Player, 5);

        await context.ChargeService.TryHoldAsync(Player, Guild, Feature, "key-1");
        var second = await context.ChargeService.TryHoldAsync(Player, Guild, Feature, "key-2");

        second.Status.Should().Be(ChargeHoldStatus.Held, "an expired reservation frees the funds");
    }

    [Fact]
    public async Task CommitAsync_OnAnUnknownHold_FailsAndWritesNothing()
    {
        using var context = new CurrencyTestContext();

        var commit = await context.ChargeService.CommitAsync(Guid.NewGuid());

        commit.Success.Should().BeFalse();
        commit.Error.Should().Be(CurrencyErrors.HoldNotFound);
        (await context.Db.LedgerTransactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CommitAsync_AfterTheFundsWentElsewhere_FailsAndDropsTheHold()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedPriceAsync(currency.Id, Feature, 5);
        await context.SeedBalanceAsync(currency.Id, Player, 5);

        var held = await context.ChargeService.TryHoldAsync(Player, Guild, Feature, "key-1");

        // A fine between the hold and the commit takes the balance the hold was counting on.
        await context.WalletService.FineAsync(currency.Id, Player, 5, "late night", 999UL, null);

        var commit = await context.ChargeService.CommitAsync(held.HoldId!.Value);

        commit.Success.Should().BeFalse();
        commit.Error.Should().Be(CurrencyErrors.InsufficientFunds);
        context.Holds.Get(held.HoldId!.Value).Should().BeNull();
    }

    // Refunds reverse a spend once, and only a spend.

    [Fact]
    public async Task RefundAsync_WritesARefundReferencingTheSpend()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedPriceAsync(currency.Id, Feature, 5);
        await context.SeedBalanceAsync(currency.Id, Player, 12);

        var held = await context.ChargeService.TryHoldAsync(Player, Guild, Feature, "key-1");
        var commit = await context.ChargeService.CommitAsync(held.HoldId!.Value);

        var refund = await context.ChargeService.RefundAsync(commit.Transaction!.Id, "sound never played", 999UL);

        refund.Success.Should().BeTrue();
        refund.Transaction!.Type.Should().Be(LedgerTransactionType.Refund);
        refund.Transaction.Amount.Should().Be(5);
        refund.Transaction.ReferenceTransactionId.Should().Be(commit.Transaction.Id);
        refund.Transaction.FeatureKey.Should().Be(Feature);
        (await context.BalanceOfAsync(currency.Id, Player)).Should().Be(12);
    }

    [Fact]
    public async Task RefundAsync_Twice_IsRefusedAndWritesOnce()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedPriceAsync(currency.Id, Feature, 5);
        await context.SeedBalanceAsync(currency.Id, Player, 12);

        var held = await context.ChargeService.TryHoldAsync(Player, Guild, Feature, "key-1");
        var commit = await context.ChargeService.CommitAsync(held.HoldId!.Value);

        await context.ChargeService.RefundAsync(commit.Transaction!.Id, "sound never played", 999UL);
        var second = await context.ChargeService.RefundAsync(commit.Transaction.Id, "again", 999UL);

        second.Success.Should().BeFalse();
        second.Error.Should().Be(CurrencyErrors.AlreadyRefunded);
        (await context.Db.LedgerTransactions.CountAsync(t => t.Type == LedgerTransactionType.Refund)).Should().Be(1);
        (await context.BalanceOfAsync(currency.Id, Player)).Should().Be(12);
    }

    [Fact]
    public async Task RefundAsync_OnARowThatIsNotASpend_IsRefused()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedBalanceAsync(currency.Id, Player, 10);

        var mint = await context.Db.LedgerTransactions.FirstAsync();

        var refund = await context.ChargeService.RefundAsync(mint.Id, "wrong row", 999UL);

        refund.Success.Should().BeFalse();
        refund.Error.Should().Be(CurrencyErrors.NotRefundable);
    }

    [Fact]
    public async Task RefundAsync_OnAMissingRow_IsRefused()
    {
        using var context = new CurrencyTestContext();

        var refund = await context.ChargeService.RefundAsync(4242L, "no such spend", 999UL);

        refund.Success.Should().BeFalse();
        refund.Error.Should().Be(CurrencyErrors.ReferenceNotFound);
    }
}
