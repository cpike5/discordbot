using System.Text.Json;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Dashboard;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscordBot.Tests.Bot.Services;

/// <summary>
/// The dashboard's hero numbers: how they are worked out, that the pushed payload carries the
/// names the page script reads, and that a burst of changes becomes one broadcast.
/// </summary>
public class DashboardStatsTests
{
    private static GuildDto Guild(ulong id, bool active, int? members) =>
        new() { Id = id, Name = $"Guild {id}", IsActive = active, MemberCount = members };

    [Fact]
    public void Build_CountsOnlyActiveGuilds()
    {
        var stats = DashboardStatsProvider.Build(
            new[] { Guild(1, true, 10), Guild(2, true, null), Guild(3, false, 500) },
            commandsLast24Hours: 42,
            uptimePercent24Hours: 99.94);

        stats.TotalServers.Should().Be(2, "a server the bot has left is not a server it is in");
        stats.TotalMembers.Should().Be(10, "members of an inactive guild are not counted; unknown counts as zero");
        stats.CommandsLast24Hours.Should().Be(42);
        stats.UptimePercent24Hours.Should().Be(99.9);
    }

    [Fact]
    public async Task GetStatsAsync_ReadsGuildsCommandsAndUptime()
    {
        var guilds = new Mock<IGuildService>();
        guilds.Setup(g => g.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto> { Guild(1, true, 7) });
        var commands = new Mock<ICommandLogService>();
        DateTime? requestedSince = null;
        commands.Setup(c => c.GetCommandStatsAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime?, CancellationToken>((since, _) => requestedSince = since)
            .ReturnsAsync(new Dictionary<string, int> { ["ping"] = 3, ["help"] = 2 });
        var connection = new Mock<IConnectionStateService>();
        connection.Setup(c => c.GetUptimePercentage(TimeSpan.FromHours(24))).Returns(87.5);

        var provider = new DashboardStatsProvider(guilds.Object, commands.Object, connection.Object);
        var before = DateTime.UtcNow;
        var stats = await provider.GetStatsAsync();

        stats.Should().BeEquivalentTo(new { TotalServers = 1, TotalMembers = 7, CommandsLast24Hours = 5, UptimePercent24Hours = 87.5 },
            options => options.ExcludingMissingMembers());
        requestedSince.Should().NotBeNull();
        requestedSince!.Value.Should().BeCloseTo(before - TimeSpan.FromHours(24), TimeSpan.FromMinutes(1),
            "the card says 24 hours, so the window is 24 hours");
    }

    [Fact]
    public void Payload_UsesTheFieldNamesTheDashboardScriptReads()
    {
        // StatsUpdated reaches the page as camelCased JSON. dashboard-stats.js looks fields up by
        // name, so a rename on either side silently freezes the hero cards (audit finding S-3).
        var json = JsonSerializer.Serialize(new DashboardStatsDto(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var sent = JsonDocument.Parse(json).RootElement.EnumerateObject()
            .Select(p => p.Name)
            .Where(n => n != "timestamp")
            .ToList();

        var script = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "DiscordBot.Bot", "wwwroot", "js", "dashboard-stats.js"));

        foreach (var name in sent)
        {
            script.Should().Contain($"{name}:", $"dashboard-stats.js must read the '{name}' field the hub sends");
        }
    }

    [Fact]
    public async Task NotifyChanged_CoalescesABurstIntoOneBroadcast()
    {
        var (broadcaster, update, _) = CreateBroadcaster();

        for (var i = 0; i < 5; i++) broadcaster.NotifyChanged();
        await WaitUntilAsync(() => update.Invocations.Count(i => i.Method.Name == nameof(IDashboardUpdateService.BroadcastStatsUpdateAsync)) >= 1);
        await Task.Delay(150).ConfigureAwait(false);

        update.Verify(u => u.BroadcastStatsUpdateAsync(It.IsAny<DashboardStatsDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyChanged_AfterABroadcast_SchedulesAnother()
    {
        var (broadcaster, update, _) = CreateBroadcaster();

        broadcaster.NotifyChanged();
        await WaitUntilAsync(() => update.Invocations.Count == 1);
        broadcaster.NotifyChanged();
        await WaitUntilAsync(() => update.Invocations.Count == 2);

        update.Verify(u => u.BroadcastStatsUpdateAsync(It.IsAny<DashboardStatsDto>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task NotifyChanged_WhenReadingFails_DoesNotThrowAndRecovers()
    {
        var (broadcaster, update, provider) = CreateBroadcaster();
        provider.SetupSequence(p => p.GetStatsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database is down"))
            .ReturnsAsync(new DashboardStatsDto { TotalServers = 1 });

        broadcaster.NotifyChanged();
        await WaitUntilAsync(() => provider.Invocations.Count == 1);
        await Task.Delay(100).ConfigureAwait(false);
        update.Invocations.Should().BeEmpty("nothing was read, so nothing is pushed");

        broadcaster.NotifyChanged();
        await WaitUntilAsync(() => update.Invocations.Count == 1);
        update.Verify(u => u.BroadcastStatsUpdateAsync(It.Is<DashboardStatsDto>(s => s.TotalServers == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static (DashboardStatsBroadcaster Broadcaster, Mock<IDashboardUpdateService> Update, Mock<IDashboardStatsProvider> Provider) CreateBroadcaster()
    {
        var provider = new Mock<IDashboardStatsProvider>();
        provider.Setup(p => p.GetStatsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new DashboardStatsDto());
        var update = new Mock<IDashboardUpdateService>();

        var services = new ServiceCollection();
        services.AddScoped(_ => provider.Object);
        var broadcaster = new DashboardStatsBroadcaster(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            update.Object,
            NullLogger<DashboardStatsBroadcaster>.Instance)
        {
            Debounce = TimeSpan.FromMilliseconds(30)
        };
        return (broadcaster, update, provider);
    }

    /// <summary>Polls with ConfigureAwait(false): a plain await can sit behind xUnit's worker queue.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition was not met in time.");
            await Task.Delay(10).ConfigureAwait(false);
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DiscordBot.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests must be able to find the repository they are testing");
        return directory!.FullName;
    }
}
