using DiscordBot.Bot.Controllers;
using DiscordBot.Bot.Middleware;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using DiscordBot.Core.DTOs.Llm.Reporting;

namespace DiscordBot.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="LlmUsageController"/>: date-range validation/defaults, pageSize
/// capping, and DTO mapping (string IDs, resolved display names).
/// </summary>
[Trait("Category", "Unit")]
public class LlmUsageControllerTests
{
    private readonly Mock<ILlmUsageRepository> _mockUsageRepository;
    private readonly Mock<IDiscordUserResolver> _mockUserResolver;
    private readonly LlmUsageController _controller;

    public LlmUsageControllerTests()
    {
        _mockUsageRepository = new Mock<ILlmUsageRepository>();
        _mockUserResolver = new Mock<IDiscordUserResolver>();

        _controller = new LlmUsageController(
            _mockUsageRepository.Object,
            _mockUserResolver.Object,
            Mock.Of<ILogger<LlmUsageController>>());

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        _controller.HttpContext.Items[CorrelationIdMiddleware.ItemKey] = "test-correlation-id";

        _mockUsageRepository
            .Setup(r => r.GetTotalsAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmUsageTotals { MessageCount = 3, CostUsd = 1.5m, BilledCostShare = 0.5 });
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
    }

    #region GetSummary - range validation and defaults

    [Fact]
    public async Task GetSummary_ShouldDefaultToLast30Days_WhenNoRangeGiven()
    {
        LlmUsageQuery? captured = null;
        _mockUsageRepository
            .Setup(r => r.GetTotalsAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<CancellationToken>()))
            .Callback<LlmUsageQuery, CancellationToken>((q, _) => captured = q)
            .ReturnsAsync(new LlmUsageTotals());

        var result = await _controller.GetSummary(from: null, to: null, guildId: null, mode: null, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        captured.Should().NotBeNull();
        (captured!.To - captured.From).TotalDays.Should().BeApproximately(30, 0.01);
    }

    [Fact]
    public async Task GetSummary_ShouldReturnBadRequest_WhenToIsBeforeFrom()
    {
        var result = await _controller.GetSummary(
            from: new DateTime(2026, 9, 10), to: new DateTime(2026, 9, 1),
            guildId: null, mode: null, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var bad = result.Result as BadRequestObjectResult;
        (bad!.Value as ApiErrorDto)!.Message.Should().Contain("to");
    }

    [Fact]
    public async Task GetSummary_ShouldReturnBadRequest_WhenRangeExceedsMaxDays()
    {
        var result = await _controller.GetSummary(
            from: new DateTime(2020, 1, 1), to: new DateTime(2026, 1, 1),
            guildId: null, mode: null, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetSummary_ShouldAccept366DayRange()
    {
        // 'to' is a bare date, so the controller rounds it up to end-of-day; from.AddDays(365) at
        // 23:59:59.999 spans just under 366 days from 'from' at midnight.
        var from = new DateTime(2026, 1, 1);
        var to = from.AddDays(365);

        var result = await _controller.GetSummary(from: from, to: to, guildId: null, mode: null, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetSummary_ShouldReject367DayRange()
    {
        var from = new DateTime(2026, 1, 1);
        var to = from.AddDays(366);

        var result = await _controller.GetSummary(from: from, to: to, guildId: null, mode: null, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetSummary_ShouldRoundBareDateToEndOfDay_WhenToHasNoTimeComponent()
    {
        LlmUsageQuery? captured = null;
        _mockUsageRepository
            .Setup(r => r.GetTotalsAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<CancellationToken>()))
            .Callback<LlmUsageQuery, CancellationToken>((q, _) => captured = q)
            .ReturnsAsync(new LlmUsageTotals());

        // Same-day range: 'to' has no time component, so it must be rounded up to end-of-day
        // rather than treated as midnight (which would make the range span zero time).
        var day = new DateTime(2026, 9, 10);

        var result = await _controller.GetSummary(from: day, to: day, guildId: null, mode: null, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        captured.Should().NotBeNull();
        captured!.To.Should().Be(day.Date.AddDays(1).AddTicks(-1));
    }

    [Fact]
    public async Task GetSummary_ShouldResolveUsersExactlyOnce()
    {
        await _controller.GetSummary(
            from: new DateTime(2026, 9, 1), to: new DateTime(2026, 9, 10),
            guildId: null, mode: null, CancellationToken.None);

        _mockUserResolver.Verify(r => r.ResolveUsersAsync(It.IsAny<IEnumerable<ulong>>()), Times.Once);
    }

    [Fact]
    public async Task GetSummary_ShouldPassGuildAndModeFiltersThrough()
    {
        LlmUsageQuery? captured = null;
        _mockUsageRepository
            .Setup(r => r.GetTotalsAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<CancellationToken>()))
            .Callback<LlmUsageQuery, CancellationToken>((q, _) => captured = q)
            .ReturnsAsync(new LlmUsageTotals());

        await _controller.GetSummary(
            from: new DateTime(2026, 9, 1), to: new DateTime(2026, 9, 10),
            guildId: 123UL, mode: LlmMode.DmAssistant, CancellationToken.None);

        captured!.GuildId.Should().Be(123UL);
        captured.Mode.Should().Be(LlmMode.DmAssistant);
    }

    #endregion

    #region GetSummary - mapping

    [Fact]
    public async Task GetSummary_ShouldMapTotalsAndResolveUserDisplayNames()
    {
        _mockUsageRepository
            .Setup(r => r.GetByUserAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LlmUsageByUser>
            {
                new() { UserId = 555UL, MessageCount = 2, CostUsd = 1.25m, CostShare = 1.0 }
            });
        _mockUserResolver
            .Setup(r => r.ResolveUsersAsync(It.IsAny<IEnumerable<ulong>>()))
            .ReturnsAsync(new Dictionary<ulong, (string Username, string? AvatarUrl)>
            {
                [555UL] = ("SomeUser", "https://example.com/avatar.png")
            });

        var result = await _controller.GetSummary(
            from: new DateTime(2026, 9, 1), to: new DateTime(2026, 9, 10),
            guildId: null, mode: null, CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        var response = ok!.Value as LlmUsageSummaryResponseDto;

        response!.Totals.MessageCount.Should().Be(3);
        response.Totals.CostUsd.Should().Be(1.5m);
        response.ByUser.Should().ContainSingle();
        var userRow = response.ByUser[0];
        userRow.UserId.Should().Be("555");
        userRow.DisplayName.Should().Be("SomeUser");
        userRow.AvatarUrl.Should().Be("https://example.com/avatar.png");
    }

    [Fact]
    public async Task GetSummary_ShouldFallBackToUnknownDisplayName_WhenResolverHasNoEntry()
    {
        _mockUsageRepository
            .Setup(r => r.GetByUserAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LlmUsageByUser> { new() { UserId = 777UL } });

        var result = await _controller.GetSummary(
            from: new DateTime(2026, 9, 1), to: new DateTime(2026, 9, 10),
            guildId: null, mode: null, CancellationToken.None);

        var response = (result.Result as OkObjectResult)!.Value as LlmUsageSummaryResponseDto;
        response!.ByUser[0].DisplayName.Should().Be("Unknown#777");
    }

    #endregion

    #region GetRecords - pageSize cap and mapping

    [Fact]
    public async Task GetRecords_ShouldCapPageSizeAt200()
    {
        int? capturedPageSize = null;
        _mockUsageRepository
            .Setup(r => r.GetRecordsAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<LlmUsageQuery, int, int, CancellationToken>((_, _, pageSize, _) => capturedPageSize = pageSize)
            .ReturnsAsync(new LlmUsagePagedRecords { Records = new List<LlmUsageRecord>(), TotalCount = 0 });

        await _controller.GetRecords(
            from: null, to: null, guildId: null, mode: null, userId: null,
            page: 1, pageSize: 5000, CancellationToken.None);

        capturedPageSize.Should().Be(200);
    }

    [Fact]
    public async Task GetRecords_ShouldReturnBadRequest_WhenPageIsLessThanOne()
    {
        var result = await _controller.GetRecords(
            from: null, to: null, guildId: null, mode: null, userId: null,
            page: 0, pageSize: 50, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetRecords_ShouldReturnBadRequest_WhenPageSizeIsLessThanOne()
    {
        var result = await _controller.GetRecords(
            from: null, to: null, guildId: null, mode: null, userId: null,
            page: 1, pageSize: 0, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetRecords_ShouldResolveUsersExactlyOnce()
    {
        _mockUsageRepository
            .Setup(r => r.GetRecordsAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmUsagePagedRecords { Records = new List<LlmUsageRecord>(), TotalCount = 0 });

        await _controller.GetRecords(
            from: null, to: null, guildId: null, mode: null, userId: null,
            page: 1, pageSize: 50, CancellationToken.None);

        _mockUserResolver.Verify(r => r.ResolveUsersAsync(It.IsAny<IEnumerable<ulong>>()), Times.Once);
    }

    [Fact]
    public async Task GetRecords_ShouldMapIdsAsStringsAndResolveDisplayName()
    {
        _mockUsageRepository
            .Setup(r => r.GetRecordsAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmUsagePagedRecords
            {
                Records = new List<LlmUsageRecord>
                {
                    new()
                    {
                        Id = 42,
                        Timestamp = new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc),
                        Mode = LlmMode.GuildAssistant,
                        UserId = 888UL,
                        GuildId = 999UL,
                        Model = "anthropic/claude-sonnet-4.6",
                        CostUsd = 0.02m,
                        CostSource = LlmCostSource.Billed,
                        Success = true
                    }
                },
                TotalCount = 1
            });
        _mockUserResolver
            .Setup(r => r.ResolveUsersAsync(It.IsAny<IEnumerable<ulong>>()))
            .ReturnsAsync(new Dictionary<ulong, (string Username, string? AvatarUrl)>
            {
                [888UL] = ("Alice", null)
            });

        var result = await _controller.GetRecords(
            from: null, to: null, guildId: null, mode: null, userId: 888UL,
            page: 1, pageSize: 50, CancellationToken.None);

        var response = (result.Result as OkObjectResult)!.Value as LlmUsagePagedRecordsResponseDto;

        response!.TotalCount.Should().Be(1);
        var row = response.Records.Should().ContainSingle().Subject;
        row.UserId.Should().Be("888");
        row.GuildId.Should().Be("999");
        row.DisplayName.Should().Be("Alice");
        row.Mode.Should().Be("GuildAssistant");
        row.CostSource.Should().Be("Billed");
    }

    #endregion
}
