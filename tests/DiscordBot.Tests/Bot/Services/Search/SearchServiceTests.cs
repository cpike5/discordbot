using System.Security.Claims;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Bot.Services.Search;

/// <summary>
/// Unit tests for the <see cref="SearchService"/> orchestrator.
/// Verifies routing, authorization guards, caching, and empty-result behaviour.
/// </summary>
public class SearchServiceTests
{
    private readonly Mock<ISearchProvider> _guildsProvider;
    private readonly Mock<IAuthorizationService> _authorizationService;
    private readonly IMemoryCache _cache;
    private readonly SearchService _service;
    private readonly ClaimsPrincipal _adminUser;
    private readonly ClaimsPrincipal _regularUser;

    public SearchServiceTests()
    {
        _guildsProvider = new Mock<ISearchProvider>();
        _guildsProvider.Setup(p => p.Category).Returns(SearchCategory.Guilds);
        _guildsProvider.Setup(p => p.RequiresAdmin).Returns(false);

        _authorizationService = new Mock<IAuthorizationService>();

        _cache = new MemoryCache(new MemoryCacheOptions());

        var cachingOptions = Options.Create(new CachingOptions { SearchResultsCacheDurationSeconds = 60 });
        var logger = new Mock<ILogger<SearchService>>();

        _service = new SearchService(
            new[] { _guildsProvider.Object },
            _authorizationService.Object,
            _cache,
            cachingOptions,
            logger.Object);

        _adminUser = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "admin@example.com") }, "test"));

        _regularUser = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "user@example.com") }, "test"));

        // Default: admin check passes for admin user
        _authorizationService
            .Setup(a => a.AuthorizeAsync(_adminUser, null, "RequireAdmin"))
            .ReturnsAsync(AuthorizationResult.Success());

        _authorizationService
            .Setup(a => a.AuthorizeAsync(_regularUser, null, "RequireAdmin"))
            .ReturnsAsync(AuthorizationResult.Failed());
    }

    [Fact]
    public async Task SearchAsync_EmptySearchTerm_ReturnsEmptyResult()
    {
        var query = new SearchQueryDto { SearchTerm = "" };
        var result = await _service.SearchAsync(query, _regularUser);

        result.HasResults.Should().BeFalse();
        _guildsProvider.Verify(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_SingleCharSearchTerm_ReturnsEmptyResult()
    {
        var query = new SearchQueryDto { SearchTerm = "a" };
        var result = await _service.SearchAsync(query, _regularUser);

        result.HasResults.Should().BeFalse();
        _guildsProvider.Verify(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_ValidTerm_DelegatesToProviders()
    {
        var expectedResult = new SearchCategoryResult
        {
            Category = SearchCategory.Guilds,
            DisplayName = "Guilds",
            Items = new List<SearchResultItemDto>
            {
                new SearchResultItemDto { Id = "123", Title = "Test Guild" }
            }
        };

        _guildsProvider
            .Setup(p => p.SearchAsync("test guild", 5, _regularUser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var query = new SearchQueryDto { SearchTerm = "test guild", MaxResultsPerCategory = 5 };
        var result = await _service.SearchAsync(query, _regularUser);

        result.Guilds.Items.Should().HaveCount(1);
        result.Guilds.Items[0].Title.Should().Be("Test Guild");
    }

    [Fact]
    public async Task SearchAsync_AdminProviderWithNonAdminUser_SkipsProvider()
    {
        var adminProvider = new Mock<ISearchProvider>();
        adminProvider.Setup(p => p.Category).Returns(SearchCategory.Users);
        adminProvider.Setup(p => p.RequiresAdmin).Returns(true);

        var cachingOptions = Options.Create(new CachingOptions { SearchResultsCacheDurationSeconds = 60 });
        var logger = new Mock<ILogger<SearchService>>();
        var service = new SearchService(
            new[] { adminProvider.Object },
            _authorizationService.Object,
            new MemoryCache(new MemoryCacheOptions()),
            cachingOptions,
            logger.Object);

        var query = new SearchQueryDto { SearchTerm = "admin search" };
        await service.SearchAsync(query, _regularUser);

        adminProvider.Verify(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_AdminProviderWithAdminUser_RunsProvider()
    {
        var adminProvider = new Mock<ISearchProvider>();
        adminProvider.Setup(p => p.Category).Returns(SearchCategory.Users);
        adminProvider.Setup(p => p.RequiresAdmin).Returns(true);
        adminProvider
            .Setup(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), _adminUser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchCategoryResult { Category = SearchCategory.Users, DisplayName = "Users" });

        var cachingOptions = Options.Create(new CachingOptions { SearchResultsCacheDurationSeconds = 60 });
        var logger = new Mock<ILogger<SearchService>>();
        var service = new SearchService(
            new[] { adminProvider.Object },
            _authorizationService.Object,
            new MemoryCache(new MemoryCacheOptions()),
            cachingOptions,
            logger.Object);

        var query = new SearchQueryDto { SearchTerm = "admin search" };
        await service.SearchAsync(query, _adminUser);

        adminProvider.Verify(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), _adminUser, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ProviderThrows_ReturnsEmptyResultForThatCategory()
    {
        _guildsProvider
            .Setup(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var query = new SearchQueryDto { SearchTerm = "failing search" };
        var result = await _service.SearchAsync(query, _regularUser);

        // Should return empty result without throwing
        result.Guilds.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_SameQueryTwice_CachesResult()
    {
        _guildsProvider
            .Setup(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), _regularUser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchCategoryResult { Category = SearchCategory.Guilds, DisplayName = "Guilds" });

        var query = new SearchQueryDto { SearchTerm = "cached query" };
        await _service.SearchAsync(query, _regularUser);
        await _service.SearchAsync(query, _regularUser);

        // Provider should only be called once (second call should be served from cache)
        _guildsProvider.Verify(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_CategoryFilter_OnlyRunsMatchingProvider()
    {
        var commandsProvider = new Mock<ISearchProvider>();
        commandsProvider.Setup(p => p.Category).Returns(SearchCategory.Commands);
        commandsProvider.Setup(p => p.RequiresAdmin).Returns(false);

        var cachingOptions = Options.Create(new CachingOptions { SearchResultsCacheDurationSeconds = 60 });
        var logger = new Mock<ILogger<SearchService>>();
        var service = new SearchService(
            new ISearchProvider[] { _guildsProvider.Object, commandsProvider.Object },
            _authorizationService.Object,
            new MemoryCache(new MemoryCacheOptions()),
            cachingOptions,
            logger.Object);

        commandsProvider
            .Setup(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchCategoryResult { Category = SearchCategory.Commands, DisplayName = "Commands" });

        var query = new SearchQueryDto
        {
            SearchTerm = "ping",
            CategoryFilter = SearchCategory.Commands
        };
        await service.SearchAsync(query, _regularUser);

        commandsProvider.Verify(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Once);
        _guildsProvider.Verify(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchCategoryAsync_EmptyTerm_ReturnsEmptyResult()
    {
        var result = await _service.SearchCategoryAsync(SearchCategory.Guilds, "", 5, _regularUser);

        result.Items.Should().BeEmpty();
        _guildsProvider.Verify(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchCategoryAsync_NoProviderRegistered_ReturnsEmptyResult()
    {
        var result = await _service.SearchCategoryAsync(SearchCategory.AuditLogs, "test", 5, _regularUser);

        result.Items.Should().BeEmpty();
        result.Category.Should().Be(SearchCategory.AuditLogs);
    }

    [Fact]
    public async Task SearchCategoryAsync_AdminCategoryWithNonAdmin_ReturnsEmptyResult()
    {
        var adminProvider = new Mock<ISearchProvider>();
        adminProvider.Setup(p => p.Category).Returns(SearchCategory.Users);
        adminProvider.Setup(p => p.RequiresAdmin).Returns(true);

        var cachingOptions = Options.Create(new CachingOptions { SearchResultsCacheDurationSeconds = 60 });
        var logger = new Mock<ILogger<SearchService>>();
        var service = new SearchService(
            new[] { adminProvider.Object },
            _authorizationService.Object,
            new MemoryCache(new MemoryCacheOptions()),
            cachingOptions,
            logger.Object);

        var result = await service.SearchCategoryAsync(SearchCategory.Users, "john", 5, _regularUser);

        result.Items.Should().BeEmpty();
        adminProvider.Verify(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
