using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Moq;
// "Index" alone is ambiguous with System.Index (implicit usings pull in System) once this file
// also references the page type by its short name - alias it rather than fully qualifying every use.
using IndexPage = DiscordBot.Bot.Blazor.Pages.Admin.Users.Index;

namespace DiscordBot.ComponentTests.Blazor.Pages.Admin.Users;

/// <summary>
/// Component tests for <see cref="Index"/>, the routable replacement for
/// <c>Pages/Admin/Users/Index.cshtml</c> + <c>IndexModel</c> (docs/plans/blazor-port-plan.md §5
/// Phase 4, cluster 4a).
/// </summary>
public class IndexTests : BlazorComponentTestContext
{
    private const string CurrentUserId = "admin-1";

    private readonly Mock<IUserManagementService> _service = new();

    public IndexTests()
    {
        Services.AddSingleton(_service.Object);
        _service.Setup(s => s.GetAvailableRolesAsync(CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "Viewer", "Moderator", "Admin", "SuperAdmin" });
    }

    private void AuthorizeAsAdmin(string userId = CurrentUserId)
        => AddAuthorization().SetAuthorized("admin").SetClaims(
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, "Admin"));

    private static PaginatedResponseDto<UserDto> Page(params UserDto[] users) => new()
    {
        Items = users,
        Page = 1,
        PageSize = 20,
        TotalCount = users.Length
    };

    private static UserDto BuildUser(string id, string email, string role = "Viewer", bool isActive = true) => new()
    {
        Id = id,
        Email = email,
        IsActive = isActive,
        Roles = new[] { role }
    };

    [Fact]
    public void OnInitialized_CallsServiceWithTheBoundQuery_AndRendersRows()
    {
        AuthorizeAsAdmin();
        AddBunitPersistentComponentState();
        _service.Setup(s => s.GetUsersAsync(It.Is<UserSearchQueryDto>(q =>
                q.SearchTerm == null && q.Role == null && q.IsActive == null && q.Page == 1 && q.SortBy == "CreatedAt" && q.SortDescending),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(BuildUser("u1", "alice@example.test")));

        var cut = Render<IndexPage>();

        cut.FindAll("[data-testid='users-row']").Should().HaveCount(1);
        cut.Markup.Should().Contain("alice@example.test");
        _service.Verify(s => s.GetUsersAsync(It.IsAny<UserSearchQueryDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void NoUsers_ShowsEmptyState()
    {
        AuthorizeAsAdmin();
        AddBunitPersistentComponentState();
        _service.Setup(s => s.GetUsersAsync(It.IsAny<UserSearchQueryDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Page());

        var cut = Render<IndexPage>();

        cut.FindAll("[data-testid='users-row']").Should().BeEmpty();
        cut.Markup.Should().Contain("No users yet");
    }

    [Fact]
    public void FilterForm_Submit_NavigatesWithTheTypedFilters()
    {
        AuthorizeAsAdmin();
        AddBunitPersistentComponentState();
        _service.Setup(s => s.GetUsersAsync(It.IsAny<UserSearchQueryDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Page());
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        var cut = Render<IndexPage>();
        cut.Find("#SearchTerm").Input("alice");
        cut.Find("#RoleFilter").Change("Admin");
        cut.Find("[data-testid='users-filter-form']").Submit();

        navMan.Uri.Should().Contain("SearchTerm=alice").And.Contain("RoleFilter=Admin");
    }

    [Fact]
    public void SupplyParameterFromQuery_PassesFiltersToTheService()
    {
        AuthorizeAsAdmin();
        AddBunitPersistentComponentState();
        _service.Setup(s => s.GetUsersAsync(It.Is<UserSearchQueryDto>(q => q.SearchTerm == "bob" && q.Role == "Admin" && q.IsActive == true), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page());
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/Admin/Users?SearchTerm=bob&RoleFilter=Admin&ActiveFilter=true");

        Render<IndexPage>();

        _service.Verify(s => s.GetUsersAsync(It.Is<UserSearchQueryDto>(q => q.SearchTerm == "bob" && q.Role == "Admin" && q.IsActive == true), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void CreateUserLink_OnlyRendersForAdminOrSuperAdmin()
    {
        AddAuthorization().SetAuthorized("viewer").SetClaims(new Claim(ClaimTypes.NameIdentifier, CurrentUserId), new Claim(ClaimTypes.Role, "Viewer"));
        AddBunitPersistentComponentState();
        _service.Setup(s => s.GetUsersAsync(It.IsAny<UserSearchQueryDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Page());

        var cut = Render<IndexPage>();

        cut.FindAll("a[href='/Admin/Users/Create']").Should().BeEmpty();
    }

    [Fact]
    public void ToggleActive_Confirmed_CallsTheServiceAndToasts_ThenReloads()
    {
        AuthorizeAsAdmin();
        AddBunitPersistentComponentState();
        var user = BuildUser("u1", "alice@example.test", isActive: true);
        _service.SetupSequence(s => s.GetUsersAsync(It.IsAny<UserSearchQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(user))
            .ReturnsAsync(Page(BuildUser("u1", "alice@example.test", isActive: false)));
        _service.Setup(s => s.SetUserActiveStatusAsync("u1", false, CurrentUserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserManagementResult.Success());

        var cut = Render<IndexPage>();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Disable").Click();

        // The modal's footer renders Cancel then Confirm - the last button inside it is Confirm.
        cut.Find("#toggle-active-modal").QuerySelectorAll("button").Last().Click();

        cut.WaitForAssertion(() => _service.Verify(
            s => s.SetUserActiveStatusAsync("u1", false, CurrentUserId, null, It.IsAny<CancellationToken>()), Times.Once));
        var toast = Services.GetRequiredService<IToastService>();
        cut.WaitForAssertion(() => toast.Toasts.Should().Contain(t => t.Level == ToastLevel.Success));
    }

    [Fact]
    public void Pagination_OnPageChanged_Navigates()
    {
        AuthorizeAsAdmin();
        AddBunitPersistentComponentState();
        var users = Enumerable.Range(1, 20).Select(i => BuildUser($"u{i}", $"user{i}@example.test")).ToArray();
        _service.Setup(s => s.GetUsersAsync(It.IsAny<UserSearchQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginatedResponseDto<UserDto> { Items = users, Page = 1, PageSize = 20, TotalCount = 45 });
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        var cut = Render<IndexPage>();
        cut.InvokeAsync(() => cut.FindComponent<DiscordBot.Bot.Blazor.Shared.Pagination>().Instance.OnPageChanged.InvokeAsync(2));

        navMan.Uri.Should().Contain("pageNumber=2");
    }
}
