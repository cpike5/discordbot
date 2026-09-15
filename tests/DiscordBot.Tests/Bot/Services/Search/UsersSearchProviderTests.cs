using System.Security.Claims;
using DiscordBot.Bot.Services.Search;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Services.Search;

/// <summary>
/// Unit tests for <see cref="UsersSearchProvider"/>. Covers the review finding on
/// <c>ViewAllUrl</c>: the query parameter it builds must match what
/// <c>Blazor/Pages/Admin/Users/Index.razor.cs</c> actually binds
/// (<c>[SupplyParameterFromQuery(Name = "SearchTerm")]</c>) - the provider previously built
/// <c>?search=...</c>, a name the page never reads, so "View all" silently dropped the term.
/// </summary>
public class UsersSearchProviderTests
{
    private readonly Mock<IUserManagementService> _userManagementService = new();
    private readonly UsersSearchProvider _provider;

    public UsersSearchProviderTests()
    {
        _provider = new UsersSearchProvider(_userManagementService.Object, Mock.Of<ILogger<UsersSearchProvider>>());
    }

    private void SetUpUsers(params UserDto[] users)
    {
        _userManagementService
            .Setup(s => s.GetUsersAsync(It.IsAny<UserSearchQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginatedResponseDto<UserDto>
            {
                Items = users,
                Page = 1,
                PageSize = 25,
                TotalCount = users.Length
            });
    }

    [Fact]
    public async Task SearchAsync_BuildsViewAllUrl_UsingSearchTermQueryParameter()
    {
        SetUpUsers(new UserDto { Id = "u1", Email = "alice@example.com" });

        var result = await _provider.SearchAsync("alice", maxResults: 5, new ClaimsPrincipal());

        result.ViewAllUrl.Should().Be("/Admin/Users?SearchTerm=alice");
    }

    [Fact]
    public async Task SearchAsync_BuildsViewAllUrl_UrlEncodesTheSearchTerm()
    {
        SetUpUsers();

        var result = await _provider.SearchAsync("a b&c", maxResults: 5, new ClaimsPrincipal());

        result.ViewAllUrl.Should().Be("/Admin/Users?SearchTerm=a%20b%26c");
    }

    [Fact]
    public async Task SearchAsync_MapsUserFields_IntoSearchResultItem()
    {
        SetUpUsers(new UserDto
        {
            Id = "u1",
            Email = "alice@example.com",
            DisplayName = "Alice",
            Roles = ["Admin"],
            IsActive = true,
            IsDiscordLinked = false
        });

        var result = await _provider.SearchAsync("alice", maxResults: 5, new ClaimsPrincipal());

        result.Category.Should().Be(SearchCategory.Users);
        var item = result.Items.Should().ContainSingle().Subject;
        item.Title.Should().Be("Alice");
        item.Subtitle.Should().Be("alice@example.com");
        item.Url.Should().Be("/Admin/Users/Details?id=u1");
        item.Description.Should().Be("No Discord account linked");
    }
}
