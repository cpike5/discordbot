using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Tests for the balance rules in the currency spec: what mint, spend, transfer, fine, and
/// adjustment are allowed to do to a balance, and what a deactivated currency refuses.
/// </summary>
public class WalletServiceTests
{
    private const ulong Payer = 111UL;
    private const ulong Payee = 222UL;
    private const ulong Moderator = 999UL;

    // Spend and TransferOut: never below zero, never while already below it.

    [Fact]
    public async Task SpendAsync_BelowTheBalance_IsRefusedWithInsufficientFunds()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedBalanceAsync(currency.Id, Payer, 4);

        var result = await context.WalletService.SpendAsync(currency.Id, Payer, 5, "soundboard:x", "spend-1", Payer);

        result.Success.Should().BeFalse();
        result.Error.Should().Be(CurrencyErrors.InsufficientFunds);
        result.Balance.Should().Be(4);
        (await context.BalanceOfAsync(currency.Id, Payer)).Should().Be(4);
    }

    [Fact]
    public async Task SpendAsync_AtExactlyTheBalance_IsAllowedAndLandsOnZero()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedBalanceAsync(currency.Id, Payer, 5);

        var result = await context.WalletService.SpendAsync(currency.Id, Payer, 5, "soundboard:x", "spend-1", Payer);

        result.Success.Should().BeTrue();
        result.Transaction!.Type.Should().Be(LedgerTransactionType.Spend);
        result.Transaction.Amount.Should().Be(-5);
        result.Transaction.FeatureKey.Should().Be("soundboard:x");
        (await context.BalanceOfAsync(currency.Id, Payer)).Should().Be(0);
    }

    [Fact]
    public async Task SpendAsync_WhileInDebt_IsRefusedWithInDebt()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync(allowNegative: true, debtFloor: -100);
        await context.SeedBalanceAsync(currency.Id, Payer, -20);

        var result = await context.WalletService.SpendAsync(currency.Id, Payer, 1, "soundboard:x", "spend-1", Payer);

        result.Success.Should().BeFalse();
        result.Error.Should().Be(CurrencyErrors.InDebt);
        (await context.BalanceOfAsync(currency.Id, Payer)).Should().Be(-20);
    }

    [Fact]
    public async Task SpendAsync_WithNoWallet_IsTreatedAsABalanceOfZero()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();

        var result = await context.WalletService.SpendAsync(currency.Id, Payer, 1, "soundboard:x", "spend-1", Payer);

        result.Success.Should().BeFalse();
        result.Error.Should().Be(CurrencyErrors.InsufficientFunds);
        (await context.Db.Wallets.CountAsync()).Should().Be(0, "a refused spend must not create a wallet");
    }

    [Fact]
    public async Task SpendAsync_RepeatedWithTheSameKey_ChargesOnce()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedBalanceAsync(currency.Id, Payer, 10);

        await context.WalletService.SpendAsync(currency.Id, Payer, 5, "soundboard:x", "spend-1", Payer);
        var repeat = await context.WalletService.SpendAsync(currency.Id, Payer, 5, "soundboard:x", "spend-1", Payer);

        repeat.Success.Should().BeTrue();
        repeat.WasDuplicate.Should().BeTrue();
        (await context.BalanceOfAsync(currency.Id, Payer)).Should().Be(5);
    }

    // Transfers.

    [Fact]
    public async Task TransferAsync_WritesTwoRowsThatReferenceEachOther()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedBalanceAsync(currency.Id, Payer, 50);

        var result = await context.WalletService.TransferAsync(currency.Id, Payer, Payee, 20, "thanks", "pay-1");

        result.Success.Should().BeTrue();
        result.Transaction!.Type.Should().Be(LedgerTransactionType.TransferOut);
        result.Transaction.Amount.Should().Be(-20);
        result.Transaction.IdempotencyKey.Should().Be("pay-1:out");
        result.CounterpartTransaction!.Type.Should().Be(LedgerTransactionType.TransferIn);
        result.CounterpartTransaction.Amount.Should().Be(20);
        result.CounterpartTransaction.IdempotencyKey.Should().Be("pay-1:in");

        result.Transaction.ReferenceTransactionId.Should().Be(result.CounterpartTransaction.Id);
        result.CounterpartTransaction.ReferenceTransactionId.Should().Be(result.Transaction.Id);

        (await context.BalanceOfAsync(currency.Id, Payer)).Should().Be(30);
        (await context.BalanceOfAsync(currency.Id, Payee)).Should().Be(20);
    }

    [Fact]
    public async Task TransferAsync_MoreThanTheSenderHolds_IsRefusedAndWritesNothing()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedBalanceAsync(currency.Id, Payer, 10);

        var result = await context.WalletService.TransferAsync(currency.Id, Payer, Payee, 11, null, "pay-1");

        result.Success.Should().BeFalse();
        result.Error.Should().Be(CurrencyErrors.InsufficientFunds);
        (await context.BalanceOfAsync(currency.Id, Payer)).Should().Be(10);
        (await context.BalanceOfAsync(currency.Id, Payee)).Should().Be(0);
    }

    [Fact]
    public async Task TransferAsync_WhileInDebt_IsRefusedWithInDebt()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync(allowNegative: true, debtFloor: -100);
        await context.SeedBalanceAsync(currency.Id, Payer, -5);

        var result = await context.WalletService.TransferAsync(currency.Id, Payer, Payee, 1, null, "pay-1");

        result.Error.Should().Be(CurrencyErrors.InDebt);
    }

    [Fact]
    public async Task TransferAsync_OnANonTransferableCurrency_IsRefused()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync(isTransferable: false);
        await context.SeedBalanceAsync(currency.Id, Payer, 50);

        var result = await context.WalletService.TransferAsync(currency.Id, Payer, Payee, 10, null, "pay-1");

        result.Error.Should().Be(CurrencyErrors.TransfersDisabled);
    }

    [Fact]
    public async Task TransferAsync_ToYourself_IsRefused()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedBalanceAsync(currency.Id, Payer, 50);

        var result = await context.WalletService.TransferAsync(currency.Id, Payer, Payer, 10, null, "pay-1");

        result.Error.Should().Be(CurrencyErrors.SelfTransfer);
    }

    // Fines: the only operation that may cross zero, and only where the currency allows it.

    [Fact]
    public async Task FineAsync_WithoutAllowNegative_ClampsToZeroAndSaysSo()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync(allowNegative: false);
        await context.SeedBalanceAsync(currency.Id, Payer, 30);

        var result = await context.WalletService.FineAsync(currency.Id, Payer, 50, "spam", Moderator, null);

        result.Success.Should().BeTrue();
        result.WasClamped.Should().BeTrue();
        result.ClampedAmount.Should().Be(30);
        result.Transaction!.Amount.Should().Be(-30);
        (await context.BalanceOfAsync(currency.Id, Payer)).Should().Be(0);
    }

    [Fact]
    public async Task FineAsync_WithAllowNegative_ClampsToTheDebtFloorAndSaysSo()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync(allowNegative: true, debtFloor: -100);
        await context.SeedBalanceAsync(currency.Id, Payer, 20);

        var result = await context.WalletService.FineAsync(currency.Id, Payer, 500, "spam", Moderator, null);

        result.Success.Should().BeTrue();
        result.ClampedAmount.Should().Be(120, "the fine may take the balance from 20 down to the -100 floor");
        (await context.BalanceOfAsync(currency.Id, Payer)).Should().Be(-100);
    }

    [Fact]
    public async Task FineAsync_WithinTheFloor_AppliesInFullAndReportsNoClamp()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync(allowNegative: true, debtFloor: -100);
        await context.SeedBalanceAsync(currency.Id, Payer, 20);

        var result = await context.WalletService.FineAsync(currency.Id, Payer, 50, "spam", Moderator, null);

        result.WasClamped.Should().BeFalse();
        result.ClampedAmount.Should().BeNull();
        (await context.BalanceOfAsync(currency.Id, Payer)).Should().Be(-30);
    }

    [Fact]
    public async Task FineAsync_AtZeroWithoutAllowNegative_StillWritesTheClampedRow()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync(allowNegative: false);

        var result = await context.WalletService.FineAsync(currency.Id, Payer, 50, "spam", Moderator, null);

        result.Success.Should().BeTrue();
        result.ClampedAmount.Should().Be(0);
        result.Transaction!.Amount.Should().Be(0);
        (await context.BalanceOfAsync(currency.Id, Payer)).Should().Be(0);
    }

    [Fact]
    public async Task FineAsync_OnAGlobalCurrency_IsRefused()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync(CurrencyScope.Global, guildId: null, name: "Bot Credit");
        await context.SeedBalanceAsync(currency.Id, Payer, 50);

        var result = await context.WalletService.FineAsync(currency.Id, Payer, 10, "spam", Moderator, null);

        result.Success.Should().BeFalse();
        result.Error.Should().Be(CurrencyErrors.FineRequiresGuildCurrency);
    }

    [Fact]
    public async Task FineAsync_LinksTheModerationCaseWhenOneIsGiven()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedBalanceAsync(currency.Id, Payer, 50);
        var caseId = Guid.NewGuid();

        var result = await context.WalletService.FineAsync(currency.Id, Payer, 10, "spam", Moderator, caseId);

        result.Transaction!.ModerationCaseId.Should().Be(caseId);
        result.Transaction.ActorId.Should().Be(Moderator);
    }

    [Fact]
    public async Task FineAsync_WithoutAReason_IsRefused()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();

        var result = await context.WalletService.FineAsync(currency.Id, Payer, 10, "  ", Moderator, null);

        result.Error.Should().Be(CurrencyErrors.ReasonRequired);
    }

    // Mint: the only source of units, and it moves a balance up from anywhere.

    [Fact]
    public async Task MintAsync_FromDebt_LandsOnTheRightNumber()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync(allowNegative: true, debtFloor: -100);
        await context.SeedBalanceAsync(currency.Id, Payer, -40);

        var result = await context.WalletService.MintAsync(
            currency.Id, Payer, 55, "bailout", LedgerSource.Manual, "mint-1", Moderator);

        result.Success.Should().BeTrue();
        result.Transaction!.BalanceAfter.Should().Be(15);
        (await context.BalanceOfAsync(currency.Id, Payer)).Should().Be(15);
    }

    [Fact]
    public async Task MintAsync_CreatesTheWalletOnFirstCredit()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();

        var result = await context.WalletService.MintAsync(
            currency.Id, Payer, 10, "welcome", LedgerSource.Income, "mint-1", null);

        result.Success.Should().BeTrue();
        result.Transaction!.Source.Should().Be(LedgerSource.Income);
        result.Transaction.ActorId.Should().BeNull("a system mint has no human actor");
        (await context.BalanceOfAsync(currency.Id, Payer)).Should().Be(10);
    }

    [Fact]
    public async Task MintAsync_RepeatedWithTheSameKey_MintsOnce()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();

        await context.WalletService.MintAsync(currency.Id, Payer, 10, "income", LedgerSource.Income, "income:week-1", null);
        var repeat = await context.WalletService.MintAsync(currency.Id, Payer, 10, "income", LedgerSource.Income, "income:week-1", null);

        repeat.WasDuplicate.Should().BeTrue();
        (await context.BalanceOfAsync(currency.Id, Payer)).Should().Be(10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task MintAsync_WithANonPositiveAmount_IsRefused(long amount)
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();

        var result = await context.WalletService.MintAsync(
            currency.Id, Payer, amount, "why", LedgerSource.Manual, "mint-1", Moderator);

        result.Error.Should().Be(CurrencyErrors.InvalidAmount);
    }

    [Fact]
    public async Task MintAsync_WithoutAReason_IsRefused()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();

        var result = await context.WalletService.MintAsync(
            currency.Id, Payer, 10, "   ", LedgerSource.Manual, "mint-1", Moderator);

        result.Error.Should().Be(CurrencyErrors.ReasonRequired);
    }

    // Adjustments.

    [Fact]
    public async Task AdjustAsync_WritesASignedRowAgainstTheReferencedWallet()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        var mint = await context.WalletService.MintAsync(
            currency.Id, Payer, 100, "oops", LedgerSource.Manual, "mint-1", Moderator);

        var result = await context.WalletService.AdjustAsync(mint.Transaction!.Id, -40, "minted too much", Moderator);

        result.Success.Should().BeTrue();
        result.Transaction!.Type.Should().Be(LedgerTransactionType.Adjustment);
        result.Transaction.ReferenceTransactionId.Should().Be(mint.Transaction.Id);
        (await context.BalanceOfAsync(currency.Id, Payer)).Should().Be(60);
    }

    [Fact]
    public async Task AdjustAsync_MayMoveTheBalanceUpAndIsNotClamped()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        var mint = await context.WalletService.MintAsync(
            currency.Id, Payer, 10, "seed", LedgerSource.Manual, "mint-1", Moderator);

        var result = await context.WalletService.AdjustAsync(mint.Transaction!.Id, -100, "correction", Moderator);

        result.Success.Should().BeTrue();
        (await context.BalanceOfAsync(currency.Id, Payer))
            .Should().Be(-90, "an adjustment corrects the record and is not clamped by the debt rules");
    }

    [Fact]
    public async Task AdjustAsync_WithoutAnExistingReference_IsRefused()
    {
        using var context = new CurrencyTestContext();
        await context.SeedCurrencyAsync();

        var result = await context.WalletService.AdjustAsync(4242, 10, "correction", Moderator);

        result.Success.Should().BeFalse();
        result.Error.Should().Be(CurrencyErrors.ReferenceNotFound);
    }

    [Fact]
    public async Task AdjustAsync_WithoutAReason_IsRefused()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        var mint = await context.WalletService.MintAsync(
            currency.Id, Payer, 10, "seed", LedgerSource.Manual, "mint-1", Moderator);

        var result = await context.WalletService.AdjustAsync(mint.Transaction!.Id, 10, "   ", Moderator);

        result.Success.Should().BeFalse();
        result.Error.Should().Be(CurrencyErrors.ReasonRequired);
    }

    [Fact]
    public async Task AdjustAsync_OfZero_IsRefused()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        var mint = await context.WalletService.MintAsync(
            currency.Id, Payer, 10, "seed", LedgerSource.Manual, "mint-1", Moderator);

        var result = await context.WalletService.AdjustAsync(mint.Transaction!.Id, 0, "correction", Moderator);

        result.Error.Should().Be(CurrencyErrors.InvalidAmount);
    }

    // A deactivated currency freezes: everything except reading is refused.

    [Fact]
    public async Task EveryWriteOnAnInactiveCurrency_IsRefusedWithCurrencyInactive()
    {
        using var context = new CurrencyTestContext();
        var active = await context.SeedCurrencyAsync();
        await context.SeedBalanceAsync(active.Id, Payer, 100);
        var mint = await context.WalletService.MintAsync(
            active.Id, Payer, 10, "seed", LedgerSource.Manual, "mint-seed", Moderator);

        // Freeze it only after the wallet and the row it references exist.
        await context.CurrencyService.DeactivateAsync(active.Id);

        (await context.WalletService.MintAsync(active.Id, Payer, 1, "r", LedgerSource.Manual, "m", Moderator))
            .Error.Should().Be(CurrencyErrors.CurrencyInactive);
        (await context.WalletService.SpendAsync(active.Id, Payer, 1, "soundboard:x", "s", Payer))
            .Error.Should().Be(CurrencyErrors.CurrencyInactive);
        (await context.WalletService.TransferAsync(active.Id, Payer, Payee, 1, null, "t"))
            .Error.Should().Be(CurrencyErrors.CurrencyInactive);
        (await context.WalletService.FineAsync(active.Id, Payer, 1, "r", Moderator, null))
            .Error.Should().Be(CurrencyErrors.CurrencyInactive);
        (await context.WalletService.AdjustAsync(mint.Transaction!.Id, 1, "r", Moderator))
            .Error.Should().Be(CurrencyErrors.CurrencyInactive);

        (await context.BalanceOfAsync(active.Id, Payer)).Should().Be(110, "a frozen currency writes nothing");
    }

    [Fact]
    public async Task ReadsStillWorkOnAnInactiveCurrency()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedBalanceAsync(currency.Id, Payer, 100);
        await context.CurrencyService.DeactivateAsync(currency.Id);

        var wallet = await context.WalletService.GetWalletAsync(currency.Id, Payer);

        wallet!.Balance.Should().Be(100);
        wallet.CurrencyName.Should().Be("Coins");
    }

    // Reads.

    [Fact]
    public async Task GetWalletsForUserAsync_NarrowsToTheCurrenciesVisibleInAGuild()
    {
        using var context = new CurrencyTestContext();
        var here = await context.SeedCurrencyAsync(guildId: 1001UL, name: "Here");
        var elsewhere = await context.SeedCurrencyAsync(guildId: 2002UL, name: "Elsewhere");
        var global = await context.SeedCurrencyAsync(CurrencyScope.Global, guildId: null, name: "Bot Credit");

        await context.SeedBalanceAsync(here.Id, Payer, 1);
        await context.SeedBalanceAsync(elsewhere.Id, Payer, 2);
        await context.SeedBalanceAsync(global.Id, Payer, 3);

        var wallets = await context.WalletService.GetWalletsForUserAsync(Payer, 1001UL);

        wallets.Select(w => w.CurrencyName).Should().BeEquivalentTo("Here", "Bot Credit");
    }

    [Fact]
    public async Task GetHistoryAsync_PagesNewestFirst()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        var wallet = await context.SeedBalanceAsync(currency.Id, Payer, 0);

        for (var i = 1; i <= 5; i++)
        {
            await context.WalletService.MintAsync(
                currency.Id, Payer, i, $"mint {i}", LedgerSource.Manual, $"mint-{i}", Moderator);
        }

        var page = await context.WalletService.GetHistoryAsync(wallet.Id, page: 1, pageSize: 2);

        page.TotalCount.Should().Be(5);
        page.TotalPages.Should().Be(3);
        page.Items.Should().HaveCount(2);
        page.Items[0].Reason.Should().Be("mint 5");
    }

    [Fact]
    public async Task EveryWriteOnAMissingCurrency_IsRefusedWithCurrencyNotFound()
    {
        using var context = new CurrencyTestContext();
        var missing = Guid.NewGuid();

        (await context.WalletService.MintAsync(missing, Payer, 1, "r", LedgerSource.Manual, "m", Moderator))
            .Error.Should().Be(CurrencyErrors.CurrencyNotFound);
        (await context.WalletService.SpendAsync(missing, Payer, 1, "soundboard:x", "s", Payer))
            .Error.Should().Be(CurrencyErrors.CurrencyNotFound);
        (await context.WalletService.TransferAsync(missing, Payer, Payee, 1, null, "t"))
            .Error.Should().Be(CurrencyErrors.CurrencyNotFound);
        (await context.WalletService.FineAsync(missing, Payer, 1, "r", Moderator, null))
            .Error.Should().Be(CurrencyErrors.CurrencyNotFound);
    }

    [Fact]
    public async Task EveryWriteWithoutAnIdempotencyKey_IsRefused()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();

        (await context.WalletService.MintAsync(currency.Id, Payer, 1, "r", LedgerSource.Manual, " ", Moderator))
            .Error.Should().Be(CurrencyErrors.IdempotencyKeyRequired);
        (await context.WalletService.SpendAsync(currency.Id, Payer, 1, "soundboard:x", " ", Payer))
            .Error.Should().Be(CurrencyErrors.IdempotencyKeyRequired);
        (await context.WalletService.TransferAsync(currency.Id, Payer, Payee, 1, null, " "))
            .Error.Should().Be(CurrencyErrors.IdempotencyKeyRequired);
    }
}
