using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="CommandExecutionLogger"/>.
/// </summary>
public class CommandExecutionLoggerTests
{
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ICommandLogRepository> _mockCommandLogRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IGuildRepository> _mockGuildRepository;
    private readonly Mock<ILogger<CommandExecutionLogger>> _mockLogger;
    private readonly CommandExecutionLogger _service;

    public CommandExecutionLoggerTests()
    {
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockCommandLogRepository = new Mock<ICommandLogRepository>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockGuildRepository = new Mock<IGuildRepository>();
        _mockLogger = new Mock<ILogger<CommandExecutionLogger>>();

        // Setup service scope factory chain
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(_mockScope.Object);
        _mockScope.Setup(s => s.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(ICommandLogRepository)))
            .Returns(_mockCommandLogRepository.Object);
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IUserRepository)))
            .Returns(_mockUserRepository.Object);
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IGuildRepository)))
            .Returns(_mockGuildRepository.Object);

        // Setup default user upsert behavior
        _mockUserRepository
            .Setup(r => r.UpsertAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) => u);

        // Setup default guild upsert behavior
        _mockGuildRepository
            .Setup(r => r.UpsertAsync(It.IsAny<Guild>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guild g, CancellationToken _) => g);

        _service = new CommandExecutionLogger(_mockScopeFactory.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task LogCommandExecutionAsync_WithSuccessfulCommand_ShouldLogToRepository()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong userId = 987654321UL;
        const string commandName = "ping";
        const string parameters = "{\"option\":\"value\"}";
        const int executionTimeMs = 150;
        const bool success = true;
        const string correlationId = "test-correlation-id";

        var mockGuild = new Mock<IGuild>();
        mockGuild.Setup(g => g.Id).Returns(guildId);
        mockGuild.Setup(g => g.Name).Returns("Test Guild");

        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(userId);
        mockUser.Setup(u => u.Username).Returns("TestUser");
        mockUser.Setup(u => u.Discriminator).Returns("0");

        var mockContext = new Mock<IInteractionContext>();
        mockContext.Setup(c => c.Guild).Returns(mockGuild.Object);
        mockContext.Setup(c => c.User).Returns(mockUser.Object);

        // Act
        await _service.LogCommandExecutionAsync(
            mockContext.Object,
            commandName,
            parameters,
            executionTimeMs,
            success,
            errorMessage: null,
            correlationId: correlationId);

        // Assert
        _mockCommandLogRepository.Verify(
            r => r.LogCommandAsync(
                guildId,
                userId,
                commandName,
                parameters,
                executionTimeMs,
                success,
                null,
                correlationId,
                It.IsAny<CancellationToken>()),
            Times.Once,
            "the command log repository should be called with correct parameters");
    }

    [Fact]
    public async Task LogCommandExecutionAsync_WithFailedCommand_ShouldLogErrorMessage()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong userId = 987654321UL;
        const string commandName = "failingcommand";
        const string errorMessage = "Command execution failed due to invalid input";
        const int executionTimeMs = 50;
        const bool success = false;

        var mockGuild = new Mock<IGuild>();
        mockGuild.Setup(g => g.Id).Returns(guildId);
        mockGuild.Setup(g => g.Name).Returns("Test Guild");

        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(userId);
        mockUser.Setup(u => u.Username).Returns("TestUser");
        mockUser.Setup(u => u.Discriminator).Returns("0");

        var mockContext = new Mock<IInteractionContext>();
        mockContext.Setup(c => c.Guild).Returns(mockGuild.Object);
        mockContext.Setup(c => c.User).Returns(mockUser.Object);

        // Act
        await _service.LogCommandExecutionAsync(
            mockContext.Object,
            commandName,
            parameters: null,
            executionTimeMs,
            success,
            errorMessage: errorMessage);

        // Assert
        _mockCommandLogRepository.Verify(
            r => r.LogCommandAsync(
                guildId,
                userId,
                commandName,
                null,
                executionTimeMs,
                success,
                errorMessage,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once,
            "the command log repository should be called with error message");
    }

    [Fact]
    public async Task LogCommandExecutionAsync_WithNullGuild_ShouldPassNullGuildId()
    {
        // Arrange
        const ulong userId = 987654321UL;
        const string commandName = "dmcommand";

        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(userId);
        mockUser.Setup(u => u.Username).Returns("TestUser");
        mockUser.Setup(u => u.Discriminator).Returns("0");

        var mockContext = new Mock<IInteractionContext>();
        mockContext.Setup(c => c.Guild).Returns((IGuild?)null); // DM context
        mockContext.Setup(c => c.User).Returns(mockUser.Object);

        // Act
        await _service.LogCommandExecutionAsync(
            mockContext.Object,
            commandName,
            parameters: null,
            executionTimeMs: 100,
            success: true);

        // Assert
        _mockCommandLogRepository.Verify(
            r => r.LogCommandAsync(
                null,
                userId,
                commandName,
                null,
                100,
                true,
                null,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once,
            "the guild ID should be null for DM commands");
    }

    [Fact]
    public async Task LogCommandExecutionAsync_WithCorrelationId_ShouldPassCorrelationId()
    {
        // Arrange
        const string correlationId = "unique-correlation-id-12345";
        const ulong userId = 123456789UL;

        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(userId);
        mockUser.Setup(u => u.Username).Returns("TestUser");
        mockUser.Setup(u => u.Discriminator).Returns("0");

        var mockContext = new Mock<IInteractionContext>();
        mockContext.Setup(c => c.Guild).Returns((IGuild?)null);
        mockContext.Setup(c => c.User).Returns(mockUser.Object);

        // Act
        await _service.LogCommandExecutionAsync(
            mockContext.Object,
            "test",
            parameters: null,
            executionTimeMs: 100,
            success: true,
            correlationId: correlationId);

        // Assert
        _mockCommandLogRepository.Verify(
            r => r.LogCommandAsync(
                It.IsAny<ulong?>(),
                It.IsAny<ulong>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                correlationId,
                It.IsAny<CancellationToken>()),
            Times.Once,
            "the correlation ID should be passed to the repository");
    }

    [Fact]
    public async Task LogCommandExecutionAsync_WhenRepositoryThrows_ShouldNotThrow()
    {
        // Arrange
        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(123456789UL);
        mockUser.Setup(u => u.Username).Returns("TestUser");
        mockUser.Setup(u => u.Discriminator).Returns("0");

        var mockContext = new Mock<IInteractionContext>();
        mockContext.Setup(c => c.Guild).Returns((IGuild?)null);
        mockContext.Setup(c => c.User).Returns(mockUser.Object);

        _mockCommandLogRepository
            .Setup(r => r.LogCommandAsync(
                It.IsAny<ulong?>(),
                It.IsAny<ulong>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act & Assert
        await FluentActions.Invoking(async () =>
            await _service.LogCommandExecutionAsync(
                mockContext.Object,
                "test",
                parameters: null,
                executionTimeMs: 100,
                success: true))
            .Should().NotThrowAsync("logging failures should not break command execution");
    }

    [Fact]
    public async Task LogCommandExecutionAsync_WhenRepositoryThrows_ShouldLogError()
    {
        // Arrange
        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(123456789UL);
        mockUser.Setup(u => u.Username).Returns("TestUser");
        mockUser.Setup(u => u.Discriminator).Returns("0");

        var mockContext = new Mock<IInteractionContext>();
        mockContext.Setup(c => c.Guild).Returns((IGuild?)null);
        mockContext.Setup(c => c.User).Returns(mockUser.Object);

        var expectedException = new InvalidOperationException("Database error");
        _mockCommandLogRepository
            .Setup(r => r.LogCommandAsync(
                It.IsAny<ulong?>(),
                It.IsAny<ulong>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        await _service.LogCommandExecutionAsync(
            mockContext.Object,
            "test",
            parameters: null,
            executionTimeMs: 100,
            success: true);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to log command execution")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "the error should be logged when repository throws");
    }

    [Fact]
    public async Task LogCommandExecutionAsync_ShouldCreateNewScope()
    {
        // Arrange
        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(123456789UL);
        mockUser.Setup(u => u.Username).Returns("TestUser");
        mockUser.Setup(u => u.Discriminator).Returns("0");

        var mockContext = new Mock<IInteractionContext>();
        mockContext.Setup(c => c.Guild).Returns((IGuild?)null);
        mockContext.Setup(c => c.User).Returns(mockUser.Object);

        // Act
        await _service.LogCommandExecutionAsync(
            mockContext.Object,
            "test",
            parameters: null,
            executionTimeMs: 100,
            success: true);

        // Assert
        _mockScopeFactory.Verify(
            f => f.CreateScope(),
            Times.Once,
            "a new scope should be created for accessing scoped repositories");
    }

    [Fact]
    public async Task LogCommandExecutionAsync_ShouldDisposeScope()
    {
        // Arrange
        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(123456789UL);
        mockUser.Setup(u => u.Username).Returns("TestUser");
        mockUser.Setup(u => u.Discriminator).Returns("0");

        var mockContext = new Mock<IInteractionContext>();
        mockContext.Setup(c => c.Guild).Returns((IGuild?)null);
        mockContext.Setup(c => c.User).Returns(mockUser.Object);

        // Act
        await _service.LogCommandExecutionAsync(
            mockContext.Object,
            "test",
            parameters: null,
            executionTimeMs: 100,
            success: true);

        // Assert
        _mockScope.Verify(
            s => s.Dispose(),
            Times.Once,
            "the scope should be disposed after logging");
    }

    [Fact]
    public async Task LogCommandExecutionAsync_WithCancellationToken_ShouldPassToRepository()
    {
        // Arrange
        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(123456789UL);
        mockUser.Setup(u => u.Username).Returns("TestUser");
        mockUser.Setup(u => u.Discriminator).Returns("0");

        var mockContext = new Mock<IInteractionContext>();
        mockContext.Setup(c => c.Guild).Returns((IGuild?)null);
        mockContext.Setup(c => c.User).Returns(mockUser.Object);

        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        // Act
        await _service.LogCommandExecutionAsync(
            mockContext.Object,
            "test",
            parameters: null,
            executionTimeMs: 100,
            success: true,
            cancellationToken: cancellationToken);

        // Assert
        _mockCommandLogRepository.Verify(
            r => r.LogCommandAsync(
                It.IsAny<ulong?>(),
                It.IsAny<ulong>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                cancellationToken),
            Times.Once,
            "the cancellation token should be passed to the repository");
    }
}
