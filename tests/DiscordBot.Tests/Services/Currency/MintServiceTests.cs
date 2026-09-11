using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Tests for the mint authority check. Minting is the only source of units, so who may do it is
/// the whole point of this service; the balance half belongs to the wallet service.
/// </summary>
public class MintServiceTests
{
    private const ulong Guild = 1001UL;
    private const ulong Minter = 111UL;
    private const ulong Recipient = 222UL;
    private const ulong MinterRole = 555UL;

    [Fact]
    public async Task MintAsync_ByAUserWithNoGrant_IsRefusedAndWritesNothing()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();

        var result = await context.MintService.MintAsync(
            currency.Id, Recipient, 10, "welcome", LedgerSource.Manual, "mint-1", Minter);

        result.Success.Should().BeFalse();
        result.Error.Should().Be(CurrencyErrors.NotAuthorizedToMint);
        (await context.Db.LedgerTransactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task MintAsync_ByAListedUser_Mints()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedMintAuthorityAsync(currency.Id, MintPrincipalType.User, Minter);

        var result = await context.MintService.MintAsync(
            currency.Id, Recipient, 10, "welcome", LedgerSource.Manual, "mint-1", Minter);

        result.Success.Should().BeTrue();
        result.Transaction!.Type.Should().Be(LedgerTransactionType.Mint);
        result.Transaction.ActorId.Should().Be(Minter);
        (await context.BalanceOfAsync(currency.Id, Recipient)).Should().Be(10);
    }

    [Fact]
    public async Task MintAsync_ByAListedRole_Mints()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedMintAuthorityAsync(currency.Id, MintPrincipalType.Role, MinterRole);
        context.SetMemberRoles(Guild, Minter, MinterRole);

        var result = await context.MintService.MintAsync(
            currency.Id, Recipient, 10, "payday", LedgerSource.Manual, "mint-1", Minter);

        result.Success.Should().BeTrue();
        (await context.BalanceOfAsync(currency.Id, Recipient)).Should().Be(10);
    }

    [Fact]
    public async Task MintAsync_ByAUserWithoutTheListedRole_IsRefused()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedMintAuthorityAsync(currency.Id, MintPrincipalType.Role, MinterRole);
        context.SetMemberRoles(Guild, Minter, 777UL);

        var result = await context.MintService.MintAsync(
            currency.Id, Recipient, 10, "payday", LedgerSource.Manual, "mint-1", Minter);

        result.Success.Should().BeFalse();
        result.Error.Should().Be(CurrencyErrors.NotAuthorizedToMint);
    }

    [Fact]
    public async Task MintAsync_BySystemWithASystemGrant_MintsWithNoActor()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedMintAuthorityAsync(currency.Id, MintPrincipalType.System, null);

        var result = await context.MintService.MintAsync(
            currency.Id, Recipient, 25, "weekly income", LedgerSource.Income, "income:1", null);

        result.Success.Should().BeTrue();
        result.Transaction!.ActorId.Should().BeNull();
        result.Transaction.Source.Should().Be(LedgerSource.Income);
        (await context.BalanceOfAsync(currency.Id, Recipient)).Should().Be(25);
    }

    [Fact]
    public async Task MintAsync_BySystemWithoutASystemGrant_IsRefused()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedMintAuthorityAsync(currency.Id, MintPrincipalType.User, Minter);

        var result = await context.MintService.MintAsync(
            currency.Id, Recipient, 25, "weekly income", LedgerSource.Income, "income:1", null);

        result.Success.Should().BeFalse();
        result.Error.Should().Be(CurrencyErrors.NotAuthorizedToMint);
    }

    [Fact]
    public async Task MintAsync_OnAMissingCurrency_IsRefused()
    {
        using var context = new CurrencyTestContext();

        var result = await context.MintService.MintAsync(
            Guid.NewGuid(), Recipient, 10, "welcome", LedgerSource.Manual, "mint-1", Minter);

        result.Success.Should().BeFalse();
        result.Error.Should().Be(CurrencyErrors.CurrencyNotFound);
    }

    [Fact]
    public async Task MintAsync_OnSuccess_IsAudited()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedMintAuthorityAsync(currency.Id, MintPrincipalType.User, Minter);

        await context.MintService.MintAsync(
            currency.Id, Recipient, 10, "welcome", LedgerSource.Manual, "mint-1", Minter);

        context.AuditLogBuilder.Verify(b => b.WithAction(AuditLogAction.CurrencyMinted), Times.Once);
        context.AuditLogBuilder.Verify(b => b.ByUser(Minter.ToString()), Times.Once);
        context.AuditLogBuilder.Verify(b => b.LogAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MintAsync_WhenRefused_IsNotAudited()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();

        await context.MintService.MintAsync(
            currency.Id, Recipient, 10, "welcome", LedgerSource.Manual, "mint-1", Minter);

        context.AuditLogBuilder.Verify(b => b.LogAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MintAsync_RepeatedWithTheSameKey_WritesOnce()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedMintAuthorityAsync(currency.Id, MintPrincipalType.User, Minter);

        await context.MintService.MintAsync(
            currency.Id, Recipient, 10, "welcome", LedgerSource.Manual, "mint-1", Minter);
        var second = await context.MintService.MintAsync(
            currency.Id, Recipient, 10, "welcome", LedgerSource.Manual, "mint-1", Minter);

        second.WasDuplicate.Should().BeTrue();
        (await context.BalanceOfAsync(currency.Id, Recipient)).Should().Be(10);
        (await context.Db.LedgerTransactions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CanMintAsync_MatchesTheGrantList()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedMintAuthorityAsync(currency.Id, MintPrincipalType.Role, MinterRole);

        (await context.MintService.CanMintAsync(currency.Id, Minter, new[] { MinterRole })).Should().BeTrue();
        (await context.MintService.CanMintAsync(currency.Id, Minter, Array.Empty<ulong>())).Should().BeFalse();
    }
}
