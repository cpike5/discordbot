using DiscordBot.Core.Entities;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Infrastructure.Data.Repositories;

/// <summary>
/// Unit tests for WelcomeConfigurationRepository.
/// </summary>
public class WelcomeConfigurationRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly WelcomeConfigurationRepository _repository;
    private readonly Mock<ILogger<WelcomeConfigurationRepository>> _mockLogger;
    private readonly Mock<ILogger<Repository<WelcomeConfiguration>>> _mockBaseLogger;

    public WelcomeConfigurationRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<WelcomeConfigurationRepository>>();
        _mockBaseLogger = new Mock<ILogger<Repository<WelcomeConfiguration>>>();
        _repository = new WelcomeConfigurationRepository(_context, _mockLogger.Object, _mockBaseLogger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// Helper method to create a guild entity for tests.
    /// Required because WelcomeConfiguration has a foreign key constraint to Guild.
    /// </summary>
    private async Task<Guild> CreateGuildAsync(ulong guildId, string name = "Test Guild")
    {
        var guild = new Guild
        {
            Id = guildId,
            Name = name,
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };
        await _context.Guilds.AddAsync(guild);
        await _context.SaveChangesAsync();
        return guild;
    }

    [Fact]
    public async Task GetByGuildIdAsync_WithExistingConfiguration_ReturnsConfiguration()
    {
        // Arrange
        var guildId = 123456789UL;
        await CreateGuildAsync(guildId);

        var configuration = new WelcomeConfiguration
        {
            GuildId = guildId,
            IsEnabled = true,
            WelcomeChannelId = 987654321UL,
            WelcomeMessage = "Welcome {user} to {guild}!",
            IncludeAvatar = true,
            UseEmbed = true,
            EmbedColor = "#5865F2",
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            UpdatedAt = DateTime.UtcNow
        };
        await _context.WelcomeConfigurations.AddAsync(configuration);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByGuildIdAsync(guildId);

        // Assert
        result.Should().NotBeNull();
        result!.GuildId.Should().Be(guildId);
        result.IsEnabled.Should().BeTrue();
        result.WelcomeChannelId.Should().Be(987654321UL);
        result.WelcomeMessage.Should().Be("Welcome {user} to {guild}!");
        result.IncludeAvatar.Should().BeTrue();
        result.UseEmbed.Should().BeTrue();
        result.EmbedColor.Should().Be("#5865F2");
    }

    [Fact]
    public async Task GetByGuildIdAsync_WithNonExistentConfiguration_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByGuildIdAsync(999999999UL);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByGuildIdAsync_WithValidCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var guildId = 123456789UL;
        await CreateGuildAsync(guildId);

        var configuration = new WelcomeConfiguration
        {
            GuildId = guildId,
            IsEnabled = true,
            WelcomeMessage = "Test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.WelcomeConfigurations.AddAsync(configuration);
        await _context.SaveChangesAsync();

        var cancellationTokenSource = new CancellationTokenSource();

        // Act
        var result = await _repository.GetByGuildIdAsync(guildId, cancellationTokenSource.Token);

        // Assert
        result.Should().NotBeNull();
        result!.GuildId.Should().Be(guildId);
        result.WelcomeMessage.Should().Be("Test");
    }

    [Fact]
    public async Task AddAsync_CreatesConfiguration()
    {
        // Arrange
        var guildId = 123456789UL;
        await CreateGuildAsync(guildId);

        var configuration = new WelcomeConfiguration
        {
            GuildId = guildId,
            IsEnabled = true,
            WelcomeChannelId = 987654321UL,
            WelcomeMessage = "Welcome {user}!",
            IncludeAvatar = true,
            UseEmbed = true,
            EmbedColor = "#00FF00",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = await _repository.AddAsync(configuration);

        // Assert
        result.Should().NotBeNull();
        result.GuildId.Should().Be(guildId);
        result.WelcomeMessage.Should().Be("Welcome {user}!");

        // Verify it was saved to the database
        var savedConfiguration = await _context.WelcomeConfigurations.FindAsync(guildId);
        savedConfiguration.Should().NotBeNull();
        savedConfiguration!.GuildId.Should().Be(guildId);
        savedConfiguration.WelcomeMessage.Should().Be("Welcome {user}!");
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingConfiguration()
    {
        // Arrange
        var guildId = 123456789UL;
        await CreateGuildAsync(guildId);

        var originalConfiguration = new WelcomeConfiguration
        {
            GuildId = guildId,
            IsEnabled = false,
            WelcomeChannelId = 111111111UL,
            WelcomeMessage = "Original message",
            IncludeAvatar = false,
            UseEmbed = false,
            EmbedColor = null,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        await _context.WelcomeConfigurations.AddAsync(originalConfiguration);
        await _context.SaveChangesAsync();

        // Detach the entity to simulate a fresh update
        _context.Entry(originalConfiguration).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var updatedConfiguration = new WelcomeConfiguration
        {
            GuildId = guildId,
            IsEnabled = true,
            WelcomeChannelId = 999999999UL,
            WelcomeMessage = "Updated message with {user} and {guild}",
            IncludeAvatar = true,
            UseEmbed = true,
            EmbedColor = "#FF0000",
            CreatedAt = originalConfiguration.CreatedAt, // CreatedAt should not change
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        await _repository.UpdateAsync(updatedConfiguration);

        // Assert
        // Verify the changes were persisted
        var savedConfiguration = await _context.WelcomeConfigurations.FindAsync(guildId);
        savedConfiguration.Should().NotBeNull();
        savedConfiguration!.GuildId.Should().Be(guildId);
        savedConfiguration.IsEnabled.Should().BeTrue();
        savedConfiguration.WelcomeChannelId.Should().Be(999999999UL);
        savedConfiguration.WelcomeMessage.Should().Be("Updated message with {user} and {guild}");
        savedConfiguration.IncludeAvatar.Should().BeTrue();
        savedConfiguration.UseEmbed.Should().BeTrue();
        savedConfiguration.EmbedColor.Should().Be("#FF0000");
    }

    [Fact]
    public async Task DeleteAsync_RemovesConfiguration()
    {
        // Arrange
        var guildId = 123456789UL;
        await CreateGuildAsync(guildId);

        var configuration = new WelcomeConfiguration
        {
            GuildId = guildId,
            IsEnabled = true,
            WelcomeMessage = "Test message",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.WelcomeConfigurations.AddAsync(configuration);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(configuration);

        // Assert
        var deletedConfiguration = await _context.WelcomeConfigurations.FindAsync(guildId);
        deletedConfiguration.Should().BeNull();
    }

    [Fact]
    public async Task GetByGuildIdAsync_WithDisabledConfiguration_ReturnsConfiguration()
    {
        // Arrange
        var guildId = 123456789UL;
        await CreateGuildAsync(guildId);

        var configuration = new WelcomeConfiguration
        {
            GuildId = guildId,
            IsEnabled = false, // Explicitly disabled
            WelcomeChannelId = null,
            WelcomeMessage = string.Empty,
            IncludeAvatar = false,
            UseEmbed = false,
            EmbedColor = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.WelcomeConfigurations.AddAsync(configuration);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByGuildIdAsync(guildId);

        // Assert
        result.Should().NotBeNull();
        result!.GuildId.Should().Be(guildId);
        result.IsEnabled.Should().BeFalse();
        result.WelcomeChannelId.Should().BeNull();
    }

    [Fact]
    public async Task GetByGuildIdAsync_WithNullEmbedColor_ReturnsConfiguration()
    {
        // Arrange
        var guildId = 123456789UL;
        await CreateGuildAsync(guildId);

        var configuration = new WelcomeConfiguration
        {
            GuildId = guildId,
            IsEnabled = true,
            WelcomeChannelId = 987654321UL,
            WelcomeMessage = "Welcome!",
            IncludeAvatar = true,
            UseEmbed = true,
            EmbedColor = null, // Null color should use Discord default
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.WelcomeConfigurations.AddAsync(configuration);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByGuildIdAsync(guildId);

        // Assert
        result.Should().NotBeNull();
        result!.GuildId.Should().Be(guildId);
        result.EmbedColor.Should().BeNull();
        result.UseEmbed.Should().BeTrue();
    }

    [Fact]
    public async Task GetByGuildIdAsync_WithEmptyWelcomeMessage_ReturnsConfiguration()
    {
        // Arrange
        var guildId = 123456789UL;
        await CreateGuildAsync(guildId);

        var configuration = new WelcomeConfiguration
        {
            GuildId = guildId,
            IsEnabled = true,
            WelcomeChannelId = 987654321UL,
            WelcomeMessage = string.Empty, // Empty message
            IncludeAvatar = true,
            UseEmbed = false,
            EmbedColor = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.WelcomeConfigurations.AddAsync(configuration);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByGuildIdAsync(guildId);

        // Assert
        result.Should().NotBeNull();
        result!.GuildId.Should().Be(guildId);
        result.WelcomeMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByGuildIdAsync_WithLongWelcomeMessage_ReturnsConfiguration()
    {
        // Arrange
        var guildId = 123456789UL;
        await CreateGuildAsync(guildId);

        var longMessage = new string('A', 1000); // Long message with 1000 characters
        var configuration = new WelcomeConfiguration
        {
            GuildId = guildId,
            IsEnabled = true,
            WelcomeChannelId = 987654321UL,
            WelcomeMessage = longMessage,
            IncludeAvatar = true,
            UseEmbed = true,
            EmbedColor = "#123456",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.WelcomeConfigurations.AddAsync(configuration);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByGuildIdAsync(guildId);

        // Assert
        result.Should().NotBeNull();
        result!.GuildId.Should().Be(guildId);
        result.WelcomeMessage.Should().HaveLength(1000);
        result.WelcomeMessage.Should().Be(longMessage);
    }

    [Fact]
    public async Task GetByGuildIdAsync_WithMultipleConfigurations_ReturnsCorrectConfiguration()
    {
        // Arrange
        await CreateGuildAsync(111111111UL, "Guild 1");
        await CreateGuildAsync(222222222UL, "Guild 2");
        await CreateGuildAsync(333333333UL, "Guild 3");

        var configuration1 = new WelcomeConfiguration
        {
            GuildId = 111111111UL,
            IsEnabled = true,
            WelcomeMessage = "Guild 1 message",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var configuration2 = new WelcomeConfiguration
        {
            GuildId = 222222222UL,
            IsEnabled = false,
            WelcomeMessage = "Guild 2 message",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var configuration3 = new WelcomeConfiguration
        {
            GuildId = 333333333UL,
            IsEnabled = true,
            WelcomeMessage = "Guild 3 message",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.WelcomeConfigurations.AddRangeAsync(configuration1, configuration2, configuration3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByGuildIdAsync(222222222UL);

        // Assert
        result.Should().NotBeNull();
        result!.GuildId.Should().Be(222222222UL);
        result.WelcomeMessage.Should().Be("Guild 2 message");
        result.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllConfigurations()
    {
        // Arrange
        await CreateGuildAsync(111111111UL, "Guild 1");
        await CreateGuildAsync(222222222UL, "Guild 2");
        await CreateGuildAsync(333333333UL, "Guild 3");

        var configuration1 = new WelcomeConfiguration
        {
            GuildId = 111111111UL,
            IsEnabled = true,
            WelcomeMessage = "Guild 1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var configuration2 = new WelcomeConfiguration
        {
            GuildId = 222222222UL,
            IsEnabled = false,
            WelcomeMessage = "Guild 2",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var configuration3 = new WelcomeConfiguration
        {
            GuildId = 333333333UL,
            IsEnabled = true,
            WelcomeMessage = "Guild 3",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.WelcomeConfigurations.AddRangeAsync(configuration1, configuration2, configuration3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(c => c.GuildId == 111111111UL);
        result.Should().Contain(c => c.GuildId == 222222222UL);
        result.Should().Contain(c => c.GuildId == 333333333UL);
    }

    [Fact]
    public async Task GetByIdAsync_WithGuildId_ReturnsConfiguration()
    {
        // Arrange
        var guildId = 123456789UL;
        await CreateGuildAsync(guildId);

        var configuration = new WelcomeConfiguration
        {
            GuildId = guildId,
            IsEnabled = true,
            WelcomeMessage = "Test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.WelcomeConfigurations.AddAsync(configuration);
        await _context.SaveChangesAsync();

        // Act
        // GetByIdAsync is inherited from Repository<T> base class
        var result = await _repository.GetByIdAsync(guildId);

        // Assert
        result.Should().NotBeNull();
        result!.GuildId.Should().Be(guildId);
    }
}
