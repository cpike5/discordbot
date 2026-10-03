using DiscordBot.Bot.Controllers;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.Authorization;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using static DiscordBot.Tests.Controllers.Currency.CurrencyControllerTestContext;

namespace DiscordBot.Tests.Controllers.Currency;

/// <summary>
/// Tests for <see cref="CurrenciesController"/>, mostly about who may act on a currency: the
/// guild-keyed routes lean on the GuildAccess policy, but everything keyed by currency id has to
/// resolve the scope itself.
/// </summary>
[Trait("Category", "Unit")]
public class CurrenciesControllerTests
{
    private readonly Mock<ICurrencyService> _currencyService = new();
    private readonly Mock<IAuditLogService> _auditLog = AuditLog();

    private CurrenciesController Build(CurrencyAccessLevel level) =>
        new CurrenciesController(
            _auditLog.Object,
            Mock.Of<ILogger<CurrenciesController>>(),
            _currencyService.Object,
            Access(level).Object)
            .WithUser(AdminUser());

    private CurrenciesController BuildWithoutFeature() =>
        new CurrenciesController(_auditLog.Object, Mock.Of<ILogger<CurrenciesController>>())
            .WithUser(AdminUser());

    [Fact]
    public async Task GetGuildCurrencies_ReturnsNotFound_WhenTheFeatureIsDisabled()
    {
        var controller = BuildWithoutFeature();

        var result = await controller.GetGuildCurrencies(GuildId);

        StatusOf(result.Result).Should().Be(404,
            "with Currency:Enabled false nothing is registered and the routes should simply not be there");
    }

    [Fact]
    public async Task CreateGuildCurrency_ForcesTheRouteGuildAndScope()
    {
        var controller = Build(CurrencyAccessLevel.Administer);
        CurrencyCreateDto? captured = null;

        _currencyService
            .Setup(s => s.CreateAsync(It.IsAny<CurrencyCreateDto>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .Callback<CurrencyCreateDto, ulong, CancellationToken>((dto, _, _) => captured = dto)
            .ReturnsAsync(new CurrencyResult { Success = true, Currency = GuildCurrency() });

        // A caller naming some other guild must not be able to create a currency there.
        await controller.CreateGuildCurrency(GuildId, new CurrencyCreateDto
        {
            Scope = CurrencyScope.Global,
            GuildId = 999UL,
            Name = "Rat Coin",
            Symbol = "🪙"
        });

        captured.Should().NotBeNull();
        captured!.Scope.Should().Be(CurrencyScope.Guild);
        captured.GuildId.Should().Be(GuildId);
    }

    [Fact]
    public async Task CreateGuildCurrency_ReturnsBadRequest_WhenDiscordIsNotLinked()
    {
        var controller = new CurrenciesController(
            _auditLog.Object,
            Mock.Of<ILogger<CurrenciesController>>(),
            _currencyService.Object,
            Access(CurrencyAccessLevel.Administer).Object)
            .WithUser(User(discordUserId: 0, roles: Roles.Admin));

        var result = await controller.CreateGuildCurrency(GuildId, new CurrencyCreateDto { Name = "X", Symbol = "x" });

        StatusOf(result.Result).Should().Be(400,
            "the creator becomes the first mint authority, so there has to be a Discord account to record");

        _currencyService.Verify(
            s => s.CreateAsync(It.IsAny<CurrencyCreateDto>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateGuildCurrency_ReturnsUnprocessable_OnADuplicateName()
    {
        var controller = Build(CurrencyAccessLevel.Administer);

        _currencyService
            .Setup(s => s.CreateAsync(It.IsAny<CurrencyCreateDto>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CurrencyResult.Failed(CurrencyErrors.DuplicateName));

        var result = await controller.CreateGuildCurrency(GuildId, new CurrencyCreateDto { Name = "Rat Coin", Symbol = "🪙" });

        var error = (result.Result as ObjectResult)?.Value as ApiErrorDto;
        error!.StatusCode.Should().Be(422);
        error.ErrorCode.Should().Be(CurrencyErrors.DuplicateName,
            "the page script branches on the code, not on the sentence");
    }

    [Fact]
    public async Task UpdateCurrency_ReturnsNotFound_WhenTheCurrencyIsNotVisibleToTheCaller()
    {
        var currency = GuildCurrency();
        var controller = Build(CurrencyAccessLevel.None);

        _currencyService
            .Setup(s => s.GetAsync(currency.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);

        var result = await controller.UpdateCurrency(currency.Id, new CurrencyUpdateDto { Name = "New" });

        StatusOf(result.Result).Should().Be(404,
            "a currency the caller may not see should not be confirmed to exist");

        _currencyService.Verify(
            s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<CurrencyUpdateDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateCurrency_ReturnsForbidden_ForAModerator()
    {
        var currency = GuildCurrency();
        var controller = Build(CurrencyAccessLevel.Moderate);

        _currencyService
            .Setup(s => s.GetAsync(currency.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);

        var result = await controller.UpdateCurrency(currency.Id, new CurrencyUpdateDto { Name = "New" });

        StatusOf(result.Result).Should().Be(403,
            "moderators may fine, not rewrite the rules");
    }

    [Fact]
    public async Task UpdateCurrency_SavesAndAudits_ForAnAdministrator()
    {
        var currency = GuildCurrency();
        var controller = Build(CurrencyAccessLevel.Administer);

        _currencyService
            .Setup(s => s.GetAsync(currency.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);

        _currencyService
            .Setup(s => s.UpdateAsync(currency.Id, It.IsAny<CurrencyUpdateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrencyResult { Success = true, Currency = currency with { Name = "New" } });

        var result = await controller.UpdateCurrency(currency.Id, new CurrencyUpdateDto { Name = "New" });

        result.Result.Should().BeOfType<OkObjectResult>();
        _auditLog.Verify(a => a.CreateBuilder(), Times.Once);
    }

    [Fact]
    public async Task DeactivateCurrency_ReturnsForbidden_WithoutAdministerAccess()
    {
        var currency = GuildCurrency();
        var controller = Build(CurrencyAccessLevel.Read);

        _currencyService
            .Setup(s => s.GetAsync(currency.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);

        var result = await controller.DeactivateCurrency(currency.Id);

        StatusOf(result.Result).Should().Be(403);
        _currencyService.Verify(s => s.DeactivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GrantMintAuthority_PassesThePrincipalAndTheActor()
    {
        var currency = GuildCurrency();
        var controller = Build(CurrencyAccessLevel.Administer);

        _currencyService
            .Setup(s => s.GetAsync(currency.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);

        _currencyService
            .Setup(s => s.GrantMintAuthorityAsync(
                currency.Id, MintPrincipalType.Role, 987654321UL, ActorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MintAuthorityResult
            {
                Success = true,
                Authority = new MintAuthorityDto { Id = Guid.NewGuid(), CurrencyId = currency.Id }
            });

        var result = await controller.GrantMintAuthority(currency.Id, new MintAuthorityGrantRequestDto
        {
            PrincipalType = MintPrincipalType.Role,
            PrincipalId = 987654321UL
        });

        result.Result.Should().BeOfType<OkObjectResult>();
        _currencyService.VerifyAll();
    }

    [Fact]
    public async Task GrantMintAuthority_RefusesTheEveryoneRole_BecauseItsIdIsTheGuildsId()
    {
        var currency = GuildCurrency();
        var controller = Build(CurrencyAccessLevel.Administer);

        _currencyService
            .Setup(s => s.GetAsync(currency.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);

        var result = await controller.GrantMintAuthority(currency.Id, new MintAuthorityGrantRequestDto
        {
            PrincipalType = MintPrincipalType.Role,
            PrincipalId = GuildId
        });

        StatusOf(result.Result).Should().Be(400);
        _currencyService.Verify(
            s => s.GrantMintAuthorityAsync(It.IsAny<Guid>(), It.IsAny<MintPrincipalType>(), It.IsAny<ulong?>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GrantMintAuthority_StillAllowsAUserWhoseIdEqualsTheGuildsId()
    {
        // Only a role can be @everyone; the ID check must not catch other principal types
        var currency = GuildCurrency();
        var controller = Build(CurrencyAccessLevel.Administer);

        _currencyService
            .Setup(s => s.GetAsync(currency.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
        _currencyService
            .Setup(s => s.GrantMintAuthorityAsync(
                currency.Id, MintPrincipalType.User, GuildId, ActorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MintAuthorityResult
            {
                Success = true,
                Authority = new MintAuthorityDto { Id = Guid.NewGuid(), CurrencyId = currency.Id }
            });

        var result = await controller.GrantMintAuthority(currency.Id, new MintAuthorityGrantRequestDto
        {
            PrincipalType = MintPrincipalType.User,
            PrincipalId = GuildId
        });

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetRoleGrantRefusal_RefusesEveryoneAndManagedRoles_AndAllowsOrdinaryOnes()
    {
        const ulong roleId = 987654321UL;

        CurrenciesController.GetRoleGrantRefusal(GuildId, GuildId, null).Should().NotBeNull();

        var managed = new Mock<Discord.IRole>();
        managed.SetupGet(r => r.IsManaged).Returns(true);
        CurrenciesController.GetRoleGrantRefusal(GuildId, roleId, managed.Object).Should().Contain("managed");

        var ordinary = new Mock<Discord.IRole>();
        CurrenciesController.GetRoleGrantRefusal(GuildId, roleId, ordinary.Object).Should().BeNull();
        CurrenciesController.GetRoleGrantRefusal(GuildId, roleId, null).Should().BeNull("an uncached role is allowed, as before");
    }

    [Fact]
    public async Task RevokeMintAuthority_RefusesAGrantOnAnotherCurrency()
    {
        var currency = GuildCurrency();
        var controller = Build(CurrencyAccessLevel.Administer);

        _currencyService
            .Setup(s => s.GetAsync(currency.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);

        // The currency the caller was authorized against carries no such grant.
        _currencyService
            .Setup(s => s.GetMintAuthoritiesAsync(currency.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MintAuthorityDto>());

        var result = await controller.RevokeMintAuthority(currency.Id, Guid.NewGuid());

        StatusOf(result).Should().Be(404);
        _currencyService.Verify(
            s => s.RevokeMintAuthorityAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "revoking through a currency you administer must not reach a grant on one you do not");
    }

    [Fact]
    public async Task GetMintAuthorities_AddsDisplayNames_ForUsersAndTheSystemPrincipal()
    {
        var currency = GuildCurrency();
        var resolver = new Mock<IDiscordUserResolver>();
        resolver
            .Setup(r => r.ResolveUsersAsync(It.IsAny<IEnumerable<ulong>>()))
            .ReturnsAsync(new Dictionary<ulong, (string Username, string? AvatarUrl)>
            {
                [111UL] = ("alice", null),
                [222UL] = ("Unknown#222", null)
            });

        var controller = new CurrenciesController(
            _auditLog.Object,
            Mock.Of<ILogger<CurrenciesController>>(),
            _currencyService.Object,
            Access(CurrencyAccessLevel.Administer).Object,
            resolver.Object)
            .WithUser(AdminUser());

        _currencyService
            .Setup(s => s.GetAsync(currency.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
        _currencyService
            .Setup(s => s.GetMintAuthoritiesAsync(currency.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MintAuthorityDto>
            {
                new() { Id = Guid.NewGuid(), CurrencyId = currency.Id, PrincipalType = MintPrincipalType.User, PrincipalId = 111UL },
                new() { Id = Guid.NewGuid(), CurrencyId = currency.Id, PrincipalType = MintPrincipalType.User, PrincipalId = 222UL },
                new() { Id = Guid.NewGuid(), CurrencyId = currency.Id, PrincipalType = MintPrincipalType.Role, PrincipalId = 333UL },
                new() { Id = Guid.NewGuid(), CurrencyId = currency.Id, PrincipalType = MintPrincipalType.System }
            });

        var result = await controller.GetMintAuthorities(currency.Id);

        var named = ((result.Result as OkObjectResult)!.Value as IReadOnlyList<MintAuthorityDto>)!;
        named[0].PrincipalName.Should().Be("alice");
        named[1].PrincipalName.Should().BeNull("an unresolved user keeps only the ID, never a fake name");
        named[2].PrincipalName.Should().BeNull("a role needs a connected guild to be named");
        named[3].PrincipalName.Should().Be("System");
    }

    [Fact]
    public async Task Reconcile_ReturnsTheWalletsThatDisagreeWithTheirLedger()
    {
        var currency = GuildCurrency();
        var controller = Build(CurrencyAccessLevel.Administer);

        var drifted = new List<WalletReconciliationDto>
        {
            new() { WalletId = Guid.NewGuid(), UserId = 5UL, CachedBalance = 10, LedgerSum = 7 }
        };

        _currencyService
            .Setup(s => s.GetAsync(currency.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);

        _currencyService
            .Setup(s => s.ReconcileAsync(currency.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(drifted);

        var result = await controller.Reconcile(currency.Id);

        (result.Result as OkObjectResult)!.Value.Should().BeEquivalentTo(drifted);
    }

    [Fact]
    public async Task CreateGlobalCurrency_ForcesTheGlobalScope()
    {
        var controller = Build(CurrencyAccessLevel.Administer);
        CurrencyCreateDto? captured = null;

        _currencyService
            .Setup(s => s.CreateAsync(It.IsAny<CurrencyCreateDto>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .Callback<CurrencyCreateDto, ulong, CancellationToken>((dto, _, _) => captured = dto)
            .ReturnsAsync(new CurrencyResult { Success = true, Currency = GlobalCurrency() });

        await controller.CreateGlobalCurrency(new CurrencyCreateDto
        {
            Scope = CurrencyScope.Guild,
            GuildId = GuildId,
            Name = "Bot Credit",
            Symbol = "💳"
        });

        captured!.Scope.Should().Be(CurrencyScope.Global);
        captured.GuildId.Should().BeNull();
    }
}
