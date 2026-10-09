using DiscordBot.Bot.Services.Moderation;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services.Moderation;

/// <summary>
/// <see cref="ModerationService.CreateCaseAsync"/> hands every new case to the mod-log feed, off the
/// caller's thread, in a scope of its own.
/// </summary>
public class ModerationServiceModLogTests
{
    private const ulong GuildId = 100UL;

    private readonly Mock<IModerationCaseRepository> _caseRepository = new();
    private readonly Mock<IDiscordUserResolver> _userResolver = new();
    private readonly Mock<IBackgroundTaskRunner> _taskRunner = new();
    private readonly Mock<IModLogNotifier> _notifier = new();
    private readonly ModerationService _service;

    public ModerationServiceModLogTests()
    {
        _caseRepository
            .Setup(r => r.GetNextCaseNumberAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);
        _caseRepository
            .Setup(r => r.AddAsync(It.IsAny<ModerationCase>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModerationCase c, CancellationToken _) => c);
        _userResolver
            .Setup(r => r.ResolveUserAsync(It.IsAny<ulong>()))
            .ReturnsAsync(("someone", null));

        var services = new ServiceCollection();
        services.AddScoped(_ => _notifier.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        _service = new ModerationService(
            _caseRepository.Object,
            _userResolver.Object,
            _taskRunner.Object,
            scopeFactory,
            Mock.Of<ILogger<ModerationService>>());
    }

    private static ModerationCaseCreateDto Create() => new()
    {
        GuildId = GuildId,
        TargetUserId = 300,
        ModeratorUserId = 200,
        Type = CaseType.Warn,
        Reason = "test"
    };

    [Fact]
    public async Task CreateCase_QueuesTheModLogPost_AfterTheCaseIsSaved()
    {
        Func<CancellationToken, Task>? queued = null;
        _taskRunner
            .Setup(r => r.Run(It.IsAny<Func<CancellationToken, Task>>(), "modlog.case_created"))
            .Callback<Func<CancellationToken, Task>, string>((work, _) => queued = work);

        var created = await _service.CreateCaseAsync(Create());

        _caseRepository.Verify(r => r.AddAsync(It.IsAny<ModerationCase>(), It.IsAny<CancellationToken>()), Times.Once);
        queued.Should().NotBeNull("the post is queued, not awaited, so a slow Discord never delays the reply");
        _notifier.Verify(n => n.CaseCreatedAsync(It.IsAny<ModerationCaseDto>(), It.IsAny<CancellationToken>()), Times.Never);

        await queued!(CancellationToken.None);

        _notifier.Verify(n => n.CaseCreatedAsync(
            It.Is<ModerationCaseDto>(c => c.Id == created.Id && c.CaseNumber == 7 && c.GuildId == GuildId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateCase_ReturnsTheCase_EvenWhenNothingRunsTheQueue()
    {
        var created = await _service.CreateCaseAsync(Create());

        created.CaseNumber.Should().Be(7);
        created.TargetUserId.Should().Be(300);
    }
}
