using DiscordBot.Bot.Pages.Guilds.ScheduledMessages;
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
/// The create page's handling of a next-run time that a daylight-saving change skips.
/// </summary>
public class CreateModelTests
{
    private const ulong GuildId = 123456789UL;

    private readonly Mock<IScheduledMessageService> _messages = new();
    private readonly CreateModel _model;

    public CreateModelTests()
    {
        var guilds = new Mock<IGuildService>();
        guilds
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildDto { Id = GuildId, Name = "Test Server", IsActive = true });
        var channels = new Mock<IDiscordChannelResolver>();
        channels
            .Setup(c => c.GetTextChannels(GuildId))
            .Returns(new List<ChannelInfo> { new(111UL, "general", 0, ChannelDisplayType.Text) });

        _model = new CreateModel(_messages.Object, guilds.Object, channels.Object, Mock.Of<ILogger<CreateModel>>());
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new PageActionDescriptor(), new ModelStateDictionary());
        _model.PageContext = new PageContext(actionContext);
    }

    [Fact]
    public async Task OnPost_WithATimeInADaylightSavingGap_ReportsItOnTheTimeField_InsteadOfThrowing()
    {
        _model.Input = new CreateModel.InputModel
        {
            Title = "t",
            Content = "x",
            ChannelId = 111UL,
            Frequency = ScheduleFrequency.Daily,
            IsEnabled = true,
            NextExecutionAt = new DateTime(2026, 3, 8, 2, 30, 0),
            UserTimezone = "America/New_York"
        };

        var result = await _model.OnPostAsync(GuildId, CancellationToken.None);

        result.Should().BeOfType<PageResult>();
        _model.ModelState["Input.NextExecutionAt"]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().StartWith("That time doesn't exist in America/New_York because of a daylight-saving change");
        _model.Editor.NextExecutionLocal.Should().Be("2026-03-08T02:30");
        _model.Editor.SummaryNextRunUtcIso.Should().BeNull();
        _messages.Verify(s => s.CreateAsync(It.IsAny<ScheduledMessageCreateDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnPost_WithAnOrdinaryTime_StillCreatesTheMessage()
    {
        _messages
            .Setup(s => s.CreateAsync(It.IsAny<ScheduledMessageCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScheduledMessageDto { Id = Guid.NewGuid(), GuildId = GuildId });
        _model.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(new DefaultHttpContext(), Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        _model.Input = new CreateModel.InputModel
        {
            Title = "t",
            Content = "x",
            ChannelId = 111UL,
            Frequency = ScheduleFrequency.Daily,
            IsEnabled = true,
            NextExecutionAt = new DateTime(2026, 3, 8, 3, 30, 0),
            UserTimezone = "America/New_York"
        };

        var result = await _model.OnPostAsync(GuildId, CancellationToken.None);

        result.Should().BeOfType<RedirectToPageResult>();
        _messages.Verify(s => s.CreateAsync(
            It.Is<ScheduledMessageCreateDto>(d => d.NextExecutionAt == new DateTime(2026, 3, 8, 7, 30, 0, DateTimeKind.Utc)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
