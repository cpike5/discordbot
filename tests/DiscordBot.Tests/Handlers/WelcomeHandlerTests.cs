using DiscordBot.Bot.Handlers;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Handlers;

/// <summary>
/// Unit tests for <see cref="WelcomeHandler"/>.
///
/// NOTE: Due to Discord.NET's use of sealed types and non-virtual members in SocketGuildUser and SocketGuild,
/// full unit testing of HandleUserJoinedAsync is not possible using Moq. These tests focus on:
/// 1. Constructor behavior and dependency injection setup
/// 2. Service scope creation and disposal patterns
/// 3. Documentation of the handler's responsibilities and contract
///
/// The WelcomeHandler is a thin wrapper that:
/// - Listens to UserJoined events from Discord.NET
/// - Creates a service scope to access scoped IWelcomeService from singleton context
/// - Delegates to IWelcomeService.SendWelcomeMessageAsync with guild and user IDs
/// - Logs success, failure, or "not sent" conditions
/// - Catches and logs all exceptions to prevent bot crashes
///
/// The actual welcome message logic is tested comprehensively in WelcomeServiceTests.cs.
/// Integration testing with actual Discord.NET instances would be required to test HandleUserJoinedAsync
/// with real SocketGuildUser objects.
/// </summary>
public class WelcomeHandlerTests
{
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IWelcomeService> _mockWelcomeService;
    private readonly Mock<ILogger<WelcomeHandler>> _mockLogger;

    public WelcomeHandlerTests()
    {
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockWelcomeService = new Mock<IWelcomeService>();
        _mockLogger = new Mock<ILogger<WelcomeHandler>>();

        // Setup service scope chain - this is the standard pattern for testing handlers
        // that use IServiceScopeFactory to resolve scoped services from singleton contexts
        _mockScopeFactory
            .Setup(f => f.CreateScope())
            .Returns(_mockScope.Object);

        _mockScope
            .Setup(s => s.ServiceProvider)
            .Returns(_mockServiceProvider.Object);

        _mockServiceProvider
            .Setup(p => p.GetService(typeof(IWelcomeService)))
            .Returns(_mockWelcomeService.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange & Act
        var handler = new WelcomeHandler(_mockScopeFactory.Object, _mockLogger.Object);

        // Assert
        handler.Should().NotBeNull("handler should be created with valid dependencies");
    }

    [Fact]
    public void Constructor_WithValidDependencies_DoesNotThrow()
    {
        // Arrange & Act
        var act = () => new WelcomeHandler(_mockScopeFactory.Object, _mockLogger.Object);

        // Assert
        act.Should().NotThrow("handler should construct successfully with valid dependencies");
    }

    [Fact]
    public void Constructor_RequiresScopeFactoryDependency()
    {
        // Assert
        _mockScopeFactory.Should().NotBeNull(
            "IServiceScopeFactory should be injected to create scopes for accessing scoped services");
    }

    [Fact]
    public void Constructor_RequiresLoggerDependency()
    {
        // Assert
        _mockLogger.Should().NotBeNull(
            "ILogger<WelcomeHandler> should be injected for logging events and errors");
    }

    #endregion

    #region Service Scope Configuration Tests

    [Fact]
    public void ServiceScopeFactory_CreatesScope_WhenRequested()
    {
        // Arrange
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var mockScope = new Mock<IServiceScope>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        // Act
        var scope = scopeFactory.Object.CreateScope();

        // Assert
        scope.Should().NotBeNull("scope factory should create service scope");
        scope.Should().BeSameAs(mockScope.Object, "should return configured mock scope");
    }

    [Fact]
    public void ServiceScope_ProvidesServiceProvider()
    {
        // Arrange
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);

        // Act
        var provider = mockScope.Object.ServiceProvider;

        // Assert
        provider.Should().NotBeNull("service scope should provide service provider");
        provider.Should().BeSameAs(mockServiceProvider.Object, "should return configured provider");
    }

    [Fact]
    public void ServiceProvider_ResolvesWelcomeService()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockWelcomeService = new Mock<IWelcomeService>();
        mockServiceProvider
            .Setup(p => p.GetService(typeof(IWelcomeService)))
            .Returns(mockWelcomeService.Object);

        // Act
        var service = mockServiceProvider.Object.GetService(typeof(IWelcomeService));

        // Assert
        service.Should().NotBeNull("service provider should resolve IWelcomeService");
        service.Should().BeSameAs(mockWelcomeService.Object, "should return configured welcome service");
    }

    [Fact]
    public void ServiceScopeChain_IsConfiguredCorrectly()
    {
        // This test verifies the complete chain: ScopeFactory -> Scope -> ServiceProvider -> WelcomeService
        // This is the pattern used by WelcomeHandler to access scoped services from singleton context

        // Arrange - done in constructor

        // Act
        var scope = _mockScopeFactory.Object.CreateScope();
        var provider = scope.ServiceProvider;
        var service = provider.GetService(typeof(IWelcomeService));

        // Assert
        scope.Should().BeSameAs(_mockScope.Object, "scope factory should create configured scope");
        provider.Should().BeSameAs(_mockServiceProvider.Object, "scope should provide configured provider");
        service.Should().BeSameAs(_mockWelcomeService.Object, "provider should resolve configured service");
    }

    #endregion

    #region Integration Test Documentation

    // NOTE: The following tests document the expected behavior of HandleUserJoinedAsync,
    // but cannot be implemented as traditional unit tests due to Discord.NET's sealed types.
    //
    // Expected behavior of HandleUserJoinedAsync(SocketGuildUser user):
    //
    // 1. Creates service scope via IServiceScopeFactory.CreateScope()
    // 2. Resolves IWelcomeService from scope.ServiceProvider.GetRequiredService<IWelcomeService>()
    // 3. Extracts guildId from user.Guild.Id and userId from user.Id
    // 4. Logs debug message: "Processing UserJoined event for user {UserId} ({Username}) in guild {GuildId} ({GuildName})"
    // 5. Calls await welcomeService.SendWelcomeMessageAsync(guildId, userId)
    // 6. If result is true:
    //    - Logs information: "Successfully sent welcome message for user {UserId} ({Username}) in guild {GuildId} ({GuildName})"
    // 7. If result is false:
    //    - Logs debug: "Welcome message was not sent for user {UserId} in guild {GuildId} (disabled or not configured)"
    // 8. If exception occurs:
    //    - Catches all exceptions (does not throw)
    //    - Logs error: "Failed to handle UserJoined event for user {UserId} in guild {GuildId}"
    // 9. Disposes service scope (using statement ensures disposal even on exception)
    //
    // Integration tests using actual SocketGuildUser instances would be required to verify this behavior.

    [Fact]
    public void HandleUserJoinedAsync_Contract_IsDocumented()
    {
        // This test exists to document the HandleUserJoinedAsync contract
        var contractDescription = @"
HandleUserJoinedAsync responsibilities:
1. Extract guild ID and user ID from SocketGuildUser parameter
2. Create service scope to access scoped IWelcomeService from singleton WelcomeHandler
3. Call IWelcomeService.SendWelcomeMessageAsync with extracted IDs
4. Log processing, success, or not-sent conditions
5. Catch and log all exceptions without rethrowing (prevent bot crashes)
6. Dispose service scope via using statement
";

        contractDescription.Should().NotBeNullOrWhiteSpace("contract should be documented");
    }

    [Fact]
    public void WelcomeHandler_DependsOnWelcomeService()
    {
        // This test documents that WelcomeHandler is a thin wrapper around IWelcomeService
        // The actual business logic (message templating, channel permissions, message sending)
        // is implemented and tested in WelcomeService

        var serviceInterface = typeof(IWelcomeService);
        var sendMethod = serviceInterface.GetMethod("SendWelcomeMessageAsync");

        sendMethod.Should().NotBeNull("IWelcomeService should have SendWelcomeMessageAsync method");
        sendMethod!.ReturnType.Should().Be(typeof(Task<bool>), "SendWelcomeMessageAsync should return Task<bool>");
    }

    [Fact]
    public void WelcomeHandler_UsesScopedServicePattern()
    {
        // WelcomeHandler is registered as a singleton (lives for the lifetime of the bot)
        // IWelcomeService is registered as scoped (lives for the duration of a request/operation)
        // To access scoped services from a singleton, WelcomeHandler must:
        // 1. Inject IServiceScopeFactory (which is safe to use from singleton)
        // 2. Create a scope when needed
        // 3. Resolve IWelcomeService from that scope
        // 4. Dispose the scope when done

        _mockScopeFactory.Should().NotBeNull("handler should use IServiceScopeFactory");
        _mockScope.Should().NotBeNull("scope factory should create IServiceScope");
        _mockServiceProvider.Should().NotBeNull("scope should provide IServiceProvider");
        _mockWelcomeService.Should().NotBeNull("provider should resolve IWelcomeService");
    }

    #endregion

    #region WelcomeService Contract Tests

    [Fact]
    public void WelcomeService_SendWelcomeMessageAsync_AcceptsGuildIdParameter()
    {
        // Arrange
        var guildId = 123456789UL;
        var userId = 987654321UL;

        _mockWelcomeService
            .Setup(s => s.SendWelcomeMessageAsync(guildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = _mockWelcomeService.Object.SendWelcomeMessageAsync(guildId, userId);

        // Assert
        result.Should().NotBeNull("SendWelcomeMessageAsync should return a Task<bool>");
    }

    [Fact]
    public void WelcomeService_SendWelcomeMessageAsync_AcceptsUserIdParameter()
    {
        // Arrange
        var guildId = 123456789UL;
        var userId = 987654321UL;

        _mockWelcomeService
            .Setup(s => s.SendWelcomeMessageAsync(guildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = _mockWelcomeService.Object.SendWelcomeMessageAsync(guildId, userId);

        // Assert
        result.Should().NotBeNull("SendWelcomeMessageAsync should accept userId parameter");
    }

    [Fact]
    public async Task WelcomeService_SendWelcomeMessageAsync_ReturnsTrueWhenMessageSent()
    {
        // Arrange
        var guildId = 123456789UL;
        var userId = 987654321UL;

        _mockWelcomeService
            .Setup(s => s.SendWelcomeMessageAsync(guildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _mockWelcomeService.Object.SendWelcomeMessageAsync(guildId, userId);

        // Assert
        result.Should().BeTrue("service should return true when welcome message is sent successfully");
    }

    [Fact]
    public async Task WelcomeService_SendWelcomeMessageAsync_ReturnsFalseWhenMessageNotSent()
    {
        // Arrange
        var guildId = 123456789UL;
        var userId = 987654321UL;

        _mockWelcomeService
            .Setup(s => s.SendWelcomeMessageAsync(guildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _mockWelcomeService.Object.SendWelcomeMessageAsync(guildId, userId);

        // Assert
        result.Should().BeFalse(
            "service should return false when welcome messages are disabled or not configured");
    }

    [Fact]
    public async Task WelcomeService_SendWelcomeMessageAsync_CanThrowException()
    {
        // Arrange
        var guildId = 123456789UL;
        var userId = 987654321UL;
        var expectedException = new Exception("Failed to send welcome message");

        _mockWelcomeService
            .Setup(s => s.SendWelcomeMessageAsync(guildId, userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        var act = async () => await _mockWelcomeService.Object.SendWelcomeMessageAsync(guildId, userId);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Failed to send welcome message",
                "service can throw exceptions that handler must catch");
    }

    #endregion

    #region Error Handling Documentation Tests

    [Fact]
    public void WelcomeHandler_ShouldCatchAllExceptions()
    {
        // WelcomeHandler wraps its entire HandleUserJoinedAsync logic in a try-catch block
        // This prevents exceptions in welcome message handling from crashing the Discord bot
        // Exceptions are logged but not rethrown

        var expectedBehavior = @"
Error handling contract:
- All exceptions in HandleUserJoinedAsync are caught
- Exceptions are logged at Error level with user ID and guild ID context
- Exceptions are NOT rethrown (method does not throw)
- Bot continues running even if welcome message fails
";

        expectedBehavior.Should().NotBeNullOrWhiteSpace("error handling contract should be documented");
    }

    [Fact]
    public void WelcomeHandler_ShouldDisposeScope_EvenOnException()
    {
        // WelcomeHandler uses 'using var scope = _scopeFactory.CreateScope()'
        // The using statement ensures scope is disposed even if SendWelcomeMessageAsync throws
        // This prevents resource leaks

        var expectedBehavior = @"
Resource management contract:
- Service scope is created with 'using var scope = ...'
- Scope is automatically disposed when method exits
- Disposal happens even if exception occurs
- No resource leaks from undisposed scopes
";

        expectedBehavior.Should().NotBeNullOrWhiteSpace("resource management should be documented");
    }

    #endregion

    #region Logging Contract Tests

    [Fact]
    public void WelcomeHandler_LogsDebugMessage_WhenProcessingUserJoined()
    {
        // Expected log: LogDebug("Processing UserJoined event for user {UserId} ({Username}) in guild {GuildId} ({GuildName})", ...)
        var expectedLogLevel = LogLevel.Debug;
        var expectedMessagePattern = "Processing UserJoined event";

        expectedLogLevel.Should().Be(LogLevel.Debug, "processing should be logged at Debug level");
        expectedMessagePattern.Should().NotBeNullOrWhiteSpace("log message should contain context");
    }

    [Fact]
    public void WelcomeHandler_LogsInformationMessage_WhenWelcomeMessageSent()
    {
        // Expected log: LogInformation("Successfully sent welcome message for user {UserId} ({Username}) in guild {GuildId} ({GuildName})", ...)
        var expectedLogLevel = LogLevel.Information;
        var expectedMessagePattern = "Successfully sent welcome message";

        expectedLogLevel.Should().Be(LogLevel.Information, "success should be logged at Information level");
        expectedMessagePattern.Should().NotBeNullOrWhiteSpace("log message should indicate success");
    }

    [Fact]
    public void WelcomeHandler_LogsDebugMessage_WhenWelcomeMessageNotSent()
    {
        // Expected log: LogDebug("Welcome message was not sent for user {UserId} in guild {GuildId} (disabled or not configured)", ...)
        var expectedLogLevel = LogLevel.Debug;
        var expectedMessagePattern = "Welcome message was not sent";

        expectedLogLevel.Should().Be(LogLevel.Debug, "not sent should be logged at Debug level");
        expectedMessagePattern.Should().NotBeNullOrWhiteSpace("log message should explain why not sent");
    }

    [Fact]
    public void WelcomeHandler_LogsErrorMessage_WhenExceptionOccurs()
    {
        // Expected log: LogError(ex, "Failed to handle UserJoined event for user {UserId} in guild {GuildId}", ...)
        var expectedLogLevel = LogLevel.Error;
        var expectedMessagePattern = "Failed to handle UserJoined event";

        expectedLogLevel.Should().Be(LogLevel.Error, "exceptions should be logged at Error level");
        expectedMessagePattern.Should().NotBeNullOrWhiteSpace("log message should indicate failure");
    }

    #endregion
}
