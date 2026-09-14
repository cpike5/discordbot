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
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Regression coverage for docs/lessons-learned/scheduled-message-repeated-update-tracking.md.
/// The root cause was never <see cref="Repository{T}"/>: it's that a Blazor circuit's DI scope -
/// and therefore its <see cref="BotDbContext"/> - lives for the whole circuit, so a second
/// fetch-mutate-<c>Update()</c> of the same entity against the *same* long-lived context collided
/// with the first call's still-tracked graph. <see cref="Repository{T}.UpdateAsync"/>/<see cref="Repository{T}.DeleteAsync"/>
/// are back to their original, unconditional <c>DbSet.Update(entity)</c>/<c>DbSet.Remove(entity)</c>
/// bodies (a generic reconciliation there was tried and rejected - see the lessons-learned note -
/// because detaching whatever the tracker happens to hold can silently drop a concurrent caller's
/// pending edit). The real fix is resolving a fresh scope, and therefore a fresh
/// <see cref="BotDbContext"/>, per mutation (<c>Blazor/Common/ScopedOperations.cs</c>), which this
/// test proves directly: it updates the same <see cref="ScheduledMessage"/> twice and then deletes
/// it, each operation against its own <see cref="BotDbContext"/> on its own connection to one
/// shared file-backed database - exactly what a Blazor page's <c>IServiceScopeFactory.RunAsync</c>
/// call produces - and none of it throws, because each context starts with an empty change
/// tracker instead of accumulating stale entries across the whole "circuit".
/// </summary>
public class ScheduledMessageRepeatedUpdateTrackingTests : IDisposable
{
    private readonly SharedTestDatabase _database = TestDbContextFactory.CreateSharedDatabase();
    private readonly Guid _messageId = Guid.NewGuid();

    public ScheduledMessageRepeatedUpdateTrackingTests()
    {
        using var seedContext = _database.CreateContext();
        seedContext.Guilds.Add(new Guild { Id = 1UL, Name = "G", JoinedAt = DateTime.UtcNow, IsActive = true });
        seedContext.ScheduledMessages.Add(new ScheduledMessage
        {
            Id = _messageId,
            GuildId = 1UL,
            ChannelId = 2UL,
            Title = "T",
            Content = "C",
            Frequency = ScheduleFrequency.Daily,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "u",
            UpdatedAt = DateTime.UtcNow
        });
        seedContext.SaveChanges();
    }

    public void Dispose() => _database.Dispose();

    /// <summary>
    /// A service backed by its own <see cref="BotDbContext"/> - the equivalent of what
    /// <c>ScopedOperations.RunAsync</c> hands a mutation handler: <c>await using var scope =
    /// scopeFactory.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService{T}())</c>.
    /// Disposing this disposes the underlying context, the same as the scope disposing at the end
    /// of <c>RunAsync</c>.
    /// </summary>
    private sealed class ScopedScheduledMessageService(ScheduledMessageService service, BotDbContext context) : IDisposable
    {
        public ScheduledMessageService Service { get; } = service;

        public void Dispose() => context.Dispose();
    }

    private ScopedScheduledMessageService NewScopedService()
    {
        var context = _database.CreateContext();
        var repoLogger = new Mock<ILogger<ScheduledMessageRepository>>();
        var baseLogger = new Mock<ILogger<Repository<ScheduledMessage>>>();
        var repository = new ScheduledMessageRepository(context, repoLogger.Object, baseLogger.Object);

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

        var service = new ScheduledMessageService(repository, discordClient.Object, svcLogger.Object, audit.Object);
        return new ScopedScheduledMessageService(service, context);
    }

    [Fact]
    public async Task UpdateTwiceThenDelete_EachInItsOwnScope_DoesNotThrow()
    {
        ScheduledMessageDto? afterFirstUpdate;
        using (var first = NewScopedService())
        {
            afterFirstUpdate = await first.Service.UpdateAsync(_messageId, new ScheduledMessageUpdateDto { IsEnabled = false });
        }

        ScheduledMessageDto? afterSecondUpdate;
        using (var second = NewScopedService())
        {
            afterSecondUpdate = await second.Service.UpdateAsync(_messageId, new ScheduledMessageUpdateDto { IsEnabled = true });
        }

        bool deleted;
        using (var third = NewScopedService())
        {
            deleted = await third.Service.DeleteAsync(_messageId);
        }

        afterFirstUpdate.Should().NotBeNull();
        afterFirstUpdate!.IsEnabled.Should().BeFalse();
        afterSecondUpdate.Should().NotBeNull();
        afterSecondUpdate!.IsEnabled.Should().BeTrue();
        deleted.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateRepeatedly_EachInItsOwnScope_DoesNotThrow()
    {
        for (var i = 0; i < 4; i++)
        {
            using var scoped = NewScopedService();
            var updated = await scoped.Service.UpdateAsync(_messageId, new ScheduledMessageUpdateDto { IsEnabled = i % 2 == 0 });
            updated.Should().NotBeNull();
            updated!.IsEnabled.Should().Be(i % 2 == 0);
        }
    }
}
