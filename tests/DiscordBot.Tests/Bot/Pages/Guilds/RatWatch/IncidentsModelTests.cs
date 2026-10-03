using System.Text;
using DiscordBot.Bot.Pages.Guilds.RatWatch;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Guilds.RatWatch;

/// <summary>
/// The Rat Watch incidents page: the CSV export holds every row the filters select (it used to
/// hold only the page on screen), neutralises spreadsheet formulas, and says UTC; and the page
/// number is bound as <c>pageNumber</c>.
/// </summary>
public class IncidentsModelTests
{
    private const ulong GuildId = 123456789UL;

    private readonly Mock<IRatWatchService> _ratWatch = new();
    private readonly Mock<IGuildService> _guilds = new();
    private readonly IncidentsModel _model;

    public IncidentsModelTests()
    {
        _model = new IncidentsModel(_ratWatch.Object, _guilds.Object, Mock.Of<ILogger<IncidentsModel>>());
        var http = new DefaultHttpContext();
        _model.PageContext = new PageContext(new ActionContext(http, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary()));
        _model.TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>());
    }

    private static RatWatchDto Incident(int n, string accused = "Ada", string? message = null, RatWatchStatus status = RatWatchStatus.Guilty) => new()
    {
        Id = Guid.NewGuid(),
        GuildId = GuildId,
        AccusedUsername = accused,
        InitiatorUsername = "Grace",
        CustomMessage = message,
        ScheduledAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Unspecified).AddMinutes(n),
        Status = status,
        GuiltyVotes = 3,
        NotGuiltyVotes = 1
    };

    private static string Text(IActionResult result)
    {
        var file = result.Should().BeOfType<FileContentResult>().Subject;
        return Encoding.UTF8.GetString(file.FileContents);
    }

    [Fact]
    public async Task Export_ReadsEveryPage_NotJustTheOneOnScreen()
    {
        // 1,200 rows: three pages of 500 and 500 and 200
        var pages = new Dictionary<int, List<RatWatchDto>>
        {
            [1] = Enumerable.Range(0, 500).Select(i => Incident(i)).ToList(),
            [2] = Enumerable.Range(500, 500).Select(i => Incident(i)).ToList(),
            [3] = Enumerable.Range(1000, 200).Select(i => Incident(i)).ToList()
        };
        _ratWatch
            .Setup(s => s.GetFilteredByGuildAsync(GuildId, It.IsAny<RatWatchIncidentFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ulong _, RatWatchIncidentFilterDto f, CancellationToken _) =>
                ((IEnumerable<RatWatchDto>)pages[f.Page], 1200));
        _model.PageNumber = 2; // the page on screen must not limit the export
        _model.PageSize = 10;

        var result = await _model.OnGetExportCsvAsync((long)GuildId);

        var lines = Text(result).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(1 + 1200, "a header and every incident");
        ((FileContentResult)result).FileDownloadName.Should().StartWith($"ratwatch-incidents-{GuildId}-").And.EndWith(".csv");
    }

    [Fact]
    public async Task Export_UsesTheSameFilterAsThePage()
    {
        RatWatchIncidentFilterDto? seen = null;
        _ratWatch
            .Setup(s => s.GetFilteredByGuildAsync(GuildId, It.IsAny<RatWatchIncidentFilterDto>(), It.IsAny<CancellationToken>()))
            .Callback<ulong, RatWatchIncidentFilterDto, CancellationToken>((_, f, _) => seen = f)
            .ReturnsAsync((new List<RatWatchDto> { Incident(0) }, 1));
        _model.Statuses = new List<RatWatchStatus> { RatWatchStatus.Guilty };
        _model.Keyword = "late";
        _model.StartDate = new DateTime(2026, 9, 1);
        _model.EndDate = new DateTime(2026, 9, 30);

        await _model.OnGetExportCsvAsync((long)GuildId);

        seen!.Statuses.Should().Equal(RatWatchStatus.Guilty);
        seen.Keyword.Should().Be("late");
        seen.StartDate.Should().Be(new DateTime(2026, 9, 1));
        seen.EndDate.Should().Be(new DateTime(2026, 9, 30).AddDays(1).AddTicks(-1), "the end date is the whole day");
    }

    [Fact]
    public async Task Export_NeutralisesFormulas_AndQuotesCells()
    {
        _ratWatch
            .Setup(s => s.GetFilteredByGuildAsync(GuildId, It.IsAny<RatWatchIncidentFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<RatWatchDto> { Incident(0, accused: "=SUM(1+1)", message: "say \"hi\", ok") }, 1));

        var csv = Text(await _model.OnGetExportCsvAsync((long)GuildId));

        csv.Should().Contain("\"'=SUM(1+1)\"", "a leading = is made text");
        csv.Should().Contain("\"say \"\"hi\"\", ok\"", "quotes are doubled and the cell is quoted");
    }

    [Fact]
    public async Task Export_SaysUtc_InTheHeaderAndTheValues_AndUsesStatusWords()
    {
        _ratWatch
            .Setup(s => s.GetFilteredByGuildAsync(GuildId, It.IsAny<RatWatchIncidentFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<RatWatchDto> { Incident(0, status: RatWatchStatus.ClearedEarly) }, 1));

        var csv = Text(await _model.OnGetExportCsvAsync((long)GuildId));

        csv.Should().Contain("Scheduled (UTC)");
        csv.Should().Contain("2026-10-01 12:00:00");
        csv.Should().Contain("\"Cleared early\"").And.NotContain("ClearedEarly");
    }

    [Fact]
    public async Task Export_WhenTheServiceFails_RedirectsWithAToastInsteadOfServingAnError()
    {
        _ratWatch
            .Setup(s => s.GetFilteredByGuildAsync(GuildId, It.IsAny<RatWatchIncidentFilterDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database is down"));

        var result = await _model.OnGetExportCsvAsync((long)GuildId);

        result.Should().BeOfType<RedirectToPageResult>();
        ((string)_model.TempData["ToastError"]!).Should().NotContain("database is down");
    }

    [Fact]
    public void BuildFilter_WithNoDates_DefaultsToTheLastThirtyDays()
    {
        var (filter, start, end) = _model.BuildFilter(1, 25);

        start.Should().Be(DateTime.Today.AddDays(-30));
        end.Should().Be(DateTime.Today);
        filter.EndDate.Should().Be(DateTime.Today.AddDays(1).AddTicks(-1));
    }

    [Fact]
    public void FilterState_DoesNotCountTheDefaultDatesAsFilters_ButCountsAChosenRangeOnce()
    {
        var defaults = new DiscordBot.Bot.ViewModels.Pages.RatWatchIncidentFilterState
        {
            StartDate = DateTime.Today.AddDays(-30),
            EndDate = DateTime.Today,
            DatesAreDefault = true
        };
        var chosen = defaults with { DatesAreDefault = false };

        defaults.GetActiveFilterCount().Should().Be(0);
        chosen.GetActiveFilterCount().Should().Be(1);
    }

    [Fact]
    public void CsvCell_QuotesAndNeutralises()
    {
        IncidentsModel.CsvCell(null).Should().Be("\"\"");
        IncidentsModel.CsvCell("@home").Should().Be("\"'@home\"");
        IncidentsModel.CsvCell("a\"b").Should().Be("\"a\"\"b\"");
    }
}
