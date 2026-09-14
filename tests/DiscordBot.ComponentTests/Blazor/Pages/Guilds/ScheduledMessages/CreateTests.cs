using System.Security.Claims;
using Bunit;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Configuration;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Utilities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using CreatePage = DiscordBot.Bot.Blazor.Pages.Guilds.ScheduledMessages.Create;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds.ScheduledMessages;

/// <summary>
/// Component tests for <see cref="CreatePage"/>, the routable replacement for
/// <c>Pages/Guilds/ScheduledMessages/Create.cshtml</c> + <c>CreateModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b). Covers required-field validation,
/// cron-required-only-for-Custom, and the timezone conversion applied before the service call.
/// </summary>
public class CreateTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;
    private const string TimeZoneId = "America/Toronto";

    private readonly Mock<IScheduledMessageService> _service = new();
    private readonly Mock<IDiscordChannelResolver> _channelResolver = new();

    public CreateTests()
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

    private IRenderedComponent<CreatePage> RenderCreate()
    {
        AddAuthorizedAdmin();
        AddBunitPersistentComponentState();
        RegisterMockProvider();
        SetInteractiveRendererInfo();
        return Render<CreatePage>(p => p.Add(x => x.GuildId, (long)GuildId));
    }

    [Fact]
    public void MissingRequiredFields_DoesNotCallCreateAsync()
    {
        var cut = RenderCreate();

        cut.Find("form").Submit();

        _service.Verify(s => s.CreateAsync(It.IsAny<ScheduledMessageCreateDto>(), It.IsAny<CancellationToken>()), Times.Never);
        cut.Markup.Should().Contain("required");
    }

    [Fact]
    public void CustomFrequency_WithoutCron_ShowsError_AndDoesNotCallCreateAsync()
    {
        var cut = RenderCreate();
        cut.Find("#Input_Title").Input("Nightly digest");
        cut.Find("#Input_Content").Input("Here's what happened today");
        cut.Find("#Input_ChannelId").Change("55");
        cut.Find("#Input_NextExecutionAt").Change(DateTime.Now.AddHours(2).ToString("yyyy-MM-ddTHH:mm"));
        // Select Custom frequency via its schedule-type card.
        cut.FindAll("label").Single(l => l.TextContent.Trim() == "Custom schedule").Click();

        cut.Find("form").Submit();

        cut.Markup.Should().Contain("Cron expression is required");
        _service.Verify(s => s.CreateAsync(It.IsAny<ScheduledMessageCreateDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ValidSubmit_ConvertsLocalTimeToUtc_UsingDetectedTimeZone()
    {
        var cut = RenderCreate();
        ScheduledMessageCreateDto? captured = null;
        _service.Setup(s => s.CreateAsync(It.IsAny<ScheduledMessageCreateDto>(), It.IsAny<CancellationToken>()))
            .Callback<ScheduledMessageCreateDto, CancellationToken>((dto, _) => captured = dto)
            .ReturnsAsync(new ScheduledMessageDto { Id = Guid.NewGuid() });

        cut.Find("#Input_Title").Input("Nightly digest");
        cut.Find("#Input_Content").Input("Here's what happened today");
        cut.Find("#Input_ChannelId").Change("55");
        var localTime = new DateTime(2026, 6, 1, 9, 0, 0);
        cut.Find("#Input_NextExecutionAt").Change(localTime.ToString("yyyy-MM-ddTHH:mm"));

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => captured.Should().NotBeNull());
        var expectedUtc = TimezoneHelper.ConvertToUtc(localTime, TimeZoneId);
        captured!.NextExecutionAt.Should().Be(expectedUtc);
        captured.GuildId.Should().Be(GuildId);
        captured.ChannelId.Should().Be(55UL);
    }
}
