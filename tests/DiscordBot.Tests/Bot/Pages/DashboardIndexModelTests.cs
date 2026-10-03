using System.Security.Claims;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using DashboardIndexModel = DiscordBot.Bot.Pages.IndexModel;

namespace DiscordBot.Tests.Bot.Pages;

/// <summary>
/// The dashboard page: who sees which card and action (D8), and what its handlers answer.
/// </summary>
public class DashboardIndexModelTests
{
    private readonly Mock<IBotService> _bot = new();
    private readonly Mock<IGuildService> _guilds = new();
    private readonly Mock<ICommandLogService> _commandLogs = new();
    private readonly Mock<IAuditLogService> _audit = new();
    private readonly Mock<IRatWatchService> _ratWatch = new();
    private readonly Mock<IConnectionStateService> _connection = new();
    private readonly Mock<IDashboardStatsProvider> _stats = new();
    private readonly Mock<IDashboardStatsBroadcaster> _broadcaster = new();

    private DashboardIndexModel CreateModel(params string[] roles)
    {
        var versions = new Mock<IVersionService>();
        versions.Setup(v => v.GetVersion()).Returns("1.0.0");

        _bot.Setup(b => b.GetStatus()).Returns(new BotStatusDto { ConnectionState = "Connected", Uptime = TimeSpan.FromMinutes(5) });
        _guilds.Setup(g => g.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto> { new() { Id = 1, Name = "One", IsActive = true, MemberCount = 5 } });
        _commandLogs.Setup(c => c.GetCommandStatsAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int> { ["ping"] = 4 });
        _commandLogs.Setup(c => c.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginatedResponseDto<CommandLogDto>());
        _commandLogs.Setup(c => c.GetCommandCountsByGuildAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<ulong, int> { [1] = 4 });
        _audit.Setup(a => a.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLogDto>(), 0));
        _ratWatch.Setup(r => r.GetRecentActivityAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RatWatchDto>());
        _connection.Setup(c => c.GetUptimePercentage(It.IsAny<TimeSpan>())).Returns(100);

        var model = new DashboardIndexModel(
            NullLogger<DashboardIndexModel>.Instance,
            _bot.Object, _guilds.Object, _commandLogs.Object, _audit.Object, versions.Object,
            _ratWatch.Object, _connection.Object, _stats.Object, _broadcaster.Object);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, "tester") }.Concat(roles.Select(r => new Claim(ClaimTypes.Role, r))),
                "TestAuth"))
        };
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor());
        model.PageContext = new PageContext(actionContext);

        // Url.Page needs an ActionContext; the mock answers every route with no URL, so the page
        // model falls back to its plain paths
        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.Setup(u => u.ActionContext).Returns(actionContext);
        model.Url = urlHelper.Object;
        return model;
    }

    [Fact]
    public async Task OnGet_AsViewer_HidesWhatAViewerCannotUse()
    {
        var model = CreateModel("Viewer");

        await model.OnGetAsync();

        model.ShowConnectedServers.Should().BeFalse("a Viewer cannot open a server page");
        model.ShowAuditLog.Should().BeFalse();
        model.QuickActions.UserIsAdmin.Should().BeFalse();
        model.QuickActions.Actions.Where(a => !a.IsAdminOnly).Should().BeEmpty(
            "every quick action needs Admin, so a Viewer's card has nothing in it and is left out");
    }

    [Fact]
    public async Task OnGet_AsModerator_SeesServersButNotAdminCards()
    {
        var model = CreateModel("Moderator");

        await model.OnGetAsync();

        model.ShowConnectedServers.Should().BeTrue();
        model.ShowAuditLog.Should().BeFalse();
    }

    [Fact]
    public async Task OnGet_AsAdmin_SeesEverythingAndSyncAsksFirst()
    {
        var model = CreateModel("Admin");

        await model.OnGetAsync();

        model.ShowConnectedServers.Should().BeTrue();
        model.ShowAuditLog.Should().BeTrue();
        var sync = model.QuickActions.Actions.Single(a => a.Handler == "SyncAllGuilds");
        sync.RequiresConfirmation.Should().BeTrue();
        sync.ConfirmationModalId.Should().Be("syncGuildsModal");
        model.QuickActions.Actions.Should().OnlyContain(a => a.IsAdminOnly);
    }

    [Fact]
    public async Task OnGet_HeroCardsSayWhatTheyCount()
    {
        var model = CreateModel("Admin");

        await model.OnGetAsync();

        model.HeroMetrics.Select(m => m.Title).Should().Equal("Servers", "Members", "Commands (24h)", "Uptime (24h)");
        model.HeroMetrics.Select(m => m.Value).Should().Equal("1", "5", "4", "100%");
        model.HeroMetrics.Select(m => m.DataAttribute).Should().Equal(
            "data-stat-servers", "data-stat-members", "data-stat-commands", "data-stat-uptime");
    }

    [Fact]
    public async Task OnGetStats_ReturnsTheProvidersNumbers()
    {
        var model = CreateModel("Viewer");
        var expected = new DashboardStatsDto { TotalServers = 2, CommandsLast24Hours = 9 };
        _stats.Setup(s => s.GetStatsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await model.OnGetStatsAsync(CancellationToken.None);

        result.Should().BeOfType<JsonResult>().Which.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task OnGetConnectedServers_AsViewer_IsForbidden()
    {
        var model = CreateModel("Viewer");

        var result = await model.OnGetConnectedServersAsync();

        result.Should().BeOfType<ForbidResult>();
        _guilds.Verify(g => g.GetAllGuildsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnGetConnectedServers_AsModerator_ReturnsRowsWithStringIds()
    {
        var model = CreateModel("Moderator");

        var result = await model.OnGetConnectedServersAsync();

        var json = result.Should().BeOfType<JsonResult>().Subject;
        var text = System.Text.Json.JsonSerializer.Serialize(json.Value);
        text.Should().Contain("\"id\":\"1\"", "a snowflake must reach JavaScript as a string");
        text.Should().Contain("\"commandsToday\":4");
    }

    [Fact]
    public async Task OnPostSyncAllGuilds_AsViewer_IsRefusedAndDoesNothing()
    {
        var model = CreateModel("Viewer");

        var result = await model.OnPostSyncAllGuildsAsync();

        result.Should().BeOfType<JsonResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _guilds.Verify(g => g.SyncAllGuildsAsync(It.IsAny<CancellationToken>()), Times.Never);
        _broadcaster.Verify(b => b.NotifyChanged(), Times.Never);
    }

    [Fact]
    public async Task OnPostSyncAllGuilds_AsAdmin_SyncsAndNotifiesOpenDashboards()
    {
        var model = CreateModel("Admin");
        _guilds.Setup(g => g.SyncAllGuildsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var result = await model.OnPostSyncAllGuildsAsync();

        var json = result.Should().BeOfType<JsonResult>().Subject;
        System.Text.Json.JsonSerializer.Serialize(json.Value).Should().Contain("Synced 3 servers");
        _broadcaster.Verify(b => b.NotifyChanged(), Times.Once);
    }

    [Fact]
    public async Task OnPostSyncAllGuilds_WithNothingConnected_SaysSoInsteadOfClaimingSuccess()
    {
        var model = CreateModel("Admin");
        _guilds.Setup(g => g.SyncAllGuildsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var result = await model.OnPostSyncAllGuildsAsync();

        System.Text.Json.JsonSerializer.Serialize(result.Should().BeOfType<JsonResult>().Subject.Value)
            .Should().Contain("nothing to sync");
    }

    [Fact]
    public async Task OnPostRestartBot_InOfflineMode_AnswersWithAPlainReason()
    {
        var model = CreateModel("Admin");
        _bot.Setup(b => b.RestartAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new NotSupportedException("offline"));

        var result = await model.OnPostRestartBotAsync();

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        System.Text.Json.JsonSerializer.Serialize(json.Value).Should().Contain("offline mode");
    }

    [Fact]
    public async Task OnPostRestartBot_AsViewer_IsForbidden()
    {
        var model = CreateModel("Viewer");

        var result = await model.OnPostRestartBotAsync();

        result.Should().BeOfType<ForbidResult>();
        _bot.Verify(b => b.RestartAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
