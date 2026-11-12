using System.Security.Claims;
using System.Text.Json;
using Discord.WebSocket;
using DiscordBot.Bot.Authorization;
using DiscordBot.Bot.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscordBot.Tests.Bot.Pages;

/// <summary>
/// Tests for <see cref="SearchModel.OnGetSuggestAsync"/>, the navbar's live suggestions.
/// </summary>
public class SearchModelSuggestTests
{
    private const ulong AllowedGuildId = 123456789012345678UL;
    private const ulong HiddenGuildId = 987654321098765432UL;

    private readonly Mock<ISearchService> _searchService = new();
    private readonly Mock<IAuthorizationService> _authorizationService = new();
    private readonly SearchModel _model;

    public SearchModelSuggestTests()
    {
        var userManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);

        _model = new SearchModel(
            _searchService.Object,
            _authorizationService.Object,
            Mock.Of<IUserDiscordGuildService>(),
            userManager.Object,
            new DiscordSocketClient(),
            NullLogger<SearchModel>.Instance);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "viewer")], "Test"))
        };
        _model.PageContext = new PageContext(
            new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary()));

        // By default the user may open every guild; tests that hide one override this.
        _authorizationService
            .Setup(s => s.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        SetupCategory(SearchCategory.Guilds, []);
        SetupCategory(SearchCategory.Commands, []);
    }

    private void SetupCategory(SearchCategory category, List<SearchResultItemDto> items)
    {
        _searchService
            .Setup(s => s.SearchAsync(
                It.Is<SearchQueryDto>(q => q.CategoryFilter == category),
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchQueryDto q, ClaimsPrincipal _, CancellationToken _) =>
            {
                var result = new SearchCategoryResult { Category = category, Items = items, TotalCount = items.Count };
                return category == SearchCategory.Guilds
                    ? new UnifiedSearchResultDto { SearchTerm = q.SearchTerm, Guilds = result }
                    : new UnifiedSearchResultDto { SearchTerm = q.SearchTerm, Commands = result };
            });
    }

    private static SearchResultItemDto Guild(ulong id, string name) => new()
    {
        Id = id.ToString(),
        Title = name,
        Url = $"/Guilds/Details/{id}"
    };

    private static SearchResultItemDto Command(string fullName) => new()
    {
        Id = fullName,
        Title = "/" + fullName,
        Url = $"/Commands#cmd-{fullName.Replace(' ', '-')}"
    };

    private static SearchSuggestResponse Body(IActionResult result) =>
        result.Should().BeOfType<JsonResult>().Subject.Value.Should().BeOfType<SearchSuggestResponse>().Subject;

    [Fact]
    public async Task Suggest_ReturnsGuildIdsAsStrings_ThatSurviveJavaScript()
    {
        SetupCategory(SearchCategory.Guilds, [Guild(AllowedGuildId, "Pixel Pirates")]);

        var result = await _model.OnGetSuggestAsync("pix", CancellationToken.None);

        var body = Body(result);
        body.Term.Should().Be("pix");
        body.Guilds.Should().ContainSingle().Which.Should().Be(new SearchSuggestGuild(
            "123456789012345678", "Pixel Pirates", "/Guilds/Details/123456789012345678", null));

        // The wire format carries the id as a JSON string, so no digit is rounded away in the browser
        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.Should().Contain("\"id\":\"123456789012345678\"");
    }

    [Fact]
    public async Task Suggest_AsksForGuildsAndCommandsOnly()
    {
        await _model.OnGetSuggestAsync("pix", CancellationToken.None);

        _searchService.Verify(s => s.SearchAsync(
            It.Is<SearchQueryDto>(q => q.CategoryFilter == SearchCategory.Guilds),
            It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Once);
        _searchService.Verify(s => s.SearchAsync(
            It.Is<SearchQueryDto>(q => q.CategoryFilter == SearchCategory.Commands && q.MaxResultsPerCategory == 3),
            It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Once);
        _searchService.Verify(s => s.SearchAsync(
            It.Is<SearchQueryDto>(q => q.CategoryFilter == null),
            It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Suggest_WhenTheServiceFindsNothingTheUserMaySee_ReturnsEmptyLists()
    {
        var result = await _model.OnGetSuggestAsync("secret", CancellationToken.None);

        var body = Body(result);
        body.Guilds.Should().BeEmpty();
        body.Commands.Should().BeEmpty();
    }

    [Fact]
    public async Task Suggest_LeavesOutGuildsTheUserCannotOpen()
    {
        SetupCategory(SearchCategory.Guilds, [Guild(HiddenGuildId, "Hidden Haven"), Guild(AllowedGuildId, "Pixel Pirates")]);
        _authorizationService
            .Setup(s => s.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.Is<object>(o => o is ulong && (ulong)o == HiddenGuildId),
                It.Is<IEnumerable<IAuthorizationRequirement>>(r => r.OfType<GuildAccessRequirement>().Any())))
            .ReturnsAsync(AuthorizationResult.Failed());

        var result = await _model.OnGetSuggestAsync("ha", CancellationToken.None);

        Body(result).Guilds.Select(g => g.Name).Should().Equal("Pixel Pirates");
    }

    [Fact]
    public async Task Suggest_ListsAtMostFiveGuilds()
    {
        SetupCategory(SearchCategory.Guilds,
            Enumerable.Range(1, 8).Select(i => Guild((ulong)i, $"Guild {i}")).ToList());

        var result = await _model.OnGetSuggestAsync("guild", CancellationToken.None);

        Body(result).Guilds.Should().HaveCount(5);
    }

    [Fact]
    public async Task Suggest_SkipsGuildsWithMalformedIds()
    {
        SetupCategory(SearchCategory.Guilds, [new SearchResultItemDto { Id = "not-a-snowflake", Title = "Broken" }]);

        var result = await _model.OnGetSuggestAsync("bro", CancellationToken.None);

        Body(result).Guilds.Should().BeEmpty();
    }

    [Fact]
    public async Task Suggest_CommandsOpenTheCommandListFilteredToThem()
    {
        SetupCategory(SearchCategory.Commands, [Command("play"), Command("sound play")]);

        var result = await _model.OnGetSuggestAsync("play", CancellationToken.None);

        Body(result).Commands.Should().Equal(
            new SearchSuggestCommand("/play", "/Commands?q=play"),
            new SearchSuggestCommand("/sound play", "/Commands?q=sound%20play"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData("  a  ")]
    public async Task Suggest_WithFewerThanTwoCharacters_IsBadRequest(string? term)
    {
        var result = await _model.OnGetSuggestAsync(term, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeOfType<ProblemDetails>().Which.Status.Should().Be(400);
        _searchService.Verify(s => s.SearchAsync(
            It.IsAny<SearchQueryDto>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Suggest_TrimsTheTermAndCapsItAtOneHundredCharacters()
    {
        var longTerm = "  " + new string('x', 150) + "  ";

        var result = await _model.OnGetSuggestAsync(longTerm, CancellationToken.None);

        Body(result).Term.Should().Be(new string('x', 100));
        _searchService.Verify(s => s.SearchAsync(
            It.Is<SearchQueryDto>(q => q.SearchTerm == new string('x', 100)),
            It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Suggest_TrimsSurroundingSpace()
    {
        var result = await _model.OnGetSuggestAsync("  pix ", CancellationToken.None);

        Body(result).Term.Should().Be("pix");
    }
}
