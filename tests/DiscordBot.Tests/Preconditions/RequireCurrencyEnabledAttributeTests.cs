using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace DiscordBot.Tests.Preconditions;

/// <summary>
/// Unit tests for <see cref="RequireCurrencyEnabledAttribute"/>.
/// </summary>
public class RequireCurrencyEnabledAttributeTests
{
    private readonly Mock<IInteractionContext> _mockContext = new();
    private readonly Mock<ICommandInfo> _mockCommandInfo = new();
    private readonly Mock<IServiceProvider> _mockServiceProvider = new();
    private readonly Mock<ICurrencyService> _mockCurrencyService = new();
    private readonly Mock<ISettingsService> _mockSettingsService = new();
    private readonly RequireCurrencyEnabledAttribute _attribute = new();

    public RequireCurrencyEnabledAttributeTests()
    {
        _mockContext.Setup(c => c.Guild).Returns(Mock.Of<IGuild>(g => g.Id == 123UL));

        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(ICurrencyService)))
            .Returns(_mockCurrencyService.Object);
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(ISettingsService)))
            .Returns(_mockSettingsService.Object);

        _mockSettingsService
            .Setup(s => s.GetSettingValueAsync<bool?>("Features:CurrencyEnabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync((bool?)null);
    }

    private Task<PreconditionResult> CheckAsync() => _attribute.CheckRequirementsAsync(
        _mockContext.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);

    [Fact]
    public async Task CheckRequirementsAsync_WhenSettingUnset_DefaultsToEnabled()
    {
        var result = await CheckAsync();

        result.IsSuccess.Should().BeTrue("an unset feature flag means the feature is on");
    }

    [Fact]
    public async Task CheckRequirementsAsync_OutsideGuild_Fails()
    {
        _mockContext.Setup(c => c.Guild).Returns((IGuild?)null);

        var result = await CheckAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorReason.Should().Contain("server");
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenCurrencyServicesNotRegistered_Fails()
    {
        // Currency:Enabled false means AddCurrency never ran, so the service is simply absent.
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(ICurrencyService))).Returns(null!);

        var result = await CheckAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorReason.Should().Contain("not enabled");
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenDisabledBySetting_Fails()
    {
        _mockSettingsService
            .Setup(s => s.GetSettingValueAsync<bool?>("Features:CurrencyEnabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CheckAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorReason.Should().Contain("disabled");
    }
}
