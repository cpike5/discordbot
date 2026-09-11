using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Constants;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// What the soundboard costs. The charge seam is the real one over an in-memory ledger, so these
/// assert on rows written rather than on calls made: a refused play writes nothing, an accepted
/// play writes one <c>Spend</c>, and a play that falls over gives the money back.
/// </summary>
public class SoundboardOrchestrationServiceChargeTests : IDisposable
{
    private const ulong Guild = 1001UL;
    private const ulong Player = 111UL;

    private readonly CurrencyTestContext _currency = new();

    private readonly Mock<ISoundService> _sounds = new();
    private readonly Mock<ISoundFileService> _files = new();
    private readonly Mock<IPlaybackService> _playback = new();
    private readonly Mock<IAudioService> _audio = new();
    private readonly Mock<IGuildAudioSettingsService> _audioSettings = new();
    private readonly Mock<ISettingsService> _settings = new();
    private readonly Mock<IAudioNotifier> _notifier = new();
    private readonly Mock<IAudioModerationLogService> _moderationLog = new();
    private readonly Mock<DiscordSocketClient> _discord = new();

    private readonly Sound _sound = new()
    {
        Id = Guid.NewGuid(),
        GuildId = Guild,
        Name = "airhorn",
        FileName = "airhorn.mp3",
        DurationSeconds = 2
    };

    public SoundboardOrchestrationServiceChargeTests()
    {
        _settings
            .Setup(s => s.GetSettingValueAsync<bool?>("Features:AudioEnabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _audioSettings
            .Setup(s => s.GetSettingsAsync(Guild, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAudioSettings { GuildId = Guild, AudioEnabled = true });

        _audio.Setup(a => a.IsConnected(Guild)).Returns(true);

        _sounds
            .Setup(s => s.GetByIdAsync(_sound.Id, Guild, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_sound);

        _files.Setup(f => f.SoundFileExists(Guild, _sound.FileName)).Returns(true);
    }

    public void Dispose()
    {
        _currency.Dispose();
        GC.SuppressFinalize(this);
    }

    // The default everywhere: nothing is priced, so nothing changes.

    [Fact]
    public async Task PlaySoundAsync_WithNoPrice_PlaysFreeAndWritesNoLedgerRow()
    {
        var service = CreateService();

        var result = await service.PlaySoundAsync(Guild, _sound.Id, Player, queueEnabled: false);

        result.Success.Should().BeTrue();
        result.ChargeStatus.Should().Be(ChargeHoldStatus.Free);
        result.Price.Should().BeNull();
        (await _currency.Db.LedgerTransactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PlaySoundAsync_WithTheCurrencyFeatureOff_PlaysAndReportsNoCharge()
    {
        var service = CreateService(withCharging: false);

        var result = await service.PlaySoundAsync(Guild, _sound.Id, Player, queueEnabled: false);

        result.Success.Should().BeTrue();
        result.ChargeStatus.Should().BeNull();
        _playback.Verify(
            p => p.PlayAsync(Guild, _sound, false, It.IsAny<AudioFilter>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PlaySoundAsync_WhenAnAdminSwitchesCurrencyOffAtRuntime_PlaysAPricedSoundFree()
    {
        var currency = await _currency.SeedCurrencyAsync();
        await _currency.SeedPriceAsync(currency.Id, CurrencyFeatureKeys.Soundboard(_sound.Id), 5);
        await _currency.SeedBalanceAsync(currency.Id, Player, 20);

        _settings
            .Setup(s => s.GetSettingValueAsync<bool?>("Features:CurrencyEnabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();

        var result = await service.PlaySoundAsync(Guild, _sound.Id, Player, queueEnabled: false);

        result.Success.Should().BeTrue();
        result.ChargeStatus.Should().BeNull();
        (await _currency.BalanceOfAsync(currency.Id, Player)).Should().Be(20);
    }

    // A priced sound: held before playback, committed once the sound is accepted.

    [Fact]
    public async Task PlaySoundAsync_WithAPricedSoundAndTheFunds_ChargesOnce()
    {
        var currency = await _currency.SeedCurrencyAsync();
        await _currency.SeedPriceAsync(currency.Id, CurrencyFeatureKeys.Soundboard(_sound.Id), 5);
        await _currency.SeedBalanceAsync(currency.Id, Player, 20);

        var service = CreateService();

        var result = await service.PlaySoundAsync(Guild, _sound.Id, Player, queueEnabled: false);

        result.Success.Should().BeTrue();
        result.ChargeStatus.Should().Be(ChargeHoldStatus.Held);
        result.Price.Should().Be(5);
        result.Balance.Should().Be(15);
        result.CurrencySymbol.Should().Be("C");

        (await _currency.BalanceOfAsync(currency.Id, Player)).Should().Be(15);

        var spends = await _currency.Db.LedgerTransactions
            .Where(t => t.Type == LedgerTransactionType.Spend)
            .ToListAsync();

        spends.Should().HaveCount(1);
        spends[0].Amount.Should().Be(-5);
        spends[0].FeatureKey.Should().Be(CurrencyFeatureKeys.Soundboard(_sound.Id));

        // Nothing is left reserved once the spend is written.
        var wallet = await _currency.Wallets.GetAsync(currency.Id, Player);
        _currency.Holds.SumOpenHolds(wallet!.Id).Should().Be(0);
    }

    [Fact]
    public async Task PlaySoundAsync_WithAPricedSoundAndTheFunds_ChargesEachPlay()
    {
        var currency = await _currency.SeedCurrencyAsync();
        await _currency.SeedPriceAsync(currency.Id, CurrencyFeatureKeys.Soundboard(_sound.Id), 5);
        await _currency.SeedBalanceAsync(currency.Id, Player, 20);

        var service = CreateService();

        await service.PlaySoundAsync(Guild, _sound.Id, Player, queueEnabled: false);
        await service.PlaySoundAsync(Guild, _sound.Id, Player, queueEnabled: false);

        (await _currency.BalanceOfAsync(currency.Id, Player)).Should().Be(10);
    }

    // Refusals happen before any voice work, and say what the sound costs.

    [Fact]
    public async Task PlaySoundAsync_WithoutTheFunds_IsRefusedBeforePlaybackStarts()
    {
        var currency = await _currency.SeedCurrencyAsync();
        await _currency.SeedPriceAsync(currency.Id, CurrencyFeatureKeys.Soundboard(_sound.Id), 5);
        await _currency.SeedBalanceAsync(currency.Id, Player, 2);

        var service = CreateService();

        var result = await service.PlaySoundAsync(Guild, _sound.Id, Player, queueEnabled: false);

        result.Success.Should().BeFalse();
        result.ChargeStatus.Should().Be(ChargeHoldStatus.InsufficientFunds);
        result.Price.Should().Be(5);
        result.Balance.Should().Be(2);
        result.ErrorMessage.Should().Be("This sound costs 5 C. You have 2 C.");

        _playback.Verify(
            p => p.PlayAsync(It.IsAny<ulong>(), It.IsAny<Sound>(), It.IsAny<bool>(), It.IsAny<AudioFilter>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _sounds.Verify(s => s.LogPlayAsync(It.IsAny<Guid>(), It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
        (await _currency.BalanceOfAsync(currency.Id, Player)).Should().Be(2);
    }

    [Fact]
    public async Task PlaySoundAsync_WhileInDebt_IsRefusedWithTheDebtMessage()
    {
        var currency = await _currency.SeedCurrencyAsync(allowNegative: true, debtFloor: -100);
        await _currency.SeedPriceAsync(currency.Id, CurrencyFeatureKeys.Soundboard(_sound.Id), 5);
        await _currency.SeedBalanceAsync(currency.Id, Player, -40);

        var service = CreateService();

        var result = await service.PlaySoundAsync(Guild, _sound.Id, Player, queueEnabled: false);

        result.Success.Should().BeFalse();
        result.ChargeStatus.Should().Be(ChargeHoldStatus.InDebt);
        result.ErrorMessage.Should().Be("You owe 40 C. Priced features are locked until you're back above zero.");
    }

    // Every way out after the hold gives the money back.

    [Fact]
    public async Task PlaySoundAsync_WhenPlaybackThrows_ReleasesTheHold()
    {
        var currency = await _currency.SeedCurrencyAsync();
        await _currency.SeedPriceAsync(currency.Id, CurrencyFeatureKeys.Soundboard(_sound.Id), 5);
        var wallet = await _currency.SeedBalanceAsync(currency.Id, Player, 20);

        _playback
            .Setup(p => p.PlayAsync(Guild, _sound, It.IsAny<bool>(), It.IsAny<AudioFilter>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("voice client gone"));

        var service = CreateService();

        var result = await service.PlaySoundAsync(Guild, _sound.Id, Player, queueEnabled: false);

        result.Success.Should().BeFalse();
        (await _currency.BalanceOfAsync(currency.Id, Player)).Should().Be(20);
        (await _currency.Db.LedgerTransactions.CountAsync(t => t.Type == LedgerTransactionType.Spend)).Should().Be(0);
        _currency.Holds.SumOpenHolds(wallet.Id).Should().Be(0);
    }

    [Fact]
    public async Task PlaySoundAsync_WhenTheFileIsMissing_ReleasesTheHold()
    {
        var currency = await _currency.SeedCurrencyAsync();
        await _currency.SeedPriceAsync(currency.Id, CurrencyFeatureKeys.Soundboard(_sound.Id), 5);
        var wallet = await _currency.SeedBalanceAsync(currency.Id, Player, 20);

        _files.Setup(f => f.SoundFileExists(Guild, _sound.FileName)).Returns(false);

        var service = CreateService();

        var result = await service.PlaySoundAsync(Guild, _sound.Id, Player, queueEnabled: false);

        result.Success.Should().BeFalse();
        result.ChargeStatus.Should().BeNull();
        (await _currency.BalanceOfAsync(currency.Id, Player)).Should().Be(20);
        _currency.Holds.SumOpenHolds(wallet.Id).Should().Be(0);

        // The released funds are available again straight away.
        var retry = await _currency.ChargeService.TryHoldAsync(
            Player, Guild, CurrencyFeatureKeys.Soundboard(_sound.Id), "retry");
        retry.Status.Should().Be(ChargeHoldStatus.Held);
    }

    [Fact]
    public async Task PlaySoundAsync_WhenTheSoundIsGone_HoldsNothingAndStaysFree()
    {
        var currency = await _currency.SeedCurrencyAsync();
        await _currency.SeedPriceAsync(currency.Id, CurrencyFeatureKeys.Soundboard(_sound.Id), 5);
        await _currency.SeedBalanceAsync(currency.Id, Player, 20);

        _sounds
            .Setup(s => s.GetByIdAsync(_sound.Id, Guild, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sound?)null);

        var service = CreateService();

        var result = await service.PlaySoundAsync(Guild, _sound.Id, Player, queueEnabled: false);

        result.Success.Should().BeFalse();
        (await _currency.BalanceOfAsync(currency.Id, Player)).Should().Be(20);
    }

    // The checks that come before the charge still come before it.

    [Fact]
    public async Task PlaySoundAsync_WhenNotConnectedToVoice_NeverReachesTheChargeSeam()
    {
        var currency = await _currency.SeedCurrencyAsync();
        await _currency.SeedPriceAsync(currency.Id, CurrencyFeatureKeys.Soundboard(_sound.Id), 5);
        await _currency.SeedBalanceAsync(currency.Id, Player, 20);

        _audio.Setup(a => a.IsConnected(Guild)).Returns(false);

        var service = CreateService();

        var result = await service.PlaySoundAsync(Guild, _sound.Id, Player, queueEnabled: false);

        result.Success.Should().BeFalse();
        result.ChargeStatus.Should().BeNull();
        result.ErrorMessage.Should().Contain("voice channel");
        (await _currency.BalanceOfAsync(currency.Id, Player)).Should().Be(20);
    }

    [Fact]
    public async Task PlaySoundAsync_ForAnExemptRole_PlaysFree()
    {
        const ulong exemptRole = 555UL;

        var currency = await _currency.SeedCurrencyAsync();
        await _currency.SeedPriceAsync(
            currency.Id, CurrencyFeatureKeys.Soundboard(_sound.Id), 5, Guild, true, exemptRole);
        await _currency.SeedBalanceAsync(currency.Id, Player, 20);
        _currency.SetMemberRoles(Guild, Player, exemptRole);

        var service = CreateService();

        var result = await service.PlaySoundAsync(Guild, _sound.Id, Player, queueEnabled: false);

        result.Success.Should().BeTrue();
        result.ChargeStatus.Should().Be(ChargeHoldStatus.Free);
        (await _currency.BalanceOfAsync(currency.Id, Player)).Should().Be(20);
    }

    /// <summary>
    /// The rollback path: with <c>Currency:Enabled</c> false nothing registers
    /// <see cref="IChargeService"/>, and the soundboard still has to come out of the container.
    /// </summary>
    [Fact]
    public void SoundboardOrchestrationService_ResolvesFromDi_WithoutTheCurrencyFeature()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_sounds.Object);
        services.AddSingleton(_files.Object);
        services.AddSingleton(_playback.Object);
        services.AddSingleton(_audio.Object);
        services.AddSingleton(_audioSettings.Object);
        services.AddSingleton(_settings.Object);
        services.AddSingleton(_notifier.Object);
        services.AddSingleton(_moderationLog.Object);
        services.AddSingleton(_discord.Object);
        services.AddScoped<ISoundboardOrchestrationService, SoundboardOrchestrationService>();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ISoundboardOrchestrationService>().Should().NotBeNull();
    }

    private SoundboardOrchestrationService CreateService(bool withCharging = true) => new(
        _sounds.Object,
        _files.Object,
        _playback.Object,
        _audio.Object,
        _audioSettings.Object,
        _settings.Object,
        _notifier.Object,
        _discord.Object,
        _moderationLog.Object,
        NullLogger<SoundboardOrchestrationService>.Instance,
        withCharging ? _currency.ChargeService : null);
}
