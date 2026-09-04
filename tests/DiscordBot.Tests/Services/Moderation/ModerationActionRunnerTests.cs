using Discord;
using DiscordBot.Bot.Services.Moderation;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services.Moderation;

/// <summary>
/// Unit tests for <see cref="ModerationActionRunner"/> — the shared validate/act/notify/case
/// pipeline behind the warn/kick/ban/unban/mute slash commands.
/// </summary>
public class ModerationActionRunnerTests
{
    private const ulong GuildId = 100UL;
    private const ulong ModeratorId = 200UL;
    private const ulong TargetId = 300UL;
    private const ulong BotId = 999UL;

    private readonly Mock<IModerationService> _mockModerationService;
    private readonly Mock<ILogger<ModerationActionRunner>> _mockLogger;
    private readonly ModerationActionRunner _runner;

    public ModerationActionRunnerTests()
    {
        _mockModerationService = new Mock<IModerationService>();
        _mockLogger = new Mock<ILogger<ModerationActionRunner>>();
        _runner = new ModerationActionRunner(_mockModerationService.Object, _mockLogger.Object);
    }

    private static Mock<IGuild> CreateGuildMock()
    {
        var guild = new Mock<IGuild>();
        guild.SetupGet(g => g.Id).Returns(GuildId);
        guild.SetupGet(g => g.Name).Returns("Test Guild");
        return guild;
    }

    private static Mock<IUser> CreateModeratorMock()
    {
        var moderator = new Mock<IUser>();
        moderator.SetupGet(m => m.Id).Returns(ModeratorId);
        moderator.SetupGet(m => m.Username).Returns("Moderator");
        moderator.SetupGet(m => m.Mention).Returns("<@200>");
        return moderator;
    }

    private static Mock<IModerationCommandContext> CreateContext(
        Mock<IGuild>? guild = null,
        Mock<IUser>? moderator = null,
        int? moderatorHierarchy = 10,
        IGuildUser? resolvedGuildUser = null,
        bool resolveThrowsOrReturnsNull = false)
    {
        guild ??= CreateGuildMock();
        moderator ??= CreateModeratorMock();

        var context = new Mock<IModerationCommandContext>();
        context.SetupGet(c => c.Guild).Returns(guild.Object);
        context.SetupGet(c => c.ModeratorUser).Returns(moderator.Object);
        context.SetupGet(c => c.BotUserId).Returns(BotId);
        context.SetupGet(c => c.ModeratorHierarchy).Returns(moderatorHierarchy);
        context.Setup(c => c.ResolveGuildUserAsync(It.IsAny<IUser>()))
            .ReturnsAsync(resolveThrowsOrReturnsNull ? null : resolvedGuildUser);

        return context;
    }

    private static Mock<IGuildUser> CreateTargetGuildUserMock(int hierarchy = 5)
    {
        var target = new Mock<IGuildUser>();
        target.SetupGet(t => t.Id).Returns(TargetId);
        target.SetupGet(t => t.Username).Returns("Target");
        target.SetupGet(t => t.Mention).Returns("<@300>");
        target.SetupGet(t => t.IsBot).Returns(false);
        target.SetupGet(t => t.Hierarchy).Returns(hierarchy);
        return target;
    }

    private static ModerationCaseDto MakeCaseDto(int caseNumber, CaseType type, DateTime? expiresAt = null) => new()
    {
        Id = Guid.NewGuid(),
        CaseNumber = caseNumber,
        GuildId = GuildId,
        TargetUserId = TargetId,
        ModeratorUserId = ModeratorId,
        Type = type,
        ExpiresAt = expiresAt
    };

    // ---------- Warn ----------

    [Fact]
    public async Task WarnAsync_HappyPath_CreatesCaseAndReturnsSuccessEmbed()
    {
        var target = new Mock<IUser>();
        target.SetupGet(t => t.Id).Returns(TargetId);
        target.SetupGet(t => t.Username).Returns("Target");
        target.SetupGet(t => t.Mention).Returns("<@300>");
        target.SetupGet(t => t.IsBot).Returns(false);

        var caseDto = MakeCaseDto(1, CaseType.Warn);
        _mockModerationService
            .Setup(s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(caseDto);

        var context = CreateContext();

        var result = await _runner.WarnAsync(context.Object, target.Object, "being rude");

        result.Success.Should().BeTrue();
        result.Embed.Should().NotBeNull();
        result.PlainText.Should().BeNull();
        _mockModerationService.Verify(
            s => s.CreateCaseAsync(
                It.Is<ModerationCaseCreateDto>(d =>
                    d.Type == CaseType.Warn &&
                    d.TargetUserId == TargetId &&
                    d.ModeratorUserId == ModeratorId &&
                    d.Reason == "being rude"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WarnAsync_SelfTarget_ReturnsErrorWithoutCreatingCase()
    {
        var moderator = CreateModeratorMock();
        var context = CreateContext(moderator: moderator);

        var result = await _runner.WarnAsync(context.Object, moderator.Object, "reason");

        result.Success.Should().BeFalse();
        result.PlainText.Should().Be("You cannot warn yourself.");
        _mockModerationService.Verify(
            s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WarnAsync_TargetIsTheBot_ReturnsError()
    {
        var bot = new Mock<IUser>();
        bot.SetupGet(b => b.Id).Returns(BotId);
        bot.SetupGet(b => b.IsBot).Returns(true);

        var context = CreateContext();

        var result = await _runner.WarnAsync(context.Object, bot.Object, null);

        result.Success.Should().BeFalse();
        result.PlainText.Should().Be("I cannot be warned.");
    }

    // ---------- Kick ----------

    [Fact]
    public async Task KickAsync_HappyPath_KicksUserAndCreatesCase()
    {
        var target = CreateTargetGuildUserMock(hierarchy: 5);
        target.Setup(t => t.KickAsync(It.IsAny<string>(), It.IsAny<RequestOptions>())).Returns(Task.CompletedTask);

        var caseDto = MakeCaseDto(2, CaseType.Kick);
        _mockModerationService
            .Setup(s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(caseDto);

        var context = CreateContext(moderatorHierarchy: 10, resolvedGuildUser: target.Object);

        var result = await _runner.KickAsync(context.Object, target.Object, "spamming");

        result.Success.Should().BeTrue();
        result.Embed.Should().NotBeNull();
        target.Verify(t => t.KickAsync("spamming", It.IsAny<RequestOptions>()), Times.Once);
        _mockModerationService.Verify(
            s => s.CreateCaseAsync(It.Is<ModerationCaseCreateDto>(d => d.Type == CaseType.Kick), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task KickAsync_TargetNotInGuild_ReturnsErrorWithoutCreatingCase()
    {
        var target = new Mock<IUser>();
        target.SetupGet(t => t.Id).Returns(TargetId);
        target.SetupGet(t => t.Username).Returns("Target");

        var context = CreateContext(resolvedGuildUser: null, resolveThrowsOrReturnsNull: true);

        var result = await _runner.KickAsync(context.Object, target.Object, null);

        result.Success.Should().BeFalse();
        result.PlainText.Should().Be("Could not find that user in this server. They may have left or were never a member.");
        _mockModerationService.Verify(
            s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task KickAsync_TargetHasEqualOrHigherHierarchy_ReturnsErrorWithoutCreatingCase()
    {
        var target = CreateTargetGuildUserMock(hierarchy: 20);
        var context = CreateContext(moderatorHierarchy: 10, resolvedGuildUser: target.Object);

        var result = await _runner.KickAsync(context.Object, target.Object, null);

        result.Success.Should().BeFalse();
        result.PlainText.Should().Be("You cannot kick a user with an equal or higher role than yours.");
        _mockModerationService.Verify(
            s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
        target.Verify(t => t.KickAsync(It.IsAny<string>(), It.IsAny<RequestOptions>()), Times.Never);
    }

    // ---------- Ban ----------

    [Fact]
    public async Task BanAsync_HappyPath_BansUserAndCreatesCase()
    {
        var target = new Mock<IUser>();
        target.SetupGet(t => t.Id).Returns(TargetId);
        target.SetupGet(t => t.Username).Returns("Target");
        target.SetupGet(t => t.Mention).Returns("<@300>");
        target.SetupGet(t => t.IsBot).Returns(false);

        var guild = CreateGuildMock();
        guild.Setup(g => g.AddBanAsync(target.Object, 0, "banned for cause", It.IsAny<RequestOptions>()))
            .Returns(Task.CompletedTask);

        var caseDto = MakeCaseDto(3, CaseType.Ban);
        _mockModerationService
            .Setup(s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(caseDto);

        var context = CreateContext(guild: guild);

        var result = await _runner.BanAsync(context.Object, target.Object, duration: null, reason: "banned for cause", deleteMessageDays: 0);

        result.Success.Should().BeTrue();
        result.Embed.Should().NotBeNull();
        guild.Verify(g => g.AddBanAsync(target.Object, 0, "banned for cause", It.IsAny<RequestOptions>()), Times.Once);
        _mockModerationService.Verify(
            s => s.CreateCaseAsync(It.Is<ModerationCaseCreateDto>(d => d.Type == CaseType.Ban && d.Duration == null), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BanAsync_InvalidDuration_ReturnsErrorWithoutCreatingCase()
    {
        var target = new Mock<IUser>();
        target.SetupGet(t => t.Id).Returns(TargetId);

        var context = CreateContext();

        var result = await _runner.BanAsync(context.Object, target.Object, duration: "not-a-duration", reason: null, deleteMessageDays: 0);

        result.Success.Should().BeFalse();
        result.Embed.Should().NotBeNull();
        _mockModerationService.Verify(
            s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Sets up a mocked DM channel that <paramref name="user"/>.CreateDMChannelAsync() returns,
    /// with SendMessageAsync configured per <paramref name="throwing"/>/<paramref name="onSend"/>
    /// so DM behaviour (success/failure/ordering) can be asserted without hitting the real
    /// extension-method-based Discord.Net send path.
    /// </summary>
    private static Mock<IDMChannel> SetupDmChannel(Mock<IUser> user, bool throwing = false, Action? onSend = null)
    {
        var dmChannel = new Mock<IDMChannel>();
        user.Setup(u => u.CreateDMChannelAsync(It.IsAny<RequestOptions>())).ReturnsAsync(dmChannel.Object);

        var setup = dmChannel.Setup(c => c.SendMessageAsync(
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageReference>(), It.IsAny<MessageComponent>(),
            It.IsAny<ISticker[]>(), It.IsAny<Embed[]>(), It.IsAny<MessageFlags>(), It.IsAny<PollProperties>()));

        if (throwing)
        {
            setup.ThrowsAsync(new InvalidOperationException("Cannot send messages to this user"));
        }
        else
        {
            if (onSend != null)
            {
                setup.Callback(onSend);
            }
            setup.ReturnsAsync(Mock.Of<IUserMessage>());
        }

        return dmChannel;
    }

    [Fact]
    public async Task KickAsync_DmThrows_StillKicksUserAndCreatesCase()
    {
        var target = CreateTargetGuildUserMock(hierarchy: 5);
        SetupDmChannel(target.As<IUser>(), throwing: true);
        target.Setup(t => t.KickAsync(It.IsAny<string>(), It.IsAny<RequestOptions>())).Returns(Task.CompletedTask);

        var caseDto = MakeCaseDto(6, CaseType.Kick);
        _mockModerationService
            .Setup(s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(caseDto);

        var context = CreateContext(moderatorHierarchy: 10, resolvedGuildUser: target.Object);

        var result = await _runner.KickAsync(context.Object, target.Object, "spamming");

        result.Success.Should().BeTrue();
        target.Verify(t => t.KickAsync("spamming", It.IsAny<RequestOptions>()), Times.Once);
        _mockModerationService.Verify(
            s => s.CreateCaseAsync(It.Is<ModerationCaseCreateDto>(d => d.Type == CaseType.Kick), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BanAsync_DmThrows_StillBansUserAndCreatesCase()
    {
        var target = new Mock<IUser>();
        target.SetupGet(t => t.Id).Returns(TargetId);
        target.SetupGet(t => t.Username).Returns("Target");
        target.SetupGet(t => t.Mention).Returns("<@300>");
        target.SetupGet(t => t.IsBot).Returns(false);
        SetupDmChannel(target, throwing: true);

        var guild = CreateGuildMock();
        guild.Setup(g => g.AddBanAsync(target.Object, 0, "banned for cause", It.IsAny<RequestOptions>()))
            .Returns(Task.CompletedTask);

        var caseDto = MakeCaseDto(7, CaseType.Ban);
        _mockModerationService
            .Setup(s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(caseDto);

        var context = CreateContext(guild: guild);

        var result = await _runner.BanAsync(context.Object, target.Object, duration: null, reason: "banned for cause", deleteMessageDays: 0);

        result.Success.Should().BeTrue();
        guild.Verify(g => g.AddBanAsync(target.Object, 0, "banned for cause", It.IsAny<RequestOptions>()), Times.Once);
        _mockModerationService.Verify(
            s => s.CreateCaseAsync(It.Is<ModerationCaseCreateDto>(d => d.Type == CaseType.Ban), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BanAsync_SendsDmBeforeAddingBan()
    {
        var target = new Mock<IUser>();
        target.SetupGet(t => t.Id).Returns(TargetId);
        target.SetupGet(t => t.Username).Returns("Target");
        target.SetupGet(t => t.Mention).Returns("<@300>");
        target.SetupGet(t => t.IsBot).Returns(false);

        var callOrder = new List<string>();
        SetupDmChannel(target, onSend: () => callOrder.Add("dm"));

        var guild = CreateGuildMock();
        guild.Setup(g => g.AddBanAsync(target.Object, 0, It.IsAny<string>(), It.IsAny<RequestOptions>()))
            .Callback(() => callOrder.Add("ban"))
            .Returns(Task.CompletedTask);

        var caseDto = MakeCaseDto(8, CaseType.Ban);
        _mockModerationService
            .Setup(s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(caseDto);

        var context = CreateContext(guild: guild);

        var result = await _runner.BanAsync(context.Object, target.Object, duration: null, reason: null, deleteMessageDays: 0);

        result.Success.Should().BeTrue();
        callOrder.Should().Equal("dm", "ban");
    }

    [Fact]
    public async Task BanAsync_TargetHasEqualOrHigherHierarchy_ReturnsErrorWithoutCreatingCase()
    {
        var target = CreateTargetGuildUserMock(hierarchy: 20);
        var context = CreateContext(moderatorHierarchy: 10);

        var result = await _runner.BanAsync(context.Object, target.Object, duration: null, reason: null, deleteMessageDays: 0);

        result.Success.Should().BeFalse();
        result.PlainText.Should().Be("You cannot ban a user with an equal or higher role than yours.");
        _mockModerationService.Verify(
            s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------- Unban ----------

    [Fact]
    public async Task UnbanAsync_HappyPath_RemovesBanAndCreatesCase()
    {
        var bannedUser = new Mock<IUser>();
        bannedUser.SetupGet(u => u.Id).Returns(TargetId);
        bannedUser.SetupGet(u => u.Username).Returns("Target");

        var ban = new Mock<IBan>();
        ban.SetupGet(b => b.User).Returns(bannedUser.Object);
        ban.SetupGet(b => b.Reason).Returns("original reason");

        var guild = CreateGuildMock();
        guild.Setup(g => g.GetBanAsync(TargetId, It.IsAny<RequestOptions>())).ReturnsAsync(ban.Object);
        guild.Setup(g => g.RemoveBanAsync(TargetId, It.IsAny<RequestOptions>())).Returns(Task.CompletedTask);

        var caseDto = MakeCaseDto(4, CaseType.Unban);
        _mockModerationService
            .Setup(s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(caseDto);

        var context = CreateContext(guild: guild);

        var result = await _runner.UnbanAsync(context.Object, TargetId.ToString(), "appeal accepted");

        result.Success.Should().BeTrue();
        guild.Verify(g => g.RemoveBanAsync(TargetId, It.IsAny<RequestOptions>()), Times.Once);
        _mockModerationService.Verify(
            s => s.CreateCaseAsync(It.Is<ModerationCaseCreateDto>(d => d.Type == CaseType.Unban), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UnbanAsync_InvalidUserIdFormat_ReturnsError()
    {
        var context = CreateContext();

        var result = await _runner.UnbanAsync(context.Object, "not-a-ulong", null);

        result.Success.Should().BeFalse();
        result.PlainText.Should().Be("Invalid user ID format. Please provide a valid Discord user ID.");
        _mockModerationService.Verify(
            s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UnbanAsync_UserNotBanned_ReturnsErrorWithoutCreatingCase()
    {
        var guild = CreateGuildMock();
        guild.Setup(g => g.GetBanAsync(TargetId, It.IsAny<RequestOptions>())).ReturnsAsync((IBan?)null);

        var context = CreateContext(guild: guild);

        var result = await _runner.UnbanAsync(context.Object, TargetId.ToString(), null);

        result.Success.Should().BeFalse();
        result.PlainText.Should().Be("That user is not banned from this server.");
        _mockModerationService.Verify(
            s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------- Mute ----------

    [Fact]
    public async Task MuteAsync_HappyPath_TimesOutUserAndCreatesCase()
    {
        var target = CreateTargetGuildUserMock(hierarchy: 5);
        target.Setup(t => t.SetTimeOutAsync(It.IsAny<TimeSpan>(), It.IsAny<RequestOptions>())).Returns(Task.CompletedTask);

        var caseDto = MakeCaseDto(5, CaseType.Mute, DateTime.UtcNow.AddHours(1));
        _mockModerationService
            .Setup(s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(caseDto);

        var context = CreateContext(moderatorHierarchy: 10, resolvedGuildUser: target.Object);

        var result = await _runner.MuteAsync(context.Object, target.Object, "1h", "cool off");

        result.Success.Should().BeTrue();
        target.Verify(t => t.SetTimeOutAsync(It.Is<TimeSpan>(ts => ts == TimeSpan.FromHours(1)), It.IsAny<RequestOptions>()), Times.Once);
        _mockModerationService.Verify(
            s => s.CreateCaseAsync(It.Is<ModerationCaseCreateDto>(d => d.Type == CaseType.Mute), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MuteAsync_InvalidDuration_ReturnsErrorWithoutCreatingCase()
    {
        var target = CreateTargetGuildUserMock();
        var context = CreateContext(resolvedGuildUser: target.Object);

        var result = await _runner.MuteAsync(context.Object, target.Object, "not-a-duration", null);

        result.Success.Should().BeFalse();
        result.Embed.Should().NotBeNull();
        _mockModerationService.Verify(
            s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MuteAsync_DurationExceedsMax_ReturnsErrorWithoutCreatingCase()
    {
        var target = CreateTargetGuildUserMock();
        var context = CreateContext(resolvedGuildUser: target.Object);

        var result = await _runner.MuteAsync(context.Object, target.Object, "30d", null);

        result.Success.Should().BeFalse();
        result.Embed.Should().NotBeNull();
        _mockModerationService.Verify(
            s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MuteAsync_SelfTarget_ReturnsErrorWithoutCreatingCase()
    {
        var moderator = CreateModeratorMock();
        var moderatorAsGuildUser = moderator.As<IGuildUser>();
        moderatorAsGuildUser.SetupGet(m => m.Hierarchy).Returns(10);
        moderatorAsGuildUser.SetupGet(m => m.IsBot).Returns(false);

        var context = CreateContext(moderator: moderator, resolvedGuildUser: moderatorAsGuildUser.Object);

        var result = await _runner.MuteAsync(context.Object, moderator.Object, "10m", null);

        result.Success.Should().BeFalse();
        result.PlainText.Should().Be("You cannot mute yourself.");
        _mockModerationService.Verify(
            s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
