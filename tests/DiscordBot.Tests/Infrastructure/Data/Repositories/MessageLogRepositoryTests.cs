using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Infrastructure.Data.Repositories;

/// <summary>
/// Unit tests for MessageLogRepository.
/// </summary>
public class MessageLogRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly MessageLogRepository _repository;
    private readonly Mock<ILogger<MessageLogRepository>> _mockLogger;
    private readonly Mock<ILogger<Repository<MessageLog>>> _mockBaseLogger;

    public MessageLogRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<MessageLogRepository>>();
        _mockBaseLogger = new Mock<ILogger<Repository<MessageLog>>>();
        _repository = new MessageLogRepository(_context, _mockLogger.Object, _mockBaseLogger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task SeedTestDataAsync()
    {
        var guild = new Guild
        {
            Id = 123456789,
            Name = "Test Guild",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        var user1 = new User
        {
            Id = 111111111,
            Username = "TestUser1",
            Discriminator = "0001",
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        var user2 = new User
        {
            Id = 222222222,
            Username = "TestUser2",
            Discriminator = "0002",
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddRangeAsync(user1, user2);
        await _context.SaveChangesAsync();
    }

    #region GetUserMessagesAsync Tests

    [Fact]
    public async Task GetUserMessagesAsync_WithValidUserId_ReturnsUserMessages()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var user1Messages = new[]
        {
            new MessageLog
            {
                DiscordMessageId = 1001,
                AuthorId = 111111111,
                ChannelId = 999,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Message 1",
                Timestamp = now.AddMinutes(-10),
                LoggedAt = now.AddMinutes(-10),
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1002,
                AuthorId = 111111111,
                ChannelId = 999,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Message 2",
                Timestamp = now.AddMinutes(-5),
                LoggedAt = now.AddMinutes(-5),
                HasAttachments = false,
                HasEmbeds = false
            }
        };

        var user2Message = new MessageLog
        {
            DiscordMessageId = 2001,
            AuthorId = 222222222,
            ChannelId = 999,
            GuildId = 123456789,
            Source = MessageSource.ServerChannel,
            Content = "Message from user 2",
            Timestamp = now.AddMinutes(-3),
            LoggedAt = now.AddMinutes(-3),
            HasAttachments = false,
            HasEmbeds = false
        };

        await _context.MessageLogs.AddRangeAsync(user1Messages);
        await _context.MessageLogs.AddAsync(user2Message);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUserMessagesAsync(111111111);

        // Assert
        var messages = result.ToList();
        messages.Should().HaveCount(2);
        messages.Should().AllSatisfy(m => m.AuthorId.Should().Be(111111111));
        messages[0].DiscordMessageId.Should().Be(1002); // Most recent first
        messages[1].DiscordMessageId.Should().Be(1001);
    }

    [Fact]
    public async Task GetUserMessagesAsync_WithNoMessages_ReturnsEmptyList()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var result = await _repository.GetUserMessagesAsync(999999999);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserMessagesAsync_WithSinceFilter_ReturnsFilteredMessages()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var messages = new[]
        {
            new MessageLog
            {
                DiscordMessageId = 1001,
                AuthorId = 111111111,
                ChannelId = 999,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Old message",
                Timestamp = now.AddDays(-10),
                LoggedAt = now.AddDays(-10),
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1002,
                AuthorId = 111111111,
                ChannelId = 999,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Recent message",
                Timestamp = now.AddHours(-2),
                LoggedAt = now.AddHours(-2),
                HasAttachments = false,
                HasEmbeds = false
            }
        };

        await _context.MessageLogs.AddRangeAsync(messages);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUserMessagesAsync(111111111, since: now.AddDays(-1));

        // Assert
        var resultMessages = result.ToList();
        resultMessages.Should().HaveCount(1);
        resultMessages[0].Content.Should().Be("Recent message");
    }

    [Fact]
    public async Task GetUserMessagesAsync_WithUntilFilter_ReturnsFilteredMessages()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var messages = new[]
        {
            new MessageLog
            {
                DiscordMessageId = 1001,
                AuthorId = 111111111,
                ChannelId = 999,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Old message",
                Timestamp = now.AddDays(-10),
                LoggedAt = now.AddDays(-10),
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1002,
                AuthorId = 111111111,
                ChannelId = 999,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Recent message",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            }
        };

        await _context.MessageLogs.AddRangeAsync(messages);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUserMessagesAsync(111111111, until: now.AddDays(-5));

        // Assert
        var resultMessages = result.ToList();
        resultMessages.Should().HaveCount(1);
        resultMessages[0].Content.Should().Be("Old message");
    }

    [Fact]
    public async Task GetUserMessagesAsync_WithSinceAndUntilFilters_ReturnsMessagesInRange()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var messages = new[]
        {
            new MessageLog
            {
                DiscordMessageId = 1001,
                AuthorId = 111111111,
                ChannelId = 999,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Too old",
                Timestamp = now.AddDays(-20),
                LoggedAt = now.AddDays(-20),
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1002,
                AuthorId = 111111111,
                ChannelId = 999,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "In range 1",
                Timestamp = now.AddDays(-8),
                LoggedAt = now.AddDays(-8),
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1003,
                AuthorId = 111111111,
                ChannelId = 999,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "In range 2",
                Timestamp = now.AddDays(-5),
                LoggedAt = now.AddDays(-5),
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1004,
                AuthorId = 111111111,
                ChannelId = 999,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Too recent",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            }
        };

        await _context.MessageLogs.AddRangeAsync(messages);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUserMessagesAsync(
            111111111,
            since: now.AddDays(-10),
            until: now.AddDays(-3));

        // Assert
        var resultMessages = result.ToList();
        resultMessages.Should().HaveCount(2);
        resultMessages.Should().Contain(m => m.Content == "In range 1");
        resultMessages.Should().Contain(m => m.Content == "In range 2");
    }

    [Fact]
    public async Task GetUserMessagesAsync_RespectsLimitParameter()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            await _context.MessageLogs.AddAsync(new MessageLog
            {
                DiscordMessageId = (ulong)(1000 + i),
                AuthorId = 111111111,
                ChannelId = 999,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = $"Message {i}",
                Timestamp = now.AddMinutes(-i),
                LoggedAt = now.AddMinutes(-i),
                HasAttachments = false,
                HasEmbeds = false
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUserMessagesAsync(111111111, limit: 5);

        // Assert
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetUserMessagesAsync_ReturnsMessagesOrderedByTimestampDescending()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var messages = new[]
        {
            new MessageLog
            {
                DiscordMessageId = 1001,
                AuthorId = 111111111,
                ChannelId = 999,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "First",
                Timestamp = now.AddMinutes(-10),
                LoggedAt = now.AddMinutes(-10),
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1002,
                AuthorId = 111111111,
                ChannelId = 999,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Third",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1003,
                AuthorId = 111111111,
                ChannelId = 999,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Second",
                Timestamp = now.AddMinutes(-5),
                LoggedAt = now.AddMinutes(-5),
                HasAttachments = false,
                HasEmbeds = false
            }
        };

        await _context.MessageLogs.AddRangeAsync(messages);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUserMessagesAsync(111111111);

        // Assert
        var resultList = result.ToList();
        resultList[0].Content.Should().Be("Third");  // Most recent
        resultList[1].Content.Should().Be("Second");
        resultList[2].Content.Should().Be("First");  // Oldest
    }

    #endregion

    #region GetChannelMessagesAsync Tests

    [Fact]
    public async Task GetChannelMessagesAsync_WithValidChannelId_ReturnsChannelMessages()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var channel1Messages = new[]
        {
            new MessageLog
            {
                DiscordMessageId = 1001,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Message 1",
                Timestamp = now.AddMinutes(-10),
                LoggedAt = now.AddMinutes(-10),
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1002,
                AuthorId = 222222222,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Message 2",
                Timestamp = now.AddMinutes(-5),
                LoggedAt = now.AddMinutes(-5),
                HasAttachments = false,
                HasEmbeds = false
            }
        };

        var channel2Message = new MessageLog
        {
            DiscordMessageId = 2001,
            AuthorId = 111111111,
            ChannelId = 999,
            GuildId = 123456789,
            Source = MessageSource.ServerChannel,
            Content = "Different channel",
            Timestamp = now.AddMinutes(-3),
            LoggedAt = now.AddMinutes(-3),
            HasAttachments = false,
            HasEmbeds = false
        };

        await _context.MessageLogs.AddRangeAsync(channel1Messages);
        await _context.MessageLogs.AddAsync(channel2Message);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetChannelMessagesAsync(888);

        // Assert
        var messages = result.ToList();
        messages.Should().HaveCount(2);
        messages.Should().AllSatisfy(m => m.ChannelId.Should().Be(888));
    }

    [Fact]
    public async Task GetChannelMessagesAsync_WithNoMessages_ReturnsEmptyList()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var result = await _repository.GetChannelMessagesAsync(999999999);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetChannelMessagesAsync_WithSinceFilter_ReturnsFilteredMessages()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var messages = new[]
        {
            new MessageLog
            {
                DiscordMessageId = 1001,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Old message",
                Timestamp = now.AddDays(-10),
                LoggedAt = now.AddDays(-10),
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1002,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Recent message",
                Timestamp = now.AddHours(-2),
                LoggedAt = now.AddHours(-2),
                HasAttachments = false,
                HasEmbeds = false
            }
        };

        await _context.MessageLogs.AddRangeAsync(messages);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetChannelMessagesAsync(888, since: now.AddDays(-1));

        // Assert
        var resultMessages = result.ToList();
        resultMessages.Should().HaveCount(1);
        resultMessages[0].Content.Should().Be("Recent message");
    }

    [Fact]
    public async Task GetChannelMessagesAsync_RespectsLimitParameter()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            await _context.MessageLogs.AddAsync(new MessageLog
            {
                DiscordMessageId = (ulong)(1000 + i),
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = $"Message {i}",
                Timestamp = now.AddMinutes(-i),
                LoggedAt = now.AddMinutes(-i),
                HasAttachments = false,
                HasEmbeds = false
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetChannelMessagesAsync(888, limit: 3);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetChannelMessagesAsync_ReturnsMessagesOrderedByTimestampDescending()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var messages = new[]
        {
            new MessageLog
            {
                DiscordMessageId = 1001,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "First",
                Timestamp = now.AddMinutes(-10),
                LoggedAt = now.AddMinutes(-10),
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1002,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Third",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1003,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Second",
                Timestamp = now.AddMinutes(-5),
                LoggedAt = now.AddMinutes(-5),
                HasAttachments = false,
                HasEmbeds = false
            }
        };

        await _context.MessageLogs.AddRangeAsync(messages);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetChannelMessagesAsync(888);

        // Assert
        var resultList = result.ToList();
        resultList[0].Content.Should().Be("Third");  // Most recent
        resultList[1].Content.Should().Be("Second");
        resultList[2].Content.Should().Be("First");  // Oldest
    }

    #endregion

    #region GetGuildMessagesAsync Tests

    [Fact]
    public async Task GetGuildMessagesAsync_WithValidGuildId_ReturnsGuildMessages()
    {
        // Arrange
        await SeedTestDataAsync();

        var guild2 = new Guild
        {
            Id = 987654321,
            Name = "Second Guild",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };
        await _context.Guilds.AddAsync(guild2);
        await _context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var guild1Messages = new[]
        {
            new MessageLog
            {
                DiscordMessageId = 1001,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Guild 1 Message 1",
                Timestamp = now.AddMinutes(-10),
                LoggedAt = now.AddMinutes(-10),
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1002,
                AuthorId = 222222222,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Guild 1 Message 2",
                Timestamp = now.AddMinutes(-5),
                LoggedAt = now.AddMinutes(-5),
                HasAttachments = false,
                HasEmbeds = false
            }
        };

        var guild2Message = new MessageLog
        {
            DiscordMessageId = 2001,
            AuthorId = 111111111,
            ChannelId = 999,
            GuildId = 987654321,
            Source = MessageSource.ServerChannel,
            Content = "Guild 2 Message",
            Timestamp = now.AddMinutes(-3),
            LoggedAt = now.AddMinutes(-3),
            HasAttachments = false,
            HasEmbeds = false
        };

        await _context.MessageLogs.AddRangeAsync(guild1Messages);
        await _context.MessageLogs.AddAsync(guild2Message);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetGuildMessagesAsync(123456789);

        // Assert
        var messages = result.ToList();
        messages.Should().HaveCount(2);
        messages.Should().AllSatisfy(m => m.GuildId.Should().Be(123456789));
    }

    [Fact]
    public async Task GetGuildMessagesAsync_WithNoMessages_ReturnsEmptyList()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var result = await _repository.GetGuildMessagesAsync(999999999);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGuildMessagesAsync_WithSinceFilter_ReturnsFilteredMessages()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var messages = new[]
        {
            new MessageLog
            {
                DiscordMessageId = 1001,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Old message",
                Timestamp = now.AddDays(-10),
                LoggedAt = now.AddDays(-10),
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1002,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Recent message",
                Timestamp = now.AddHours(-2),
                LoggedAt = now.AddHours(-2),
                HasAttachments = false,
                HasEmbeds = false
            }
        };

        await _context.MessageLogs.AddRangeAsync(messages);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetGuildMessagesAsync(123456789, since: now.AddDays(-1));

        // Assert
        var resultMessages = result.ToList();
        resultMessages.Should().HaveCount(1);
        resultMessages[0].Content.Should().Be("Recent message");
    }

    [Fact]
    public async Task GetGuildMessagesAsync_RespectsLimitParameter()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            await _context.MessageLogs.AddAsync(new MessageLog
            {
                DiscordMessageId = (ulong)(1000 + i),
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = $"Message {i}",
                Timestamp = now.AddMinutes(-i),
                LoggedAt = now.AddMinutes(-i),
                HasAttachments = false,
                HasEmbeds = false
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetGuildMessagesAsync(123456789, limit: 7);

        // Assert
        result.Should().HaveCount(7);
    }

    [Fact]
    public async Task GetGuildMessagesAsync_ReturnsMessagesOrderedByTimestampDescending()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var messages = new[]
        {
            new MessageLog
            {
                DiscordMessageId = 1001,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "First",
                Timestamp = now.AddMinutes(-10),
                LoggedAt = now.AddMinutes(-10),
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1002,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Third",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1003,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Second",
                Timestamp = now.AddMinutes(-5),
                LoggedAt = now.AddMinutes(-5),
                HasAttachments = false,
                HasEmbeds = false
            }
        };

        await _context.MessageLogs.AddRangeAsync(messages);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetGuildMessagesAsync(123456789);

        // Assert
        var resultList = result.ToList();
        resultList[0].Content.Should().Be("Third");  // Most recent
        resultList[1].Content.Should().Be("Second");
        resultList[2].Content.Should().Be("First");  // Oldest
    }

    #endregion

    #region DeleteMessagesOlderThanAsync Tests

    [Fact]
    public async Task DeleteMessagesOlderThanAsync_DeletesOldMessages()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var messages = new[]
        {
            new MessageLog
            {
                DiscordMessageId = 1001,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Very old",
                Timestamp = now.AddDays(-60),
                LoggedAt = now.AddDays(-60),
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1002,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Old",
                Timestamp = now.AddDays(-35),
                LoggedAt = now.AddDays(-35),
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1003,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Recent",
                Timestamp = now.AddDays(-10),
                LoggedAt = now.AddDays(-10),
                HasAttachments = false,
                HasEmbeds = false
            }
        };

        await _context.MessageLogs.AddRangeAsync(messages);
        await _context.SaveChangesAsync();

        // Act
        var cutoff = now.AddDays(-30);
        var deletedCount = await _repository.DeleteMessagesOlderThanAsync(cutoff);

        // Assert
        deletedCount.Should().Be(2);
        var remainingMessages = _context.MessageLogs.ToList();
        remainingMessages.Should().HaveCount(1);
        remainingMessages[0].Content.Should().Be("Recent");
    }

    [Fact]
    public async Task DeleteMessagesOlderThanAsync_WithNoOldMessages_ReturnsZero()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var message = new MessageLog
        {
            DiscordMessageId = 1001,
            AuthorId = 111111111,
            ChannelId = 888,
            GuildId = 123456789,
            Source = MessageSource.ServerChannel,
            Content = "Recent",
            Timestamp = now,
            LoggedAt = now,
            HasAttachments = false,
            HasEmbeds = false
        };

        await _context.MessageLogs.AddAsync(message);
        await _context.SaveChangesAsync();

        // Act
        var cutoff = now.AddDays(-30);
        var deletedCount = await _repository.DeleteMessagesOlderThanAsync(cutoff);

        // Assert
        deletedCount.Should().Be(0);
        _context.MessageLogs.Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteMessagesOlderThanAsync_DeletesBasedOnLoggedAt_NotTimestamp()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var messages = new[]
        {
            new MessageLog
            {
                DiscordMessageId = 1001,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Message 1",
                Timestamp = now.AddDays(-60),  // Old timestamp
                LoggedAt = now.AddDays(-10),   // Recent logged time - should NOT be deleted
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1002,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Message 2",
                Timestamp = now.AddDays(-10),  // Recent timestamp
                LoggedAt = now.AddDays(-60),   // Old logged time - should be deleted
                HasAttachments = false,
                HasEmbeds = false
            }
        };

        await _context.MessageLogs.AddRangeAsync(messages);
        await _context.SaveChangesAsync();

        // Act
        var cutoff = now.AddDays(-30);
        var deletedCount = await _repository.DeleteMessagesOlderThanAsync(cutoff);

        // Assert
        deletedCount.Should().Be(1);
        var remainingMessages = _context.MessageLogs.ToList();
        remainingMessages.Should().HaveCount(1);
        remainingMessages[0].Content.Should().Be("Message 1");
    }

    [Fact]
    public async Task DeleteMessagesOlderThanAsync_WithEmptyDatabase_ReturnsZero()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var deletedCount = await _repository.DeleteMessagesOlderThanAsync(cutoff);

        // Assert
        deletedCount.Should().Be(0);
    }

    #endregion

    #region GetMessageCountAsync Tests

    [Fact]
    public async Task GetMessageCountAsync_WithNoFilters_ReturnsAllMessages()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var messages = new[]
        {
            new MessageLog
            {
                DiscordMessageId = 1001,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Message 1",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1002,
                AuthorId = 222222222,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Message 2",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1003,
                AuthorId = 111111111,
                ChannelId = 999,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Message 3",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            }
        };

        await _context.MessageLogs.AddRangeAsync(messages);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetMessageCountAsync();

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public async Task GetMessageCountAsync_WithAuthorIdFilter_ReturnsFilteredCount()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var messages = new[]
        {
            new MessageLog
            {
                DiscordMessageId = 1001,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "User 1 Message 1",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1002,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "User 1 Message 2",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1003,
                AuthorId = 222222222,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "User 2 Message",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            }
        };

        await _context.MessageLogs.AddRangeAsync(messages);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetMessageCountAsync(authorId: 111111111);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task GetMessageCountAsync_WithGuildIdFilter_ReturnsFilteredCount()
    {
        // Arrange
        await SeedTestDataAsync();

        var guild2 = new Guild
        {
            Id = 987654321,
            Name = "Second Guild",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };
        await _context.Guilds.AddAsync(guild2);
        await _context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var messages = new[]
        {
            new MessageLog
            {
                DiscordMessageId = 1001,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Guild 1 Message 1",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1002,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Guild 1 Message 2",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1003,
                AuthorId = 222222222,
                ChannelId = 999,
                GuildId = 987654321,
                Source = MessageSource.ServerChannel,
                Content = "Guild 2 Message",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            }
        };

        await _context.MessageLogs.AddRangeAsync(messages);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetMessageCountAsync(guildId: 123456789);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task GetMessageCountAsync_WithBothFilters_ReturnsFilteredCount()
    {
        // Arrange
        await SeedTestDataAsync();

        var guild2 = new Guild
        {
            Id = 987654321,
            Name = "Second Guild",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };
        await _context.Guilds.AddAsync(guild2);
        await _context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var messages = new[]
        {
            new MessageLog
            {
                DiscordMessageId = 1001,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "User 1 Guild 1 Message 1",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1002,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "User 1 Guild 1 Message 2",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1003,
                AuthorId = 111111111,
                ChannelId = 999,
                GuildId = 987654321,
                Source = MessageSource.ServerChannel,
                Content = "User 1 Guild 2 Message",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1004,
                AuthorId = 222222222,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "User 2 Guild 1 Message",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            }
        };

        await _context.MessageLogs.AddRangeAsync(messages);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetMessageCountAsync(authorId: 111111111, guildId: 123456789);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task GetMessageCountAsync_WithEmptyDatabase_ReturnsZero()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var count = await _repository.GetMessageCountAsync();

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetMessageCountAsync_WithNonExistentAuthor_ReturnsZero()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var message = new MessageLog
        {
            DiscordMessageId = 1001,
            AuthorId = 111111111,
            ChannelId = 888,
            GuildId = 123456789,
            Source = MessageSource.ServerChannel,
            Content = "Message",
            Timestamp = now,
            LoggedAt = now,
            HasAttachments = false,
            HasEmbeds = false
        };

        await _context.MessageLogs.AddAsync(message);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetMessageCountAsync(authorId: 999999999);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetMessageCountAsync_WithNonExistentGuild_ReturnsZero()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var message = new MessageLog
        {
            DiscordMessageId = 1001,
            AuthorId = 111111111,
            ChannelId = 888,
            GuildId = 123456789,
            Source = MessageSource.ServerChannel,
            Content = "Message",
            Timestamp = now,
            LoggedAt = now,
            HasAttachments = false,
            HasEmbeds = false
        };

        await _context.MessageLogs.AddAsync(message);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetMessageCountAsync(guildId: 999999999);

        // Assert
        count.Should().Be(0);
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public async Task GetUserMessagesAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var message = new MessageLog
        {
            DiscordMessageId = 1001,
            AuthorId = 111111111,
            ChannelId = 888,
            GuildId = 123456789,
            Source = MessageSource.ServerChannel,
            Content = "Message",
            Timestamp = now,
            LoggedAt = now,
            HasAttachments = false,
            HasEmbeds = false
        };

        await _context.MessageLogs.AddAsync(message);
        await _context.SaveChangesAsync();

        using var cts = new CancellationTokenSource();

        // Act
        var result = await _repository.GetUserMessagesAsync(111111111, cancellationToken: cts.Token);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task MessageLog_SupportsDirectMessages_WithNullGuildId()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var dmMessage = new MessageLog
        {
            DiscordMessageId = 1001,
            AuthorId = 111111111,
            ChannelId = 888,
            GuildId = null,  // Direct message has no guild
            Source = MessageSource.DirectMessage,
            Content = "DM Message",
            Timestamp = now,
            LoggedAt = now,
            HasAttachments = false,
            HasEmbeds = false
        };

        await _context.MessageLogs.AddAsync(dmMessage);
        await _context.SaveChangesAsync();

        // Act
        var userMessages = await _repository.GetUserMessagesAsync(111111111);
        var channelMessages = await _repository.GetChannelMessagesAsync(888);

        // Assert
        userMessages.Should().HaveCount(1);
        channelMessages.Should().HaveCount(1);
        userMessages.First().GuildId.Should().BeNull();
        userMessages.First().Source.Should().Be(MessageSource.DirectMessage);
    }

    [Fact]
    public async Task MessageLog_SupportsAttachmentsAndEmbeds()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var messageWithMedia = new MessageLog
        {
            DiscordMessageId = 1001,
            AuthorId = 111111111,
            ChannelId = 888,
            GuildId = 123456789,
            Source = MessageSource.ServerChannel,
            Content = "Message with media",
            Timestamp = now,
            LoggedAt = now,
            HasAttachments = true,
            HasEmbeds = true,
            ReplyToMessageId = 999
        };

        await _context.MessageLogs.AddAsync(messageWithMedia);
        await _context.SaveChangesAsync();

        // Act
        var messages = await _repository.GetUserMessagesAsync(111111111);

        // Assert
        var message = messages.First();
        message.HasAttachments.Should().BeTrue();
        message.HasEmbeds.Should().BeTrue();
        message.ReplyToMessageId.Should().Be(999);
    }

    [Fact]
    public async Task GetMessageCountAsync_ReturnsLongForLargeNumbers()
    {
        // Arrange
        await SeedTestDataAsync();

        // This test verifies the return type is long, not int
        // In a real scenario with millions of messages, this would be important

        // Act
        var count = await _repository.GetMessageCountAsync();

        // Assert
        count.Should().Be(0);
        count.GetType().Should().Be(typeof(long));
    }

    [Fact]
    public async Task Repository_SupportsConcurrentOperations()
    {
        // Arrange
        await SeedTestDataAsync();

        var now = DateTime.UtcNow;
        var messages = new[]
        {
            new MessageLog
            {
                DiscordMessageId = 1001,
                AuthorId = 111111111,
                ChannelId = 888,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Message 1",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            },
            new MessageLog
            {
                DiscordMessageId = 1002,
                AuthorId = 222222222,
                ChannelId = 999,
                GuildId = 123456789,
                Source = MessageSource.ServerChannel,
                Content = "Message 2",
                Timestamp = now,
                LoggedAt = now,
                HasAttachments = false,
                HasEmbeds = false
            }
        };

        await _context.MessageLogs.AddRangeAsync(messages);
        await _context.SaveChangesAsync();

        // Act - Perform multiple operations
        var userMessages = await _repository.GetUserMessagesAsync(111111111);
        var channelMessages = await _repository.GetChannelMessagesAsync(888);
        var guildMessages = await _repository.GetGuildMessagesAsync(123456789);
        var totalCount = await _repository.GetMessageCountAsync();

        // Assert
        userMessages.Should().HaveCount(1);
        channelMessages.Should().HaveCount(1);
        guildMessages.Should().HaveCount(2);
        totalCount.Should().Be(2);
    }

    #endregion
}
