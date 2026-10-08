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
/// Runs <see cref="ScheduledMessageExecutionService"/>'s processing cycle against a real
/// PostgreSQL database with the real repository and service, so DbContext sharing between
/// concurrent executions shows up as it would in production.
/// </summary>
public class ScheduledMessageExecutionServiceDatabaseTests
{
    private const ulong GuildId = 555000111UL;

    [Fact]
    public async Task ProcessDueMessagesAsync_TwoDueMessages_BothSendOnceAndBothSave()
    {
        using var database = TestDbContextFactory.CreateDatabase();

        // Whole seconds, so the value survives PostgreSQL's microsecond precision unchanged.
        var now = DateTime.UtcNow;
        var scheduledAt = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc).AddMinutes(-1);
        var first = NewDueMessage(channelId: 1001UL, scheduledAt);
        var second = NewDueMessage(channelId: 1002UL, scheduledAt);

        using (var setup = database.CreateContext())
        {
            setup.Guilds.Add(new Guild { Id = GuildId, Name = "Guild", JoinedAt = DateTime.UtcNow, IsActive = true });
            setup.ScheduledMessages.AddRange(first, second);
            await setup.SaveChangesAsync();
        }

        var client = new Mock<DiscordSocketClient>();
        var discord = client.As<IDiscordClient>();
        client.SetupGet(c => c.ConnectionState).Returns(ConnectionState.Connected);
        var firstChannel = MockChannel(discord, first.ChannelId);
        var secondChannel = MockChannel(discord, second.ChannelId);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<BotDbContext>(_ => database.CreateContext());
        services.AddScoped<IScheduledMessageRepository, ScheduledMessageRepository>();
        services.AddScoped<IScheduledMessageService, ScheduledMessageService>();
        services.AddSingleton(client.Object);
        services.AddSingleton(new Mock<IAuditLogService> { DefaultValue = DefaultValue.Mock }.Object);
        using var provider = services.BuildServiceProvider();

        var service = new ScheduledMessageExecutionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ScheduledMessagesOptions
            {
                CheckIntervalSeconds = 60,
                MaxConcurrentExecutions = 5,
                ExecutionTimeoutSeconds = 30
            }),
            NullLogger<ScheduledMessageExecutionService>.Instance,
            provider);

        // Act
        var processed = await service.ProcessDueMessagesAsync(CancellationToken.None);

        // Assert
        processed.Should().Be(2);
        VerifySentOnce(firstChannel);
        VerifySentOnce(secondChannel);

        using var verify = database.CreateContext();
        var rows = await verify.ScheduledMessages.AsNoTracking().OrderBy(m => m.ChannelId).ToListAsync();
        rows.Should().HaveCount(2);
        rows.Should().AllSatisfy(m =>
        {
            m.IsEnabled.Should().BeTrue();
            m.NextExecutionAt.Should().Be(scheduledAt.AddDays(1));
            m.LastExecutedAt.Should().NotBeNull();
        });
    }

    private static ScheduledMessage NewDueMessage(ulong channelId, DateTime scheduledAt) => new()
    {
        Id = Guid.NewGuid(),
        GuildId = GuildId,
        ChannelId = channelId,
        Title = $"Message {channelId}",
        Content = "Hello",
        Frequency = ScheduleFrequency.Daily,
        IsEnabled = true,
        NextExecutionAt = scheduledAt,
        CreatedAt = DateTime.UtcNow.AddDays(-1),
        CreatedBy = "test",
        UpdatedAt = DateTime.UtcNow.AddDays(-1)
    };

    private static Mock<ITextChannel> MockChannel(Mock<IDiscordClient> discord, ulong channelId)
    {
        var channel = new Mock<ITextChannel>();
        channel.SetupGet(c => c.Id).Returns(channelId);
        channel
            .Setup(c => c.SendMessageAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(),
                It.IsAny<AllowedMentions>(), It.IsAny<MessageReference>(), It.IsAny<MessageComponent>(),
                It.IsAny<ISticker[]>(), It.IsAny<Embed[]>(), It.IsAny<MessageFlags>(), It.IsAny<PollProperties>()))
            .ReturnsAsync(Mock.Of<IUserMessage>());
        discord
            .Setup(c => c.GetChannelAsync(channelId, CacheMode.CacheOnly, It.IsAny<RequestOptions>()))
            .ReturnsAsync(channel.Object);
        return channel;
    }

    private static void VerifySentOnce(Mock<ITextChannel> channel) =>
        channel.Verify(
            c => c.SendMessageAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(),
                It.IsAny<AllowedMentions>(), It.IsAny<MessageReference>(), It.IsAny<MessageComponent>(),
                It.IsAny<ISticker[]>(), It.IsAny<Embed[]>(), It.IsAny<MessageFlags>(), It.IsAny<PollProperties>()),
            Times.Once);
}
