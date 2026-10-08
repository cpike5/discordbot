using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Runs <see cref="ReminderExecutionService"/>'s delivery cycle against a real PostgreSQL database
/// with the real repository, mocking only Discord.
/// </summary>
public class ReminderExecutionServiceDatabaseTests
{
    private const ulong GuildId = 777000111UL;
    private const ulong UserId = 424242UL;

    [Fact]
    public async Task ProcessDueRemindersAsync_UserNotInCache_FallsBackToRestAndDelivers()
    {
        using var database = TestDbContextFactory.CreateDatabase();
        var reminder = await SeedDueReminderAsync(database);

        var client = NewClient();
        var dmChannel = new Mock<IDMChannel>();
        SetupDmSend(dmChannel).ReturnsAsync(Mock.Of<IUserMessage>());
        var user = new Mock<IUser>();
        user.SetupGet(u => u.Id).Returns(UserId);
        user.Setup(u => u.CreateDMChannelAsync(It.IsAny<RequestOptions>())).ReturnsAsync(dmChannel.Object);

        // The socket cache misses (AlwaysDownloadUsers is off); the REST lookup finds the user.
        client.Setup(c => c.GetUser(UserId)).Returns((SocketUser?)null);
        client.As<IDiscordClient>()
            .Setup(c => c.GetUserAsync(UserId, CacheMode.AllowDownload, It.IsAny<RequestOptions>()))
            .ReturnsAsync(user.Object);

        var service = NewService(database, client);
        var before = DbTimestamp.LowerBound();

        // Act
        await service.ProcessDueRemindersAsync(CancellationToken.None);

        // Assert
        using var verify = database.CreateContext();
        var row = await verify.Reminders.AsNoTracking().SingleAsync(r => r.Id == reminder.Id);
        row.Status.Should().Be(ReminderStatus.Delivered);
        row.DeliveredAt.Should().NotBeNull();
        row.DeliveredAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
        SetupDmSendVerify(dmChannel, Times.Once());
    }

    [Fact]
    public async Task ProcessDueRemindersAsync_ReminderCancelledDuringFailedDelivery_StaysCancelled()
    {
        using var database = TestDbContextFactory.CreateDatabase();
        var reminder = await SeedDueReminderAsync(database);

        var client = NewClient();
        var dmChannel = new Mock<IDMChannel>();
        SetupDmSend(dmChannel).Returns(async () =>
        {
            // The user cancels the reminder while its DM is in flight, then the DM fails.
            using (var other = database.CreateContext())
            {
                var row = await other.Reminders.SingleAsync(r => r.Id == reminder.Id);
                row.Status = ReminderStatus.Cancelled;
                await other.SaveChangesAsync();
            }

            throw new InvalidOperationException("Discord is having a bad day");
        });
        var user = new Mock<IUser>();
        user.SetupGet(u => u.Id).Returns(UserId);
        user.Setup(u => u.CreateDMChannelAsync(It.IsAny<RequestOptions>())).ReturnsAsync(dmChannel.Object);
        client.As<IDiscordClient>()
            .Setup(c => c.GetUserAsync(UserId, CacheMode.AllowDownload, It.IsAny<RequestOptions>()))
            .ReturnsAsync(user.Object);

        var service = NewService(database, client);

        // Act
        await service.ProcessDueRemindersAsync(CancellationToken.None);

        // Assert: the retry bookkeeping did not overwrite the cancellation with a stale snapshot
        using var verify = database.CreateContext();
        var stored = await verify.Reminders.AsNoTracking().SingleAsync(r => r.Id == reminder.Id);
        stored.Status.Should().Be(ReminderStatus.Cancelled);
        stored.DeliveryAttempts.Should().Be(0);
        stored.TriggerAt.Should().Be(reminder.TriggerAt);
    }

    private static Mock<DiscordSocketClient> NewClient()
    {
        var client = new Mock<DiscordSocketClient>();
        // Must be added before the mock object is first created, or interface calls are not intercepted.
        client.As<IDiscordClient>();
        client.SetupGet(c => c.ConnectionState).Returns(ConnectionState.Connected);
        return client;
    }

    private static ReminderExecutionService NewService(TestDatabase database, Mock<DiscordSocketClient> client)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<BotDbContext>(_ => database.CreateContext());
        services.AddScoped<IReminderRepository, ReminderRepository>();
        var provider = services.BuildServiceProvider();

        return new ReminderExecutionService(
            provider,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ReminderOptions
            {
                CheckIntervalSeconds = 60,
                MaxConcurrentDeliveries = 5,
                MaxDeliveryAttempts = 3,
                RetryDelayMinutes = 5
            }),
            client.Object,
            NullLogger<ReminderExecutionService>.Instance);
    }

    private static async Task<Reminder> SeedDueReminderAsync(TestDatabase database)
    {
        // Whole seconds, so the value survives PostgreSQL's microsecond precision unchanged.
        var now = DateTime.UtcNow;
        var triggerAt = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc).AddMinutes(-1);

        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            GuildId = GuildId,
            ChannelId = 9001UL,
            UserId = UserId,
            Message = "Stand up",
            TriggerAt = triggerAt,
            CreatedAt = triggerAt.AddHours(-1),
            Status = ReminderStatus.Pending,
            DeliveryAttempts = 0
        };

        using var setup = database.CreateContext();
        setup.Guilds.Add(new Guild { Id = GuildId, Name = "Guild", JoinedAt = DateTime.UtcNow, IsActive = true });
        setup.Reminders.Add(reminder);
        await setup.SaveChangesAsync();
        return reminder;
    }

    private static Moq.Language.Flow.ISetup<IDMChannel, Task<IUserMessage>> SetupDmSend(Mock<IDMChannel> channel) =>
        channel.Setup(c => c.SendMessageAsync(
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageReference>(), It.IsAny<MessageComponent>(),
            It.IsAny<ISticker[]>(), It.IsAny<Embed[]>(), It.IsAny<MessageFlags>(), It.IsAny<PollProperties>()));

    private static void SetupDmSendVerify(Mock<IDMChannel> channel, Times times) =>
        channel.Verify(c => c.SendMessageAsync(
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageReference>(), It.IsAny<MessageComponent>(),
            It.IsAny<ISticker[]>(), It.IsAny<Embed[]>(), It.IsAny<MessageFlags>(), It.IsAny<PollProperties>()),
            times);
}
