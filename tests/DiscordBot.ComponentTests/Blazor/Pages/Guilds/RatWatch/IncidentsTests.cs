using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Configuration;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using IncidentsPage = DiscordBot.Bot.Blazor.Pages.Guilds.RatWatch.Incidents;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds.RatWatch;

/// <summary>
/// Component tests for <see cref="IncidentsPage"/>, the routable replacement for
/// <c>Pages/Guilds/RatWatch/Incidents.cshtml</c> + <c>IncidentsModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d). Covers filters, the incident detail
/// modal (loaded on demand from <c>GetByIdAsync</c>), and the server-built CSV export content.
/// </summary>
public class IncidentsTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;

    private readonly Mock<IRatWatchService> _service = new();

    public IncidentsTests()
    {
        Services.AddSingleton(_service.Object);
        _service.Setup(s => s.GetGuildSettingsAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildRatWatchSettings { GuildId = GuildId, IsEnabled = true, Timezone = "UTC", MaxAdvanceHours = 24, VotingDurationMinutes = 5 });
    }

    private static GuildContext Context() => new(
        Guild: new GuildDto { Id = GuildId, Name = "Test Guild" },
        GuildId: GuildId,
        GuildIdString: GuildId.ToString(),
        IsAppAdmin: true,
        IsGuildAdmin: false,
        CanEdit: true,
        AudioEnabled: true,
        RatWatchEnabled: true,
        Tabs: GuildNavigationConfig.GetTabs());

    private void RegisterMockProvider()
    {
        var mock = new Mock<IGuildContextProvider>();
        mock.Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Ok(Context()));
        Services.AddScoped(_ => mock.Object);
    }

    private static RatWatchDto BuildIncident(RatWatchStatus status = RatWatchStatus.Guilty) => new()
    {
        Id = Guid.NewGuid(),
        GuildId = GuildId,
        AccusedUserId = 1,
        AccusedUsername = "accused-user",
        InitiatorUserId = 2,
        InitiatorUsername = "initiator-user",
        CustomMessage = "Said they'd be back in 5",
        ScheduledAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        Status = status,
        GuiltyVotes = 3,
        NotGuiltyVotes = 1
    };

    private IRenderedComponent<IncidentsPage> RenderIncidents()
    {
        AddAuthorization().SetAuthorized("mod").SetClaims(new Claim(ClaimTypes.Role, "Moderator"));
        AddBunitPersistentComponentState();
        RegisterMockProvider();
        SetInteractiveRendererInfo();

        return Render<IncidentsPage>(p => p.Add(x => x.GuildId, (long)GuildId));
    }

    [Fact]
    public void List_RendersRows_ForResolvedIncidents()
    {
        _service.Setup(s => s.GetFilteredByGuildAsync(GuildId, It.IsAny<RatWatchIncidentFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { BuildIncident() }, 1));

        var cut = RenderIncidents();

        cut.FindAll("[data-testid='ratwatch-incident-row']").Should().HaveCount(1);
        cut.Markup.Should().Contain("accused-user");
    }

    [Fact]
    public void NoIncidents_ShowsEmptyState()
    {
        _service.Setup(s => s.GetFilteredByGuildAsync(GuildId, It.IsAny<RatWatchIncidentFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<RatWatchDto>(), 0));

        var cut = RenderIncidents();

        cut.Markup.Should().Contain("No incidents found");
    }

    [Fact]
    public void DefaultDateRange_FiltersLast30Days_WhenNoDatesSupplied()
    {
        _service.Setup(s => s.GetFilteredByGuildAsync(GuildId, It.Is<RatWatchIncidentFilterDto>(f =>
                f.StartDate!.Value.Date == DateTime.Today.AddDays(-30) && f.EndDate!.Value.Date == DateTime.Today),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<RatWatchDto>(), 0));

        RenderIncidents();

        _service.Verify(s => s.GetFilteredByGuildAsync(GuildId, It.IsAny<RatWatchIncidentFilterDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ViewButton_OpensModal_AndLoadsIncidentDetail()
    {
        var incident = BuildIncident();
        _service.Setup(s => s.GetFilteredByGuildAsync(GuildId, It.IsAny<RatWatchIncidentFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { incident }, 1));
        _service.Setup(s => s.GetByIdAsync(incident.Id, It.IsAny<CancellationToken>())).ReturnsAsync(incident);

        var cut = RenderIncidents();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "View").Click();

        cut.WaitForAssertion(() => cut.Find("#incidentDetailModal").TextContent.Should().Contain("accused-user"));
    }

    [Fact]
    public void ExportCsv_DownloadsFile_WithExpectedHeaderAndRow()
    {
        var incident = BuildIncident();
        _service.Setup(s => s.GetFilteredByGuildAsync(GuildId, It.IsAny<RatWatchIncidentFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { incident }, 1));
        var downloadHandler = JSInterop.SetupModule("./js/blazor/browser.js").SetupVoid("downloadFile", _ => true);

        var cut = RenderIncidents();
        cut.FindAll("button").Single(b => b.TextContent.Contains("Export CSV")).Click();

        downloadHandler.Invocations.Should().ContainSingle();
        var invocation = downloadHandler.Invocations.Single();
        var content = (string)invocation.Arguments[1]!;
        content.Should().Contain("Date,Accused,Initiator,Status,Votes For,Votes Against,Custom Message");
        content.Should().Contain("accused-user");
        content.Should().Contain("initiator-user");
    }
}
