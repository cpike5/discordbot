using DiscordBot.Bot.Controllers;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.Authorization;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using static DiscordBot.Tests.Controllers.Currency.CurrencyControllerTestContext;

namespace DiscordBot.Tests.Controllers.Currency;

/// <summary>
/// Tests for <see cref="WalletsController"/>: who may read a wallet, who may move a balance, and
/// the rules the portal has to apply before the services see the request.
/// </summary>
[Trait("Category", "Unit")]
public class WalletsControllerTests
{
    private const ulong TargetUserId = 500000000000000005UL;

    private readonly Mock<ICurrencyService> _currencyService = new();
    private readonly Mock<IWalletService> _walletService = new();
    private readonly Mock<IWalletRepository> _walletRepository = new();
    private readonly Mock<IMintService> _mintService = new();
    private readonly Mock<ILedgerRepository> _ledgerRepository = new();
    private readonly Mock<IDiscordUserResolver> _userResolver = new();
    private readonly Mock<IModerationService> _moderationService = new();
    private readonly Mock<ICurrencyAccessService> _access = Access(CurrencyAccessLevel.Administer);

    public WalletsControllerTests()
    {
        _userResolver
            .Setup(r => r.ResolveUsersAsync(It.IsAny<IEnumerable<ulong>>()))
            .ReturnsAsync(new Dictionary<ulong, (string Username, string? AvatarUrl)>());
    }

    private WalletsController Build(CurrencyAccessLevel level, ulong actingAs = ActorId)
    {
        _access
            .Setup(a => a.GetAccessAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CurrencyDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(level);

        return new WalletsController(
            _userResolver.Object,
            _moderationService.Object,
            Mock.Of<ILogger<WalletsController>>(),
            _currencyService.Object,
            _access.Object,
            _walletService.Object,
            _walletRepository.Object,
            _mintService.Object,
            _ledgerRepository.Object)
            .WithUser(User(actingAs, Roles.Moderator));
    }

    private CurrencyDto SetUpCurrency(CurrencyDto? currency = null)
    {
        var resolved = currency ?? GuildCurrency();

        _currencyService
            .Setup(s => s.GetAsync(resolved.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolved);

        return resolved;
    }

    [Fact]
    public async Task GetWallets_ReturnsNotFound_WhenTheCallerCannotSeeTheCurrency()
    {
        var currency = SetUpCurrency();
        var controller = Build(CurrencyAccessLevel.None);

        var result = await controller.GetWallets(currency.Id);

        StatusOf(result.Result).Should().Be(404);
        _walletRepository.Verify(
            r => r.GetForCurrencyAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetWallets_NamesTheHoldersAndFlagsDebt()
    {
        var currency = SetUpCurrency();
        var controller = Build(CurrencyAccessLevel.Read);

        _walletRepository
            .Setup(r => r.GetForCurrencyAsync(currency.Id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Wallet>
            {
                new() { Id = Guid.NewGuid(), CurrencyId = currency.Id, UserId = TargetUserId, CachedBalance = -40 }
            });

        _userResolver
            .Setup(r => r.ResolveUsersAsync(It.IsAny<IEnumerable<ulong>>()))
            .ReturnsAsync(new Dictionary<ulong, (string Username, string? AvatarUrl)>
            {
                [TargetUserId] = ("ratfan", null)
            });

        var result = await controller.GetWallets(currency.Id);

        var holders = (result.Result as OkObjectResult)!.Value as IReadOnlyList<WalletHolderDto>;
        holders.Should().ContainSingle();
        holders![0].Username.Should().Be("ratfan");
        holders[0].IsInDebt.Should().BeTrue();
    }

    [Fact]
    public async Task GetLedger_LetsAWalletOwnerReadTheirOwnHistory()
    {
        var currency = SetUpCurrency();
        var walletId = Guid.NewGuid();

        _walletRepository
            .Setup(r => r.GetAsync(walletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Wallet { Id = walletId, CurrencyId = currency.Id, UserId = ActorId });

        _walletService
            .Setup(s => s.GetHistoryAsync(walletId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<LedgerTransactionDto> { Page = 1, PageSize = 20 });

        // No portal access to the currency at all: the holder still reads their own rows.
        var controller = Build(CurrencyAccessLevel.None);

        var result = await controller.GetLedger(walletId);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetLedger_ReturnsNotFound_ForSomeoneElsesWalletWithoutAccess()
    {
        var currency = SetUpCurrency();
        var walletId = Guid.NewGuid();

        _walletRepository
            .Setup(r => r.GetAsync(walletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Wallet { Id = walletId, CurrencyId = currency.Id, UserId = TargetUserId });

        var controller = Build(CurrencyAccessLevel.None);

        var result = await controller.GetLedger(walletId);

        StatusOf(result.Result).Should().Be(404);
        _walletService.Verify(
            s => s.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetLedger_CapsThePageSize()
    {
        var currency = SetUpCurrency();
        var walletId = Guid.NewGuid();

        _walletRepository
            .Setup(r => r.GetAsync(walletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Wallet { Id = walletId, CurrencyId = currency.Id, UserId = TargetUserId });

        _walletService
            .Setup(s => s.GetHistoryAsync(walletId, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<LedgerTransactionDto>());

        var controller = Build(CurrencyAccessLevel.Read);

        await controller.GetLedger(walletId, page: 0, pageSize: 5000);

        _walletService.VerifyAll();
    }

    [Fact]
    public async Task Mint_PassesTheSignedInUserAsTheActor()
    {
        var currency = SetUpCurrency();
        var controller = Build(CurrencyAccessLevel.Read);

        _mintService
            .Setup(s => s.MintAsync(
                currency.Id, TargetUserId, 25, "payday", LedgerSource.Manual,
                It.IsAny<string>(), ActorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MintResult { Success = true });

        var result = await controller.Mint(currency.Id, new MintRequestDto
        {
            UserId = TargetUserId,
            Amount = 25,
            Reason = "payday"
        });

        result.Result.Should().BeOfType<OkObjectResult>();
        _mintService.VerifyAll();
    }

    [Fact]
    public async Task Mint_ReturnsForbidden_WhenTheCallerIsNotAMintAuthority()
    {
        var currency = SetUpCurrency();
        var controller = Build(CurrencyAccessLevel.Administer);

        _mintService
            .Setup(s => s.MintAsync(
                It.IsAny<Guid>(), It.IsAny<ulong>(), It.IsAny<long>(), It.IsAny<string>(),
                It.IsAny<LedgerSource>(), It.IsAny<string>(), It.IsAny<ulong?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MintResult.Failed(CurrencyErrors.NotAuthorizedToMint));

        var result = await controller.Mint(currency.Id, new MintRequestDto
        {
            UserId = TargetUserId,
            Amount = 25,
            Reason = "payday"
        });

        StatusOf(result.Result).Should().Be(403,
            "administering a currency and being allowed to create units are different permissions");
    }

    [Fact]
    public async Task Mint_RequiresAReason()
    {
        var currency = SetUpCurrency();
        var controller = Build(CurrencyAccessLevel.Administer);

        var result = await controller.Mint(currency.Id, new MintRequestDto
        {
            UserId = TargetUserId,
            Amount = 25,
            Reason = "   "
        });

        StatusOf(result.Result).Should().Be(422);
        _mintService.Verify(
            s => s.MintAsync(It.IsAny<Guid>(), It.IsAny<ulong>(), It.IsAny<long>(), It.IsAny<string>(),
                It.IsAny<LedgerSource>(), It.IsAny<string>(), It.IsAny<ulong?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Fine_RequiresModeratorAccess()
    {
        var currency = SetUpCurrency();
        var controller = Build(CurrencyAccessLevel.Read);

        var result = await controller.Fine(currency.Id, new FineRequestDto
        {
            UserId = TargetUserId,
            Amount = 10,
            Reason = "spam"
        });

        StatusOf(result.Result).Should().Be(403);
    }

    [Fact]
    public async Task Fine_RefusesAGlobalCurrency()
    {
        var currency = SetUpCurrency(GlobalCurrency());
        var controller = Build(CurrencyAccessLevel.Administer);

        var result = await controller.Fine(currency.Id, new FineRequestDto
        {
            UserId = TargetUserId,
            Amount = 10,
            Reason = "spam"
        });

        var error = (result.Result as ObjectResult)?.Value as ApiErrorDto;
        error!.ErrorCode.Should().Be(CurrencyErrors.FineRequiresGuildCurrency,
            "fines are a guild moderation action");
    }

    [Fact]
    public async Task Fine_RefusesToFineYourself()
    {
        var currency = SetUpCurrency();
        var controller = Build(CurrencyAccessLevel.Moderate);

        var result = await controller.Fine(currency.Id, new FineRequestDto
        {
            UserId = ActorId,
            Amount = 10,
            Reason = "spam"
        });

        StatusOf(result.Result).Should().Be(403);
        _walletService.Verify(
            s => s.FineAsync(It.IsAny<Guid>(), It.IsAny<ulong>(), It.IsAny<long>(), It.IsAny<string>(),
                It.IsAny<ulong>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Fine_RefusesAModeratorFiningAnAdministrator()
    {
        var currency = SetUpCurrency();
        var controller = Build(CurrencyAccessLevel.Moderate);

        _access.Setup(a => a.IsGuildAdministrator(GuildId, TargetUserId)).Returns(true);
        _access.Setup(a => a.IsGuildAdministrator(GuildId, ActorId)).Returns(false);

        var result = await controller.Fine(currency.Id, new FineRequestDto
        {
            UserId = TargetUserId,
            Amount = 10,
            Reason = "spam"
        });

        StatusOf(result.Result).Should().Be(403,
            "the same role hierarchy rule the /wallet fine command follows");
    }

    [Fact]
    public async Task Fine_OpensAModCaseAndLinksIt_WhenAsked()
    {
        var currency = SetUpCurrency();
        var controller = Build(CurrencyAccessLevel.Moderate);
        var caseId = Guid.NewGuid();

        _moderationService
            .Setup(s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModerationCaseDto { Id = caseId, CaseNumber = 7 });

        _walletService
            .Setup(s => s.FineAsync(currency.Id, TargetUserId, 10, "spam", ActorId, caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FineResult { Success = true });

        var result = await controller.Fine(currency.Id, new FineRequestDto
        {
            UserId = TargetUserId,
            Amount = 10,
            Reason = "spam",
            OpenCase = true
        });

        result.Result.Should().BeOfType<OkObjectResult>();
        _walletService.VerifyAll();
    }

    [Fact]
    public async Task Fine_StillApplies_WhenOpeningTheModCaseFails()
    {
        var currency = SetUpCurrency();
        var controller = Build(CurrencyAccessLevel.Moderate);

        _moderationService
            .Setup(s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("discord is down"));

        _walletService
            .Setup(s => s.FineAsync(currency.Id, TargetUserId, 10, "spam", ActorId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FineResult { Success = true });

        var result = await controller.Fine(currency.Id, new FineRequestDto
        {
            UserId = TargetUserId,
            Amount = 10,
            Reason = "spam",
            OpenCase = true
        });

        result.Result.Should().BeOfType<OkObjectResult>("the fine is the point; a failed case link should not swallow it");
    }

    [Fact]
    public async Task Adjust_RequiresAdministerAccessOnTheWalletsCurrency()
    {
        var currency = SetUpCurrency();
        var walletId = Guid.NewGuid();

        _ledgerRepository
            .Setup(r => r.GetByIdAsync(42L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LedgerTransaction { Id = 42L, WalletId = walletId });

        _walletRepository
            .Setup(r => r.GetAsync(walletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Wallet { Id = walletId, CurrencyId = currency.Id, UserId = TargetUserId });

        var controller = Build(CurrencyAccessLevel.Moderate);

        var result = await controller.Adjust(42L, new AdjustRequestDto { Amount = -5, Reason = "double charge" });

        StatusOf(result.Result).Should().Be(403);
        _walletService.Verify(
            s => s.AdjustAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Adjust_ReturnsNotFound_ForAnUnknownTransaction()
    {
        _ledgerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LedgerTransaction?)null);

        var controller = Build(CurrencyAccessLevel.Administer);

        var result = await controller.Adjust(42L, new AdjustRequestDto { Amount = -5, Reason = "double charge" });

        StatusOf(result.Result).Should().Be(404);
    }

    [Fact]
    public async Task Adjust_WritesTheCorrection_ForAnAdministrator()
    {
        var currency = SetUpCurrency();
        var walletId = Guid.NewGuid();

        _ledgerRepository
            .Setup(r => r.GetByIdAsync(42L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LedgerTransaction { Id = 42L, WalletId = walletId });

        _walletRepository
            .Setup(r => r.GetAsync(walletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Wallet { Id = walletId, CurrencyId = currency.Id, UserId = TargetUserId });

        _walletService
            .Setup(s => s.AdjustAsync(42L, -5, "double charge", ActorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdjustmentResult { Success = true });

        var controller = Build(CurrencyAccessLevel.Administer);

        var result = await controller.Adjust(42L, new AdjustRequestDto { Amount = -5, Reason = "double charge" });

        result.Result.Should().BeOfType<OkObjectResult>();
        _walletService.VerifyAll();
    }
}
