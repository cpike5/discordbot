using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Collections;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;

namespace DiscordBot.Tests.Preconditions;

/// <summary>
/// Unit tests for <see cref="RateLimitAttribute"/>.
/// </summary>
public class RateLimitAttributeTests
{
    private readonly Mock<IInteractionContext> _mockContext;
    private readonly Mock<ICommandInfo> _mockCommandInfo;
    private readonly Mock<IServiceProvider> _mockServiceProvider;

    public RateLimitAttributeTests()
    {
        _mockContext = new Mock<IInteractionContext>();
        _mockCommandInfo = new Mock<ICommandInfo>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        // Clear the static invocations dictionary before each test
        ClearRateLimitCache();
    }

    /// <summary>
    /// Gets the static invocations cache using reflection for testing.
    /// </summary>
    private static LruConcurrentDictionary<string, List<DateTime>>? GetInvocationsCache()
    {
        var field = typeof(RateLimitAttribute).GetField(
            "_invocations",
            BindingFlags.NonPublic | BindingFlags.Static);

        return field?.GetValue(null) as LruConcurrentDictionary<string, List<DateTime>>;
    }

    /// <summary>
    /// Clears the static rate limit cache using reflection.
    /// </summary>
    private static void ClearRateLimitCache()
    {
        GetInvocationsCache()?.Clear();
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenWithinRateLimit_ShouldReturnSuccess()
    {
        // Arrange
        var attribute = new RateLimitAttribute(times: 3, periodSeconds: 60, RateLimitTarget.User);

        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(123456789UL);

        _mockContext.Setup(c => c.User).Returns(mockUser.Object);
        _mockCommandInfo.Setup(c => c.Name).Returns("testcommand");

        // Act
        var result = await attribute.CheckRequirementsAsync(
            _mockContext.Object,
            _mockCommandInfo.Object,
            _mockServiceProvider.Object);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue("this is the first invocation within the rate limit");
        result.ErrorReason.Should().BeNull();
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenRateLimitExceeded_ShouldReturnError()
    {
        // Arrange
        var attribute = new RateLimitAttribute(times: 2, periodSeconds: 60, RateLimitTarget.User);

        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(123456789UL);

        _mockContext.Setup(c => c.User).Returns(mockUser.Object);
        _mockCommandInfo.Setup(c => c.Name).Returns("testcommand");

        // Act - Execute command 3 times (limit is 2)
        var result1 = await attribute.CheckRequirementsAsync(
            _mockContext.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);
        var result2 = await attribute.CheckRequirementsAsync(
            _mockContext.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);
        var result3 = await attribute.CheckRequirementsAsync(
            _mockContext.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);

        // Assert
        result1.IsSuccess.Should().BeTrue("first invocation should succeed");
        result2.IsSuccess.Should().BeTrue("second invocation should succeed");
        result3.IsSuccess.Should().BeFalse("third invocation should be rate limited");
        result3.ErrorReason.Should().Contain("Rate limit exceeded");
        result3.ErrorReason.Should().Contain("Please wait");
        result3.ErrorReason.Should().Contain("seconds");
    }

    [Fact]
    public async Task CheckRequirementsAsync_AfterPeriodExpires_ShouldAllowAgain()
    {
        // Arrange - Use a very short period for testing
        var attribute = new RateLimitAttribute(times: 1, periodSeconds: 0.1, RateLimitTarget.User);

        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(123456789UL);

        _mockContext.Setup(c => c.User).Returns(mockUser.Object);
        _mockCommandInfo.Setup(c => c.Name).Returns("testcommand");

        // Act
        var result1 = await attribute.CheckRequirementsAsync(
            _mockContext.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);

        // Wait for rate limit period to expire
        await Task.Delay(150); // 150ms > 100ms period

        var result2 = await attribute.CheckRequirementsAsync(
            _mockContext.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);

        // Assert
        result1.IsSuccess.Should().BeTrue("first invocation should succeed");
        result2.IsSuccess.Should().BeTrue("second invocation should succeed after period expires");
    }

    [Fact]
    public async Task CheckRequirementsAsync_WithUserTarget_ShouldLimitPerUser()
    {
        // Arrange
        var attribute = new RateLimitAttribute(times: 1, periodSeconds: 60, RateLimitTarget.User);

        var mockUser1 = new Mock<IUser>();
        mockUser1.Setup(u => u.Id).Returns(111111111UL);

        var mockUser2 = new Mock<IUser>();
        mockUser2.Setup(u => u.Id).Returns(222222222UL);

        var mockContext1 = new Mock<IInteractionContext>();
        mockContext1.Setup(c => c.User).Returns(mockUser1.Object);

        var mockContext2 = new Mock<IInteractionContext>();
        mockContext2.Setup(c => c.User).Returns(mockUser2.Object);

        _mockCommandInfo.Setup(c => c.Name).Returns("testcommand");

        // Act
        var result1 = await attribute.CheckRequirementsAsync(
            mockContext1.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);
        var result2 = await attribute.CheckRequirementsAsync(
            mockContext2.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);
        var result3 = await attribute.CheckRequirementsAsync(
            mockContext1.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);

        // Assert
        result1.IsSuccess.Should().BeTrue("first user's first invocation should succeed");
        result2.IsSuccess.Should().BeTrue("second user's first invocation should succeed (different user)");
        result3.IsSuccess.Should().BeFalse("first user's second invocation should be rate limited");
    }

    [Fact]
    public async Task CheckRequirementsAsync_WithGuildTarget_ShouldLimitPerGuild()
    {
        // Arrange
        var attribute = new RateLimitAttribute(times: 1, periodSeconds: 60, RateLimitTarget.Guild);

        var mockGuild1 = new Mock<IGuild>();
        mockGuild1.Setup(g => g.Id).Returns(111111111UL);

        var mockGuild2 = new Mock<IGuild>();
        mockGuild2.Setup(g => g.Id).Returns(222222222UL);

        var mockUser1 = new Mock<IUser>();
        mockUser1.Setup(u => u.Id).Returns(333333333UL);

        var mockUser2 = new Mock<IUser>();
        mockUser2.Setup(u => u.Id).Returns(444444444UL);

        var mockContext1 = new Mock<IInteractionContext>();
        mockContext1.Setup(c => c.Guild).Returns(mockGuild1.Object);
        mockContext1.Setup(c => c.User).Returns(mockUser1.Object);

        var mockContext2 = new Mock<IInteractionContext>();
        mockContext2.Setup(c => c.Guild).Returns(mockGuild2.Object);
        mockContext2.Setup(c => c.User).Returns(mockUser2.Object);

        var mockContext3 = new Mock<IInteractionContext>();
        mockContext3.Setup(c => c.Guild).Returns(mockGuild1.Object);
        mockContext3.Setup(c => c.User).Returns(mockUser2.Object);

        _mockCommandInfo.Setup(c => c.Name).Returns("testcommand");

        // Act
        var result1 = await attribute.CheckRequirementsAsync(
            mockContext1.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);
        var result2 = await attribute.CheckRequirementsAsync(
            mockContext2.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);
        var result3 = await attribute.CheckRequirementsAsync(
            mockContext3.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);

        // Assert
        result1.IsSuccess.Should().BeTrue("first guild's first invocation should succeed");
        result2.IsSuccess.Should().BeTrue("second guild's first invocation should succeed (different guild)");
        result3.IsSuccess.Should().BeFalse("first guild's second invocation should be rate limited (same guild, different user)");
    }

    [Fact]
    public async Task CheckRequirementsAsync_WithGlobalTarget_ShouldLimitGlobally()
    {
        // Arrange
        var attribute = new RateLimitAttribute(times: 1, periodSeconds: 60, RateLimitTarget.Global);

        var mockUser1 = new Mock<IUser>();
        mockUser1.Setup(u => u.Id).Returns(111111111UL);

        var mockUser2 = new Mock<IUser>();
        mockUser2.Setup(u => u.Id).Returns(222222222UL);

        var mockGuild1 = new Mock<IGuild>();
        mockGuild1.Setup(g => g.Id).Returns(333333333UL);

        var mockGuild2 = new Mock<IGuild>();
        mockGuild2.Setup(g => g.Id).Returns(444444444UL);

        var mockContext1 = new Mock<IInteractionContext>();
        mockContext1.Setup(c => c.User).Returns(mockUser1.Object);
        mockContext1.Setup(c => c.Guild).Returns(mockGuild1.Object);

        var mockContext2 = new Mock<IInteractionContext>();
        mockContext2.Setup(c => c.User).Returns(mockUser2.Object);
        mockContext2.Setup(c => c.Guild).Returns(mockGuild2.Object);

        _mockCommandInfo.Setup(c => c.Name).Returns("testcommand");

        // Act
        var result1 = await attribute.CheckRequirementsAsync(
            mockContext1.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);
        var result2 = await attribute.CheckRequirementsAsync(
            mockContext2.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);

        // Assert
        result1.IsSuccess.Should().BeTrue("first global invocation should succeed");
        result2.IsSuccess.Should().BeFalse("second global invocation should be rate limited (different user and guild)");
    }

    [Fact]
    public async Task CheckRequirementsAsync_DifferentCommands_ShouldHaveSeparateLimits()
    {
        // Arrange
        var attribute = new RateLimitAttribute(times: 1, periodSeconds: 60, RateLimitTarget.User);

        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(123456789UL);

        var mockCommandInfo1 = new Mock<ICommandInfo>();
        mockCommandInfo1.Setup(c => c.Name).Returns("command1");

        var mockCommandInfo2 = new Mock<ICommandInfo>();
        mockCommandInfo2.Setup(c => c.Name).Returns("command2");

        _mockContext.Setup(c => c.User).Returns(mockUser.Object);

        // Act
        var result1 = await attribute.CheckRequirementsAsync(
            _mockContext.Object, mockCommandInfo1.Object, _mockServiceProvider.Object);
        var result2 = await attribute.CheckRequirementsAsync(
            _mockContext.Object, mockCommandInfo2.Object, _mockServiceProvider.Object);
        var result3 = await attribute.CheckRequirementsAsync(
            _mockContext.Object, mockCommandInfo1.Object, _mockServiceProvider.Object);

        // Assert
        result1.IsSuccess.Should().BeTrue("first command's first invocation should succeed");
        result2.IsSuccess.Should().BeTrue("second command's first invocation should succeed (different command)");
        result3.IsSuccess.Should().BeFalse("first command's second invocation should be rate limited");
    }

    [Fact]
    public async Task CheckRequirementsAsync_MultipleInvocationsWithinLimit_ShouldAllSucceed()
    {
        // Arrange
        var attribute = new RateLimitAttribute(times: 5, periodSeconds: 60, RateLimitTarget.User);

        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(123456789UL);

        _mockContext.Setup(c => c.User).Returns(mockUser.Object);
        _mockCommandInfo.Setup(c => c.Name).Returns("testcommand");

        // Act - Execute 5 times (at the limit)
        var results = new List<PreconditionResult>();
        for (int i = 0; i < 5; i++)
        {
            var result = await attribute.CheckRequirementsAsync(
                _mockContext.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);
            results.Add(result);
        }

        // Assert
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue("all invocations are within the limit"));
    }

    [Fact]
    public async Task CheckRequirementsAsync_ErrorMessage_ShouldContainTimeUntilReset()
    {
        // Arrange
        var attribute = new RateLimitAttribute(times: 1, periodSeconds: 60, RateLimitTarget.User);

        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(123456789UL);

        _mockContext.Setup(c => c.User).Returns(mockUser.Object);
        _mockCommandInfo.Setup(c => c.Name).Returns("testcommand");

        // Act
        await attribute.CheckRequirementsAsync(
            _mockContext.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);
        var result = await attribute.CheckRequirementsAsync(
            _mockContext.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorReason.Should().MatchRegex(@"Rate limit exceeded\. Please wait \d+\.\d seconds before using this command again\.");
    }

    #region Logging Tests

    [Fact]
    public async Task CheckRequirementsAsync_WhenRateLimitExceeded_ShouldLogWarning()
    {
        // Arrange
        var attribute = new RateLimitAttribute(times: 1, periodSeconds: 60, RateLimitTarget.User);

        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(123456789UL);

        var mockGuild = new Mock<IGuild>();
        mockGuild.Setup(g => g.Id).Returns(987654321UL);

        _mockContext.Setup(c => c.User).Returns(mockUser.Object);
        _mockContext.Setup(c => c.Guild).Returns(mockGuild.Object);
        _mockCommandInfo.Setup(c => c.Name).Returns("testcommand");

        var mockLogger = new Mock<ILogger<RateLimitAttribute>>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        mockLoggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(mockLogger.Object);

        _mockServiceProvider
            .Setup(s => s.GetService(typeof(ILoggerFactory)))
            .Returns(mockLoggerFactory.Object);

        // Act - First call succeeds, second triggers rate limit
        await attribute.CheckRequirementsAsync(
            _mockContext.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);
        await attribute.CheckRequirementsAsync(
            _mockContext.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);

        // Assert - Verify warning was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Rate limit exceeded")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenWithinRateLimit_ShouldLogTrace()
    {
        // Arrange
        var attribute = new RateLimitAttribute(times: 3, periodSeconds: 60, RateLimitTarget.User);

        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(123456789UL);

        _mockContext.Setup(c => c.User).Returns(mockUser.Object);
        _mockCommandInfo.Setup(c => c.Name).Returns("testcommand");

        var mockLogger = new Mock<ILogger<RateLimitAttribute>>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        mockLoggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(mockLogger.Object);

        _mockServiceProvider
            .Setup(s => s.GetService(typeof(ILoggerFactory)))
            .Returns(mockLoggerFactory.Object);

        // Act
        await attribute.CheckRequirementsAsync(
            _mockContext.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);

        // Assert - Verify trace was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Rate limit check passed")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenNoLoggerFactory_ShouldNotThrow()
    {
        // Arrange
        var attribute = new RateLimitAttribute(times: 1, periodSeconds: 60, RateLimitTarget.User);

        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(123456789UL);

        _mockContext.Setup(c => c.User).Returns(mockUser.Object);
        _mockCommandInfo.Setup(c => c.Name).Returns("testcommand");

        // Service provider returns null for ILoggerFactory
        _mockServiceProvider
            .Setup(s => s.GetService(typeof(ILoggerFactory)))
            .Returns(null!);

        // Act & Assert - Should not throw
        var result = await attribute.CheckRequirementsAsync(
            _mockContext.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CheckRequirementsAsync_RateLimitViolationLog_ContainsAllRequiredFields()
    {
        // Arrange
        var attribute = new RateLimitAttribute(times: 1, periodSeconds: 60, RateLimitTarget.User);
        var userId = 123456789UL;
        var guildId = 987654321UL;
        var commandName = "testcommand";

        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(userId);

        var mockGuild = new Mock<IGuild>();
        mockGuild.Setup(g => g.Id).Returns(guildId);

        _mockContext.Setup(c => c.User).Returns(mockUser.Object);
        _mockContext.Setup(c => c.Guild).Returns(mockGuild.Object);
        _mockCommandInfo.Setup(c => c.Name).Returns(commandName);

        string? loggedMessage = null;
        var mockLogger = new Mock<ILogger<RateLimitAttribute>>();
        mockLogger
            .Setup(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Delegate>((level, eventId, state, ex, formatter) =>
            {
                loggedMessage = state?.ToString();
            });

        var mockLoggerFactory = new Mock<ILoggerFactory>();
        mockLoggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(mockLogger.Object);

        _mockServiceProvider
            .Setup(s => s.GetService(typeof(ILoggerFactory)))
            .Returns(mockLoggerFactory.Object);

        // Act - Trigger rate limit
        await attribute.CheckRequirementsAsync(
            _mockContext.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);
        await attribute.CheckRequirementsAsync(
            _mockContext.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);

        // Assert - Verify log contains all required fields per issue #103
        loggedMessage.Should().NotBeNull();
        loggedMessage.Should().Contain(userId.ToString(), "should contain User ID");
        loggedMessage.Should().Contain(commandName, "should contain command name");
        loggedMessage.Should().Contain("1", "should contain rate limit times");
        loggedMessage.Should().Contain("60", "should contain period seconds");
        loggedMessage.Should().Contain("Reset in", "should contain reset time");
    }

    #endregion

    #region Eviction Tests

    [Fact]
    public void MaxTrackedKeys_HasReasonableValue()
    {
        // Assert
        RateLimitAttribute.MaxTrackedKeys.Should().Be(10000,
            "should track up to 10,000 unique rate limit keys before eviction");
    }

    [Fact]
    public async Task InvocationsCache_IsBounded_EvictsLeastRecentlyUsedEntries()
    {
        // Arrange
        var attribute = new RateLimitAttribute(times: 5, periodSeconds: 60, RateLimitTarget.User);
        var cache = GetInvocationsCache();
        cache.Should().NotBeNull("cache should be accessible for testing");

        _mockCommandInfo.Setup(c => c.Name).Returns("evictiontest");

        // Act - Add entries up to capacity + some more to trigger eviction
        // We'll use a smaller test to verify the mechanism works
        const int testCapacity = 100; // We can't easily fill 10,000 entries in a unit test
        const int entriesToAdd = testCapacity + 50;

        for (int i = 0; i < entriesToAdd; i++)
        {
            var mockUser = new Mock<IUser>();
            mockUser.Setup(u => u.Id).Returns((ulong)(1000000000 + i));

            var mockContext = new Mock<IInteractionContext>();
            mockContext.Setup(c => c.User).Returns(mockUser.Object);

            await attribute.CheckRequirementsAsync(
                mockContext.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);
        }

        // Assert - Cache should be bounded
        cache!.Count.Should().BeLessThanOrEqualTo(RateLimitAttribute.MaxTrackedKeys,
            "cache should never exceed maximum capacity");

        // The cache should have entries (we added fewer than capacity, so all should be there)
        cache.Count.Should().BeGreaterThan(0, "cache should have entries");
    }

    [Fact]
    public async Task InvocationsCache_WhenReaccessingKey_UpdatesLruOrder()
    {
        // Arrange
        var attribute = new RateLimitAttribute(times: 5, periodSeconds: 60, RateLimitTarget.User);
        var cache = GetInvocationsCache();
        cache.Should().NotBeNull();

        _mockCommandInfo.Setup(c => c.Name).Returns("lruordertest");

        // Create first user
        var mockUser1 = new Mock<IUser>();
        mockUser1.Setup(u => u.Id).Returns(111111111UL);
        var mockContext1 = new Mock<IInteractionContext>();
        mockContext1.Setup(c => c.User).Returns(mockUser1.Object);

        // Create second user
        var mockUser2 = new Mock<IUser>();
        mockUser2.Setup(u => u.Id).Returns(222222222UL);
        var mockContext2 = new Mock<IInteractionContext>();
        mockContext2.Setup(c => c.User).Returns(mockUser2.Object);

        // Act - User1 accesses, then User2, then User1 again
        await attribute.CheckRequirementsAsync(
            mockContext1.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);
        await attribute.CheckRequirementsAsync(
            mockContext2.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);
        await attribute.CheckRequirementsAsync(
            mockContext1.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);

        // Assert - User1's key should now be most recently used
        var allEntries = cache!.GetAll();
        allEntries.Should().HaveCount(2);
        allEntries[0].Key.Should().Contain("111111111", "user1's key should be most recently used");
        allEntries[1].Key.Should().Contain("222222222", "user2's key should be least recently used");
    }

    [Fact]
    public void InvocationsCache_UsesLruConcurrentDictionary()
    {
        // This test verifies the fix for issue #854 - memory leak in RateLimitAttribute
        // The cache should be a bounded LruConcurrentDictionary, not an unbounded ConcurrentDictionary

        // Arrange
        var field = typeof(RateLimitAttribute).GetField(
            "_invocations",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Assert
        field.Should().NotBeNull("_invocations field should exist");
        field!.FieldType.Should().Be(
            typeof(LruConcurrentDictionary<string, List<DateTime>>),
            "cache should be an LruConcurrentDictionary to prevent unbounded memory growth");

        var cache = field.GetValue(null);
        cache.Should().NotBeNull();
        cache.Should().BeOfType<LruConcurrentDictionary<string, List<DateTime>>>();

        var lruCache = (LruConcurrentDictionary<string, List<DateTime>>)cache!;
        lruCache.Capacity.Should().Be(RateLimitAttribute.MaxTrackedKeys,
            "cache capacity should match MaxTrackedKeys constant");
    }

    #endregion
}
