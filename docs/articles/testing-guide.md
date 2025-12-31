# Testing Guide

**Last Updated:** 2025-12-30
**Version:** 1.0

---

## Overview

This guide provides comprehensive documentation on testing practices, patterns, and conventions used in the Discord Bot project. The project follows a thorough testing strategy with over 1,600 unit tests covering services, repositories, controllers, preconditions, and UI components.

### Testing Philosophy

- **Comprehensive Coverage**: Test all business logic, data access, and critical paths
- **Fast Feedback**: Unit tests should execute quickly to support rapid development
- **Isolation**: Tests should be independent and not rely on external dependencies
- **Clear Intent**: Test names and assertions should clearly communicate what is being tested
- **Maintainability**: Tests should be easy to understand and update as the codebase evolves

---

## Test Project Structure

The project uses a single test project that mirrors the source code organization:

```
tests/
└── DiscordBot.Tests/
    ├── Bot/                              # Bot-specific tests
    │   ├── Authorization/                # Authorization handlers, claims transformation
    │   ├── Extensions/                   # Identity seeder, service extensions
    │   ├── Pages/                        # Razor Pages (PageModel tests)
    │   ├── Services/                     # Bot services (Discord tokens, guilds, welcome)
    │   └── TagHelpers/                   # Tag helper tests
    ├── Commands/                         # Discord command module tests
    ├── Components/                       # Component ID builder tests
    ├── Controllers/                      # Web API controller tests
    ├── Core/                             # Core domain entity tests
    │   ├── Authorization/                # Roles, policies
    │   ├── Entities/                     # Entity model tests
    │   └── Utilities/                    # Utility function tests
    ├── Data/                             # Repository and data access tests
    │   ├── Interceptors/                 # EF Core interceptor tests
    │   └── Repositories/                 # Repository implementation tests
    ├── Handlers/                         # Event handler tests (welcome, interactions)
    ├── Hubs/                             # SignalR hub tests
    ├── Infrastructure/                   # Infrastructure-specific tests
    │   └── Data/Repositories/            # Infrastructure repository tests
    ├── Metrics/                          # Metrics collection tests
    ├── Middleware/                       # ASP.NET middleware tests
    ├── Preconditions/                    # Discord command precondition tests
    ├── Services/                         # Application service tests
    ├── TestHelpers/                      # Test utilities and helpers
    ├── Tracing/                          # Distributed tracing tests
    ├── Utilities/                        # Utility function tests
    └── ViewModels/                       # ViewModel tests
        └── Components/                   # Component ViewModel tests
```

---

## Testing Frameworks and Libraries

The project uses the following testing stack:

| Package | Version | Purpose |
|---------|---------|---------|
| **xUnit** | 2.5.3 | Test framework - supports `[Fact]` and `[Theory]` attributes |
| **FluentAssertions** | 8.8.0 | Assertion library - provides readable, expressive assertions |
| **Moq** | 4.20.72 | Mocking framework - creates test doubles for dependencies |
| **Microsoft.EntityFrameworkCore.Sqlite** | 8.0.0 | In-memory database for integration tests |
| **Microsoft.EntityFrameworkCore.InMemory** | 8.0.11 | Alternative in-memory database provider |
| **Microsoft.Extensions.Diagnostics.Testing** | 8.0.0 | Testing utilities for diagnostics and metrics |
| **coverlet.collector** | 6.0.0 | Code coverage collection |

---

## Running Tests

### Run All Tests

```bash
# Build and run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity normal

# Run tests with minimal output
dotnet test --verbosity quiet
```

### Run Specific Tests

```bash
# Run tests in a specific namespace
dotnet test --filter "FullyQualifiedName~DiscordBot.Tests.Services"

# Run a single test class
dotnet test --filter "FullyQualifiedName~UserRepositoryTests"

# Run a single test method
dotnet test --filter "FullyQualifiedName~UserRepositoryTests.GetByDiscordIdAsync_WithExistingUser_ReturnsUser"

# Run tests by category/trait (if using [Trait] attribute)
dotnet test --filter "Category=Integration"

# Run tests matching a display name pattern
dotnet test --filter "DisplayName~Repository"
```

### Code Coverage

```bash
# Run tests with code coverage collection
dotnet test --collect:"XPlat Code Coverage"

# Coverage results will be in:
# tests/DiscordBot.Tests/TestResults/{guid}/coverage.cobertura.xml

# Generate HTML coverage report (requires ReportGenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"tests/**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
```

### Continuous Testing

```bash
# Watch mode - automatically re-run tests on file changes
dotnet watch test --project tests/DiscordBot.Tests
```

---

## Test Categories

### Unit Tests

Unit tests validate individual components in isolation using mocks for dependencies.

**Characteristics:**
- Fast execution (milliseconds per test)
- No external dependencies (database, network, file system)
- Uses Moq to create test doubles
- Tests a single method or behavior

**Coverage:**
- Services (business logic)
- Controllers (HTTP request handling)
- ViewModels (data transformation)
- Utilities (helper functions)
- Preconditions (Discord command authorization)
- Validation logic

### Integration Tests

Integration tests validate interactions between components, typically involving database operations.

**Characteristics:**
- Slower execution than unit tests (but still fast)
- Uses in-memory SQLite database
- Tests database queries, migrations, and data persistence
- Validates repository implementations

**Coverage:**
- Repository implementations
- Entity Framework queries
- Database constraints and relationships
- Data access patterns

### System Tests

The project currently does not have dedicated end-to-end system tests. UI testing is performed manually and through prototypes in `docs/prototypes/`.

---

## Naming Conventions

### Test Class Naming

Test classes follow the pattern: `{ClassUnderTest}Tests`

```csharp
// Testing UserRepository
public class UserRepositoryTests : IDisposable

// Testing GuildsController
public class GuildsControllerTests

// Testing CommandAnalyticsService
public class CommandAnalyticsServiceTests
```

### Test Method Naming

Test methods use descriptive names that follow the pattern:
`{MethodName}_{Scenario}_{ExpectedBehavior}`

```csharp
[Fact]
public async Task GetByDiscordIdAsync_WithExistingUser_ReturnsUser()

[Fact]
public async Task GetByDiscordIdAsync_WithNonExistentUser_ReturnsNull()

[Fact]
public async Task UpdateGuild_WithNullRequest_ShouldReturnBadRequest()

[Fact]
public void CreateState_ReturnsUniqueCorrelationId()
```

---

## Writing Unit Tests

### Basic Test Structure

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="CommandAnalyticsService"/>.
/// </summary>
public class CommandAnalyticsServiceTests
{
    // Dependencies (mocks)
    private readonly Mock<ICommandLogRepository> _mockCommandLogRepository;
    private readonly Mock<ILogger<CommandAnalyticsService>> _mockLogger;

    // System under test
    private readonly CommandAnalyticsService _service;

    public CommandAnalyticsServiceTests()
    {
        // Arrange - Create mocks
        _mockCommandLogRepository = new Mock<ICommandLogRepository>();
        _mockLogger = new Mock<ILogger<CommandAnalyticsService>>();

        // Create the service with mocked dependencies
        _service = new CommandAnalyticsService(
            _mockCommandLogRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetUsageOverTimeAsync_ShouldReturnDataFromRepository()
    {
        // Arrange
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var expectedData = new List<UsageOverTimeDto>
        {
            new() { Date = start, Count = 10 },
            new() { Date = start.AddDays(1), Count = 15 }
        };

        _mockCommandLogRepository
            .Setup(r => r.GetUsageOverTimeAsync(start, end, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedData);

        // Act
        var result = await _service.GetUsageOverTimeAsync(start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2, "there are 2 data points");
        result.Should().BeEquivalentTo(expectedData);
    }
}
```

### AAA Pattern (Arrange-Act-Assert)

All tests follow the AAA pattern with clear separation:

```csharp
[Fact]
public async Task UpdateGuild_WithValidRequest_ShouldReturnOkWithUpdatedGuild()
{
    // Arrange - Set up test data and mock behavior
    const ulong guildId = 123456789UL;
    var request = new GuildUpdateRequestDto
    {
        Prefix = "?",
        Settings = "{\"feature\":true}",
        IsActive = false
    };

    var updatedGuild = new GuildDto { /* ... */ };

    _mockGuildService
        .Setup(s => s.UpdateGuildAsync(guildId, request, It.IsAny<CancellationToken>()))
        .ReturnsAsync(updatedGuild);

    // Act - Execute the method under test
    var result = await _controller.UpdateGuild(guildId, request, CancellationToken.None);

    // Assert - Verify the results
    result.Should().NotBeNull();
    result.Result.Should().BeOfType<OkObjectResult>();

    var okResult = result.Result as OkObjectResult;
    var guildDto = okResult!.Value as GuildDto;
    guildDto!.Prefix.Should().Be("?");
}
```

---

## Mocking with Moq

### Basic Mock Setup

```csharp
// Create a mock
var mockRepository = new Mock<IUserRepository>();

// Setup a method to return a specific value
mockRepository
    .Setup(r => r.GetByDiscordIdAsync(123456789))
    .ReturnsAsync(new User { Id = 123456789, Username = "TestUser" });

// Setup a method with parameter matching
mockRepository
    .Setup(r => r.GetByDiscordIdAsync(It.IsAny<ulong>()))
    .ReturnsAsync((User?)null);

// Setup a method to throw an exception
mockRepository
    .Setup(r => r.GetByDiscordIdAsync(999999999))
    .ThrowsAsync(new InvalidOperationException("User not found"));
```

### Verifying Mock Interactions

```csharp
// Verify a method was called once
_mockGuildService.Verify(
    s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()),
    Times.Once,
    "service should be called exactly once");

// Verify a method was never called
_mockGuildService.Verify(
    s => s.UpdateGuildAsync(It.IsAny<ulong>(), It.IsAny<GuildUpdateRequestDto>(), It.IsAny<CancellationToken>()),
    Times.Never,
    "service should not be called when request is null");

// Verify with specific parameters
_mockCommandLogRepository.Verify(
    r => r.GetUsageOverTimeAsync(start, end, null, cancellationToken),
    Times.Once,
    "cancellation token should be passed to repository");
```

### Mocking Logger

```csharp
// Verify a log message was written
_mockLogger.Verify(
    l => l.Log(
        LogLevel.Information,
        It.IsAny<EventId>(),
        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Guild") && v.ToString()!.Contains("sync requested")),
        It.IsAny<Exception>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
    Times.Once,
    "an information log should be written when guild sync is requested");
```

---

## Integration Tests with Database

### Using TestDbContextFactory

The project provides a `TestDbContextFactory` helper for creating in-memory SQLite databases:

```csharp
using DiscordBot.Tests.TestHelpers;

public class UserRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly UserRepository _repository;

    public UserRepositoryTests()
    {
        // Create in-memory database
        (_context, _connection) = TestDbContextFactory.CreateContext();

        // Create repository with real DbContext
        _repository = new UserRepository(_context, mockLogger.Object, mockBaseLogger.Object);
    }

    public void Dispose()
    {
        // Clean up resources
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetByDiscordIdAsync_WithExistingUser_ReturnsUser()
    {
        // Arrange - Add test data to database
        var user = new User
        {
            Id = 987654321,
            Username = "TestUser",
            Discriminator = "1234",
            FirstSeenAt = DateTime.UtcNow.AddDays(-30),
            LastSeenAt = DateTime.UtcNow
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act - Query the database
        var result = await _repository.GetByDiscordIdAsync(987654321);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(987654321);
        result.Username.Should().Be("TestUser");
    }
}
```

### Test Lifecycle Management

Integration tests that use database resources implement `IDisposable`:

```csharp
public class CommandLogRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;

    public CommandLogRepositoryTests()
    {
        // Setup runs before each test
        (_context, _connection) = TestDbContextFactory.CreateContext();
    }

    public void Dispose()
    {
        // Cleanup runs after each test
        _context.Dispose();
        _connection.Dispose();
    }
}
```

---

## Testing Patterns

### Testing Controllers

```csharp
public class GuildsControllerTests
{
    private readonly Mock<IGuildService> _mockGuildService;
    private readonly Mock<ILogger<GuildsController>> _mockLogger;
    private readonly GuildsController _controller;

    public GuildsControllerTests()
    {
        _mockGuildService = new Mock<IGuildService>();
        _mockLogger = new Mock<ILogger<GuildsController>>();
        _controller = new GuildsController(_mockGuildService.Object, _mockLogger.Object);

        // Setup HttpContext for TraceIdentifier and other HTTP features
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task GetAllGuilds_ShouldReturnOkWithGuildList()
    {
        // Arrange
        var guilds = new List<GuildDto> { /* test data */ }.AsReadOnly();
        _mockGuildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act
        var result = await _controller.GetAllGuilds(CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var guildList = okResult!.Value as IReadOnlyList<GuildDto>;
        guildList.Should().HaveCount(2);
    }
}
```

### Testing Services

```csharp
public class InteractionStateServiceTests
{
    private readonly Mock<ILogger<InteractionStateService>> _mockLogger;
    private readonly Mock<IOptions<CachingOptions>> _mockCachingOptions;
    private readonly InteractionStateService _service;

    public InteractionStateServiceTests()
    {
        _mockLogger = new Mock<ILogger<InteractionStateService>>();
        _mockCachingOptions = new Mock<IOptions<CachingOptions>>();
        _mockCachingOptions.Setup(x => x.Value).Returns(new CachingOptions());
        _service = new InteractionStateService(_mockLogger.Object, _mockCachingOptions.Object);
    }

    [Fact]
    public void CreateState_ReturnsUniqueCorrelationId()
    {
        // Arrange
        const ulong userId = 123456789UL;
        var data = new TestStateData { Message = "Test data" };

        // Act
        var correlationId1 = _service.CreateState(userId, data);
        var correlationId2 = _service.CreateState(userId, data);

        // Assert
        correlationId1.Should().NotBe(correlationId2,
            "each CreateState call should generate a unique correlation ID");
    }
}
```

### Testing Discord Preconditions

```csharp
public class RequireAdminAttributeTests
{
    private readonly Mock<IInteractionContext> _mockContext;
    private readonly Mock<ICommandInfo> _mockCommandInfo;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly RequireAdminAttribute _attribute;

    [Fact]
    public async Task CheckRequirementsAsync_WhenUserHasAdministratorPermission_ShouldReturnSuccess()
    {
        // Arrange
        var mockGuild = new Mock<IGuild>();
        mockGuild.Setup(g => g.Id).Returns(123456789UL);

        var guildPermissions = new GuildPermissions(administrator: true);
        var mockGuildUser = new Mock<IGuildUser>();
        mockGuildUser.Setup(u => u.GuildPermissions).Returns(guildPermissions);

        _mockContext.Setup(c => c.Guild).Returns(mockGuild.Object);
        _mockContext.Setup(c => c.User).Returns(mockGuildUser.Object);

        // Act
        var result = await _attribute.CheckRequirementsAsync(
            _mockContext.Object,
            _mockCommandInfo.Object,
            _mockServiceProvider.Object);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue("the user has Administrator permission");
        result.ErrorReason.Should().BeNull();
    }
}
```

### Testing with Theory and InlineData

Use `[Theory]` with `[InlineData]` for parameterized tests:

```csharp
[Theory]
[InlineData(null)]
[InlineData("")]
[InlineData("   ")]
public void TryGetState_WithNullOrEmptyCorrelationId_ReturnsFalse(string? correlationId)
{
    // Act
    var success = _service.TryGetState<TestStateData>(correlationId!, out var retrievedData);

    // Assert
    success.Should().BeFalse("null or empty correlation ID should return false");
    retrievedData.Should().BeNull();
}
```

---

## FluentAssertions Best Practices

### Common Assertions

```csharp
// Null checks
result.Should().NotBeNull();
result.Should().BeNull();

// Type assertions
result.Should().BeOfType<OkObjectResult>();
result.Should().BeAssignableTo<IReadOnlyList<GuildDto>>();

// Equality
user.Id.Should().Be(123456789);
user.Username.Should().Be("TestUser");

// Collections
list.Should().HaveCount(3);
list.Should().BeEmpty();
list.Should().NotBeEmpty();
list.Should().Contain(x => x.Id == 123);
list.Should().NotContain(x => x.Id == 999);

// Object equivalence (deep comparison)
result.Should().BeEquivalentTo(expectedData);

// String assertions
message.Should().Contain("error");
message.Should().StartWith("Error:");
message.Should().Be("exact match");

// Boolean assertions
result.IsSuccess.Should().BeTrue("the operation succeeded");
result.IsSuccess.Should().BeFalse("the user lacks permissions");

// Numeric assertions
count.Should().BeGreaterThan(0);
count.Should().BeLessThan(100);
average.Should().BeApproximately(82.2, 0.1);

// DateTime assertions
timestamp.Should().BeAfter(startTime);
timestamp.Should().BeBefore(endTime);
timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
```

### Assertion Messages

Always provide clear "because" messages for complex assertions:

```csharp
result.Should().NotBeNull("the user should be found in the database");
count.Should().Be(3, "exactly 3 items should be returned");
success.Should().BeTrue("the state should be successfully retrieved");
```

---

## Testing Async Code

### Async Test Methods

```csharp
[Fact]
public async Task GetByDiscordIdAsync_WithExistingUser_ReturnsUser()
{
    // Arrange
    var user = new User { Id = 987654321, Username = "TestUser" };
    await _context.Users.AddAsync(user);
    await _context.SaveChangesAsync();

    // Act
    var result = await _repository.GetByDiscordIdAsync(987654321);

    // Assert
    result.Should().NotBeNull();
}
```

### Testing CancellationToken Propagation

```csharp
[Fact]
public async Task GetAllGuilds_WithCancellationToken_ShouldPassToService()
{
    // Arrange
    var cancellationTokenSource = new CancellationTokenSource();
    var cancellationToken = cancellationTokenSource.Token;

    _mockGuildService
        .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<GuildDto>().AsReadOnly());

    // Act
    await _controller.GetAllGuilds(cancellationToken);

    // Assert
    _mockGuildService.Verify(
        s => s.GetAllGuildsAsync(cancellationToken),
        Times.Once,
        "the cancellation token should be passed to the service");
}
```

---

## Test Data Management

### Test Data Builders

Use helper methods to create consistent test data:

```csharp
private static User CreateTestUser(ulong id = 123456789, string username = "TestUser")
{
    return new User
    {
        Id = id,
        Username = username,
        Discriminator = "1234",
        FirstSeenAt = DateTime.UtcNow.AddDays(-30),
        LastSeenAt = DateTime.UtcNow
    };
}

[Fact]
public async Task TestMethod()
{
    var user = CreateTestUser(987654321, "CustomUser");
    // ...
}
```

### Test Helper Classes

Define test-specific classes for complex scenarios:

```csharp
private class TestStateData
{
    public string Message { get; set; } = string.Empty;
    public int Value { get; set; }
}

[Fact]
public void CreateState_WithDifferentDataTypes_WorksCorrectly()
{
    var complexData = new TestStateData { Message = "Complex", Value = 100 };
    var complexId = _service.CreateState(userId, complexData);
    // ...
}
```

---

## Edge Cases and Error Handling

### Testing Null and Empty Inputs

```csharp
[Fact]
public async Task UpdateGuild_WithNullRequest_ShouldReturnBadRequest()
{
    // Act
    var result = await _controller.UpdateGuild(guildId, null!, CancellationToken.None);

    // Assert
    result.Result.Should().BeOfType<BadRequestObjectResult>();
}

[Theory]
[InlineData(null)]
[InlineData("")]
[InlineData("   ")]
public void Method_WithInvalidInput_HandlesCorrectly(string? input)
{
    // Test handling of null, empty, and whitespace
}
```

### Testing Boundary Conditions

```csharp
[Fact]
public async Task GetRecentlyActiveAsync_ExcludesInactiveUsers()
{
    // Arrange - Create users at the boundary of the time window
    var activeUser = CreateUser(lastSeen: now.AddMinutes(-5));      // Inside window
    var inactiveUser = CreateUser(lastSeen: now.AddHours(-25));     // Outside window

    // Act - Get users active in the last 24 hours
    var result = await _repository.GetRecentlyActiveAsync(TimeSpan.FromHours(24));

    // Assert
    result.Should().Contain(u => u.Id == activeUser.Id);
    result.Should().NotContain(u => u.Id == inactiveUser.Id);
}
```

---

## Common Testing Scenarios

### Testing Repository Methods

```csharp
[Fact]
public async Task UpsertAsync_WithNewUser_CreatesUser()
{
    // Arrange
    var newUser = new User { Id = 987654321, Username = "NewUser" };

    // Act
    var result = await _repository.UpsertAsync(newUser);

    // Assert
    result.Should().NotBeNull();
    result.Username.Should().Be("NewUser");

    // Verify it was actually saved to the database
    var savedUser = await _context.Users.FindAsync(987654321UL);
    savedUser.Should().NotBeNull();
    savedUser!.Username.Should().Be("NewUser");
}
```

### Testing Configuration Binding

```csharp
[Fact]
public void Bind_ShouldPopulateFromConfiguration()
{
    // Arrange
    var configData = new Dictionary<string, string?>
    {
        { "Discord:Token", "my-bot-token" },
        { "Discord:TestGuildId", "987654321098765432" }
    };

    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(configData)
        .Build();

    var botConfig = new BotConfiguration();

    // Act
    configuration.GetSection(BotConfiguration.SectionName).Bind(botConfig);

    // Assert
    botConfig.Token.Should().Be("my-bot-token");
    botConfig.TestGuildId.Should().Be(987654321098765432);
}
```

### Testing Entity Detachment

When testing bulk updates with `ExecuteUpdateAsync`, detach entities to see changes:

```csharp
[Fact]
public async Task UpdateLastSeenAsync_UpdatesTimestamp()
{
    // Arrange
    var user = CreateTestUser();
    await _context.Users.AddAsync(user);
    await _context.SaveChangesAsync();

    // Act
    await _repository.UpdateLastSeenAsync(user.Id);

    // Assert
    // Detach the tracked entity and reload from database to see ExecuteUpdateAsync changes
    _context.Entry(user).State = EntityState.Detached;
    var updatedUser = await _context.Users.FindAsync(user.Id);
    updatedUser!.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
}
```

---

## Performance Testing Considerations

While the project doesn't have dedicated performance tests, consider these practices:

### Avoid Slow Tests

```csharp
// BAD: Long Thread.Sleep calls
Thread.Sleep(5000); // 5 seconds per test!

// GOOD: Minimal delays only when necessary
Thread.Sleep(50); // Just enough to verify expiration
```

### Test Data Volume

```csharp
// Consider performance when testing with large datasets
[Fact]
public async Task GetAllAsync_WithLargeDataset_PerformsEfficiently()
{
    // Create 1000 test records
    var users = Enumerable.Range(1, 1000)
        .Select(i => CreateTestUser((ulong)i))
        .ToList();

    await _context.Users.AddRangeAsync(users);
    await _context.SaveChangesAsync();

    // Act
    var stopwatch = Stopwatch.StartNew();
    var result = await _repository.GetAllAsync();
    stopwatch.Stop();

    // Assert
    result.Should().HaveCount(1000);
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(500,
        "query should complete in under 500ms");
}
```

---

## Troubleshooting Common Test Issues

### Test Isolation Issues

**Problem:** Tests pass individually but fail when run together.

**Solution:** Ensure proper cleanup in `Dispose()` and avoid shared state:

```csharp
public void Dispose()
{
    _context.Dispose();
    _connection.Dispose();
    GC.SuppressFinalize(this);
}
```

### Async/Await Issues

**Problem:** Test hangs or deadlocks.

**Solution:** Always use `async Task` for async tests, never `async void`:

```csharp
// GOOD
[Fact]
public async Task MyAsyncTest()

// BAD
[Fact]
public async void MyAsyncTest() // Don't use void!
```

### Mock Setup Issues

**Problem:** Mock returns null or default value unexpectedly.

**Solution:** Verify setup matches the actual call signature:

```csharp
// Setup must match the actual call exactly
_mockRepository
    .Setup(r => r.GetByIdAsync(It.IsAny<ulong>()))  // Wrong parameter type
    .ReturnsAsync(user);

// Correct
_mockRepository
    .Setup(r => r.GetByIdAsync(It.IsAny<long>()))   // Correct parameter type
    .ReturnsAsync(user);
```

### DateTime Comparison Issues

**Problem:** DateTime assertions fail due to precision.

**Solution:** Use `BeCloseTo` instead of exact equality:

```csharp
// Instead of exact equality
result.Timestamp.Should().Be(DateTime.UtcNow); // Often fails!

// Use tolerance
result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
```

---

## Continuous Integration

Tests are automatically run in CI/CD pipelines. Ensure:

- All tests pass locally before pushing
- Tests complete in reasonable time (under 5 minutes for full suite)
- No tests depend on external services or specific machine configuration
- Test database connections are properly disposed

---

## Best Practices Summary

1. **Follow AAA Pattern**: Arrange, Act, Assert with clear separation
2. **Use Descriptive Names**: Test names should explain what is being tested
3. **Test One Thing**: Each test should verify a single behavior
4. **Mock External Dependencies**: Isolate the system under test
5. **Use FluentAssertions**: Provides clear, readable assertions
6. **Dispose Resources**: Always implement `IDisposable` for integration tests
7. **Avoid Test Interdependence**: Tests should not rely on execution order
8. **Use Appropriate Test Types**: Unit tests for logic, integration tests for data access
9. **Assert Behavior, Not Implementation**: Test what the code does, not how
10. **Keep Tests Maintainable**: Refactor test code just like production code

---

## Additional Resources

- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [Moq Documentation](https://github.com/moq/moq4)
- [Entity Framework Core Testing](https://learn.microsoft.com/en-us/ef/core/testing/)
- [ASP.NET Core Integration Tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)

---

## Related Documentation

- [Repository Pattern](repository-pattern.md) - Data access patterns and repository interfaces
- [Database Schema](database-schema.md) - Entity models and relationships
- [API Endpoints](api-endpoints.md) - REST API controller documentation
- [Authorization Policies](authorization-policies.md) - Security and access control testing
