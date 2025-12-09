using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Enums;
using FluentAssertions;
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
    /// Clears the static rate limit cache using reflection.
    /// </summary>
    private static void ClearRateLimitCache()
    {
        var field = typeof(RateLimitAttribute).GetField(
            "_invocations",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (field?.GetValue(null) is System.Collections.IDictionary dict)
        {
            dict.Clear();
        }
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
}
