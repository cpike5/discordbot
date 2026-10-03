using Discord.WebSocket;
using DiscordBot.Bot.Pages.Guilds.Members;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Guilds.Members;

/// <summary>
/// The member directory's CSV export and its "Never messaged" filter. Both were wired to nothing:
/// the page linked to a handler that did not exist, and the filter set no condition.
/// </summary>
public class IndexModelExportTests
{
    private const ulong GuildId = 123456789UL;

    private readonly Mock<IGuildMemberService> _members = new();
    private readonly Mock<IGuildService> _guilds = new();
    private readonly IndexModel _model;

    public IndexModelExportTests()
    {
        _model = new IndexModel(
            _members.Object,
            _guilds.Object,
            new Mock<DiscordSocketClient>(new DiscordSocketConfig()).Object,
            Mock.Of<ILogger<IndexModel>>())
        {
            GuildId = GuildId
        };

        var http = new DefaultHttpContext();
        var routeData = new RouteData();
        routeData.Values["page"] = "/Guilds/Members/Index";
        _model.PageContext = new PageContext(new ActionContext(http, routeData, new PageActionDescriptor(), new ModelStateDictionary()));
        _model.TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>());

        // The page builds its export link with Url.Page
        var url = new Mock<IUrlHelper>();
        url.SetupGet(u => u.ActionContext).Returns(_model.PageContext);
        url.Setup(u => u.RouteUrl(It.IsAny<UrlRouteContext>())).Returns("/Guilds/123456789/Members?handler=Export");
        _model.Url = url.Object;
    }

    [Fact]
    public async Task Export_ReturnsACsvFile_ForTheCurrentFilters()
    {
        GuildMemberQueryDto? sent = null;
        _members
            .Setup(m => m.ExportMembersToCsvAsync(GuildId, It.IsAny<GuildMemberQueryDto>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<ulong, GuildMemberQueryDto, int, CancellationToken>((_, q, _, _) => sent = q)
            .ReturnsAsync(new byte[] { 1, 2, 3 });
        _model.SearchTerm = "ada";
        _model.ActivityFilter = "active-week";

        var result = await _model.OnGetExportAsync(CancellationToken.None);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("text/csv");
        file.FileDownloadName.Should().StartWith($"members-{GuildId}-").And.EndWith(".csv");
        sent!.SearchTerm.Should().Be("ada");
        sent.LastActiveAtStart.Should().NotBeNull("the activity filter reaches the export");
    }

    [Fact]
    public async Task Export_OfSelectedMembers_SendsEachIdOnce()
    {
        GuildMemberQueryDto? sent = null;
        _members
            .Setup(m => m.ExportMembersToCsvAsync(GuildId, It.IsAny<GuildMemberQueryDto>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<ulong, GuildMemberQueryDto, int, CancellationToken>((_, q, _, _) => sent = q)
            .ReturnsAsync(new byte[] { 1 });
        _model.UserIds = new List<ulong> { 111UL, 222UL, 111UL };

        await _model.OnGetExportAsync(CancellationToken.None);

        sent!.UserIds.Should().Equal(111UL, 222UL);
    }

    [Fact]
    public async Task Export_WithNoMatchingMembers_ExplainsInsteadOfServingAnError()
    {
        _members
            .Setup(m => m.ExportMembersToCsvAsync(GuildId, It.IsAny<GuildMemberQueryDto>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No members found matching the specified criteria"));
        _model.SearchTerm = "nobody";

        var result = await _model.OnGetExportAsync(CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectToPageResult>().Subject;
        redirect.RouteValues.Should().ContainKey("SearchTerm").WhoseValue.Should().Be("nobody");
        ((string)_model.TempData["ToastError"]!).Should().Contain("nothing to export");
    }

    [Fact]
    public async Task NeverMessaged_AsksForMembersWithNoActivity_NotForNoFilter()
    {
        _guilds.Setup(g => g.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildDto { Id = GuildId, Name = "Test" });
        GuildMemberQueryDto? sent = null;
        _members
            .Setup(m => m.GetMembersAsync(GuildId, It.IsAny<GuildMemberQueryDto>(), It.IsAny<CancellationToken>()))
            .Callback<ulong, GuildMemberQueryDto, CancellationToken>((_, q, _) => sent = q)
            .ReturnsAsync(new PaginatedResponseDto<GuildMemberDto> { Items = new List<GuildMemberDto>(), Page = 1, PageSize = 25, TotalCount = 0 });
        _model.ActivityFilter = "never-messaged";

        await _model.OnGetAsync(CancellationToken.None);

        sent!.NeverActive.Should().BeTrue();
        sent.LastActiveAtStart.Should().BeNull();
        sent.LastActiveAtEnd.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(-4, 25)]
    public async Task OutOfRangePaging_IsClampedInsteadOfCrashing(int pageSize, int expected)
    {
        _guilds.Setup(g => g.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildDto { Id = GuildId, Name = "Test" });
        GuildMemberQueryDto? sent = null;
        _members
            .Setup(m => m.GetMembersAsync(GuildId, It.IsAny<GuildMemberQueryDto>(), It.IsAny<CancellationToken>()))
            .Callback<ulong, GuildMemberQueryDto, CancellationToken>((_, q, _) => sent = q)
            .ReturnsAsync(new PaginatedResponseDto<GuildMemberDto> { Items = new List<GuildMemberDto>(), Page = 1, PageSize = expected, TotalCount = 0 });
        _model.PageSize = pageSize;
        _model.CurrentPage = 0;

        await _model.OnGetAsync(CancellationToken.None);

        sent!.PageSize.Should().Be(expected);
        sent.Page.Should().Be(1);
    }
}
