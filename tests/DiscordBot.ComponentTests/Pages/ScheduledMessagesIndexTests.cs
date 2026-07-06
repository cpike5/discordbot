using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Pages.Guilds.ScheduledMessages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Pages;

/// <summary>
/// Tests for the routed Blazor twin of the old
/// Pages/Guilds/ScheduledMessages/Index.cshtml (Phase F migration). Covers the
/// table rendering, the in-circuit pause/resume toggle, and the delete flow
/// behind the shared ConfirmModal.
/// </summary>
public class ScheduledMessagesIndexTests : TestContext
{
    private const ulong GuildId = 123456789012345678UL;
    private static readonly Guid ActiveMessageId = Guid.NewGuid();
    private static readonly Guid PausedMessageId = Guid.NewGuid();

    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<IScheduledMessageService> _scheduledMessageService = new();
    private readonly Mock<IDiscordChannelResolver> _channelResolver = new();

    private readonly List<ScheduledMessageDto> _messages;

    public ScheduledMessagesIndexTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _guildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildDto { Id = GuildId, Name = "Test Guild", IsActive = true });

        _messages = new List<ScheduledMessageDto>
        {
            new()
            {
                Id = ActiveMessageId,
                GuildId = GuildId,
                ChannelId = 42,
                Title = "Daily standup ping",
                Content = "Standup in 10 minutes!",
                Frequency = ScheduleFrequency.Daily,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                NextExecutionAt = DateTime.UtcNow.AddHours(2)
            },
            new()
            {
                Id = PausedMessageId,
                GuildId = GuildId,
                ChannelId = 42,
                Title = "Weekly digest",
                Content = "Here is your weekly digest.",
                Frequency = ScheduleFrequency.Weekly,
                IsEnabled = false,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                NextExecutionAt = DateTime.UtcNow.AddDays(4)
            }
        };

        _scheduledMessageService
            .Setup(s => s.GetByGuildIdAsync(GuildId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => (_messages.AsEnumerable(), _messages.Count));
        _scheduledMessageService
            .Setup(s => s.GetByIdAsync(ActiveMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _messages[0]);
        _scheduledMessageService
            .Setup(s => s.UpdateAsync(ActiveMessageId, It.IsAny<ScheduledMessageUpdateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, ScheduledMessageUpdateDto dto, CancellationToken _) =>
                new ScheduledMessageDto { Id = ActiveMessageId, GuildId = GuildId, IsEnabled = dto.IsEnabled ?? false });
        _scheduledMessageService
            .Setup(s => s.DeleteAsync(ActiveMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _channelResolver
            .Setup(r => r.ResolveChannelName(GuildId, 42UL))
            .Returns("general");

        // The page resolves these through IServiceScopeFactory (scope per operation).
        Services.AddSingleton(_guildService.Object);
        Services.AddSingleton(_scheduledMessageService.Object);
        Services.AddSingleton(_channelResolver.Object);
        Services.AddLogging();

        // Declarative policies + the in-circuit resource-based GuildAccess recheck.
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("admin-user");
        auth.SetPolicies("RequireAdmin", "GuildAccess");
    }

    private IRenderedComponent<ScheduledMessagesIndex> RenderPage() =>
        RenderComponent<ScheduledMessagesIndex>(p => p.Add(c => c.GuildId, (long)GuildId));

    [Fact]
    public void RendersMessageRows_WithStatusChannelAndSchedule()
    {
        var cut = RenderPage();

        cut.Markup.Should().Contain("Daily standup ping");
        cut.Markup.Should().Contain("Weekly digest");
        cut.Markup.Should().Contain("general");
        cut.Markup.Should().Contain("Daily");
        cut.Markup.Should().Contain("Weekly");
        cut.Markup.Should().Contain("Active");
        cut.Markup.Should().Contain("Paused");

        // Edit links point at the routed Blazor edit page
        cut.Markup.Should().Contain($"/Guilds/ScheduledMessages/Edit/{GuildId}/{ActiveMessageId}");
    }

    [Fact]
    public void RendersEmptyState_WithCreateLink_WhenNoMessages()
    {
        _messages.Clear();

        var cut = RenderPage();

        cut.Markup.Should().Contain("No Scheduled Messages");
        cut.Markup.Should().Contain($"/Guilds/ScheduledMessages/Create/{GuildId}");
    }

    [Fact]
    public void Toggle_PausesActiveMessage_AndShowsSuccessAlert()
    {
        var cut = RenderPage();

        cut.FindAll("button[title=Pause]").First().Click();

        cut.WaitForAssertion(() =>
        {
            _scheduledMessageService.Verify(s => s.UpdateAsync(
                ActiveMessageId,
                It.Is<ScheduledMessageUpdateDto>(d => d.IsEnabled == false),
                It.IsAny<CancellationToken>()), Times.Once);
            cut.Markup.Should().Contain("Scheduled message paused successfully.");
        });
    }

    [Fact]
    public void Delete_ConfirmedThroughModal_CallsService_AndShowsSuccessAlert()
    {
        var cut = RenderPage();

        cut.FindAll("button[title=Delete]").First().Click();

        cut.WaitForAssertion(() => cut.Find("[role=alertdialog]"));
        cut.Markup.Should().Contain("Delete Scheduled Message");
        cut.Find("[role=alertdialog]").QuerySelectorAll("button")
            .First(b => b.TextContent.Trim() == "Delete")
            .Click();

        cut.WaitForAssertion(() =>
        {
            _scheduledMessageService.Verify(s => s.DeleteAsync(ActiveMessageId, It.IsAny<CancellationToken>()), Times.Once);
            cut.Markup.Should().Contain("Scheduled message deleted successfully.");
        });
    }

    [Fact]
    public void CancellingDeleteModal_DoesNotCallService()
    {
        var cut = RenderPage();

        cut.FindAll("button[title=Delete]").First().Click();
        cut.WaitForAssertion(() => cut.Find("[role=alertdialog]"));
        cut.Find("[role=alertdialog]").QuerySelectorAll("button")
            .First(b => b.TextContent.Trim() == "Cancel")
            .Click();

        _scheduledMessageService.Verify(
            s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
