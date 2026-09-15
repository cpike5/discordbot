using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using IndexPage = DiscordBot.Bot.Blazor.Pages.Guilds.FeatureRequests.Index;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds.FeatureRequests;

/// <summary>
/// Component tests for <see cref="IndexPage"/>, the routable replacement for
/// <c>Pages/Guilds/FeatureRequests/Index.cshtml</c> + <c>IndexModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b).
/// </summary>
public class IndexTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;

    private readonly Mock<IFeatureRequestService> _service = new();

    public IndexTests()
    {
        Services.AddSingleton(_service.Object);
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

    private void NavigateTo(string path)
        => ((BunitNavigationManager)Services.GetRequiredService<NavigationManager>()).NavigateTo(path);

    private static FeatureRequest BuildRequest(FeatureRequestStatus status = FeatureRequestStatus.Submitted) => new()
    {
        Id = Guid.NewGuid(),
        GuildId = GuildId,
        SubmittedByUserId = 111,
        Description = "Add a /vote command",
        Status = status,
        CreatedAt = DateTime.UtcNow
    };

    private void SetUp()
    {
        AddAuthorizedAdmin();
        AddBunitPersistentComponentState();
        RegisterMockProvider();
        SetInteractiveRendererInfo();
    }

    [Fact]
    public void List_RendersRows_ForResolvedItems()
    {
        SetUp();
        _service.Setup(s => s.GetByGuildIdAsync(GuildId, null, 1, 20))
            .ReturnsAsync((new[] { BuildRequest() }, 1));
        NavigateTo($"/Guilds/FeatureRequests/{GuildId}");

        var cut = Render<IndexPage>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.FindAll("[data-testid='feature-request-row']").Should().HaveCount(1);
        cut.Markup.Should().Contain("Add a /vote command");
    }

    [Fact]
    public void NoItems_ShowsEmptyState()
    {
        SetUp();
        _service.Setup(s => s.GetByGuildIdAsync(GuildId, null, 1, 20)).ReturnsAsync((Array.Empty<FeatureRequest>(), 0));
        NavigateTo($"/Guilds/FeatureRequests/{GuildId}");

        var cut = Render<IndexPage>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.Markup.Should().Contain("No feature requests");
    }

    [Fact]
    public void StatusFilter_Change_NavigatesWithStatusQuery()
    {
        SetUp();
        _service.Setup(s => s.GetByGuildIdAsync(GuildId, It.IsAny<FeatureRequestStatus?>(), 1, 20))
            .ReturnsAsync((Array.Empty<FeatureRequest>(), 0));
        NavigateTo($"/Guilds/FeatureRequests/{GuildId}");
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        var cut = Render<IndexPage>(p => p.Add(x => x.GuildId, (long)GuildId));
        cut.Find("#statusFilter").Change(((int)FeatureRequestStatus.Approved).ToString());

        navMan.Uri.Should().Contain($"StatusFilter={(int)FeatureRequestStatus.Approved}");
    }

    [Fact]
    public void PageFallback_LegacyPageQuery_IsUsed_WhenPageNumberAbsent()
    {
        SetUp();
        _service.Setup(s => s.GetByGuildIdAsync(GuildId, null, 2, 20)).ReturnsAsync((Array.Empty<FeatureRequest>(), 0));
        NavigateTo($"/Guilds/FeatureRequests/{GuildId}?page=2");

        Render<IndexPage>(p => p.Add(x => x.GuildId, (long)GuildId));

        _service.Verify(s => s.GetByGuildIdAsync(GuildId, null, 2, 20), Times.Once);
    }
}
