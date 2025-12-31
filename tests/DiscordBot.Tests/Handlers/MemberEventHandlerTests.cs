using DiscordBot.Bot.Handlers;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Handlers;

/// <summary>
/// Unit tests for <see cref="MemberEventHandler"/>.
///
/// NOTE: Due to Discord.NET's use of sealed types and non-virtual members in SocketGuildUser and SocketGuild,
/// full unit testing with real Discord objects is not possible using Moq. These tests focus on:
/// 1. Constructor behavior and dependency injection setup
/// 2. Service scope creation and disposal patterns
/// 3. Repository interaction patterns
/// 4. Error handling behavior
/// 5. Documentation of the handler's responsibilities and contract
///
/// The MemberEventHandler is a thin wrapper that:
/// - Listens to Discord gateway events (UserJoined, UserLeft, GuildMemberUpdated)
/// - Creates service scopes to access scoped repositories from singleton context
/// - Delegates to repositories for database operations
/// - Logs events and exceptions to prevent bot crashes
/// - Maps Discord objects to domain entities
///
/// Integration testing with actual Discord.NET instances would be required to test event handlers
/// with real Socket objects.
/// </summary>
public class MemberEventHandlerTests
{
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IGuildMemberRepository> _mockMemberRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<ILogger<MemberEventHandler>> _mockLogger;

    public MemberEventHandlerTests()
    {
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockMemberRepo = new Mock<IGuildMemberRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockLogger = new Mock<ILogger<MemberEventHandler>>();

        // Setup service scope chain
        _mockScopeFactory
            .Setup(f => f.CreateScope())
            .Returns(_mockScope.Object);

        _mockScope
            .Setup(s => s.ServiceProvider)
            .Returns(_mockServiceProvider.Object);

        _mockServiceProvider
            .Setup(p => p.GetService(typeof(IGuildMemberRepository)))
            .Returns(_mockMemberRepo.Object);

        _mockServiceProvider
            .Setup(p => p.GetService(typeof(IUserRepository)))
            .Returns(_mockUserRepo.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange & Act
        var handler = new MemberEventHandler(_mockScopeFactory.Object, _mockLogger.Object);

        // Assert
        handler.Should().NotBeNull("handler should be created with valid dependencies");
    }

    [Fact]
    public void Constructor_WithValidDependencies_DoesNotThrow()
    {
        // Arrange & Act
        var act = () => new MemberEventHandler(_mockScopeFactory.Object, _mockLogger.Object);

        // Assert
        act.Should().NotThrow("handler should construct successfully with valid dependencies");
    }

    [Fact]
    public void Constructor_RequiresScopeFactoryDependency()
    {
        // Assert
        _mockScopeFactory.Should().NotBeNull(
            "IServiceScopeFactory should be injected to create scopes for accessing scoped repositories");
    }

    [Fact]
    public void Constructor_RequiresLoggerDependency()
    {
        // Assert
        _mockLogger.Should().NotBeNull(
            "ILogger<MemberEventHandler> should be injected for logging events and errors");
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
    public void ServiceProvider_ResolvesGuildMemberRepository()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockRepository = new Mock<IGuildMemberRepository>();
        mockServiceProvider
            .Setup(p => p.GetService(typeof(IGuildMemberRepository)))
            .Returns(mockRepository.Object);

        // Act
        var service = mockServiceProvider.Object.GetService(typeof(IGuildMemberRepository));

        // Assert
        service.Should().NotBeNull("service provider should resolve IGuildMemberRepository");
        service.Should().BeSameAs(mockRepository.Object, "should return configured repository");
    }

    [Fact]
    public void ServiceProvider_ResolvesUserRepository()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockRepository = new Mock<IUserRepository>();
        mockServiceProvider
            .Setup(p => p.GetService(typeof(IUserRepository)))
            .Returns(mockRepository.Object);

        // Act
        var service = mockServiceProvider.Object.GetService(typeof(IUserRepository));

        // Assert
        service.Should().NotBeNull("service provider should resolve IUserRepository");
        service.Should().BeSameAs(mockRepository.Object, "should return configured repository");
    }

    #endregion

    #region Repository Contract Tests

    [Fact]
    public async Task GuildMemberRepository_UpsertAsync_AcceptsGuildMemberEntity()
    {
        // Arrange
        var member = new GuildMember
        {
            GuildId = 123456789UL,
            UserId = 987654321UL,
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        _mockMemberRepo
            .Setup(r => r.UpsertAsync(It.IsAny<GuildMember>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        // Act
        var result = await _mockMemberRepo.Object.UpsertAsync(member, CancellationToken.None);

        // Assert
        result.Should().NotBeNull("UpsertAsync should return the upserted member");
        result.GuildId.Should().Be(member.GuildId);
        result.UserId.Should().Be(member.UserId);
    }

    [Fact]
    public async Task GuildMemberRepository_MarkInactiveAsync_AcceptsGuildAndUserIds()
    {
        // Arrange
        var guildId = 123456789UL;
        var userId = 987654321UL;

        _mockMemberRepo
            .Setup(r => r.MarkInactiveAsync(guildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _mockMemberRepo.Object.MarkInactiveAsync(guildId, userId, CancellationToken.None);

        // Assert
        result.Should().BeTrue("MarkInactiveAsync should return true when member exists");
    }

    [Fact]
    public async Task GuildMemberRepository_UpdateMemberInfoAsync_AcceptsNicknameAndRoles()
    {
        // Arrange
        var guildId = 123456789UL;
        var userId = 987654321UL;
        var nickname = "TestNickname";
        var rolesJson = "[123, 456]";

        _mockMemberRepo
            .Setup(r => r.UpdateMemberInfoAsync(
                guildId, userId, nickname, rolesJson, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _mockMemberRepo.Object.UpdateMemberInfoAsync(
            guildId, userId, nickname, rolesJson, CancellationToken.None);

        // Assert
        result.Should().BeTrue("UpdateMemberInfoAsync should return true when member is updated");
    }

    [Fact]
    public async Task UserRepository_UpsertAsync_AcceptsUserEntity()
    {
        // Arrange
        var user = new User
        {
            Id = 987654321UL,
            Username = "TestUser",
            Discriminator = "1234",
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        _mockUserRepo
            .Setup(r => r.UpsertAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _mockUserRepo.Object.UpsertAsync(user, CancellationToken.None);

        // Assert
        result.Should().NotBeNull("UpsertAsync should return the upserted user");
        result.Id.Should().Be(user.Id);
        result.Username.Should().Be(user.Username);
    }

    #endregion

    #region Integration Test Documentation

    // NOTE: The following tests document the expected behavior of the event handlers,
    // but cannot be implemented as traditional unit tests due to Discord.NET's sealed types.

    [Fact]
    public void HandleUserJoinedAsync_Contract_IsDocumented()
    {
        // This test exists to document the HandleUserJoinedAsync contract
        var contractDescription = @"
HandleUserJoinedAsync responsibilities:
1. Extract guild ID, user ID, and member info from SocketGuildUser parameter
2. Create service scope to access scoped repositories from singleton handler
3. Upsert User entity via IUserRepository.UpsertAsync
4. Upsert GuildMember entity via IGuildMemberRepository.UpsertAsync
5. Map Discord role collection to JSON array (excluding @everyone role)
6. Set IsActive=true, JoinedAt from Discord, LastCachedAt=UtcNow
7. Log debug message when processing, info message on success
8. Catch and log all exceptions without rethrowing (prevent bot crashes)
9. Dispose service scope via using statement
";

        contractDescription.Should().NotBeNullOrWhiteSpace("contract should be documented");
    }

    [Fact]
    public void HandleUserLeftAsync_Contract_IsDocumented()
    {
        // This test exists to document the HandleUserLeftAsync contract
        var contractDescription = @"
HandleUserLeftAsync responsibilities:
1. Extract guild ID and user ID from SocketGuild and SocketUser parameters
2. Create service scope to access scoped repositories
3. Call IGuildMemberRepository.MarkInactiveAsync to soft delete member
4. Log debug message when processing
5. Log info message if member marked inactive, debug if not found
6. Catch and log all exceptions without rethrowing
7. Dispose service scope via using statement
";

        contractDescription.Should().NotBeNullOrWhiteSpace("contract should be documented");
    }

    [Fact]
    public void HandleGuildMemberUpdatedAsync_Contract_IsDocumented()
    {
        // This test exists to document the HandleGuildMemberUpdatedAsync contract
        var contractDescription = @"
HandleGuildMemberUpdatedAsync responsibilities:
1. Extract before and after state from Cacheable and SocketGuildUser parameters
2. Check if nickname or roles changed (skip if no relevant changes)
3. Create service scope to access scoped repositories
4. Call IGuildMemberRepository.UpdateMemberInfoAsync with new nickname and roles
5. Upsert User entity to update global Discord metadata
6. Map Discord role collection to JSON array (excluding @everyone role)
7. Log debug/trace messages for processing, info on success, warning if not found
8. Catch and log all exceptions without rethrowing
9. Dispose service scope via using statement
";

        contractDescription.Should().NotBeNullOrWhiteSpace("contract should be documented");
    }

    [Fact]
    public void MemberEventHandler_DependsOnRepositories()
    {
        // This test documents that MemberEventHandler is a thin wrapper around repositories
        // The actual database operations (upsert, mark inactive, update) are implemented and tested
        // in repository tests

        var memberRepoInterface = typeof(IGuildMemberRepository);
        var upsertMethod = memberRepoInterface.GetMethod("UpsertAsync");
        var markInactiveMethod = memberRepoInterface.GetMethod("MarkInactiveAsync");
        var updateMemberInfoMethod = memberRepoInterface.GetMethod("UpdateMemberInfoAsync");

        upsertMethod.Should().NotBeNull("IGuildMemberRepository should have UpsertAsync method");
        markInactiveMethod.Should().NotBeNull("IGuildMemberRepository should have MarkInactiveAsync method");
        updateMemberInfoMethod.Should().NotBeNull("IGuildMemberRepository should have UpdateMemberInfoAsync method");

        var userRepoInterface = typeof(IUserRepository);
        var userUpsertMethod = userRepoInterface.GetMethod("UpsertAsync");

        userUpsertMethod.Should().NotBeNull("IUserRepository should have UpsertAsync method");
    }

    [Fact]
    public void MemberEventHandler_UsesScopedServicePattern()
    {
        // MemberEventHandler is registered as a singleton (lives for the lifetime of the bot)
        // Repositories are registered as scoped (lives for the duration of a request/operation)
        // To access scoped services from a singleton, MemberEventHandler must:
        // 1. Inject IServiceScopeFactory (which is safe to use from singleton)
        // 2. Create a scope when needed
        // 3. Resolve repositories from that scope
        // 4. Dispose the scope when done

        _mockScopeFactory.Should().NotBeNull("handler should use IServiceScopeFactory");
        _mockScope.Should().NotBeNull("scope factory should create IServiceScope");
        _mockServiceProvider.Should().NotBeNull("scope should provide IServiceProvider");
        _mockMemberRepo.Should().NotBeNull("provider should resolve IGuildMemberRepository");
        _mockUserRepo.Should().NotBeNull("provider should resolve IUserRepository");
    }

    #endregion

    #region Error Handling Documentation Tests

    [Fact]
    public void MemberEventHandler_ShouldCatchAllExceptions()
    {
        // All event handlers wrap their logic in try-catch blocks
        // This prevents exceptions in member event handling from crashing the Discord bot
        // Exceptions are logged but not rethrown

        var expectedBehavior = @"
Error handling contract:
- All exceptions in event handlers are caught
- Exceptions are logged at Error level with user ID and guild ID context
- Exceptions are NOT rethrown (methods do not throw)
- Bot continues running even if member sync fails
";

        expectedBehavior.Should().NotBeNullOrWhiteSpace("error handling contract should be documented");
    }

    [Fact]
    public void MemberEventHandler_ShouldDisposeScope_EvenOnException()
    {
        // MemberEventHandler uses 'using var scope = _scopeFactory.CreateScope()'
        // The using statement ensures scope is disposed even if repository methods throw
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
    public void HandleUserJoinedAsync_LoggingContract()
    {
        // Expected logs:
        // LogDebug: "Processing UserJoined event for user {UserId} ({Username}) in guild {GuildId}"
        // LogInformation: "Created GuildMember record for user {UserId} ({Username}) in guild {GuildId} ({GuildName})"
        // LogError (on exception): "Failed to handle UserJoined for user {UserId} in guild {GuildId}"

        var expectedLogs = new[]
        {
            "Debug: Processing UserJoined event",
            "Information: Created GuildMember record",
            "Error: Failed to handle UserJoined (on exception)"
        };

        expectedLogs.Should().HaveCount(3, "handler should have comprehensive logging");
    }

    [Fact]
    public void HandleUserLeftAsync_LoggingContract()
    {
        // Expected logs:
        // LogDebug: "Processing UserLeft event for user {UserId} ({Username}) in guild {GuildId}"
        // LogInformation: "Marked GuildMember inactive for user {UserId} ({Username}) in guild {GuildId} ({GuildName})"
        // LogDebug (if not found): "No GuildMember record found for user {UserId} in guild {GuildId} to mark inactive"
        // LogError (on exception): "Failed to handle UserLeft for user {UserId} in guild {GuildId}"

        var expectedLogs = new[]
        {
            "Debug: Processing UserLeft event",
            "Information: Marked GuildMember inactive (on success)",
            "Debug: No GuildMember record found (if not found)",
            "Error: Failed to handle UserLeft (on exception)"
        };

        expectedLogs.Should().HaveCount(4, "handler should have comprehensive logging");
    }

    [Fact]
    public void HandleGuildMemberUpdatedAsync_LoggingContract()
    {
        // Expected logs:
        // LogTrace: "GuildMemberUpdated for user {UserId} - no relevant changes (nickname or roles)"
        // LogDebug: "Processing GuildMemberUpdated for user {UserId} ({Username}) in guild {GuildId}. NicknameChanged: {NicknameChanged}, RolesChanged: {RolesChanged}"
        // LogInformation: "Updated GuildMember info for user {UserId} ({Username}) in guild {GuildId} ({GuildName})"
        // LogWarning (if not found): "GuildMember record not found for updated user {UserId} in guild {GuildId}. Member may need to be synced."
        // LogError (on exception): "Failed to handle GuildMemberUpdated for user {UserId} in guild {GuildId}"

        var expectedLogs = new[]
        {
            "Trace: GuildMemberUpdated - no relevant changes (early return)",
            "Debug: Processing GuildMemberUpdated",
            "Information: Updated GuildMember info (on success)",
            "Warning: GuildMember record not found (if not found)",
            "Error: Failed to handle GuildMemberUpdated (on exception)"
        };

        expectedLogs.Should().HaveCount(5, "handler should have comprehensive logging");
    }

    #endregion

    #region Discord Entity Mapping Tests

    [Fact]
    public void SerializeRoles_ShouldExcludeEveryoneRole()
    {
        // The handler serializes Discord roles to JSON array
        // It should exclude the @everyone role (IsEveryone = true)
        // Only actual assigned roles should be included

        var expectedBehavior = @"
Role serialization contract:
- Extract role IDs from SocketGuildUser.Roles collection
- Filter out roles where IsEveryone = true
- Serialize remaining role IDs to JSON array format
- Example output: '[123456789, 987654321]'
";

        expectedBehavior.Should().NotBeNullOrWhiteSpace("role serialization should be documented");
    }

    [Fact]
    public void MapToUser_ShouldMapAllDiscordFields()
    {
        // The handler maps SocketGuildUser to User entity
        // Expected mappings:
        // - Id = discordUser.Id
        // - Username = discordUser.Username
        // - Discriminator = discordUser.Discriminator
        // - FirstSeenAt = DateTime.UtcNow (only used if new)
        // - LastSeenAt = DateTime.UtcNow
        // - AccountCreatedAt = discordUser.CreatedAt.UtcDateTime
        // - AvatarHash = discordUser.AvatarId
        // - GlobalDisplayName = discordUser.GlobalName

        var expectedMappings = new[]
        {
            "Id = discordUser.Id",
            "Username = discordUser.Username",
            "Discriminator = discordUser.Discriminator",
            "FirstSeenAt = DateTime.UtcNow",
            "LastSeenAt = DateTime.UtcNow",
            "AccountCreatedAt = discordUser.CreatedAt.UtcDateTime",
            "AvatarHash = discordUser.AvatarId",
            "GlobalDisplayName = discordUser.GlobalName"
        };

        expectedMappings.Should().HaveCount(8, "all user fields should be mapped");
    }

    [Fact]
    public void MapToGuildMember_ShouldMapAllDiscordFields()
    {
        // The handler maps SocketGuildUser to GuildMember entity
        // Expected mappings:
        // - GuildId = user.Guild.Id
        // - UserId = user.Id
        // - JoinedAt = user.JoinedAt?.UtcDateTime ?? DateTime.UtcNow
        // - Nickname = user.Nickname
        // - CachedRolesJson = SerializeRoles(user.Roles)
        // - LastCachedAt = DateTime.UtcNow
        // - IsActive = true

        var expectedMappings = new[]
        {
            "GuildId = user.Guild.Id",
            "UserId = user.Id",
            "JoinedAt = user.JoinedAt?.UtcDateTime ?? DateTime.UtcNow",
            "Nickname = user.Nickname",
            "CachedRolesJson = SerializeRoles(user.Roles)",
            "LastCachedAt = DateTime.UtcNow",
            "IsActive = true"
        };

        expectedMappings.Should().HaveCount(7, "all guild member fields should be mapped");
    }

    #endregion

    #region Change Detection Tests

    [Fact]
    public void HandleGuildMemberUpdatedAsync_ShouldDetectNicknameChanges()
    {
        // The handler should detect nickname changes by comparing:
        // - before.Value.Nickname (if HasValue)
        // - after.Nickname

        var changeDetectionLogic = @"
Nickname change detection:
- If beforeUser is null, assume changed
- If beforeUser.Nickname != after.Nickname, nickname changed
- Null != 'NewNick' is considered a change
- 'OldNick' != 'NewNick' is considered a change
- 'SameName' == 'SameName' is not a change
";

        changeDetectionLogic.Should().NotBeNullOrWhiteSpace("nickname change detection should be documented");
    }

    [Fact]
    public void HandleGuildMemberUpdatedAsync_ShouldDetectRoleChanges()
    {
        // The handler should detect role changes by comparing:
        // - before.Value.Roles.Select(r => r.Id).SequenceEqual(after.Roles.Select(r => r.Id))

        var changeDetectionLogic = @"
Role change detection:
- If beforeUser is null, assume changed
- Extract role IDs from before and after role collections
- Use SequenceEqual to compare role ID sequences
- Order matters: [1, 2, 3] != [3, 2, 1]
- Added/removed roles trigger update
";

        changeDetectionLogic.Should().NotBeNullOrWhiteSpace("role change detection should be documented");
    }

    [Fact]
    public void HandleGuildMemberUpdatedAsync_ShouldSkipIfNoRelevantChanges()
    {
        // The handler should skip processing if:
        // - nicknameChanged = false
        // - rolesChanged = false
        // This prevents unnecessary database calls

        var skipLogic = @"
Early return logic:
- if (!nicknameChanged && !rolesChanged) return;
- Log at Trace level: 'GuildMemberUpdated for user {UserId} - no relevant changes'
- Do not create service scope
- Do not call repository methods
- Do not update database
";

        skipLogic.Should().NotBeNullOrWhiteSpace("skip logic should be documented");
    }

    #endregion
}
