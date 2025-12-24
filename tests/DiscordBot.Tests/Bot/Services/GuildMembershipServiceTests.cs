using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Bot.Services;

/// <summary>
/// Unit tests for GuildMembershipService.
/// </summary>
public class GuildMembershipServiceTests
{
    private readonly Mock<IDiscordUserInfoService> _mockUserInfoService;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<GuildMembershipService>> _mockLogger;
    private readonly Mock<IOptions<CachingOptions>> _mockCachingOptions;
    private readonly GuildMembershipService _service;

    // Discord permission flags
    private const long AdministratorPermission = 0x8; // 1 << 3
    private const long ManageGuildPermission = 0x20; // 1 << 5

    public GuildMembershipServiceTests()
    {
        _mockUserInfoService = new Mock<IDiscordUserInfoService>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<GuildMembershipService>>();
        _mockCachingOptions = new Mock<IOptions<CachingOptions>>();
        _mockCachingOptions.Setup(x => x.Value).Returns(new CachingOptions());

        _service = new GuildMembershipService(
            _mockUserInfoService.Object,
            _cache,
            _mockLogger.Object,
            _mockCachingOptions.Object);
    }

    [Fact]
    public async Task IsMemberOfGuildAsync_ReturnsTrue_WhenUserIsMember()
    {
        // Arrange
        const string userId = "user-123";
        const ulong guildId = 987654321098765432;

        var guilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto { Id = guildId, Name = "Test Guild", Owner = false, Permissions = 0 },
            new DiscordGuildDto { Id = 111111111111111111, Name = "Other Guild", Owner = false, Permissions = 0 }
        };

        _mockUserInfoService
            .Setup(s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act
        var result = await _service.IsMemberOfGuildAsync(userId, guildId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsMemberOfGuildAsync_ReturnsFalse_WhenUserIsNotMember()
    {
        // Arrange
        const string userId = "user-123";
        const ulong guildId = 987654321098765432;

        var guilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto { Id = 111111111111111111, Name = "Other Guild 1", Owner = false, Permissions = 0 },
            new DiscordGuildDto { Id = 222222222222222222, Name = "Other Guild 2", Owner = false, Permissions = 0 }
        };

        _mockUserInfoService
            .Setup(s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act
        var result = await _service.IsMemberOfGuildAsync(userId, guildId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsMemberOfGuildAsync_ReturnsFalse_WhenNoGuildsAvailable()
    {
        // Arrange
        const string userId = "user-123";
        const ulong guildId = 987654321098765432;

        _mockUserInfoService
            .Setup(s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DiscordGuildDto>());

        // Act
        var result = await _service.IsMemberOfGuildAsync(userId, guildId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsMemberOfGuildAsync_CachesResult()
    {
        // Arrange
        const string userId = "user-123";
        const ulong guildId = 987654321098765432;

        var guilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto { Id = guildId, Name = "Test Guild", Owner = false, Permissions = 0 }
        };

        _mockUserInfoService
            .Setup(s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act - Call twice
        var result1 = await _service.IsMemberOfGuildAsync(userId, guildId);
        var result2 = await _service.IsMemberOfGuildAsync(userId, guildId);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();

        // User info service should only be called once (second call uses cache)
        _mockUserInfoService.Verify(
            s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()),
            Times.Once,
            "second call should use cached result");
    }

    [Fact]
    public async Task IsGuildAdminAsync_ReturnsTrue_WhenUserIsOwner()
    {
        // Arrange
        const string userId = "user-123";
        const ulong guildId = 987654321098765432;

        var guilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto
            {
                Id = guildId,
                Name = "Test Guild",
                Owner = true, // User is owner
                Permissions = 0
            }
        };

        _mockUserInfoService
            .Setup(s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act
        var result = await _service.IsGuildAdminAsync(userId, guildId);

        // Assert
        result.Should().BeTrue("guild owner should have admin permissions");
    }

    [Fact]
    public async Task IsGuildAdminAsync_ReturnsTrue_WhenUserHasAdministratorPermission()
    {
        // Arrange
        const string userId = "user-123";
        const ulong guildId = 987654321098765432;

        var guilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto
            {
                Id = guildId,
                Name = "Test Guild",
                Owner = false,
                Permissions = AdministratorPermission // Has Administrator permission (0x8)
            }
        };

        _mockUserInfoService
            .Setup(s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act
        var result = await _service.IsGuildAdminAsync(userId, guildId);

        // Assert
        result.Should().BeTrue("user with Administrator permission should be admin");
    }

    [Fact]
    public async Task IsGuildAdminAsync_ReturnsFalse_WhenUserHasNoAdminPermissions()
    {
        // Arrange
        const string userId = "user-123";
        const ulong guildId = 987654321098765432;

        var guilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto
            {
                Id = guildId,
                Name = "Test Guild",
                Owner = false,
                Permissions = 0x400 // Send Messages permission (not admin)
            }
        };

        _mockUserInfoService
            .Setup(s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act
        var result = await _service.IsGuildAdminAsync(userId, guildId);

        // Assert
        result.Should().BeFalse("user without admin permissions should not be admin");
    }

    [Fact]
    public async Task IsGuildAdminAsync_ReturnsFalse_WhenUserIsNotMember()
    {
        // Arrange
        const string userId = "user-123";
        const ulong guildId = 987654321098765432;

        var guilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto
            {
                Id = 111111111111111111, // Different guild
                Name = "Other Guild",
                Owner = true,
                Permissions = AdministratorPermission
            }
        };

        _mockUserInfoService
            .Setup(s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act
        var result = await _service.IsGuildAdminAsync(userId, guildId);

        // Assert
        result.Should().BeFalse("user not in guild should not be admin");
    }

    [Fact]
    public async Task IsGuildAdminAsync_CachesResult()
    {
        // Arrange
        const string userId = "user-123";
        const ulong guildId = 987654321098765432;

        var guilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto
            {
                Id = guildId,
                Name = "Test Guild",
                Owner = true,
                Permissions = 0
            }
        };

        _mockUserInfoService
            .Setup(s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act - Call twice
        var result1 = await _service.IsGuildAdminAsync(userId, guildId);
        var result2 = await _service.IsGuildAdminAsync(userId, guildId);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();

        // User info service should only be called once (second call uses cache)
        _mockUserInfoService.Verify(
            s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()),
            Times.Once,
            "second call should use cached result");
    }

    [Fact]
    public async Task IsGuildAdminAsync_ReturnsTrue_WhenUserHasMultipleAdminPermissions()
    {
        // Arrange
        const string userId = "user-123";
        const ulong guildId = 987654321098765432;

        var guilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto
            {
                Id = guildId,
                Name = "Test Guild",
                Owner = false,
                Permissions = AdministratorPermission | ManageGuildPermission | 0x400 // Multiple permissions
            }
        };

        _mockUserInfoService
            .Setup(s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act
        var result = await _service.IsGuildAdminAsync(userId, guildId);

        // Assert
        result.Should().BeTrue("user with Administrator permission should be admin regardless of other permissions");
    }

    [Fact]
    public async Task GetAdministeredGuildsAsync_ReturnsOnlyGuildsWithAdminPerms()
    {
        // Arrange
        const string userId = "user-123";

        var guilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto
            {
                Id = 111111111111111111,
                Name = "Admin Guild 1",
                Owner = true, // Owner = admin
                Permissions = 0
            },
            new DiscordGuildDto
            {
                Id = 222222222222222222,
                Name = "Admin Guild 2",
                Owner = false,
                Permissions = AdministratorPermission // Has Administrator permission
            },
            new DiscordGuildDto
            {
                Id = 333333333333333333,
                Name = "Regular Guild",
                Owner = false,
                Permissions = 0x400 // Only Send Messages, not admin
            },
            new DiscordGuildDto
            {
                Id = 444444444444444444,
                Name = "Another Regular Guild",
                Owner = false,
                Permissions = 0x800 // Manage Messages, not admin
            }
        };

        _mockUserInfoService
            .Setup(s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act
        var result = await _service.GetAdministeredGuildsAsync(userId);

        // Assert
        result.Should().HaveCount(2, "should only return guilds where user has admin permissions");
        result.Should().Contain(g => g.Id == 111111111111111111, "should include owned guild");
        result.Should().Contain(g => g.Id == 222222222222222222, "should include guild with Administrator permission");
        result.Should().NotContain(g => g.Id == 333333333333333333, "should exclude regular guild");
        result.Should().NotContain(g => g.Id == 444444444444444444, "should exclude another regular guild");
    }

    [Fact]
    public async Task GetAdministeredGuildsAsync_ReturnsEmptyList_WhenNoAdminGuilds()
    {
        // Arrange
        const string userId = "user-123";

        var guilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto
            {
                Id = 111111111111111111,
                Name = "Regular Guild 1",
                Owner = false,
                Permissions = 0x400 // Send Messages, not admin
            },
            new DiscordGuildDto
            {
                Id = 222222222222222222,
                Name = "Regular Guild 2",
                Owner = false,
                Permissions = 0x800 // Manage Messages, not admin
            }
        };

        _mockUserInfoService
            .Setup(s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act
        var result = await _service.GetAdministeredGuildsAsync(userId);

        // Assert
        result.Should().BeEmpty("should return empty list when user has no admin guilds");
    }

    [Fact]
    public async Task GetAdministeredGuildsAsync_ReturnsEmptyList_WhenNoGuildsAvailable()
    {
        // Arrange
        const string userId = "user-123";

        _mockUserInfoService
            .Setup(s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DiscordGuildDto>());

        // Act
        var result = await _service.GetAdministeredGuildsAsync(userId);

        // Assert
        result.Should().BeEmpty("should return empty list when user is not in any guilds");
    }

    [Fact]
    public async Task GetAdministeredGuildsAsync_ReturnsAllGuilds_WhenUserIsOwnerOfAll()
    {
        // Arrange
        const string userId = "user-123";

        var guilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto
            {
                Id = 111111111111111111,
                Name = "Owned Guild 1",
                Owner = true,
                Permissions = 0
            },
            new DiscordGuildDto
            {
                Id = 222222222222222222,
                Name = "Owned Guild 2",
                Owner = true,
                Permissions = 0
            },
            new DiscordGuildDto
            {
                Id = 333333333333333333,
                Name = "Owned Guild 3",
                Owner = true,
                Permissions = 0
            }
        };

        _mockUserInfoService
            .Setup(s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act
        var result = await _service.GetAdministeredGuildsAsync(userId);

        // Assert
        result.Should().HaveCount(3, "should return all guilds when user is owner of all");
        result.Should().AllSatisfy(g => g.Owner.Should().BeTrue());
    }

    [Fact]
    public async Task IsMemberOfGuildAsync_HandlesExceptionFromUserInfoService()
    {
        // Arrange
        const string userId = "user-123";
        const ulong guildId = 987654321098765432;

        _mockUserInfoService
            .Setup(s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("User info service error"));

        // Act
        Func<Task> act = async () => await _service.IsMemberOfGuildAsync(userId, guildId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("User info service error");
    }

    [Fact]
    public async Task IsGuildAdminAsync_HandlesExceptionFromUserInfoService()
    {
        // Arrange
        const string userId = "user-123";
        const ulong guildId = 987654321098765432;

        _mockUserInfoService
            .Setup(s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("User info service error"));

        // Act
        Func<Task> act = async () => await _service.IsGuildAdminAsync(userId, guildId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("User info service error");
    }

    [Fact]
    public async Task GetAdministeredGuildsAsync_HandlesExceptionFromUserInfoService()
    {
        // Arrange
        const string userId = "user-123";

        _mockUserInfoService
            .Setup(s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("User info service error"));

        // Act
        Func<Task> act = async () => await _service.GetAdministeredGuildsAsync(userId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("User info service error");
    }

    [Fact]
    public async Task IsMemberOfGuildAsync_ReturnsCachedFalseResult()
    {
        // Arrange
        const string userId = "user-123";
        const ulong guildId = 987654321098765432;

        var guilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto { Id = 111111111111111111, Name = "Other Guild", Owner = false, Permissions = 0 }
        };

        _mockUserInfoService
            .Setup(s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act - Call twice
        var result1 = await _service.IsMemberOfGuildAsync(userId, guildId);
        var result2 = await _service.IsMemberOfGuildAsync(userId, guildId);

        // Assert
        result1.Should().BeFalse();
        result2.Should().BeFalse();

        // User info service should only be called once (second call uses cache)
        _mockUserInfoService.Verify(
            s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()),
            Times.Once,
            "second call should use cached result even for false");
    }

    [Fact]
    public async Task IsGuildAdminAsync_ReturnsCachedFalseResult()
    {
        // Arrange
        const string userId = "user-123";
        const ulong guildId = 987654321098765432;

        var guilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto
            {
                Id = guildId,
                Name = "Test Guild",
                Owner = false,
                Permissions = 0 // No admin permissions
            }
        };

        _mockUserInfoService
            .Setup(s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act - Call twice
        var result1 = await _service.IsGuildAdminAsync(userId, guildId);
        var result2 = await _service.IsGuildAdminAsync(userId, guildId);

        // Assert
        result1.Should().BeFalse();
        result2.Should().BeFalse();

        // User info service should only be called once (second call uses cache)
        _mockUserInfoService.Verify(
            s => s.GetUserGuildsAsync(userId, false, It.IsAny<CancellationToken>()),
            Times.Once,
            "second call should use cached result even for false");
    }
}
