using Discord.WebSocket;
using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Regression coverage for docs/lessons-learned/scheduled-message-repeated-update-tracking.md:
/// updating a <see cref="ScheduledMessage"/> more than once, then deleting it, all against one
/// long-lived <see cref="BotDbContext"/> (as a Blazor circuit's DI scope provides), used to throw
/// EF's "already tracked" <see cref="InvalidOperationException"/> on the second update -
/// <see cref="Repository{T}.UpdateAsync"/>'s unconditional <c>DbSet.Update(entity)</c> tried to
/// attach a second, freshly-fetched (AsNoTracking) instance for a key the first update's call had
/// left tracked in the context. Exercises the real <see cref="ScheduledMessageRepository"/> and
/// <see cref="ScheduledMessageService"/> (not mocks) against a real SQLite-in-memory context, since
/// the bug is in EF's change-tracker interaction, invisible to a mocked repository.
/// </summary>
public class ScheduledMessageRepeatedUpdateTrackingTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly ScheduledMessageService _service;
    private readonly ScheduledMessage _message;

    public ScheduledMessageRepeatedUpdateTrackingTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();

        var guild = new Guild { Id = 1UL, Name = "G", JoinedAt = DateTime.UtcNow, IsActive = true };
        _context.Guilds.Add(guild);

        _message = new ScheduledMessage
        {
            Id = Guid.NewGuid(),
            GuildId = 1UL,
            ChannelId = 2UL,
            Title = "T",
            Content = "C",
            Frequency = ScheduleFrequency.Daily,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "u",
            UpdatedAt = DateTime.UtcNow
        };
        _context.ScheduledMessages.Add(_message);
        _context.SaveChanges();
        // Mirrors a fresh Blazor circuit scope: nothing left tracked from seeding, only the
        // long-lived context itself carries state forward between the calls below.
        _context.ChangeTracker.Clear();

        var repoLogger = new Mock<ILogger<ScheduledMessageRepository>>();
        var baseLogger = new Mock<ILogger<Repository<ScheduledMessage>>>();
        var repository = new ScheduledMessageRepository(_context, repoLogger.Object, baseLogger.Object);

        var discordClient = new Mock<DiscordSocketClient>();
        var svcLogger = new Mock<ILogger<ScheduledMessageService>>();
        var audit = new Mock<IAuditLogService>();
        var builder = new Mock<IAuditLogBuilder>();
        builder.Setup(x => x.ForCategory(It.IsAny<AuditLogCategory>())).Returns(builder.Object);
        builder.Setup(x => x.WithAction(It.IsAny<AuditLogAction>())).Returns(builder.Object);
        builder.Setup(x => x.BySystem()).Returns(builder.Object);
        builder.Setup(x => x.OnTarget(It.IsAny<string>(), It.IsAny<string>())).Returns(builder.Object);
        builder.Setup(x => x.InGuild(It.IsAny<ulong>())).Returns(builder.Object);
        builder.Setup(x => x.WithDetails(It.IsAny<object>())).Returns(builder.Object);
        audit.Setup(x => x.CreateBuilder()).Returns(builder.Object);

        _service = new ScheduledMessageService(repository, discordClient.Object, svcLogger.Object, audit.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task UpdateTwiceThenDelete_InOneContext_DoesNotThrow()
    {
        var afterFirstUpdate = await _service.UpdateAsync(_message.Id, new ScheduledMessageUpdateDto { IsEnabled = false });
        var afterSecondUpdate = await _service.UpdateAsync(_message.Id, new ScheduledMessageUpdateDto { IsEnabled = true });
        var deleted = await _service.DeleteAsync(_message.Id);

        afterFirstUpdate.Should().NotBeNull();
        afterFirstUpdate!.IsEnabled.Should().BeFalse();
        afterSecondUpdate.Should().NotBeNull();
        afterSecondUpdate!.IsEnabled.Should().BeTrue();
        deleted.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateRepeatedly_InOneContext_DoesNotThrow()
    {
        for (var i = 0; i < 4; i++)
        {
            var updated = await _service.UpdateAsync(_message.Id, new ScheduledMessageUpdateDto { IsEnabled = i % 2 == 0 });
            updated.Should().NotBeNull();
            updated!.IsEnabled.Should().Be(i % 2 == 0);
        }
    }
}
