using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DiscordBot.Tests.Preconditions;

/// <summary>
/// Unit tests for <see cref="RequireNotXEnabledAttribute"/>.
/// </summary>
public class RequireNotXEnabledAttributeTests
{
    private readonly Mock<IInteractionContext> _mockContext;
    private readonly Mock<ICommandInfo> _mockCommandInfo;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly RequireNotXEnabledAttribute _attribute;

    public RequireNotXEnabledAttributeTests()
    {
        _mockContext = new Mock<IInteractionContext>();
        _mockCommandInfo = new Mock<ICommandInfo>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockSettingsService = new Mock<ISettingsService>();
        _attribute = new RequireNotXEnabledAttribute();

        var mockGuild = new Mock<IGuild>();
        mockGuild.Setup(g => g.Id).Returns(123456789UL);
        _mockContext.Setup(c => c.Guild).Returns(mockGuild.Object);

        SetConfigurationEnabled(true);
        SetGlobalSetting(true);
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(ISettingsService)))
            .Returns(_mockSettingsService.Object);
    }

    private void SetConfigurationEnabled(bool enabled)
    {
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IOptions<NotXOptions>)))
            .Returns(Options.Create(new NotXOptions { Enabled = enabled }));
    }

    private void SetGlobalSetting(bool? value)
    {
        _mockSettingsService
            .Setup(s => s.GetSettingValueAsync<bool?>(
                "Features:NotXEnabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync(value);
    }

    private Task<PreconditionResult> CheckAsync() => _attribute.CheckRequirementsAsync(
        _mockContext.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);

    [Fact]
    public async Task CheckRequirementsAsync_WhenEnabledEverywhere_ShouldReturnSuccess()
    {
        var result = await CheckAsync();

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenGlobalSettingRowIsAbsent_ShouldReturnSuccess()
    {
        SetGlobalSetting(null);

        var result = await CheckAsync();

        result.IsSuccess.Should().BeTrue("an unsaved setting defaults to enabled");
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenDisabledInConfiguration_ShouldReturnError()
    {
        SetConfigurationEnabled(false);

        var result = await CheckAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorReason.Should().Contain("configuration");
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenDisabledByGlobalSetting_ShouldReturnError()
    {
        SetGlobalSetting(false);

        var result = await CheckAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorReason.Should().Contain("administrator");
    }

    [Fact]
    public async Task CheckRequirementsAsync_WithoutGuildContext_ShouldReturnError()
    {
        _mockContext.Setup(c => c.Guild).Returns((IGuild?)null);

        var result = await CheckAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorReason.Should().Contain("server");
    }

    [Fact]
    public async Task CheckRequirementsAsync_ShouldNotConsultPerGuildSettings()
    {
        // The /notx commands are themselves the per-guild configuration surface, so gating
        // them on NotXGuildSettings.IsEnabled would make `/notx enable` unreachable once a
        // guild had disabled itself. Resolving that repository at all is the bug to catch.
        var mockGuildSettingsRepo = new Mock<INotXGuildSettingsRepository>();
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(INotXGuildSettingsRepository)))
            .Returns(mockGuildSettingsRepo.Object);

        var result = await CheckAsync();

        result.IsSuccess.Should().BeTrue();
        mockGuildSettingsRepo.Verify(
            r => r.GetOrCreateAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never);
        mockGuildSettingsRepo.Verify(
            r => r.GetByGuildIdAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
