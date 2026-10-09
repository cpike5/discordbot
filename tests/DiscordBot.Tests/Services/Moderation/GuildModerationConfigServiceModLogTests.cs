using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services.Moderation;

/// <summary>
/// The mod-log columns round-trip through <see cref="GuildModerationConfigService"/>, and a preset
/// (which replaces the rules) leaves them alone.
/// </summary>
public class GuildModerationConfigServiceModLogTests
{
    private const ulong GuildId = 100UL;

    private readonly Mock<IGuildModerationConfigRepository> _repository = new();
    private readonly GuildModerationConfigService _service;
    private GuildModerationConfig? _stored;

    public GuildModerationConfigServiceModLogTests()
    {
        _repository
            .Setup(r => r.GetByGuildIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _stored);
        _repository
            .Setup(r => r.AddAsync(It.IsAny<GuildModerationConfig>(), It.IsAny<CancellationToken>()))
            .Callback<GuildModerationConfig, CancellationToken>((c, _) => _stored = c)
            .ReturnsAsync((GuildModerationConfig c, CancellationToken _) => c);
        _repository
            .Setup(r => r.UpdateAsync(It.IsAny<GuildModerationConfig>(), It.IsAny<CancellationToken>()))
            .Callback<GuildModerationConfig, CancellationToken>((c, _) => _stored = c)
            .Returns(Task.CompletedTask);

        _service = new GuildModerationConfigService(_repository.Object, Mock.Of<ILogger<GuildModerationConfigService>>());
    }

    [Fact]
    public async Task Default_HasNoChannel_AndEveryKind()
    {
        var config = await _service.GetConfigAsync(GuildId);

        config.ModLogChannelId.Should().BeNull();
        config.ModLogEvents.Should().Be(ModLogEventKinds.All);
    }

    [Fact]
    public async Task Update_StoresChannelAndKinds_AndReadsThemBack()
    {
        var dto = await _service.GetConfigAsync(GuildId);
        dto.ModLogChannelId = 555;
        dto.ModLogEvents = ModLogEventKinds.Cases;

        await _service.UpdateConfigAsync(GuildId, dto);
        var read = await _service.GetConfigAsync(GuildId);

        _stored!.ModLogChannelId.Should().Be(555);
        _stored.ModLogEvents.Should().Be(ModLogEventKinds.Cases);
        read.ModLogChannelId.Should().Be(555);
        read.ModLogEvents.Should().Be(ModLogEventKinds.Cases);
    }

    [Fact]
    public async Task Update_CanTurnTheFeedOff()
    {
        var dto = await _service.GetConfigAsync(GuildId);
        dto.ModLogChannelId = 555;
        await _service.UpdateConfigAsync(GuildId, dto);

        dto.ModLogChannelId = null;
        await _service.UpdateConfigAsync(GuildId, dto);

        _stored!.ModLogChannelId.Should().BeNull();
    }

    [Fact]
    public async Task ApplyPreset_ReplacesTheRules_ButKeepsTheModLogChannel()
    {
        var dto = await _service.GetConfigAsync(GuildId);
        dto.ModLogChannelId = 555;
        dto.ModLogEvents = ModLogEventKinds.Cases | ModLogEventKinds.AutoActions;
        await _service.UpdateConfigAsync(GuildId, dto);

        var result = await _service.ApplyPresetAsync(GuildId, "Strict");

        result.SimplePreset.Should().Be("Strict");
        result.SpamConfig.AutoAction.Should().Be(AutoAction.Mute, "the preset's rules were applied");
        result.ModLogChannelId.Should().Be(555);
        result.ModLogEvents.Should().Be(ModLogEventKinds.Cases | ModLogEventKinds.AutoActions);
        _stored!.ModLogChannelId.Should().Be(555);
    }
}
