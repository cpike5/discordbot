using DiscordBot.Bot.Pages.Admin;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Admin;

/// <summary>
/// Unit tests for <see cref="LlmUsageModel"/>: default date range, filter pass-through to
/// <see cref="ILlmUsageRepository"/>, and display-name resolution for the by-user breakdown.
/// </summary>
[Trait("Category", "Unit")]
public class LlmUsageModelTests
{
    private readonly Mock<ILlmUsageRepository> _mockUsageRepository = new();
    private readonly Mock<IDiscordUserResolver> _mockUserResolver = new();
    private readonly Mock<IGuildService> _mockGuildService = new();
    private readonly LlmUsageModel _model;

    public LlmUsageModelTests()
    {
        _mockUsageRepository
            .Setup(r => r.GetTotalsAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmUsageTotals { MessageCount = 5, CostUsd = 2m });
        _mockUsageRepository
            .Setup(r => r.GetByUserAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LlmUsageByUser>());
        _mockUsageRepository
            .Setup(r => r.GetByModelAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LlmUsageByModel>());
        _mockUsageRepository
            .Setup(r => r.GetByModeAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LlmUsageByMode>());
        _mockUsageRepository
            .Setup(r => r.GetByDayAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LlmUsageByDay>());

        _mockUserResolver
            .Setup(r => r.ResolveUsersAsync(It.IsAny<IEnumerable<ulong>>()))
            .ReturnsAsync(new Dictionary<ulong, (string Username, string? AvatarUrl)>());

        _mockGuildService
            .Setup(g => g.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>());

        _model = new LlmUsageModel(
            _mockUsageRepository.Object,
            _mockUserResolver.Object,
            _mockGuildService.Object,
            Mock.Of<ILogger<LlmUsageModel>>());
    }

    [Fact]
    public async Task OnGetAsync_ShouldDefaultToLast30Days_WhenNoDatesGiven()
    {
        var result = await _model.OnGetAsync(CancellationToken.None);

        result.Should().BeOfType<PageResult>();
        _model.StartDate.Should().Be(_model.EndDate!.Value.AddDays(-30));
    }

    [Fact]
    public async Task OnGetAsync_ShouldPassGuildAndModeFiltersToRepository()
    {
        _model.GuildId = 42UL;
        _model.Mode = LlmMode.FeatureRequests;

        LlmUsageQuery? captured = null;
        _mockUsageRepository
            .Setup(r => r.GetTotalsAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<CancellationToken>()))
            .Callback<LlmUsageQuery, CancellationToken>((q, _) => captured = q)
            .ReturnsAsync(new LlmUsageTotals());

        await _model.OnGetAsync(CancellationToken.None);

        captured!.GuildId.Should().Be(42UL);
        captured.Mode.Should().Be(LlmMode.FeatureRequests);
    }

    [Fact]
    public async Task OnGetAsync_ShouldSwapDates_WhenStartIsAfterEnd()
    {
        _model.StartDate = new DateTime(2026, 9, 10);
        _model.EndDate = new DateTime(2026, 9, 1);

        await _model.OnGetAsync(CancellationToken.None);

        _model.StartDate.Should().BeOnOrBefore(_model.EndDate!.Value);
    }

    [Fact]
    public async Task OnGetAsync_ShouldResolveDisplayNamesForByUserRows()
    {
        _mockUsageRepository
            .Setup(r => r.GetByUserAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LlmUsageByUser> { new() { UserId = 111UL, CostUsd = 3m, CostShare = 1.0 } });
        _mockUserResolver
            .Setup(r => r.ResolveUsersAsync(It.IsAny<IEnumerable<ulong>>()))
            .ReturnsAsync(new Dictionary<ulong, (string Username, string? AvatarUrl)> { [111UL] = ("Bob", null) });

        await _model.OnGetAsync(CancellationToken.None);

        _model.ByUser.Should().ContainSingle();
        _model.ByUser[0].UserId.Should().Be("111");
        _model.ByUser[0].DisplayName.Should().Be("Bob");
    }

    [Fact]
    public async Task OnGetAsync_ShouldClampStartDate_WhenRangeExceedsMaxDays()
    {
        _model.EndDate = new DateTime(2026, 9, 10);
        _model.StartDate = new DateTime(2020, 1, 1);

        await _model.OnGetAsync(CancellationToken.None);

        _model.StartDate.Should().Be(_model.EndDate!.Value.AddDays(-LlmUsageRangeLimits.MaxRangeDays));
    }

    [Fact]
    public async Task OnGetAsync_ShouldPopulateGuildsForFilterDropdown()
    {
        _mockGuildService
            .Setup(g => g.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto> { new() { Id = 1UL, Name = "Test Guild" } });

        await _model.OnGetAsync(CancellationToken.None);

        _model.Guilds.Should().ContainSingle(g => g.Name == "Test Guild");
    }
}
