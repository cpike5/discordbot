using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using IndexPage = DiscordBot.Bot.Blazor.Pages.Guilds.Index;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds;

/// <summary>
/// Component tests for <see cref="IndexPage"/>, the routable replacement for
/// <c>Pages/Guilds/Index.cshtml</c> + <c>IndexModel</c> (docs/plans/blazor-port-plan.md §5 Phase
/// 4, cluster 4d). Top-level (not guild-scoped), under <c>MainLayout</c> - no
/// <c>IGuildContextProvider</c> mock needed, only <see cref="IGuildService"/> and
/// <c>UserManager&lt;ApplicationUser&gt;</c> (mocked the same way <c>ProfileTests</c> does).
/// </summary>
public class IndexTests : BlazorComponentTestContext
{
    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManager;

    public IndexTests()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, new Mock<IPasswordHasher<ApplicationUser>>().Object,
            Array.Empty<IUserValidator<ApplicationUser>>(), Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new Mock<ILookupNormalizer>().Object, new Mock<IdentityErrorDescriber>().Object,
            new Mock<IServiceProvider>().Object, new Mock<ILogger<UserManager<ApplicationUser>>>().Object);

        Services.AddSingleton(_guildService.Object);
        Services.AddSingleton(_userManager.Object);
    }

    private void AuthorizeWithRoles(params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user-1") };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        AddAuthorization().SetAuthorized("user").SetClaims(claims.ToArray());

        var user = new ApplicationUser { Id = "user-1", Email = "user@example.test" };
        _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(roles.ToList());
    }

    private static PaginatedResponseDto<GuildDto> Page(params GuildDto[] guilds) => new()
    {
        Items = guilds,
        Page = 1,
        PageSize = 10,
        TotalCount = guilds.Length
    };

    private static GuildDto BuildGuild(ulong id, string name, bool isActive = true) => new()
    {
        Id = id,
        Name = name,
        IsActive = isActive,
        MemberCount = 42,
        JoinedAt = DateTime.UtcNow
    };

    private void SetUp(params string[] roles)
    {
        AuthorizeWithRoles(roles.Length == 0 ? new[] { "Moderator" } : roles);
        AddBunitPersistentComponentState();
        SetInteractiveRendererInfo();
    }

    [Fact]
    public void List_RendersRows_ForResolvedGuilds()
    {
        SetUp("Admin");
        _guildService.Setup(s => s.GetGuildsAsync(It.IsAny<GuildSearchQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(BuildGuild(1, "Test Guild")));

        var cut = Render<IndexPage>();

        cut.FindAll("[data-testid='guild-row']").Should().HaveCount(1);
        cut.Markup.Should().Contain("Test Guild");
    }

    [Fact]
    public void NoGuilds_ShowsEmptyState()
    {
        SetUp("Admin");
        _guildService.Setup(s => s.GetGuildsAsync(It.IsAny<GuildSearchQueryDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Page());

        var cut = Render<IndexPage>();

        cut.Markup.Should().Contain("No servers yet");
    }

    [Fact]
    public void ModeratorRole_IsFilteredView_ShowsBanner()
    {
        SetUp("Moderator");
        _guildService.Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<GuildDto> { BuildGuild(1, "A"), BuildGuild(2, "B") });
        _guildService.Setup(s => s.GetGuildsAsync(It.IsAny<GuildSearchQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(BuildGuild(1, "A")));

        var cut = Render<IndexPage>();

        cut.Markup.Should().Contain("Only servers where you are a Discord member are shown");
    }

    [Fact]
    public void AdminRole_IsNotFiltered_NoBanner()
    {
        SetUp("Admin");
        _guildService.Setup(s => s.GetGuildsAsync(It.IsAny<GuildSearchQueryDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Page(BuildGuild(1, "A")));

        var cut = Render<IndexPage>();

        cut.Markup.Should().NotContain("Only servers where you are a Discord member are shown");
        cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Sync All"));
    }

    [Fact]
    public void ModeratorRole_DoesNotShowSyncAllButton()
    {
        SetUp("Moderator");
        _guildService.Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<GuildDto>());
        _guildService.Setup(s => s.GetGuildsAsync(It.IsAny<GuildSearchQueryDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Page());

        var cut = Render<IndexPage>();

        cut.Markup.Should().NotContain("Sync All");
    }

    [Fact]
    public void SyncGuild_Success_TogglesToastAndReloads()
    {
        SetUp("Admin");
        var guild = BuildGuild(1, "Test Guild");
        _guildService.SetupSequence(s => s.GetGuildsAsync(It.IsAny<GuildSearchQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(guild))
            .ReturnsAsync(Page(guild));
        _guildService.Setup(s => s.SyncGuildAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var cut = Render<IndexPage>();
        cut.Find("button[title='Sync guild data']").Click();

        cut.WaitForAssertion(() => _guildService.Verify(s => s.SyncGuildAsync(1, It.IsAny<CancellationToken>()), Times.Once));
        var toast = Services.GetRequiredService<IToastService>();
        cut.WaitForAssertion(() => toast.Toasts.Should().Contain(t => t.Level == ToastLevel.Success));
    }

    [Fact]
    public void SearchForm_Submit_NavigatesWithTheTypedFilters()
    {
        SetUp("Admin");
        _guildService.Setup(s => s.GetGuildsAsync(It.IsAny<GuildSearchQueryDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Page());
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        var cut = Render<IndexPage>();
        cut.Find("#SearchTerm").Input("alice");
        cut.Find("form").Submit();

        navMan.Uri.Should().Contain("SearchTerm=alice");
    }

    [Fact]
    public void SupplyParameterFromQuery_PassesFiltersToTheService()
    {
        SetUp("Admin");
        _guildService.Setup(s => s.GetGuildsAsync(It.Is<GuildSearchQueryDto>(q => q.SearchTerm == "bob" && q.IsActive == true), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page());
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/Guilds?SearchTerm=bob&StatusFilter=true");

        Render<IndexPage>();

        _guildService.Verify(s => s.GetGuildsAsync(It.Is<GuildSearchQueryDto>(q => q.SearchTerm == "bob" && q.IsActive == true), It.IsAny<CancellationToken>()), Times.Once);
    }
}
