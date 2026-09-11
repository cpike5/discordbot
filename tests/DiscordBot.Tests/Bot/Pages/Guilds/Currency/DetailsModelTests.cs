using System.Security.Claims;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Pages.Guilds.Currency;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Guilds.Currency;

/// <summary>
/// Tests for the currency detail page model: which currencies it will render, and how it decides
/// what the viewer may do. The page asks the same access seam the API routes ask, so the buttons
/// it renders and the calls the API accepts cannot drift apart.
/// </summary>
[Trait("Category", "Unit")]
public class DetailsModelTests
{
    private const ulong TestGuildId = 987654321UL;
    private const ulong ActorId = 100000000000000001UL;

    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<ISettingsService> _settingsService = new();
    private readonly Mock<ICurrencyService> _currencyService = new();
    private readonly Mock<ICurrencyAccessService> _access = new();
    private readonly Mock<IWalletRepository> _walletRepository = new();
    private readonly Mock<IMintService> _mintService = new();

    private readonly CurrencyDto _currency = new()
    {
        Id = Guid.NewGuid(),
        Scope = CurrencyScope.Guild,
        GuildId = TestGuildId,
        Name = "Rat Coin",
        Symbol = "🪙",
        IsActive = true
    };

    public DetailsModelTests()
    {
        _guildService
            .Setup(s => s.GetGuildByIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildDto { Id = TestGuildId, Name = "Test Guild" });

        _settingsService
            .Setup(s => s.GetSettingValueAsync<bool?>("Features:CurrencyEnabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _currencyService
            .Setup(s => s.GetAsync(_currency.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_currency);

        _walletRepository
            .Setup(r => r.GetForCurrencyAsync(It.IsAny<Guid>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Wallet>
            {
                new() { Id = Guid.NewGuid(), CurrencyId = _currency.Id, UserId = 1UL, CachedBalance = 30 },
                new() { Id = Guid.NewGuid(), CurrencyId = _currency.Id, UserId = 2UL, CachedBalance = -10 }
            });

        _access
            .Setup(a => a.GetGuildRoleIdsAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ulong>());
    }

    private DetailsModel Build(CurrencyAccessLevel level, bool canMint = false)
    {
        _access
            .Setup(a => a.GetAccessAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CurrencyDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(level);

        _mintService
            .Setup(s => s.CanMintAsync(It.IsAny<Guid>(), It.IsAny<ulong>(), It.IsAny<IReadOnlyCollection<ulong>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(canMint);

        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("discord:user_id", ActorId.ToString()) }, "TestAuth"));

        var model = new DetailsModel(
            _guildService.Object,
            _settingsService.Object,
            Mock.Of<ILogger<DetailsModel>>(),
            _currencyService.Object,
            _access.Object,
            _walletRepository.Object,
            _mintService.Object);

        model.PageContext = new PageContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        return model;
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheCurrencyBelongsToAnotherGuild()
    {
        _currencyService
            .Setup(s => s.GetAsync(_currency.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_currency with { GuildId = 111UL });

        var result = await Build(CurrencyAccessLevel.Administer).OnGetAsync(TestGuildId, _currency.Id);

        result.Should().BeOfType<NotFoundResult>(
            "the route scopes this page to one guild, so another guild's currency is not addressable here");
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_ForAGlobalCurrency()
    {
        _currencyService
            .Setup(s => s.GetAsync(_currency.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_currency with { Scope = CurrencyScope.Global, GuildId = null });

        var result = await Build(CurrencyAccessLevel.Administer).OnGetAsync(TestGuildId, _currency.Id);

        result.Should().BeOfType<NotFoundResult>("a global currency's wallets live on the bot-wide page");
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheViewerHasNoAccess()
    {
        var result = await Build(CurrencyAccessLevel.None).OnGetAsync(TestGuildId, _currency.Id);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task OnGetAsync_GivesAModeratorFiningButNotAdministration()
    {
        var model = Build(CurrencyAccessLevel.Moderate);

        await model.OnGetAsync(TestGuildId, _currency.Id);

        model.ViewModel.CanFine.Should().BeTrue();
        model.ViewModel.CanAdminister.Should().BeFalse();
        model.WalletPanel.CanAdminister.Should().BeFalse("adjusting a row is an administrator's action");
    }

    [Fact]
    public async Task OnGetAsync_OffersMintingOnlyToAMintAuthority()
    {
        var withoutAuthority = Build(CurrencyAccessLevel.Administer, canMint: false);
        await withoutAuthority.OnGetAsync(TestGuildId, _currency.Id);
        withoutAuthority.ViewModel.CanMint.Should().BeFalse(
            "administering a currency and being allowed to create units are different permissions");

        var withAuthority = Build(CurrencyAccessLevel.Administer, canMint: true);
        await withAuthority.OnGetAsync(TestGuildId, _currency.Id);
        withAuthority.ViewModel.CanMint.Should().BeTrue();
    }

    [Fact]
    public async Task OnGetAsync_TotalsHoldersCirculationAndDebtors()
    {
        var model = Build(CurrencyAccessLevel.Administer);

        await model.OnGetAsync(TestGuildId, _currency.Id);

        model.ViewModel.HolderCount.Should().Be(2);
        model.ViewModel.Circulation.Should().Be(20);
        model.ViewModel.DebtorCount.Should().Be(1);
    }

    [Fact]
    public async Task OnGetAsync_TurnsOffEveryActionOnADeactivatedCurrency()
    {
        _currencyService
            .Setup(s => s.GetAsync(_currency.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_currency with { IsActive = false });

        var model = Build(CurrencyAccessLevel.Administer, canMint: true);

        await model.OnGetAsync(TestGuildId, _currency.Id);

        model.ViewModel.CanFine.Should().BeFalse("a deactivated currency refuses everything except reading");
        model.ViewModel.CanMint.Should().BeFalse();
        model.WalletPanel.CanAdminister.Should().BeFalse();
    }
}
