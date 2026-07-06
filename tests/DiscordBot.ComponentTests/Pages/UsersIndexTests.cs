using System.Security.Claims;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Pages.Admin.Users;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Pages;

/// <summary>
/// Tests for the routed Blazor twin of the old Pages/Admin/Users/Index.cshtml
/// (Phase F migration). Covers list rendering, live filters, and the ported
/// ToggleActive handler (ConfirmModal + service call on the same arguments).
/// </summary>
public class UsersIndexTests : TestContext
{
    private readonly Mock<IUserManagementService> _userService = new();
    private readonly List<UserSearchQueryDto> _queries = new();

    private static readonly UserDto ActiveUser = new()
    {
        Id = "user-2",
        Email = "alice@example.com",
        DisplayName = "Alice",
        IsActive = true,
        Roles = new[] { "Moderator" },
        IsDiscordLinked = true,
        DiscordUsername = "alice#1",
        LastLoginAt = DateTime.UtcNow.AddDays(-1)
    };

    private static readonly UserDto InactiveUser = new()
    {
        Id = "user-3",
        Email = "bob@example.com",
        IsActive = false,
        Roles = new[] { "Viewer" }
    };

    public UsersIndexTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _userService
            .Setup(s => s.GetUsersAsync(It.IsAny<UserSearchQueryDto>(), It.IsAny<CancellationToken>()))
            .Callback<UserSearchQueryDto, CancellationToken>((q, _) => _queries.Add(q))
            .ReturnsAsync(new PaginatedResponseDto<UserDto>
            {
                Items = new[] { ActiveUser, InactiveUser },
                Page = 1,
                PageSize = 20,
                TotalCount = 2
            });
        _userService
            .Setup(s => s.GetAvailableRolesAsync("admin-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "Admin", "Moderator", "Viewer" });
        _userService
            .Setup(s => s.SetUserActiveStatusAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserManagementResult.Success());

        // The page resolves the service through IServiceScopeFactory (scope per operation).
        Services.AddSingleton(_userService.Object);
        Services.AddScoped<ToastInterop>();
        Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        Services.AddScoped<CircuitClientInfoService>();
        Services.AddLogging();

        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("Admin User");
        auth.SetRoles("Admin");
        auth.SetClaims(new Claim(ClaimTypes.NameIdentifier, "admin-1"));
        auth.SetPolicies("RequireAdmin");
    }

    [Fact]
    public void RendersUserRows_AndCreateButtonForAdmin()
    {
        var cut = RenderComponent<UsersIndex>();

        cut.Markup.Should().Contain("alice@example.com");
        cut.Markup.Should().Contain("bob@example.com");
        cut.Markup.Should().Contain("Moderator"); // role badge
        cut.Markup.Should().Contain("Create User");
        cut.Markup.Should().Contain("/Admin/Users/Edit?id=user-2");
        cut.Markup.Should().Contain("/Admin/Users/Details?id=user-2");

        // Same query shape the page model built in OnGetAsync.
        _queries.Should().NotBeEmpty();
        _queries[0].SortBy.Should().Be("CreatedAt");
        _queries[0].SortDescending.Should().BeTrue();
        _queries[0].PageSize.Should().Be(20);
    }

    [Fact]
    public void StatusFilter_AppliesImmediately()
    {
        var cut = RenderComponent<UsersIndex>();
        _queries.Clear();

        // selects: [0] Role, [1] Status, [2] Discord
        cut.FindAll("select")[1].Change("true");

        cut.WaitForAssertion(() =>
        {
            _queries.Should().ContainSingle();
            _queries[0].IsActive.Should().BeTrue();
            _queries[0].Page.Should().Be(1);
        });
    }

    [Fact]
    public void DisableUser_ConfirmedThroughModal_CallsServiceWithActor()
    {
        var cut = RenderComponent<UsersIndex>();

        // Row action for the active user opens the ConfirmModal…
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Disable").Click();

        // …whose confirm button carries the same "Disable" label.
        cut.WaitForAssertion(() => cut.Find("[role=alertdialog]"));
        cut.Find("[role=alertdialog]").QuerySelectorAll("button")
            .First(b => b.TextContent.Trim() == "Disable")
            .Click();

        cut.WaitForAssertion(() =>
        {
            _userService.Verify(s => s.SetUserActiveStatusAsync(
                "user-2", false, "admin-1", It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
            cut.Markup.Should().Contain("User disabled successfully");
        });
    }

    [Fact]
    public void CancellingConfirmModal_DoesNotCallService()
    {
        var cut = RenderComponent<UsersIndex>();

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Disable").Click();
        cut.WaitForAssertion(() => cut.Find("[role=alertdialog]"));
        cut.Find("[role=alertdialog]").QuerySelectorAll("button")
            .First(b => b.TextContent.Trim() == "Cancel")
            .Click();

        _userService.Verify(s => s.SetUserActiveStatusAsync(
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
