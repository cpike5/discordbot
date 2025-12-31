using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Globalization;
using System.Text;

namespace DiscordBot.Tests.Bot.Services;

/// <summary>
/// Unit tests for GuildMemberService.
/// Tests cover member retrieval, caching, filtering, pagination, and CSV export functionality.
/// </summary>
public class GuildMemberServiceTests : IDisposable
{
    private readonly Mock<IGuildMemberRepository> _mockRepository;
    private readonly Mock<DiscordSocketClient> _mockClient;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<GuildMemberService>> _mockLogger;
    private readonly Mock<IOptions<CachingOptions>> _mockCachingOptions;
    private readonly GuildMemberService _service;
    private readonly CachingOptions _cachingOptions;

    public GuildMemberServiceTests()
    {
        _mockRepository = new Mock<IGuildMemberRepository>();
        _mockClient = new Mock<DiscordSocketClient>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<GuildMemberService>>();
        _cachingOptions = new CachingOptions
        {
            GuildMemberListDurationMinutes = 5,
            GuildMemberDetailDurationMinutes = 1
        };
        _mockCachingOptions = new Mock<IOptions<CachingOptions>>();
        _mockCachingOptions.Setup(x => x.Value).Returns(_cachingOptions);

        _service = new GuildMemberService(
            _mockRepository.Object,
            _mockClient.Object,
            _cache,
            _mockLogger.Object,
            _mockCachingOptions.Object);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    #region GetMembersAsync Tests

    [Fact]
    public async Task GetMembersAsync_WithValidQuery_ReturnsMembers()
    {
        // Arrange
        const ulong guildId = 123456789;
        var query = new GuildMemberQueryDto { Page = 1, PageSize = 10 };
        var members = CreateTestMembers(guildId, 5);
        var totalCount = 5;

        _mockRepository.Setup(r => r.GetMembersAsync(
                guildId,
                It.IsAny<string?>(),
                It.IsAny<List<ulong>?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool?>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<ulong>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((members, totalCount));

        // Act
        var result = await _service.GetMembersAsync(guildId, query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(5);
        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);

        _mockRepository.Verify(r => r.GetMembersAsync(
            guildId,
            query.SearchTerm,
            query.RoleIds,
            query.JoinedAtStart,
            query.JoinedAtEnd,
            query.LastActiveAtStart,
            query.LastActiveAtEnd,
            query.IsActive,
            query.SortBy,
            query.SortDescending,
            query.Page,
            query.PageSize,
            query.UserIds,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMembersAsync_CachesResult_OnFirstCall()
    {
        // Arrange
        const ulong guildId = 123456789;
        var query = new GuildMemberQueryDto { Page = 1, PageSize = 10 };
        var members = CreateTestMembers(guildId, 3);
        var totalCount = 3;

        _mockRepository.Setup(r => r.GetMembersAsync(
                It.IsAny<ulong>(),
                It.IsAny<string>(),
                It.IsAny<List<ulong>>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool?>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<ulong>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((members, totalCount));

        // Act
        var result = await _service.GetMembersAsync(guildId, query);

        // Assert
        result.Items.Should().HaveCount(3);
        _mockRepository.Verify(r => r.GetMembersAsync(
            It.IsAny<ulong>(),
            It.IsAny<string>(),
            It.IsAny<List<ulong>>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<bool?>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<List<ulong>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMembersAsync_ReturnsCachedResult_OnSecondCall()
    {
        // Arrange
        const ulong guildId = 123456789;
        var query = new GuildMemberQueryDto { Page = 1, PageSize = 10 };
        var members = CreateTestMembers(guildId, 3);
        var totalCount = 3;

        _mockRepository.Setup(r => r.GetMembersAsync(
                It.IsAny<ulong>(),
                It.IsAny<string>(),
                It.IsAny<List<ulong>>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool?>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<ulong>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((members, totalCount));

        // Act
        var result1 = await _service.GetMembersAsync(guildId, query);
        var result2 = await _service.GetMembersAsync(guildId, query);

        // Assert
        result1.Items.Should().HaveCount(3);
        result2.Items.Should().HaveCount(3);
        result1.Should().BeSameAs(result2, "second call should return cached result");

        _mockRepository.Verify(r => r.GetMembersAsync(
            It.IsAny<ulong>(),
            It.IsAny<string>(),
            It.IsAny<List<ulong>>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<bool?>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<List<ulong>?>(),
            It.IsAny<CancellationToken>()), Times.Once, "repository should only be called once");
    }

    [Fact]
    public async Task GetMembersAsync_WithEmptyResult_ReturnsEmptyList()
    {
        // Arrange
        const ulong guildId = 123456789;
        var query = new GuildMemberQueryDto { Page = 1, PageSize = 10 };
        var members = new List<GuildMember>();
        var totalCount = 0;

        _mockRepository.Setup(r => r.GetMembersAsync(
                It.IsAny<ulong>(),
                It.IsAny<string>(),
                It.IsAny<List<ulong>>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool?>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<ulong>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((members, totalCount));

        // Act
        var result = await _service.GetMembersAsync(guildId, query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetMembersAsync_WithInvalidPage_ThrowsArgumentException()
    {
        // Arrange
        const ulong guildId = 123456789;
        var query = new GuildMemberQueryDto { Page = 0, PageSize = 10 };

        // Act
        Func<Task> act = async () => await _service.GetMembersAsync(guildId, query);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Page must be greater than 0*");
    }

    [Fact]
    public async Task GetMembersAsync_WithInvalidPageSize_ThrowsArgumentException()
    {
        // Arrange
        const ulong guildId = 123456789;
        var query1 = new GuildMemberQueryDto { Page = 1, PageSize = 0 };
        var query2 = new GuildMemberQueryDto { Page = 1, PageSize = 101 };

        // Act
        Func<Task> act1 = async () => await _service.GetMembersAsync(guildId, query1);
        Func<Task> act2 = async () => await _service.GetMembersAsync(guildId, query2);

        // Assert
        await act1.Should().ThrowAsync<ArgumentException>()
            .WithMessage("PageSize must be between 1 and 100*");
        await act2.Should().ThrowAsync<ArgumentException>()
            .WithMessage("PageSize must be between 1 and 100*");
    }

    [Fact]
    public async Task GetMembersAsync_MapsDtoCorrectly()
    {
        // Arrange
        const ulong guildId = 123456789;
        var query = new GuildMemberQueryDto { Page = 1, PageSize = 10 };
        var members = new List<GuildMember>
        {
            new GuildMember
            {
                GuildId = guildId,
                UserId = 111111111,
                JoinedAt = DateTime.UtcNow.AddDays(-30),
                Nickname = "TestNick",
                CachedRolesJson = "[123, 456]",
                LastActiveAt = DateTime.UtcNow.AddHours(-1),
                IsActive = true,
                LastCachedAt = DateTime.UtcNow,
                User = new User
                {
                    Id = 111111111,
                    Username = "testuser",
                    Discriminator = "1234",
                    GlobalDisplayName = "TestDisplay",
                    AvatarHash = "avatar123",
                    AccountCreatedAt = DateTime.UtcNow.AddYears(-2),
                    FirstSeenAt = DateTime.UtcNow.AddDays(-30),
                    LastSeenAt = DateTime.UtcNow
                }
            }
        };

        _mockRepository.Setup(r => r.GetMembersAsync(
                It.IsAny<ulong>(),
                It.IsAny<string>(),
                It.IsAny<List<ulong>>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool?>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<ulong>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((members, 1));

        // Act
        var result = await _service.GetMembersAsync(guildId, query);

        // Assert
        result.Items.Should().HaveCount(1);
        var dto = result.Items[0];
        dto.UserId.Should().Be(111111111);
        dto.Username.Should().Be("testuser");
        dto.Discriminator.Should().Be("1234");
        dto.GlobalDisplayName.Should().Be("TestDisplay");
        dto.Nickname.Should().Be("TestNick");
        dto.DisplayName.Should().Be("TestNick", "nickname should take precedence");
        dto.AvatarHash.Should().Be("avatar123");
        dto.IsActive.Should().BeTrue();
        dto.RoleIds.Should().HaveCount(2);
        dto.RoleIds.Should().Contain(123);
        dto.RoleIds.Should().Contain(456);
    }

    [Fact]
    public async Task GetMembersAsync_WithoutDiscordGuild_DoesNotEnrichRoles()
    {
        // Arrange
        const ulong guildId = 123456789;
        const ulong roleId1 = 111;
        const ulong roleId2 = 222;
        var query = new GuildMemberQueryDto { Page = 1, PageSize = 10 };

        var members = new List<GuildMember>
        {
            new GuildMember
            {
                GuildId = guildId,
                UserId = 987654321,
                JoinedAt = DateTime.UtcNow,
                CachedRolesJson = $"[{roleId1}, {roleId2}]",
                IsActive = true,
                LastCachedAt = DateTime.UtcNow,
                User = CreateTestUser(987654321)
            }
        };

        // Discord guild not available (returns null)
        _mockClient.Setup(c => c.GetGuild(guildId)).Returns((SocketGuild?)null);

        _mockRepository.Setup(r => r.GetMembersAsync(
                It.IsAny<ulong>(),
                It.IsAny<string?>(),
                It.IsAny<List<ulong>?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool?>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<ulong>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((members, 1));

        // Act
        var result = await _service.GetMembersAsync(guildId, query);

        // Assert
        result.Items.Should().HaveCount(1);
        var dto = result.Items[0];
        dto.RoleIds.Should().HaveCount(2);
        dto.RoleIds.Should().Contain(roleId1);
        dto.RoleIds.Should().Contain(roleId2);
        dto.Roles.Should().BeEmpty("roles should not be enriched when guild is not available");
    }

    #endregion

    #region GetMemberAsync Tests

    [Fact]
    public async Task GetMemberAsync_WithExistingMember_ReturnsMember()
    {
        // Arrange
        const ulong guildId = 123456789;
        const ulong userId = 987654321;
        var member = CreateTestMember(guildId, userId);

        _mockRepository.Setup(r => r.GetMemberAsync(guildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        // Act
        var result = await _service.GetMemberAsync(guildId, userId);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.Username.Should().Be("testuser");

        _mockRepository.Verify(r => r.GetMemberAsync(guildId, userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMemberAsync_WithNonExistentMember_ReturnsNull()
    {
        // Arrange
        const ulong guildId = 123456789;
        const ulong userId = 987654321;

        _mockRepository.Setup(r => r.GetMemberAsync(guildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildMember?)null);

        // Act
        var result = await _service.GetMemberAsync(guildId, userId);

        // Assert
        result.Should().BeNull();
        _mockRepository.Verify(r => r.GetMemberAsync(guildId, userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMemberAsync_CachesResult_OnFirstCall()
    {
        // Arrange
        const ulong guildId = 123456789;
        const ulong userId = 987654321;
        var member = CreateTestMember(guildId, userId);

        _mockRepository.Setup(r => r.GetMemberAsync(guildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        // Act
        var result = await _service.GetMemberAsync(guildId, userId);

        // Assert
        result.Should().NotBeNull();
        _mockRepository.Verify(r => r.GetMemberAsync(guildId, userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMemberAsync_ReturnsCachedResult_OnSecondCall()
    {
        // Arrange
        const ulong guildId = 123456789;
        const ulong userId = 987654321;
        var member = CreateTestMember(guildId, userId);

        _mockRepository.Setup(r => r.GetMemberAsync(guildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        // Act
        var result1 = await _service.GetMemberAsync(guildId, userId);
        var result2 = await _service.GetMemberAsync(guildId, userId);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Should().BeSameAs(result2, "second call should return cached result");

        _mockRepository.Verify(r => r.GetMemberAsync(guildId, userId, It.IsAny<CancellationToken>()),
            Times.Once, "repository should only be called once");
    }

    #endregion

    #region GetMemberCountAsync Tests

    [Fact]
    public async Task GetMemberCountAsync_WithNoFilters_CallsRepositorySimpleCount()
    {
        // Arrange
        const ulong guildId = 123456789;
        var query = new GuildMemberQueryDto { IsActive = true };

        _mockRepository.Setup(r => r.GetMemberCountAsync(guildId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        // Act
        var result = await _service.GetMemberCountAsync(guildId, query);

        // Assert
        result.Should().Be(42);
        _mockRepository.Verify(r => r.GetMemberCountAsync(guildId, true, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.GetMembersAsync(
            It.IsAny<ulong>(),
            It.IsAny<string>(),
            It.IsAny<List<ulong>>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<bool?>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<List<ulong>?>(),
            It.IsAny<CancellationToken>()), Times.Never, "should use simple count method");
    }

    [Fact]
    public async Task GetMemberCountAsync_WithFilters_CallsRepositoryFullQuery()
    {
        // Arrange
        const ulong guildId = 123456789;
        var query = new GuildMemberQueryDto
        {
            SearchTerm = "test",
            IsActive = true
        };

        _mockRepository.Setup(r => r.GetMembersAsync(
                guildId,
                query.SearchTerm,
                It.IsAny<List<ulong>>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                query.IsActive,
                It.IsAny<string>(),
                It.IsAny<bool>(),
                1,
                1,
                It.IsAny<List<ulong>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<GuildMember>(), 15));

        // Act
        var result = await _service.GetMemberCountAsync(guildId, query);

        // Assert
        result.Should().Be(15);
        _mockRepository.Verify(r => r.GetMemberCountAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never, "should not use simple count method when filters are present");
    }

    [Fact]
    public async Task GetMemberCountAsync_WithRoleFilter_CallsRepositoryFullQuery()
    {
        // Arrange
        const ulong guildId = 123456789;
        var query = new GuildMemberQueryDto
        {
            RoleIds = new List<ulong> { 111, 222 },
            IsActive = true
        };

        _mockRepository.Setup(r => r.GetMembersAsync(
                It.IsAny<ulong>(),
                It.IsAny<string>(),
                query.RoleIds,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool?>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<ulong>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<GuildMember>(), 8));

        // Act
        var result = await _service.GetMemberCountAsync(guildId, query);

        // Assert
        result.Should().Be(8);
    }

    [Fact]
    public async Task GetMemberCountAsync_WithDateRangeFilter_CallsRepositoryFullQuery()
    {
        // Arrange
        const ulong guildId = 123456789;
        var query = new GuildMemberQueryDto
        {
            JoinedAtStart = DateTime.UtcNow.AddDays(-30),
            IsActive = true
        };

        _mockRepository.Setup(r => r.GetMembersAsync(
                It.IsAny<ulong>(),
                It.IsAny<string>(),
                It.IsAny<List<ulong>>(),
                query.JoinedAtStart,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool?>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<ulong>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<GuildMember>(), 10));

        // Act
        var result = await _service.GetMemberCountAsync(guildId, query);

        // Assert
        result.Should().Be(10);
    }

    #endregion

    #region ExportMembersToCsvAsync Tests

    [Fact]
    public async Task ExportMembersToCsvAsync_WithValidMembers_ReturnsCsvBytes()
    {
        // Arrange
        const ulong guildId = 123456789;
        var query = new GuildMemberQueryDto();
        var members = CreateTestMembers(guildId, 3);

        _mockRepository.Setup(r => r.GetMembersAsync(
                It.IsAny<ulong>(),
                It.IsAny<string>(),
                It.IsAny<List<ulong>>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool?>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<ulong>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((members, 3));

        // Act
        var result = await _service.ExportMembersToCsvAsync(guildId, query);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();

        var csv = Encoding.UTF8.GetString(result);
        csv.Should().Contain("UserId,Username,Discriminator");
        csv.Should().Contain("testuser");
        csv.Should().Contain("1234");
    }

    [Fact]
    public async Task ExportMembersToCsvAsync_WithNoMembers_ThrowsInvalidOperationException()
    {
        // Arrange
        const ulong guildId = 123456789;
        var query = new GuildMemberQueryDto();

        _mockRepository.Setup(r => r.GetMembersAsync(
                It.IsAny<ulong>(),
                It.IsAny<string>(),
                It.IsAny<List<ulong>>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool?>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<ulong>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<GuildMember>(), 0));

        // Act
        Func<Task> act = async () => await _service.ExportMembersToCsvAsync(guildId, query);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No members found matching the specified criteria");
    }

    [Fact]
    public async Task ExportMembersToCsvAsync_RespectsMaxRowsLimit()
    {
        // Arrange
        const ulong guildId = 123456789;
        var query = new GuildMemberQueryDto();
        const int maxRows = 100;

        _mockRepository.Setup(r => r.GetMembersAsync(
                guildId,
                It.IsAny<string>(),
                It.IsAny<List<ulong>>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool?>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                1,
                maxRows,
                It.IsAny<List<ulong>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateTestMembers(guildId, 100), 100));

        // Act
        await _service.ExportMembersToCsvAsync(guildId, query, maxRows);

        // Assert
        _mockRepository.Verify(r => r.GetMembersAsync(
            guildId,
            It.IsAny<string>(),
            It.IsAny<List<ulong>>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<bool?>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            1,
            maxRows,
            It.IsAny<List<ulong>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExportMembersToCsvAsync_WithInvalidMaxRows_ThrowsArgumentException()
    {
        // Arrange
        const ulong guildId = 123456789;
        var query = new GuildMemberQueryDto();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExportMembersToCsvAsync(guildId, query, maxRows: 0));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExportMembersToCsvAsync(guildId, query, maxRows: -1));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExportMembersToCsvAsync(guildId, query, maxRows: 100001));
    }

    [Fact]
    public async Task ExportMembersToCsvAsync_EscapesSpecialCharacters()
    {
        // Arrange
        const ulong guildId = 123456789;
        var query = new GuildMemberQueryDto();
        var members = new List<GuildMember>
        {
            new GuildMember
            {
                GuildId = guildId,
                UserId = 111,
                JoinedAt = DateTime.UtcNow,
                Nickname = "Test, Nick",
                CachedRolesJson = "[]",
                IsActive = true,
                LastCachedAt = DateTime.UtcNow,
                User = new User
                {
                    Id = 111,
                    Username = "test\"user",
                    Discriminator = "0",
                    GlobalDisplayName = "Test\nDisplay",
                    FirstSeenAt = DateTime.UtcNow,
                    LastSeenAt = DateTime.UtcNow
                }
            }
        };

        _mockRepository.Setup(r => r.GetMembersAsync(
                It.IsAny<ulong>(),
                It.IsAny<string>(),
                It.IsAny<List<ulong>>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool?>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<ulong>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((members, 1));

        // Act
        var result = await _service.ExportMembersToCsvAsync(guildId, query);

        // Assert
        var csv = Encoding.UTF8.GetString(result);
        csv.Should().Contain("\"Test, Nick\"", "comma should be escaped with quotes");
        csv.Should().Contain("\"test\"\"user\"", "quotes should be escaped as double quotes");
        csv.Should().Contain("\"Test\nDisplay\"", "newlines should be wrapped in quotes");
    }

    [Fact]
    public async Task ExportMembersToCsvAsync_FormatsDatetimesCorrectly()
    {
        // Arrange
        const ulong guildId = 123456789;
        var query = new GuildMemberQueryDto();
        var joinedAt = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);
        var lastActiveAt = new DateTime(2024, 6, 20, 14, 22, 10, DateTimeKind.Utc);

        var members = new List<GuildMember>
        {
            new GuildMember
            {
                GuildId = guildId,
                UserId = 111,
                JoinedAt = joinedAt,
                LastActiveAt = lastActiveAt,
                CachedRolesJson = "[]",
                IsActive = true,
                LastCachedAt = DateTime.UtcNow,
                User = CreateTestUser(111)
            }
        };

        _mockRepository.Setup(r => r.GetMembersAsync(
                It.IsAny<ulong>(),
                It.IsAny<string>(),
                It.IsAny<List<ulong>>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool?>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<ulong>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((members, 1));

        // Act
        var result = await _service.ExportMembersToCsvAsync(guildId, query);

        // Assert
        var csv = Encoding.UTF8.GetString(result);
        csv.Should().Contain("2024-01-15 10:30:45", "joined date should be formatted correctly");
        csv.Should().Contain("2024-06-20 14:22:10", "last active date should be formatted correctly");
    }

    [Fact]
    public async Task ExportMembersToCsvAsync_IncludesAllRequiredColumns()
    {
        // Arrange
        const ulong guildId = 123456789;
        var query = new GuildMemberQueryDto();
        var members = CreateTestMembers(guildId, 1);

        _mockRepository.Setup(r => r.GetMembersAsync(
                It.IsAny<ulong>(),
                It.IsAny<string>(),
                It.IsAny<List<ulong>>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool?>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<ulong>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((members, 1));

        // Act
        var result = await _service.ExportMembersToCsvAsync(guildId, query);

        // Assert
        var csv = Encoding.UTF8.GetString(result);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCountGreaterThanOrEqualTo(2, "should have header and at least one data row");

        var header = lines[0];
        header.Should().Contain("UserId");
        header.Should().Contain("Username");
        header.Should().Contain("Discriminator");
        header.Should().Contain("GlobalDisplayName");
        header.Should().Contain("Nickname");
        header.Should().Contain("DisplayName");
        header.Should().Contain("JoinedAt");
        header.Should().Contain("LastActiveAt");
        header.Should().Contain("AccountCreatedAt");
        header.Should().Contain("RoleIds");
        header.Should().Contain("RoleNames");
        header.Should().Contain("IsActive");
    }

    [Fact]
    public async Task ExportMembersToCsvAsync_HandlesNullableFieldsCorrectly()
    {
        // Arrange
        const ulong guildId = 123456789;
        var query = new GuildMemberQueryDto();
        var members = new List<GuildMember>
        {
            new GuildMember
            {
                GuildId = guildId,
                UserId = 111,
                JoinedAt = DateTime.UtcNow,
                Nickname = null,
                LastActiveAt = null,
                CachedRolesJson = null,
                IsActive = true,
                LastCachedAt = DateTime.UtcNow,
                User = new User
                {
                    Id = 111,
                    Username = "testuser",
                    Discriminator = "0",
                    GlobalDisplayName = null,
                    AccountCreatedAt = null,
                    FirstSeenAt = DateTime.UtcNow,
                    LastSeenAt = DateTime.UtcNow
                }
            }
        };

        _mockRepository.Setup(r => r.GetMembersAsync(
                It.IsAny<ulong>(),
                It.IsAny<string>(),
                It.IsAny<List<ulong>>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool?>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<ulong>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((members, 1));

        // Act
        var result = await _service.ExportMembersToCsvAsync(guildId, query);

        // Assert
        var csv = Encoding.UTF8.GetString(result);
        csv.Should().NotBeNull();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2, "should have header and one data row");
    }

    #endregion

    #region Helper Methods

    private static User CreateTestUser(ulong userId, string username = "testuser")
    {
        return new User
        {
            Id = userId,
            Username = username,
            Discriminator = "1234",
            GlobalDisplayName = "TestDisplay",
            AvatarHash = "avatar123",
            FirstSeenAt = DateTime.UtcNow.AddDays(-30),
            LastSeenAt = DateTime.UtcNow,
            AccountCreatedAt = DateTime.UtcNow.AddYears(-2)
        };
    }

    private static GuildMember CreateTestMember(ulong guildId, ulong userId)
    {
        return new GuildMember
        {
            GuildId = guildId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow.AddDays(-30),
            Nickname = "TestNickname",
            CachedRolesJson = "[123, 456]",
            LastActiveAt = DateTime.UtcNow.AddHours(-1),
            IsActive = true,
            LastCachedAt = DateTime.UtcNow,
            User = CreateTestUser(userId)
        };
    }

    private static List<GuildMember> CreateTestMembers(ulong guildId, int count)
    {
        var members = new List<GuildMember>();
        for (int i = 1; i <= count; i++)
        {
            members.Add(new GuildMember
            {
                GuildId = guildId,
                UserId = (ulong)i,
                JoinedAt = DateTime.UtcNow.AddDays(-i),
                Nickname = $"Nickname{i}",
                CachedRolesJson = "[123, 456]",
                LastActiveAt = DateTime.UtcNow.AddHours(-i),
                IsActive = true,
                LastCachedAt = DateTime.UtcNow,
                User = CreateTestUser((ulong)i, $"testuser{i}")
            });
        }
        return members;
    }

    #endregion
}
