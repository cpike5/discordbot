using Discord.WebSocket;
using DiscordBot.Bot.Services.Preview;
using DiscordBot.Core.Entities;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DiscordBot.Tests.Bot.Services.Preview;

/// <summary>
/// Tests for <see cref="PreviewService"/>, extracted from <c>PreviewController</c>
/// (docs/plans/blazor-port-plan.md Phase 4 cluster 4d - no <c>PreviewControllerTests</c> existed
/// to move). <c>SocketUser</c>/<c>SocketGuild</c>/<c>SocketGuildUser</c> are sealed Discord.Net
/// types that cannot be mocked (see the same note in <c>WelcomeServiceTests</c>), so coverage here
/// is limited to the branches reachable through <see cref="DiscordSocketClient.GetUser(ulong)"/>/
/// <see cref="DiscordSocketClient.GetGuild(ulong)"/> returning <see langword="null"/> - exactly
/// the "not in the Discord cache" contract every caller (the controller's 404, <c>PreviewPopover</c>'s
/// error state) depends on.
/// </summary>
public class PreviewServiceTests : IDisposable
{
    private readonly BotDbContext _dbContext;
    private readonly SqliteConnection _connection;
    private readonly Mock<DiscordSocketClient> _mockDiscordClient;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly PreviewService _service;

    public PreviewServiceTests()
    {
        (_dbContext, _connection) = TestDbContextFactory.CreateContext();
        _mockDiscordClient = new Mock<DiscordSocketClient>();
        _mockUserManager = MockUserManager();

        _service = new PreviewService(
            _mockDiscordClient.Object,
            _dbContext,
            _mockUserManager.Object,
            Mock.Of<ILogger<PreviewService>>());
    }

    private static Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    [Fact]
    public async Task GetUserPreviewAsync_NoGuildContext_UserNotInCache_ReturnsNull()
    {
        _mockDiscordClient.Setup(c => c.GetUser(It.IsAny<ulong>())).Returns((SocketUser?)null);

        var result = await _service.GetUserPreviewAsync(123UL, null, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserPreviewAsync_WithGuildContext_GuildNotInCache_FallsBackToUserLookup_ReturnsNullWhenUserAlsoMissing()
    {
        _mockDiscordClient.Setup(c => c.GetGuild(It.IsAny<ulong>())).Returns((SocketGuild?)null);
        _mockDiscordClient.Setup(c => c.GetUser(It.IsAny<ulong>())).Returns((SocketUser?)null);

        var result = await _service.GetUserPreviewAsync(123UL, 456UL, CancellationToken.None);

        result.Should().BeNull();
        _mockDiscordClient.Verify(c => c.GetGuild(456UL), Times.Once);
        _mockDiscordClient.Verify(c => c.GetUser(123UL), Times.Once);
    }

    [Fact]
    public async Task GetGuildPreviewAsync_GuildNotInCache_ReturnsNull()
    {
        _mockDiscordClient.Setup(c => c.GetGuild(It.IsAny<ulong>())).Returns((SocketGuild?)null);

        var result = await _service.GetGuildPreviewAsync(789UL, CancellationToken.None);

        result.Should().BeNull();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
