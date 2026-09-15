using System.Security.Claims;
using Bunit;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Configuration;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Utilities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using EditPage = DiscordBot.Bot.Blazor.Pages.Guilds.ScheduledMessages.Edit;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds.ScheduledMessages;

/// <summary>
/// Component tests for <see cref="EditPage"/>, the routable replacement for
/// <c>Pages/Guilds/ScheduledMessages/Edit.cshtml</c> + <c>EditModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b). Covers the UTC-to-local prefill, the
/// local-to-UTC conversion on save, and delete.
/// </summary>
public class EditTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;
    private const string TimeZoneId = "America/Toronto";
    private static readonly Guid MessageId = Guid.NewGuid();

    private readonly Mock<IScheduledMessageService> _service = new();
    private readonly Mock<IDiscordChannelResolver> _channelResolver = new();

    public EditTests()
    {
        Services.AddSingleton(_service.Object);
        Services.AddSingleton(_channelResolver.Object);
        _channelResolver.Setup(r => r.GetTextChannels(GuildId)).Returns([new ChannelInfo(55, "general", 0, ChannelDisplayType.Text)]);
        JSInterop.SetupModule("./js/blazor/browser.js").Setup<string>("getTimeZone", _ => true).SetResult(TimeZoneId);
    }

    private static GuildContext Context() => new(
        Guild: new GuildDto { Id = GuildId, Name = "Test Guild" },
        GuildId: GuildId,
        GuildIdString: GuildId.ToString(),
        IsAppAdmin: true,
        IsGuildAdmin: false,
        CanEdit: true,
        AudioEnabled: true,
        RatWatchEnabled: true,
        Tabs: GuildNavigationConfig.GetTabs());

    private void RegisterMockProvider()
    {
        var mock = new Mock<IGuildContextProvider>();
        mock.Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Ok(Context()));
        Services.AddScoped(_ => mock.Object);
    }

    private static ScheduledMessageDto BuildMessage(DateTime nextExecutionUtc) => new()
    {
        Id = MessageId,
        GuildId = GuildId,
        ChannelId = 55,
        Title = "Daily standup",
        Content = "Time to stand up!",
        Frequency = ScheduleFrequency.Daily,
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow.AddDays(-3),
        NextExecutionAt = nextExecutionUtc
    };

    private IRenderedComponent<EditPage> RenderEdit(ScheduledMessageDto message)
    {
        AddAuthorizedAdmin();
        AddBunitPersistentComponentState();
        RegisterMockProvider();
        _service.Setup(s => s.GetByIdAsync(MessageId, It.IsAny<CancellationToken>())).ReturnsAsync(message);
        SetInteractiveRendererInfo();
        return Render<EditPage>(p => p.Add(x => x.GuildId, (long)GuildId).Add(x => x.Id, MessageId));
    }

    [Fact]
    public void Load_PrefillsNextExecutionAt_ConvertedToDetectedLocalTimeZone()
    {
        var nextUtc = new DateTime(2026, 6, 1, 13, 0, 0, DateTimeKind.Utc);
        var cut = RenderEdit(BuildMessage(nextUtc));

        var expectedLocal = TimezoneHelper.ConvertFromUtc(nextUtc, TimeZoneId);
        cut.WaitForAssertion(() =>
            cut.Find("#Input_NextExecutionAt").GetAttribute("value").Should().StartWith(expectedLocal.ToString("yyyy-MM-ddTHH:mm")));
    }

    [Fact]
    public void Save_ConvertsEditedLocalTime_BackToUtc()
    {
        var cut = RenderEdit(BuildMessage(new DateTime(2026, 6, 1, 13, 0, 0, DateTimeKind.Utc)));
        ScheduledMessageUpdateDto? captured = null;
        _service.Setup(s => s.UpdateAsync(MessageId, It.IsAny<ScheduledMessageUpdateDto>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, ScheduledMessageUpdateDto, CancellationToken>((_, dto, _) => captured = dto)
            .ReturnsAsync(BuildMessage(new DateTime(2026, 6, 1, 13, 0, 0, DateTimeKind.Utc)));

        var newLocal = new DateTime(2026, 6, 2, 8, 30, 0);
        cut.WaitForState(() => cut.Find("#Input_NextExecutionAt").GetAttribute("value") is { Length: > 0 });
        cut.Find("#Input_NextExecutionAt").Change(newLocal.ToString("yyyy-MM-ddTHH:mm"));
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => captured.Should().NotBeNull());
        captured!.NextExecutionAt.Should().Be(TimezoneHelper.ConvertToUtc(newLocal, TimeZoneId));
    }

    [Fact]
    public void Delete_Confirmed_CallsDeleteAsync_AndNavigatesToIndex()
    {
        var cut = RenderEdit(BuildMessage(DateTime.UtcNow.AddHours(1)));
        _service.Setup(s => s.DeleteAsync(MessageId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Delete").Click();
        cut.Find("#delete-scheduled-message-edit-modal").QuerySelectorAll("button").Last().Click();

        cut.WaitForAssertion(() => _service.Verify(s => s.DeleteAsync(MessageId, It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public void UnknownMessage_RendersNotFound()
    {
        AddAuthorizedAdmin();
        AddBunitPersistentComponentState();
        RegisterMockProvider();
        _service.Setup(s => s.GetByIdAsync(MessageId, It.IsAny<CancellationToken>())).ReturnsAsync((ScheduledMessageDto?)null);
        SetInteractiveRendererInfo();

        var cut = Render<EditPage>(p => p.Add(x => x.GuildId, (long)GuildId).Add(x => x.Id, MessageId));

        cut.Markup.Should().Contain("not found");
    }
}
