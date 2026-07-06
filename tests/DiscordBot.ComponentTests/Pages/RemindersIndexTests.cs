using Bunit.TestDoubles;
using Discord.WebSocket;
using DiscordBot.Bot.Blazor.Pages.Guilds.Reminders;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Pages;

/// <summary>
/// Tests for the routed Blazor twin of the old Pages/Guilds/Reminders/Index.cshtml
/// (Phase F migration). Covers the stats cards, the reminder table, the status
/// filter, and the cancel flow behind the shared ConfirmModal. The
/// DiscordSocketClient is a real (disconnected) instance, so the user lookup falls
/// through to the "Unknown (id)" fallback exactly like the page model when the
/// guild is unavailable.
/// </summary>
public class RemindersIndexTests : TestContext
{
    private const ulong GuildId = 123456789012345678UL;
    private static readonly Guid PendingReminderId = Guid.NewGuid();
    private static readonly Guid DeliveredReminderId = Guid.NewGuid();

    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<IReminderRepository> _reminderRepository = new();

    private readonly List<Reminder> _reminders;
    private ReminderStatus? _lastStatusFilter;

    public RemindersIndexTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _guildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildDto { Id = GuildId, Name = "Test Guild", IsActive = true });

        _reminders = new List<Reminder>
        {
            new()
            {
                Id = PendingReminderId,
                GuildId = GuildId,
                UserId = 111UL,
                Message = "Water the plants",
                TriggerAt = DateTime.UtcNow.AddHours(3),
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                Status = ReminderStatus.Pending
            },
            new()
            {
                Id = DeliveredReminderId,
                GuildId = GuildId,
                UserId = 222UL,
                Message = "Ship the release",
                TriggerAt = DateTime.UtcNow.AddHours(-2),
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                Status = ReminderStatus.Delivered
            }
        };

        _reminderRepository
            .Setup(r => r.GetByGuildAsync(GuildId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<ReminderStatus?>(), It.IsAny<CancellationToken>()))
            .Callback<ulong, int, int, ReminderStatus?, CancellationToken>((_, _, _, status, _) => _lastStatusFilter = status)
            .ReturnsAsync(() => (_reminders.AsEnumerable(), _reminders.Count));
        _reminderRepository
            .Setup(r => r.GetGuildStatsAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((10, 4, 2, 1));
        _reminderRepository
            .Setup(r => r.GetByIdAsync(PendingReminderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _reminders[0]);

        // The page resolves these through IServiceScopeFactory (scope per operation).
        Services.AddSingleton(_guildService.Object);
        Services.AddSingleton(_reminderRepository.Object);
        Services.AddSingleton(new DiscordSocketClient());
        Services.AddLogging();

        // Declarative GuildAccess policy + the in-circuit resource-based recheck.
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("moderator-user");
        auth.SetPolicies("GuildAccess");
    }

    private IRenderedComponent<RemindersIndex> RenderPage() =>
        RenderComponent<RemindersIndex>(p => p.Add(c => c.GuildId, (long)GuildId));

    [Fact]
    public void RendersStatsCards_AndReminderRows()
    {
        var cut = RenderPage();

        // Stats from GetGuildStatsAsync
        cut.Markup.Should().Contain("Total Reminders");
        cut.Markup.Should().Contain("Awaiting delivery");
        cut.Markup.Should().Contain(">10<");
        cut.Markup.Should().Contain(">4<");
        cut.Markup.Should().Contain(">2<");
        cut.Markup.Should().Contain(">1<");

        // Rows (usernames fall back to Unknown (id) with a disconnected client)
        cut.Markup.Should().Contain("Water the plants");
        cut.Markup.Should().Contain("Ship the release");
        cut.Markup.Should().Contain("Unknown (111)");
        cut.Markup.Should().Contain("Pending");
        cut.Markup.Should().Contain("Delivered");
    }

    [Fact]
    public void StatusFilter_AppliesImmediately_AndClearLinkResets()
    {
        var cut = RenderPage();

        cut.Find("select#status").Change(((int)ReminderStatus.Failed).ToString());
        cut.WaitForAssertion(() => _lastStatusFilter.Should().Be(ReminderStatus.Failed));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Clear Filters").Click();
        cut.WaitForAssertion(() => _lastStatusFilter.Should().BeNull());
    }

    [Fact]
    public void CancelReminder_ConfirmedThroughModal_UpdatesRepository_AndShowsSuccessAlert()
    {
        var cut = RenderPage();

        // Only the pending reminder shows the cancel action.
        cut.FindAll("button[title='Cancel Reminder']").First().Click();

        cut.WaitForAssertion(() => cut.Find("[role=alertdialog]"));
        cut.Markup.Should().Contain("Cancel Reminder");
        cut.Find("[role=alertdialog]").QuerySelectorAll("button")
            .First(b => b.TextContent.Trim() == "Cancel Reminder")
            .Click();

        cut.WaitForAssertion(() =>
        {
            _reminderRepository.Verify(r => r.UpdateAsync(
                It.Is<Reminder>(rem => rem.Id == PendingReminderId && rem.Status == ReminderStatus.Cancelled),
                It.IsAny<CancellationToken>()), Times.Once);
            cut.Markup.Should().Contain("Reminder cancelled successfully.");
        });
    }

    [Fact]
    public void KeepingReminderInModal_DoesNotUpdateRepository()
    {
        var cut = RenderPage();

        cut.FindAll("button[title='Cancel Reminder']").First().Click();
        cut.WaitForAssertion(() => cut.Find("[role=alertdialog]"));
        cut.Find("[role=alertdialog]").QuerySelectorAll("button")
            .First(b => b.TextContent.Trim() == "Keep Reminder")
            .Click();

        _reminderRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Reminder>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
