using System.Security.Cryptography;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Entities;
using DiscordBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Services;

/// <summary>
/// Unit tests for DiscordTokenService.
/// </summary>
public class DiscordTokenServiceTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly Mock<IDataProtectionProvider> _mockDataProtectionProvider;
    private readonly Mock<IDataProtector> _mockDataProtector;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<DiscordTokenService>> _mockLogger;
    private readonly DiscordTokenService _service;

    public DiscordTokenServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new BotDbContext(options);

        // Setup data protection mocks to return predictable values
        // IDataProtector uses byte[] methods, but extension methods convert string to byte[]
        _mockDataProtector = new Mock<IDataProtector>();
        _mockDataProtector
            .Setup(p => p.Protect(It.IsAny<byte[]>()))
            .Returns<byte[]>(input =>
            {
                // Simple transformation: prepend "ENCRYPTED:" prefix
                var plainText = System.Text.Encoding.UTF8.GetString(input);
                var encrypted = "ENCRYPTED:" + plainText;
                return System.Text.Encoding.UTF8.GetBytes(encrypted);
            });
        _mockDataProtector
            .Setup(p => p.Unprotect(It.IsAny<byte[]>()))
            .Returns<byte[]>(input =>
            {
                // Reverse transformation: remove "ENCRYPTED:" prefix
                var encrypted = System.Text.Encoding.UTF8.GetString(input);
                if (encrypted.StartsWith("ENCRYPTED:"))
                {
                    var plainText = encrypted.Substring("ENCRYPTED:".Length);
                    return System.Text.Encoding.UTF8.GetBytes(plainText);
                }
                return input;
            });

        _mockDataProtectionProvider = new Mock<IDataProtectionProvider>();
        _mockDataProtectionProvider
            .Setup(p => p.CreateProtector(It.IsAny<string>()))
            .Returns(_mockDataProtector.Object);

        // Setup HTTP client factory mock
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();

        // Setup configuration mock
        _mockConfiguration = new Mock<IConfiguration>();
        _mockConfiguration.Setup(c => c["Discord:OAuth:ClientId"]).Returns("test-client-id");
        _mockConfiguration.Setup(c => c["Discord:OAuth:ClientSecret"]).Returns("test-client-secret");

        // Setup logger mock
        _mockLogger = new Mock<ILogger<DiscordTokenService>>();

        // Create service instance
        _service = new DiscordTokenService(
            _context,
            _mockDataProtectionProvider.Object,
            _mockHttpClientFactory.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task StoreTokensAsync_CreatesNewTokenRecord()
    {
        // Arrange
        const string userId = "user-123";
        const ulong discordUserId = 123456789012345678;
        const string accessToken = "access-token";
        const string refreshToken = "refresh-token";
        var expiresAt = DateTime.UtcNow.AddHours(1);
        const string scopes = "identify email guilds";

        // Act
        await _service.StoreTokensAsync(userId, discordUserId, accessToken, refreshToken, expiresAt, scopes);

        // Assert
        var storedToken = await _context.DiscordOAuthTokens
            .FirstOrDefaultAsync(t => t.ApplicationUserId == userId);

        storedToken.Should().NotBeNull();
        storedToken!.ApplicationUserId.Should().Be(userId);
        storedToken.DiscordUserId.Should().Be(discordUserId);
        storedToken.Scopes.Should().Be(scopes);
        storedToken.AccessTokenExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
        storedToken.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        storedToken.LastRefreshedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));

        // Verify tokens were encrypted (using the mock protector)
        _mockDataProtector.Verify(
            p => p.Protect(It.IsAny<byte[]>()),
            Times.Exactly(2),
            "access and refresh tokens should be encrypted");
    }

    [Fact]
    public async Task StoreTokensAsync_UpdatesExistingTokenRecord()
    {
        // Arrange
        const string userId = "user-123";
        const ulong originalDiscordUserId = 111111111111111111;
        const ulong newDiscordUserId = 222222222222222222;

        // Create initial token
        var initialToken = new DiscordOAuthToken
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            DiscordUserId = originalDiscordUserId,
            EncryptedAccessToken = "old-encrypted-access",
            EncryptedRefreshToken = "old-encrypted-refresh",
            AccessTokenExpiresAt = DateTime.UtcNow.AddHours(1),
            Scopes = "identify",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            LastRefreshedAt = DateTime.UtcNow.AddDays(-1)
        };
        _context.DiscordOAuthTokens.Add(initialToken);
        await _context.SaveChangesAsync();

        var newExpiresAt = DateTime.UtcNow.AddHours(2);

        // Act
        await _service.StoreTokensAsync(userId, newDiscordUserId, "new-access", "new-refresh", newExpiresAt, "identify email guilds");

        // Assert
        var tokens = await _context.DiscordOAuthTokens
            .Where(t => t.ApplicationUserId == userId)
            .ToListAsync();

        tokens.Should().ContainSingle("should update existing record, not create new one");
        var updatedToken = tokens.First();
        updatedToken.Id.Should().Be(initialToken.Id, "should preserve the original ID");
        updatedToken.DiscordUserId.Should().Be(newDiscordUserId);
        updatedToken.Scopes.Should().Be("identify email guilds");
        updatedToken.AccessTokenExpiresAt.Should().BeCloseTo(newExpiresAt, TimeSpan.FromSeconds(1));
        updatedToken.CreatedAt.Should().BeCloseTo(initialToken.CreatedAt, TimeSpan.FromSeconds(1), "CreatedAt should not change");
        updatedToken.LastRefreshedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2), "LastRefreshedAt should be updated");
    }

    [Fact]
    public async Task GetAccessTokenAsync_ReturnsDecryptedToken_WhenValidTokenExists()
    {
        // Arrange
        const string userId = "user-123";
        const string plainAccessToken = "test-access-token";

        // The service uses Protect(string) extension which:
        // 1. Converts string to UTF8 bytes
        // 2. Calls Protect(byte[])
        // 3. Converts result to Base64 string
        // Our mock adds "ENCRYPTED:" prefix, so we need to simulate the full flow
        var encryptedBytes = System.Text.Encoding.UTF8.GetBytes("ENCRYPTED:" + plainAccessToken);
        var encryptedAccessToken = Convert.ToBase64String(encryptedBytes);

        var refreshBytes = System.Text.Encoding.UTF8.GetBytes("ENCRYPTED:test-refresh-token");
        var encryptedRefreshToken = Convert.ToBase64String(refreshBytes);

        var token = new DiscordOAuthToken
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            DiscordUserId = 123456789012345678,
            EncryptedAccessToken = encryptedAccessToken,
            EncryptedRefreshToken = encryptedRefreshToken,
            AccessTokenExpiresAt = DateTime.UtcNow.AddHours(24), // Valid and far from expiring
            Scopes = "identify",
            CreatedAt = DateTime.UtcNow,
            LastRefreshedAt = DateTime.UtcNow
        };
        _context.DiscordOAuthTokens.Add(token);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAccessTokenAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(plainAccessToken, "token should be decrypted correctly");
        _mockDataProtector.Verify(
            p => p.Unprotect(It.IsAny<byte[]>()),
            Times.Once,
            "token should be decrypted");
    }

    [Fact]
    public async Task GetAccessTokenAsync_ReturnsNull_WhenNoTokenExists()
    {
        // Arrange
        const string userId = "user-with-no-token";

        // Act
        var result = await _service.GetAccessTokenAsync(userId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAccessTokenAsync_ReturnsNull_WhenTokenIsExpired()
    {
        // Arrange
        const string userId = "user-123";

        var expiredToken = new DiscordOAuthToken
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            DiscordUserId = 123456789012345678,
            EncryptedAccessToken = "encrypted-access-token",
            EncryptedRefreshToken = "encrypted-refresh-token",
            AccessTokenExpiresAt = DateTime.UtcNow.AddHours(-1), // Expired
            Scopes = "identify",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            LastRefreshedAt = DateTime.UtcNow.AddDays(-1)
        };
        _context.DiscordOAuthTokens.Add(expiredToken);
        await _context.SaveChangesAsync();

        // Note: The service will attempt to refresh expired tokens, but without a proper HTTP client mock
        // that returns a valid Discord token response, the refresh will fail and return null.

        // Act
        var result = await _service.GetAccessTokenAsync(userId);

        // Assert
        result.Should().BeNull("expired token should trigger refresh, which will fail without proper HTTP mock");
    }

    [Fact]
    public async Task GetAccessTokenAsync_ReturnsNull_WhenDecryptionFails()
    {
        // Arrange
        const string userId = "user-123";

        var token = new DiscordOAuthToken
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            DiscordUserId = 123456789012345678,
            EncryptedAccessToken = "encrypted-access-token",
            EncryptedRefreshToken = "encrypted-refresh-token",
            AccessTokenExpiresAt = DateTime.UtcNow.AddHours(1),
            Scopes = "identify",
            CreatedAt = DateTime.UtcNow,
            LastRefreshedAt = DateTime.UtcNow
        };
        _context.DiscordOAuthTokens.Add(token);
        await _context.SaveChangesAsync();

        // Setup data protector to throw on decryption
        _mockDataProtector
            .Setup(p => p.Unprotect(It.IsAny<byte[]>()))
            .Throws<CryptographicException>();

        // Act
        var result = await _service.GetAccessTokenAsync(userId);

        // Assert
        result.Should().BeNull("should return null when decryption fails");
    }

    [Fact]
    public async Task HasValidTokenAsync_ReturnsTrue_ForValidNonExpiredToken()
    {
        // Arrange
        const string userId = "user-123";

        var validToken = new DiscordOAuthToken
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            DiscordUserId = 123456789012345678,
            EncryptedAccessToken = "encrypted-access-token",
            EncryptedRefreshToken = "encrypted-refresh-token",
            AccessTokenExpiresAt = DateTime.UtcNow.AddHours(1), // Valid
            Scopes = "identify",
            CreatedAt = DateTime.UtcNow,
            LastRefreshedAt = DateTime.UtcNow
        };
        _context.DiscordOAuthTokens.Add(validToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.HasValidTokenAsync(userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasValidTokenAsync_ReturnsFalse_WhenNoTokenExists()
    {
        // Arrange
        const string userId = "user-with-no-token";

        // Act
        var result = await _service.HasValidTokenAsync(userId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasValidTokenAsync_ReturnsFalse_WhenTokenIsExpired()
    {
        // Arrange
        const string userId = "user-123";

        var expiredToken = new DiscordOAuthToken
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            DiscordUserId = 123456789012345678,
            EncryptedAccessToken = "encrypted-access-token",
            EncryptedRefreshToken = "encrypted-refresh-token",
            AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(-10), // Expired
            Scopes = "identify",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            LastRefreshedAt = DateTime.UtcNow.AddDays(-1)
        };
        _context.DiscordOAuthTokens.Add(expiredToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.HasValidTokenAsync(userId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTokensAsync_RemovesTokenRecord()
    {
        // Arrange
        const string userId = "user-123";

        var token = new DiscordOAuthToken
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            DiscordUserId = 123456789012345678,
            EncryptedAccessToken = "encrypted-access-token",
            EncryptedRefreshToken = "encrypted-refresh-token",
            AccessTokenExpiresAt = DateTime.UtcNow.AddHours(1),
            Scopes = "identify",
            CreatedAt = DateTime.UtcNow,
            LastRefreshedAt = DateTime.UtcNow
        };
        _context.DiscordOAuthTokens.Add(token);
        await _context.SaveChangesAsync();

        // Verify token exists
        var tokenBeforeDelete = await _context.DiscordOAuthTokens
            .FirstOrDefaultAsync(t => t.ApplicationUserId == userId);
        tokenBeforeDelete.Should().NotBeNull();

        // Act
        await _service.DeleteTokensAsync(userId);

        // Assert
        var tokenAfterDelete = await _context.DiscordOAuthTokens
            .FirstOrDefaultAsync(t => t.ApplicationUserId == userId);
        tokenAfterDelete.Should().BeNull("token should be deleted");
    }

    [Fact]
    public async Task DeleteTokensAsync_HandlesUserWithNoTokenGracefully()
    {
        // Arrange
        const string userId = "user-with-no-token";

        // Act
        Func<Task> act = async () => await _service.DeleteTokensAsync(userId);

        // Assert
        await act.Should().NotThrowAsync("should handle deletion of non-existent token gracefully");
    }

    [Fact]
    public async Task GetExpiringTokensAsync_ReturnsTokensExpiringWithinTimespan()
    {
        // Arrange
        const string user1 = "user-1";
        const string user2 = "user-2";
        const string user3 = "user-3";

        // Token expiring in 5 minutes (within threshold)
        var expiringSoonToken = new DiscordOAuthToken
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = user1,
            DiscordUserId = 111111111111111111,
            EncryptedAccessToken = "encrypted-access-token-1",
            EncryptedRefreshToken = "encrypted-refresh-token-1",
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(5),
            Scopes = "identify",
            CreatedAt = DateTime.UtcNow,
            LastRefreshedAt = DateTime.UtcNow
        };

        // Token expiring in 2 hours (outside threshold of 10 minutes)
        var notExpiringSoonToken = new DiscordOAuthToken
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = user2,
            DiscordUserId = 222222222222222222,
            EncryptedAccessToken = "encrypted-access-token-2",
            EncryptedRefreshToken = "encrypted-refresh-token-2",
            AccessTokenExpiresAt = DateTime.UtcNow.AddHours(2),
            Scopes = "identify",
            CreatedAt = DateTime.UtcNow,
            LastRefreshedAt = DateTime.UtcNow
        };

        // Token expiring in 1 minute (within threshold)
        var almostExpiredToken = new DiscordOAuthToken
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = user3,
            DiscordUserId = 333333333333333333,
            EncryptedAccessToken = "encrypted-access-token-3",
            EncryptedRefreshToken = "encrypted-refresh-token-3",
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(1),
            Scopes = "identify",
            CreatedAt = DateTime.UtcNow,
            LastRefreshedAt = DateTime.UtcNow
        };

        _context.DiscordOAuthTokens.AddRange(expiringSoonToken, notExpiringSoonToken, almostExpiredToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetExpiringTokensAsync(TimeSpan.FromMinutes(10));

        // Assert
        result.Should().HaveCount(2, "should return 2 tokens expiring within 10 minutes");
        result.Should().Contain(t => t.ApplicationUserId == user1);
        result.Should().Contain(t => t.ApplicationUserId == user3);
        result.Should().NotContain(t => t.ApplicationUserId == user2, "user2's token expires outside the threshold");
    }

    [Fact]
    public async Task GetExpiringTokensAsync_ExcludesAlreadyExpiredTokens()
    {
        // Arrange
        const string user1 = "user-1";
        const string user2 = "user-2";

        // Token expiring in 5 minutes (within threshold)
        var expiringSoonToken = new DiscordOAuthToken
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = user1,
            DiscordUserId = 111111111111111111,
            EncryptedAccessToken = "encrypted-access-token-1",
            EncryptedRefreshToken = "encrypted-refresh-token-1",
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(5),
            Scopes = "identify",
            CreatedAt = DateTime.UtcNow,
            LastRefreshedAt = DateTime.UtcNow
        };

        // Token already expired (should be excluded)
        var alreadyExpiredToken = new DiscordOAuthToken
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = user2,
            DiscordUserId = 222222222222222222,
            EncryptedAccessToken = "encrypted-access-token-2",
            EncryptedRefreshToken = "encrypted-refresh-token-2",
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(-10), // Already expired
            Scopes = "identify",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            LastRefreshedAt = DateTime.UtcNow.AddDays(-1)
        };

        _context.DiscordOAuthTokens.AddRange(expiringSoonToken, alreadyExpiredToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetExpiringTokensAsync(TimeSpan.FromMinutes(10));

        // Assert
        result.Should().ContainSingle("should only return tokens expiring soon, not already expired");
        result.First().ApplicationUserId.Should().Be(user1);
    }

    [Fact]
    public async Task StoreTokensAsync_EncryptsTokensBeforeStorage()
    {
        // Arrange
        const string userId = "user-123";
        const string plainAccessToken = "plain-access-token";
        const string plainRefreshToken = "plain-refresh-token";

        // Act
        await _service.StoreTokensAsync(
            userId,
            123456789012345678,
            plainAccessToken,
            plainRefreshToken,
            DateTime.UtcNow.AddHours(1),
            "identify");

        // Assert
        var storedToken = await _context.DiscordOAuthTokens
            .FirstOrDefaultAsync(t => t.ApplicationUserId == userId);

        storedToken.Should().NotBeNull();

        // Verify the Protect method was called with the plain tokens
        _mockDataProtector.Verify(
            p => p.Protect(It.IsAny<byte[]>()),
            Times.Exactly(2),
            "both access and refresh tokens should be encrypted");

        // The stored tokens should be different from plain tokens (encrypted)
        storedToken!.EncryptedAccessToken.Should().NotBeEmpty();
        storedToken.EncryptedRefreshToken.Should().NotBeEmpty();
    }
}
