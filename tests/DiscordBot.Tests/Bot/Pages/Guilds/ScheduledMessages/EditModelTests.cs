using DiscordBot.Bot.Pages.Guilds.ScheduledMessages;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Guilds.ScheduledMessages;

/// <summary>
/// Tests for the scheduled message edit page: the next-run time must survive a failed save
/// unchanged (it used to shift by the viewer's UTC offset on every failed submit), the guild chrome
/// must still render, and the message can be deleted.
/// </summary>
public class EditModelTests
{
    private const ulong GuildId = 123456789UL;
    private static readonly Guid MessageId = Guid.NewGuid();

    private readonly Mock<IScheduledMessageService> _messages = new();
    private readonly Mock<IGuildService> _guilds = new();
    private readonly Mock<IDiscordChannelResolver> _channels = new();
    private readonly EditModel _model;

    public EditModelTests()
    {
        _guilds
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildDto { Id = GuildId, Name = "Test Server", IsActive = true });
        _channels
            .Setup(c => c.GetTextChannels(GuildId))
            .Returns(new List<ChannelInfo> { new(111UL, "general", 0, ChannelDisplayType.Text) });
        _messages
            .Setup(s => s.GetByIdAsync(MessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScheduledMessageDto
            {
                Id = MessageId,
                GuildId = GuildId,
                ChannelId = 111UL,
                Title = "Standup",
                Content = "Time for standup",
                Frequency = ScheduleFrequency.Daily,
                IsEnabled = true,
                NextExecutionAt = new DateTime(2026, 10, 5, 14, 0, 0, DateTimeKind.Utc)
            });

        _model = new EditModel(_messages.Object, _guilds.Object, _channels.Object, Mock.Of<ILogger<EditModel>>());

        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new PageActionDescriptor(), new ModelStateDictionary());
        _model.PageContext = new PageContext(actionContext);
    }

    [Fact]
    public async Task OnGet_GivesTheStoredTimeAsUtc_ForTheBrowserToConvertOnce()
    {
        await _model.OnGetAsync(GuildId, MessageId, CancellationToken.None);

        _model.Editor.NextExecutionUtcIso.Should().Be("2026-10-05T14:00:00.000Z");
        _model.Editor.NextExecutionLocal.Should().BeNull("the field is filled by the script from the UTC instant");
        _model.Editor.DirtyOnLoad.Should().BeFalse();
    }

    [Fact]
    public async Task OnPost_WithInvalidInput_ShowsTheTypedTimeAsTypedAndDoesNotAskForAConversion()
    {
        // The user is in UTC+10 and typed 09:30 local time, but left the title empty
        _model.Input = new EditModel.InputModel
        {
            Title = "",
            Content = "Time for standup",
            ChannelId = 111UL,
            Frequency = ScheduleFrequency.Daily,
            IsEnabled = true,
            NextExecutionAt = new DateTime(2026, 10, 6, 9, 30, 0),
            UserTimezone = "Australia/Brisbane"
        };
        _model.ModelState.AddModelError("Input.Title", "Title is required.");

        var result = await _model.OnPostAsync(GuildId, MessageId, CancellationToken.None);

        result.Should().BeOfType<PageResult>();
        _model.Editor.NextExecutionLocal.Should().Be("2026-10-06T09:30", "the field shows what the user typed");
        _model.Editor.NextExecutionUtcIso.Should().BeNull("a typed local time must never be converted again as if it were UTC");
        _model.Editor.SummaryNextRunUtcIso.Should().Be("2026-10-05T23:30:00.000Z", "the summary is the typed time converted once, with the user's zone");
        _model.Editor.DirtyOnLoad.Should().BeTrue("the form shows input that is not saved yet");
    }

    [Fact]
    public async Task OnPost_WithInvalidInput_KeepsTheGuildChrome()
    {
        _model.Input = new EditModel.InputModel { Content = "x", ChannelId = 111UL, NextExecutionAt = DateTime.UtcNow };
        _model.ModelState.AddModelError("Input.Title", "Title is required.");

        await _model.OnPostAsync(GuildId, MessageId, CancellationToken.None);

        _model.Header.GuildId.Should().Be(GuildId);
        _model.Navigation.GuildId.Should().Be(GuildId);
        _model.Navigation.Tabs.Should().NotBeEmpty();
        _model.Breadcrumb.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task OnPost_ForACustomScheduleWithoutACron_FailsOnTheCronField()
    {
        _model.Input = new EditModel.InputModel
        {
            Title = "t",
            Content = "x",
            ChannelId = 111UL,
            Frequency = ScheduleFrequency.Custom,
            NextExecutionAt = DateTime.UtcNow
        };

        var result = await _model.OnPostAsync(GuildId, MessageId, CancellationToken.None);

        result.Should().BeOfType<PageResult>();
        _model.ModelState["Input.CronExpression"]!.Errors.Should().NotBeEmpty();
        _messages.Verify(s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<ScheduledMessageUpdateDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnPost_WithATimeInADaylightSavingGap_ReportsItOnTheTimeField_InsteadOfThrowing()
    {
        // 02:30 on 8 March 2026 does not exist in New York: the clocks go from 02:00 to 03:00
        _model.Input = new EditModel.InputModel
        {
            Title = "t",
            Content = "x",
            ChannelId = 111UL,
            Frequency = ScheduleFrequency.Daily,
            IsEnabled = true,
            NextExecutionAt = new DateTime(2026, 3, 8, 2, 30, 0),
            UserTimezone = "America/New_York"
        };

        var result = await _model.OnPostAsync(GuildId, MessageId, CancellationToken.None);

        result.Should().BeOfType<PageResult>();
        _model.ModelState["Input.NextExecutionAt"]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("That time doesn't exist in America/New_York because of a daylight-saving change. Pick a time before or after it.");
        _model.Editor.NextExecutionLocal.Should().Be("2026-03-08T02:30", "the form keeps what the user typed");
        _model.Editor.SummaryNextRunUtcIso.Should().BeNull("a skipped time stands for no instant");
        _messages.Verify(s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<ScheduledMessageUpdateDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnPostDelete_RemovesTheMessageAndReturnsToTheList()
    {
        _messages.Setup(s => s.DeleteAsync(MessageId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _model.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(new DefaultHttpContext(), Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());

        var result = await _model.OnPostDeleteAsync(GuildId, MessageId, CancellationToken.None);

        result.Should().BeOfType<RedirectToPageResult>().Which.PageName.Should().Be("Index");
        _messages.Verify(s => s.DeleteAsync(MessageId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnPostDelete_ForAMessageInAnotherGuild_IsNotFoundAndDeletesNothing()
    {
        var otherMessage = Guid.NewGuid();
        _messages
            .Setup(s => s.GetByIdAsync(otherMessage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScheduledMessageDto { Id = otherMessage, GuildId = 999UL });

        var result = await _model.OnPostDeleteAsync(GuildId, otherMessage, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        _messages.Verify(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
