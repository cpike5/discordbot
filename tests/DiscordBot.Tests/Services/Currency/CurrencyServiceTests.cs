using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Tests for currency administration: the rules that hold when a currency is created or edited,
/// mint authority grants, feature pricing, and the reconcile check.
/// </summary>
public class CurrencyServiceTests
{
    private const ulong GuildId = 1001UL;
    private const ulong Admin = 7UL;

    [Fact]
    public async Task CreateAsync_AddsTheCreatorAsAMintAuthority()
    {
        using var context = new CurrencyTestContext();

        var result = await context.CurrencyService.CreateAsync(GuildCurrency(), Admin);

        result.Success.Should().BeTrue();
        result.Currency!.IsActive.Should().BeTrue();

        var authorities = await context.CurrencyService.GetMintAuthoritiesAsync(result.Currency.Id);
        authorities.Should().ContainSingle();
        authorities[0].PrincipalType.Should().Be(MintPrincipalType.User);
        authorities[0].PrincipalId.Should().Be(Admin);
    }

    [Fact]
    public async Task CreateAsync_DefaultsTransferableOnForGuildCurrenciesAndOffForGlobalOnes()
    {
        using var context = new CurrencyTestContext();

        var guild = await context.CurrencyService.CreateAsync(GuildCurrency(), Admin);
        var global = await context.CurrencyService.CreateAsync(
            new CurrencyCreateDto { Scope = CurrencyScope.Global, Name = "Bot Credit", Symbol = "*" }, Admin);

        guild.Currency!.IsTransferable.Should().BeTrue();
        global.Currency!.IsTransferable.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_WithADuplicateNameInTheSameScope_IsRefused()
    {
        using var context = new CurrencyTestContext();
        await context.CurrencyService.CreateAsync(GuildCurrency(), Admin);

        var duplicate = await context.CurrencyService.CreateAsync(GuildCurrency(name: "coins"), Admin);

        duplicate.Success.Should().BeFalse();
        duplicate.Error.Should().Be(CurrencyErrors.DuplicateName);
    }

    [Fact]
    public async Task CreateAsync_AllowsTheSameNameInADifferentGuild()
    {
        using var context = new CurrencyTestContext();
        await context.CurrencyService.CreateAsync(GuildCurrency(), Admin);

        var other = await context.CurrencyService.CreateAsync(GuildCurrency(guildId: 2002UL), Admin);

        other.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WithDebtAllowedButNoFloor_IsRefused()
    {
        using var context = new CurrencyTestContext();

        var result = await context.CurrencyService.CreateAsync(
            GuildCurrency() with { AllowNegative = true, DebtFloor = null }, Admin);

        result.Error.Should().Be(CurrencyErrors.InvalidCurrencyRules);
    }

    [Fact]
    public async Task CreateAsync_WithAPositiveDebtFloor_IsRefused()
    {
        using var context = new CurrencyTestContext();

        var result = await context.CurrencyService.CreateAsync(
            GuildCurrency() with { AllowNegative = true, DebtFloor = 100 }, Admin);

        result.Error.Should().Be(CurrencyErrors.InvalidCurrencyRules);
    }

    [Theory]
    [InlineData(CurrencyScope.Guild, null)]
    [InlineData(CurrencyScope.Global, GuildId)]
    public async Task CreateAsync_WithAScopeThatDoesNotMatchTheGuild_IsRefused(CurrencyScope scope, ulong? guildId)
    {
        using var context = new CurrencyTestContext();

        var result = await context.CurrencyService.CreateAsync(
            new CurrencyCreateDto { Scope = scope, GuildId = guildId, Name = "Coins", Symbol = "C" }, Admin);

        result.Error.Should().Be(CurrencyErrors.InvalidCurrencyRules);
    }

    [Theory]
    [InlineData("", "C")]
    [InlineData("   ", "C")]
    [InlineData("Coins", "")]
    public async Task CreateAsync_WithABlankNameOrSymbol_IsRefused(string name, string symbol)
    {
        using var context = new CurrencyTestContext();

        var result = await context.CurrencyService.CreateAsync(
            new CurrencyCreateDto { Scope = CurrencyScope.Guild, GuildId = GuildId, Name = name, Symbol = symbol }, Admin);

        result.Error.Should().Be(CurrencyErrors.InvalidCurrencyRules);
    }

    [Fact]
    public async Task UpdateAsync_ChangesOnlyTheFieldsThatWereSupplied()
    {
        using var context = new CurrencyTestContext();
        var created = await context.CurrencyService.CreateAsync(GuildCurrency(), Admin);

        var updated = await context.CurrencyService.UpdateAsync(
            created.Currency!.Id, new CurrencyUpdateDto { Symbol = "$" });

        updated.Currency!.Symbol.Should().Be("$");
        updated.Currency.Name.Should().Be("Coins");
        updated.Currency.IsTransferable.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_TurningDebtOff_ClearsTheFloor()
    {
        using var context = new CurrencyTestContext();
        var created = await context.CurrencyService.CreateAsync(
            GuildCurrency() with { AllowNegative = true, DebtFloor = -100 }, Admin);

        var updated = await context.CurrencyService.UpdateAsync(
            created.Currency!.Id, new CurrencyUpdateDto { AllowNegative = false });

        updated.Currency!.AllowNegative.Should().BeFalse();
        updated.Currency.DebtFloor.Should().BeNull();
    }

    [Fact]
    public async Task DeactivateAsync_FreezesTheCurrencyWithoutDeletingIt()
    {
        using var context = new CurrencyTestContext();
        var created = await context.CurrencyService.CreateAsync(GuildCurrency(), Admin);

        var result = await context.CurrencyService.DeactivateAsync(created.Currency!.Id);

        result.Currency!.IsActive.Should().BeFalse();
        (await context.Db.Currencies.CountAsync()).Should().Be(1, "currencies are never deleted");
        (await context.CurrencyService.GetVisibleInGuildAsync(GuildId)).Should().BeEmpty();
        (await context.CurrencyService.GetVisibleInGuildAsync(GuildId, includeInactive: true)).Should().ContainSingle();
    }

    [Fact]
    public async Task GetVisibleInGuildAsync_ReturnsTheGuildsOwnCurrenciesPlusTheGlobalOnes()
    {
        using var context = new CurrencyTestContext();
        await context.CurrencyService.CreateAsync(GuildCurrency(), Admin);
        await context.CurrencyService.CreateAsync(GuildCurrency(guildId: 2002UL, name: "Elsewhere"), Admin);
        await context.CurrencyService.CreateAsync(
            new CurrencyCreateDto { Scope = CurrencyScope.Global, Name = "Bot Credit", Symbol = "*" }, Admin);

        var visible = await context.CurrencyService.GetVisibleInGuildAsync(GuildId);

        visible.Select(c => c.Name).Should().BeEquivalentTo("Coins", "Bot Credit");
    }

    [Fact]
    public async Task GrantMintAuthorityAsync_AcceptsARoleAndTheSystemPrincipal()
    {
        using var context = new CurrencyTestContext();
        var created = await context.CurrencyService.CreateAsync(GuildCurrency(), Admin);
        var currencyId = created.Currency!.Id;

        var role = await context.CurrencyService.GrantMintAuthorityAsync(currencyId, MintPrincipalType.Role, 555UL, Admin);
        var system = await context.CurrencyService.GrantMintAuthorityAsync(currencyId, MintPrincipalType.System, null, Admin);

        role.Success.Should().BeTrue();
        system.Success.Should().BeTrue();

        (await context.Authorities.HasAuthorityAsync(currencyId, 404UL, new[] { 555UL })).Should().BeTrue();
        (await context.Authorities.HasAuthorityAsync(currencyId, 404UL, new[] { 111UL })).Should().BeFalse();
        (await context.Authorities.HasAuthorityAsync(currencyId, null)).Should().BeTrue("the system principal was granted");
    }

    [Theory]
    [InlineData(MintPrincipalType.User, null)]
    [InlineData(MintPrincipalType.System, 555UL)]
    public async Task GrantMintAuthorityAsync_WithAMismatchedPrincipal_IsRefused(MintPrincipalType type, ulong? principalId)
    {
        using var context = new CurrencyTestContext();
        var created = await context.CurrencyService.CreateAsync(GuildCurrency(), Admin);

        var result = await context.CurrencyService.GrantMintAuthorityAsync(created.Currency!.Id, type, principalId, Admin);

        result.Error.Should().Be(CurrencyErrors.InvalidCurrencyRules);
    }

    [Fact]
    public async Task RevokeMintAuthorityAsync_RemovesTheGrant()
    {
        using var context = new CurrencyTestContext();
        var created = await context.CurrencyService.CreateAsync(GuildCurrency(), Admin);
        var grant = await context.CurrencyService.GrantMintAuthorityAsync(
            created.Currency!.Id, MintPrincipalType.Role, 555UL, Admin);

        (await context.CurrencyService.RevokeMintAuthorityAsync(grant.Authority!.Id)).Should().BeTrue();
        (await context.CurrencyService.RevokeMintAuthorityAsync(grant.Authority.Id)).Should().BeFalse();
        (await context.Authorities.HasAuthorityAsync(created.Currency.Id, 404UL, new[] { 555UL })).Should().BeFalse();
    }

    // Pricing.

    [Fact]
    public async Task SetPriceAsync_StoresThePriceAndItsExemptRoles()
    {
        using var context = new CurrencyTestContext();
        var created = await context.CurrencyService.CreateAsync(GuildCurrency(), Admin);

        var result = await context.CurrencyService.SetPriceAsync(new PriceEntrySaveDto
        {
            FeatureKey = "soundboard:abc",
            GuildId = GuildId,
            CurrencyId = created.Currency!.Id,
            Amount = 5,
            ExemptRoleIds = new[] { 10UL, 20UL, 10UL }
        }, Admin);

        result.Success.Should().BeTrue();
        result.Price!.Amount.Should().Be(5);
        result.Price.CurrencySymbol.Should().Be("C");
        result.Price.ExemptRoleIds.Should().BeEquivalentTo(new[] { 10UL, 20UL });
    }

    [Fact]
    public async Task SetPriceAsync_ReplacesTheExistingEntryForTheSameFeatureAndGuild()
    {
        using var context = new CurrencyTestContext();
        var created = await context.CurrencyService.CreateAsync(GuildCurrency(), Admin);
        var save = new PriceEntrySaveDto
        {
            FeatureKey = "soundboard:abc",
            GuildId = GuildId,
            CurrencyId = created.Currency!.Id,
            Amount = 5
        };

        await context.CurrencyService.SetPriceAsync(save, Admin);
        await context.CurrencyService.SetPriceAsync(save with { Amount = 9 }, Admin);

        (await context.Db.PriceEntries.CountAsync()).Should().Be(1, "one active price per feature per guild");
        (await context.CurrencyService.GetActivePriceAsync("soundboard:abc", GuildId))!.Amount.Should().Be(9);
    }

    [Fact]
    public async Task SetPriceAsync_WithAGuildCurrencyPricingAnotherGuild_IsRefused()
    {
        using var context = new CurrencyTestContext();
        var created = await context.CurrencyService.CreateAsync(GuildCurrency(), Admin);

        var result = await context.CurrencyService.SetPriceAsync(new PriceEntrySaveDto
        {
            FeatureKey = "soundboard:abc",
            GuildId = 2002UL,
            CurrencyId = created.Currency!.Id,
            Amount = 5
        }, Admin);

        result.Error.Should().Be(CurrencyErrors.PriceScopeMismatch);
    }

    [Fact]
    public async Task SetPriceAsync_WithAGlobalCurrency_MayPriceAGuildOrEverywhere()
    {
        using var context = new CurrencyTestContext();
        var created = await context.CurrencyService.CreateAsync(
            new CurrencyCreateDto { Scope = CurrencyScope.Global, Name = "Bot Credit", Symbol = "*" }, Admin);

        var inGuild = await context.CurrencyService.SetPriceAsync(new PriceEntrySaveDto
        {
            FeatureKey = "media:image",
            GuildId = GuildId,
            CurrencyId = created.Currency!.Id,
            Amount = 5
        }, Admin);

        var everywhere = await context.CurrencyService.SetPriceAsync(new PriceEntrySaveDto
        {
            FeatureKey = "media:video",
            GuildId = null,
            CurrencyId = created.Currency.Id,
            Amount = 25
        }, Admin);

        inGuild.Success.Should().BeTrue();
        everywhere.Success.Should().BeTrue();
    }

    [Theory]
    [InlineData("soundboard")]
    [InlineData(":abc")]
    [InlineData("soundboard:")]
    [InlineData("")]
    public async Task SetPriceAsync_WithAMalformedFeatureKey_IsRefused(string featureKey)
    {
        using var context = new CurrencyTestContext();
        var created = await context.CurrencyService.CreateAsync(GuildCurrency(), Admin);

        var result = await context.CurrencyService.SetPriceAsync(new PriceEntrySaveDto
        {
            FeatureKey = featureKey,
            GuildId = GuildId,
            CurrencyId = created.Currency!.Id,
            Amount = 5
        }, Admin);

        result.Error.Should().Be(CurrencyErrors.InvalidFeatureKey);
    }

    [Fact]
    public async Task SetPriceAsync_WithANonPositiveAmount_IsRefused()
    {
        using var context = new CurrencyTestContext();
        var created = await context.CurrencyService.CreateAsync(GuildCurrency(), Admin);

        var result = await context.CurrencyService.SetPriceAsync(new PriceEntrySaveDto
        {
            FeatureKey = "soundboard:abc",
            GuildId = GuildId,
            CurrencyId = created.Currency!.Id,
            Amount = 0
        }, Admin);

        result.Error.Should().Be(CurrencyErrors.InvalidAmount);
    }

    [Fact]
    public async Task GetActivePriceAsync_FallsBackToTheEntryThatAppliesEverywhere()
    {
        using var context = new CurrencyTestContext();
        var created = await context.CurrencyService.CreateAsync(
            new CurrencyCreateDto { Scope = CurrencyScope.Global, Name = "Bot Credit", Symbol = "*" }, Admin);

        await context.CurrencyService.SetPriceAsync(new PriceEntrySaveDto
        {
            FeatureKey = "media:image",
            GuildId = null,
            CurrencyId = created.Currency!.Id,
            Amount = 5
        }, Admin);

        var price = await context.CurrencyService.GetActivePriceAsync("media:image", GuildId);

        price!.Amount.Should().Be(5);
        price.GuildId.Should().BeNull();
    }

    [Fact]
    public async Task RemovePriceAsync_LeavesTheFeatureFree()
    {
        using var context = new CurrencyTestContext();
        var created = await context.CurrencyService.CreateAsync(GuildCurrency(), Admin);
        await context.CurrencyService.SetPriceAsync(new PriceEntrySaveDto
        {
            FeatureKey = "soundboard:abc",
            GuildId = GuildId,
            CurrencyId = created.Currency!.Id,
            Amount = 5
        }, Admin);

        (await context.CurrencyService.RemovePriceAsync("soundboard:abc", GuildId)).Should().BeTrue();
        (await context.CurrencyService.RemovePriceAsync("soundboard:abc", GuildId)).Should().BeFalse();
        (await context.CurrencyService.GetActivePriceAsync("soundboard:abc", GuildId)).Should().BeNull();
    }

    // Reconcile.

    [Fact]
    public async Task ReconcileAsync_IsEmptyWhenEveryCacheMatchesTheLedger()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        await context.SeedBalanceAsync(currency.Id, 111UL, 40);
        await context.SeedBalanceAsync(currency.Id, 222UL, 0);

        (await context.CurrencyService.ReconcileAsync(currency.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task ReconcileAsync_ReportsAWalletWhoseCacheHasDrifted()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync();
        var wallet = await context.SeedBalanceAsync(currency.Id, 111UL, 40);

        // Simulate drift the only way that could happen: something writing the cache directly.
        wallet.CachedBalance = 999;
        await context.Db.SaveChangesAsync();

        var drifted = await context.CurrencyService.ReconcileAsync(currency.Id);

        drifted.Should().ContainSingle();
        drifted[0].CachedBalance.Should().Be(999);
        drifted[0].LedgerSum.Should().Be(40);
        drifted[0].Difference.Should().Be(959);
    }

    private static CurrencyCreateDto GuildCurrency(ulong? guildId = GuildId, string name = "Coins") => new()
    {
        Scope = CurrencyScope.Guild,
        GuildId = guildId,
        Name = name,
        Symbol = "C"
    };
}
