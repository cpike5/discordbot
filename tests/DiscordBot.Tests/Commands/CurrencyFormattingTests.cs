using DiscordBot.Bot.Helpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Commands;

/// <summary>
/// Tests for the wallet commands' presentation layer. The Discord.NET modules themselves cannot be
/// exercised in unit tests (Context and RespondAsync both need a live client), so the refusal
/// messages, currency picking, and embed contents are asserted here instead.
/// </summary>
public class CurrencyFormattingTests
{
    private static CurrencyDto Currency(
        string name = "Coins",
        string symbol = "🪙",
        CurrencyScope scope = CurrencyScope.Guild,
        bool allowNegative = false,
        long? debtFloor = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Symbol = symbol,
        Scope = scope,
        GuildId = scope == CurrencyScope.Guild ? 1UL : null,
        IsTransferable = true,
        AllowNegative = allowNegative,
        DebtFloor = debtFloor,
        IsActive = true
    };

    #region ResolveCurrency

    [Fact]
    public void ResolveCurrency_WithNoCurrencies_TellsUserToCreateOne()
    {
        var result = CurrencyFormatting.ResolveCurrency(Array.Empty<CurrencyDto>(), null);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("/currency create");
    }

    [Fact]
    public void ResolveCurrency_WithOneCurrencyAndNoSelector_PicksIt()
    {
        var only = Currency();

        var result = CurrencyFormatting.ResolveCurrency(new[] { only }, null);

        result.Success.Should().BeTrue("a single currency needs no picker");
        result.Currency!.Id.Should().Be(only.Id);
    }

    [Fact]
    public void ResolveCurrency_WithSeveralCurrenciesAndNoSelector_AsksForOne()
    {
        var result = CurrencyFormatting.ResolveCurrency(
            new[] { Currency("Coins"), Currency("Tokens") }, null);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Coins").And.Contain("Tokens");
    }

    [Fact]
    public void ResolveCurrency_WithIdFromAutocomplete_PicksThatCurrency()
    {
        var first = Currency("Coins");
        var second = Currency("Tokens");

        var result = CurrencyFormatting.ResolveCurrency(new[] { first, second }, second.Id.ToString());

        result.Currency!.Id.Should().Be(second.Id);
    }

    [Fact]
    public void ResolveCurrency_WithNameTypedByHand_MatchesCaseInsensitively()
    {
        var currency = Currency("Coins");

        var result = CurrencyFormatting.ResolveCurrency(new[] { currency }, "coins");

        result.Currency!.Id.Should().Be(currency.Id);
    }

    [Fact]
    public void ResolveCurrency_WithUnknownName_Fails()
    {
        var result = CurrencyFormatting.ResolveCurrency(new[] { Currency("Coins") }, "Doubloons");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Doubloons");
    }

    #endregion

    #region DescribeError

    [Fact]
    public void DescribeError_InsufficientFunds_ShowsPriceAndBalance()
    {
        var message = CurrencyFormatting.DescribeError(
            CurrencyErrors.InsufficientFunds, balance: 2, symbol: "🪙", amount: 5);

        message.Should().Contain("5 🪙").And.Contain("2 🪙");
    }

    [Fact]
    public void DescribeError_InDebt_ShowsTheDebtAsAPositiveAmountAndTheLock()
    {
        var message = CurrencyFormatting.DescribeError(CurrencyErrors.InDebt, balance: -40, symbol: "🪙");

        message.Should().Contain("40 🪙").And.NotContain("-40");
        message.Should().Contain("above zero");
    }

    [Fact]
    public void DescribeError_TransfersDisabled_SaysSo()
    {
        CurrencyFormatting.DescribeError(CurrencyErrors.TransfersDisabled)
            .Should().Contain("can't be sent");
    }

    [Fact]
    public void DescribeError_UnknownCode_FallsBackToAGenericMessage()
    {
        CurrencyFormatting.DescribeError("SomethingNew").Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region DescribeChargeRefusal

    [Fact]
    public void DescribeChargeRefusal_InsufficientFunds_NamesTheSubjectThePriceAndTheBalance()
    {
        var message = CurrencyFormatting.DescribeChargeRefusal(
            ChargeHoldStatus.InsufficientFunds, price: 5, balance: 2, symbol: "\U0001FA99", subject: "This sound");

        message.Should().Be("This sound costs 5 \U0001FA99. You have 2 \U0001FA99.");
    }

    [Fact]
    public void DescribeChargeRefusal_WithoutASubject_FallsBackToTheGenericWording()
    {
        CurrencyFormatting.DescribeChargeRefusal(ChargeHoldStatus.InsufficientFunds, 5, 2, "\U0001FA99")
            .Should().StartWith("This costs");
    }

    [Fact]
    public void DescribeChargeRefusal_InDebt_ShowsTheDebtAsAPositiveAmountAndTheLock()
    {
        var message = CurrencyFormatting.DescribeChargeRefusal(
            ChargeHoldStatus.InDebt, price: 5, balance: -40, symbol: "\U0001FA99");

        message.Should().Contain("40 \U0001FA99").And.NotContain("-40");
        message.Should().Contain("above zero");
    }

    [Theory]
    [InlineData(ChargeHoldStatus.CurrencyInactive)]
    [InlineData(ChargeHoldStatus.NoWallet)]
    public void DescribeChargeRefusal_WhenTheCurrencyIsUnusable_SaysSoWithoutAnAmount(ChargeHoldStatus status)
    {
        var message = CurrencyFormatting.DescribeChargeRefusal(status, price: 5, balance: 0, symbol: null);

        message.Should().NotBeNullOrWhiteSpace();
        message.Should().NotContain("5");
    }

    #endregion

    #region Embeds

    [Fact]
    public void BalanceEmbed_WithDebt_FlagsTheWalletAndTheLock()
    {
        var wallets = new[]
        {
            new WalletDto { CurrencyName = "Coins", CurrencySymbol = "🪙", Balance = -40 }
        };

        var embed = CurrencyFormatting.BalanceEmbed("someone", wallets);

        embed.Fields.Should().ContainSingle().Which.Value.Should().Contain("in debt");
        embed.Footer!.Value.Text.Should().Contain("locked");
    }

    [Fact]
    public void BalanceEmbed_WithNoWallets_SaysSo()
    {
        var embed = CurrencyFormatting.BalanceEmbed("someone", Array.Empty<WalletDto>());

        embed.Description.Should().Contain("No balances");
    }

    [Fact]
    public void HistoryEmbed_RendersEachRowWithItsRunningBalance()
    {
        var currency = Currency();
        var page = new PagedResult<LedgerTransactionDto>
        {
            Items = new[]
            {
                new LedgerTransactionDto
                {
                    Id = 1,
                    Type = LedgerTransactionType.Mint,
                    Amount = 50,
                    BalanceAfter = 50,
                    Reason = "Welcome",
                    CreatedAt = DateTime.UtcNow
                },
                new LedgerTransactionDto
                {
                    Id = 2,
                    Type = LedgerTransactionType.Spend,
                    Amount = -5,
                    BalanceAfter = 45,
                    FeatureKey = "soundboard:abc",
                    CreatedAt = DateTime.UtcNow
                }
            },
            TotalCount = 12,
            Page = 1,
            PageSize = 10
        };

        var embed = CurrencyFormatting.HistoryEmbed("someone", currency, page);

        embed.Description.Should().Contain("Welcome").And.Contain("soundboard:abc");
        embed.Description.Should().Contain("45 🪙");
        embed.Footer!.Value.Text.Should().Contain("Page 1 of 2").And.Contain("12 entries");
    }

    [Fact]
    public void HistoryEmbed_WithNoRows_SaysSo()
    {
        var embed = CurrencyFormatting.HistoryEmbed(
            "someone",
            Currency(),
            new PagedResult<LedgerTransactionDto> { Page = 1, PageSize = 10 });

        embed.Description.Should().Contain("No transactions");
    }

    [Fact]
    public void FineReceiptEmbed_WhenClamped_ShowsWhatWasActuallyTaken()
    {
        var currency = Currency(allowNegative: true, debtFloor: -100);
        var result = new FineResult
        {
            Success = true,
            ClampedAmount = 30,
            Transaction = new LedgerTransactionDto { Amount = -30, BalanceAfter = -100 }
        };

        var embed = CurrencyFormatting.FineReceiptEmbed(42UL, 50, result, currency, "spam", null);

        embed.Description.Should().Contain("30 🪙");
        embed.Fields.Should().Contain(f => f.Name == "Clamped");
        embed.Fields.Single(f => f.Name == "Clamped").Value.Should().Contain("debt floor");
    }

    [Fact]
    public void FineReceiptEmbed_WhenNotClamped_HasNoClampField()
    {
        var result = new FineResult
        {
            Success = true,
            Transaction = new LedgerTransactionDto { Amount = -5, BalanceAfter = 15 }
        };

        var embed = CurrencyFormatting.FineReceiptEmbed(42UL, 5, result, Currency(), "spam", 7);

        embed.Fields.Should().NotContain(f => f.Name == "Clamped");
        embed.Fields.Should().Contain(f => f.Name == "Mod case" && f.Value.ToString() == "#7");
    }

    [Fact]
    public void PayConfirmationEmbed_NamesTheRecipientAmountAndNote()
    {
        var embed = CurrencyFormatting.PayConfirmationEmbed(99UL, 25, Currency(), "thanks");

        embed.Description.Should().Contain("25 🪙").And.Contain("<@99>");
        embed.Fields.Should().Contain(f => f.Name == "Note" && f.Value.ToString() == "thanks");
    }

    [Fact]
    public void CurrencyListEmbed_ShowsScopeAndTransferRules()
    {
        var embed = CurrencyFormatting.CurrencyListEmbed(
            "Test Guild",
            new[]
            {
                Currency("Coins"),
                Currency("Credit", "💳", CurrencyScope.Global) with { IsTransferable = false }
            });

        embed.Fields.Should().HaveCount(2);
        embed.Fields[0].Value.ToString().Should().Contain("transferable");
        embed.Fields[1].Value.ToString().Should().Contain("global").And.Contain("not transferable");
    }

    #endregion

    [Theory]
    [InlineData(0, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(25, 10, 3)]
    public void TotalPages_RoundsUpAndNeverReturnsZero(int totalCount, int pageSize, int expected)
    {
        CurrencyFormatting.TotalPages(totalCount, pageSize).Should().Be(expected);
    }
}
