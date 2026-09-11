using Discord.WebSocket;
using DiscordBot.Bot.Pages.Guilds.Currency;
using DiscordBot.Core.Constants;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Guilds.Currency;

/// <summary>
/// Tests for the prices page model. The point of this page is the feature key: the price an
/// administrator saves has to land under the same string
/// <c>SoundboardOrchestrationService</c> asks the charge seam for, or the sound looks priced in
/// the portal and plays free in Discord.
/// </summary>
[Trait("Category", "Unit")]
public class PricesModelTests : IDisposable
{
    private const ulong TestGuildId = 987654321UL;

    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<ISoundService> _soundService = new();
    private readonly Mock<ISettingsService> _settingsService = new();
    private readonly Mock<ICurrencyService> _currencyService = new();
    private readonly DiscordSocketClient _discordClient = new();

    public PricesModelTests()
    {
        _guildService
            .Setup(s => s.GetGuildByIdAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildDto { Id = TestGuildId, Name = "Test Guild" });

        _settingsService
            .Setup(s => s.GetSettingValueAsync<bool?>("Features:CurrencyEnabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _soundService
            .Setup(s => s.GetAllByGuildAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Sound>());

        _currencyService
            .Setup(s => s.GetPricesForGuildAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PriceEntryDto>());

        _currencyService
            .Setup(s => s.GetVisibleInGuildAsync(TestGuildId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CurrencyDto>());
    }

    public void Dispose() => _discordClient.Dispose();

    private PricesModel Build(ICurrencyService? currencyService = null)
    {
        var model = new PricesModel(
            _guildService.Object,
            _soundService.Object,
            _discordClient,
            _settingsService.Object,
            Mock.Of<ILogger<PricesModel>>(),
            currencyService);

        model.PageContext = new PageContext();
        return model;
    }

    private void WithSounds(params Sound[] sounds) =>
        _soundService
            .Setup(s => s.GetAllByGuildAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sounds);

    private void WithPrices(params PriceEntryDto[] prices) =>
        _currencyService
            .Setup(s => s.GetPricesForGuildAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prices);

    private static Sound MakeSound(Guid id, string name) => new()
    {
        Id = id,
        GuildId = TestGuildId,
        Name = name,
        FileName = $"{name}.mp3",
        DurationSeconds = 4.2,
        PlayCount = 3
    };

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheCurrencyFeatureIsDisabled()
    {
        var model = Build(currencyService: null);

        var result = await model.OnGetAsync(TestGuildId);

        result.Should().BeOfType<NotFoundResult>(
            "with Currency:Enabled false the services are never registered and the page should not exist");
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_ForAnUnknownGuild()
    {
        _guildService
            .Setup(s => s.GetGuildByIdAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        var result = await Build(_currencyService.Object).OnGetAsync(TestGuildId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task OnGetAsync_KeysEverySoundWithTheSoundboardFeatureKey()
    {
        var soundId = Guid.NewGuid();
        WithSounds(MakeSound(soundId, "airhorn"));

        var model = Build(_currencyService.Object);
        await model.OnGetAsync(TestGuildId);

        model.ViewModel.Sounds.Should().ContainSingle();
        model.ViewModel.Sounds[0].FeatureKey.Should().Be(CurrencyFeatureKeys.Soundboard(soundId));
        model.ViewModel.Sounds[0].FeatureKey.Should().Be($"soundboard:{soundId}",
            "the charge seam and the portal price badge both look sounds up by this exact shape");
    }

    [Fact]
    public async Task OnGetAsync_MatchesAPriceToItsSound()
    {
        var soundId = Guid.NewGuid();
        WithSounds(MakeSound(soundId, "airhorn"));
        WithPrices(new PriceEntryDto
        {
            FeatureKey = CurrencyFeatureKeys.Soundboard(soundId),
            GuildId = TestGuildId,
            Amount = 5,
            CurrencySymbol = "🪙",
            IsActive = true
        });

        var model = Build(_currencyService.Object);
        await model.OnGetAsync(TestGuildId);

        var row = model.ViewModel.Sounds.Single();
        row.IsPriced.Should().BeTrue();
        row.Price!.Amount.Should().Be(5);
        model.ViewModel.PricedCount.Should().Be(1);
    }

    [Fact]
    public async Task OnGetAsync_TreatsADeactivatedPriceAsFree()
    {
        var soundId = Guid.NewGuid();
        WithSounds(MakeSound(soundId, "airhorn"));
        WithPrices(new PriceEntryDto
        {
            FeatureKey = CurrencyFeatureKeys.Soundboard(soundId),
            GuildId = TestGuildId,
            Amount = 5,
            IsActive = false
        });

        var model = Build(_currencyService.Object);
        await model.OnGetAsync(TestGuildId);

        model.ViewModel.Sounds.Single().IsPriced.Should().BeFalse(
            "an inactive entry costs nothing, and the page must agree with the charge seam");
        model.ViewModel.PricedCount.Should().Be(0);
    }

    [Fact]
    public async Task OnGetAsync_PrefersTheGuildEntryOverTheOneThatAppliesEverywhere()
    {
        var soundId = Guid.NewGuid();
        var key = CurrencyFeatureKeys.Soundboard(soundId);
        WithSounds(MakeSound(soundId, "airhorn"));

        WithPrices(
            new PriceEntryDto { FeatureKey = key, GuildId = null, Amount = 99, IsActive = true },
            new PriceEntryDto { FeatureKey = key, GuildId = TestGuildId, Amount = 5, IsActive = true });

        var model = Build(_currencyService.Object);
        await model.OnGetAsync(TestGuildId);

        model.ViewModel.Sounds.Single().Price!.Amount.Should().Be(5,
            "the price lookup resolves the guild's own entry first, and the page has to show what will be charged");
    }

    [Fact]
    public async Task OnGetAsync_ListsPricesThatAreNotSoundsSeparately()
    {
        var soundId = Guid.NewGuid();
        WithSounds(MakeSound(soundId, "airhorn"));

        WithPrices(
            new PriceEntryDto { FeatureKey = CurrencyFeatureKeys.Soundboard(soundId), GuildId = TestGuildId, Amount = 5, IsActive = true },
            new PriceEntryDto { FeatureKey = "media:image", GuildId = null, Amount = 20, IsActive = true },
            // A price on a sound that no longer exists in this guild is not editable here either.
            new PriceEntryDto { FeatureKey = CurrencyFeatureKeys.Soundboard(Guid.NewGuid()), GuildId = TestGuildId, Amount = 3, IsActive = true });

        var model = Build(_currencyService.Object);
        await model.OnGetAsync(TestGuildId);

        model.ViewModel.OtherPrices.Should().HaveCount(2,
            "nothing that costs money should be invisible on the page that owns prices");
        model.ViewModel.OtherPrices.Should().Contain(p => p.FeatureKey == "media:image");
    }

    [Fact]
    public async Task OnGetAsync_OffersOnlyActiveCurrenciesAndSortsSoundsByName()
    {
        WithSounds(MakeSound(Guid.NewGuid(), "zebra"), MakeSound(Guid.NewGuid(), "airhorn"));

        _currencyService
            .Setup(s => s.GetVisibleInGuildAsync(TestGuildId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CurrencyDto>
            {
                new() { Id = Guid.NewGuid(), Name = "Rat Coin", Symbol = "🪙", Scope = CurrencyScope.Guild, GuildId = TestGuildId, IsActive = true }
            });

        var model = Build(_currencyService.Object);
        await model.OnGetAsync(TestGuildId);

        model.ViewModel.Sounds.Select(s => s.SoundName).Should().ContainInOrder("airhorn", "zebra");
        model.ViewModel.Currencies.Should().ContainSingle().Which.Name.Should().Be("Rat Coin");
    }

    [Fact]
    public async Task OnGetAsync_FlagsTheRuntimeSwitchWhenCurrencyFeaturesAreOff()
    {
        _settingsService
            .Setup(s => s.GetSettingValueAsync<bool?>("Features:CurrencyEnabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var model = Build(_currencyService.Object);
        await model.OnGetAsync(TestGuildId);

        model.IsCurrencyRuntimeDisabled.Should().BeTrue(
            "prices are kept but nothing is charged, and the page says so rather than lying about it");
    }
}
