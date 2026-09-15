using System.Security.Claims;
using Bunit;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Configuration;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using IndexPage = DiscordBot.Bot.Blazor.Pages.Guilds.ScheduledMessages.Index;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds.ScheduledMessages;

/// <summary>
/// Component tests for <see cref="IndexPage"/>, the routable replacement for
/// <c>Pages/Guilds/ScheduledMessages/Index.cshtml</c> + <c>IndexModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b).
/// </summary>
public class IndexTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;

    private readonly Mock<IScheduledMessageService> _service = new();
    private readonly Mock<IDiscordChannelResolver> _channelResolver = new();

    public IndexTests()
    {
        Services.AddSingleton(_service.Object);
        Services.AddSingleton(_channelResolver.Object);
        _channelResolver.Setup(r => r.ResolveChannelName(GuildId, It.IsAny<ulong>())).Returns("general");
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

    private static ScheduledMessageDto BuildMessage(bool isEnabled = true) => new()
    {
        Id = Guid.NewGuid(),
        GuildId = GuildId,
        ChannelId = 55,
        Title = "Daily standup",
        Content = "Time to stand up!",
        Frequency = ScheduleFrequency.Daily,
        IsEnabled = isEnabled,
        CreatedAt = DateTime.UtcNow,
        NextExecutionAt = DateTime.UtcNow.AddHours(1)
    };

    private void SetUp()
    {
        AddAuthorizedAdmin();
        AddBunitPersistentComponentState();
        RegisterMockProvider();
        SetInteractiveRendererInfo();
    }

    [Fact]
    public void List_RendersRow()
    {
        SetUp();
        _service.Setup(s => s.GetByGuildIdAsync(GuildId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { BuildMessage() }, 1));

        var cut = Render<IndexPage>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.FindAll("[data-testid='scheduled-message-row']").Should().HaveCount(1);
        cut.Markup.Should().Contain("Daily standup").And.Contain("general");
    }

    [Fact]
    public void NoMessages_ShowsEmptyStateWithCreateLink()
    {
        SetUp();
        _service.Setup(s => s.GetByGuildIdAsync(GuildId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<ScheduledMessageDto>(), 0));

        var cut = Render<IndexPage>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.Markup.Should().Contain("Create First Message");
    }

    [Fact]
    public void Toggle_CallsUpdateAsync_FlippingIsEnabled()
    {
        SetUp();
        var message = BuildMessage(isEnabled: true);
        _service.Setup(s => s.GetByGuildIdAsync(GuildId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { message }, 1));
        var toggled = BuildMessage(isEnabled: false);
        toggled.Id = message.Id;
        _service.Setup(s => s.UpdateAsync(message.Id, It.Is<ScheduledMessageUpdateDto>(d => d.IsEnabled == false), It.IsAny<CancellationToken>()))
            .ReturnsAsync(toggled);

        var cut = Render<IndexPage>(p => p.Add(x => x.GuildId, (long)GuildId));
        cut.Find("button[title='Pause']").Click();

        cut.WaitForAssertion(() => _service.Verify(
            s => s.UpdateAsync(message.Id, It.Is<ScheduledMessageUpdateDto>(d => d.IsEnabled == false), It.IsAny<CancellationToken>()),
            Times.Once));
    }

    [Fact]
    public void Delete_Confirmed_CallsDeleteAsync_AndReloads()
    {
        SetUp();
        var message = BuildMessage();
        _service.SetupSequence(s => s.GetByGuildIdAsync(GuildId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { message }, 1))
            .ReturnsAsync((Array.Empty<ScheduledMessageDto>(), 0));
        _service.Setup(s => s.DeleteAsync(message.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var cut = Render<IndexPage>(p => p.Add(x => x.GuildId, (long)GuildId));
        cut.Find("button[title='Delete']").Click();
        cut.Find("#delete-scheduled-message-modal").QuerySelectorAll("button").Last().Click();

        cut.WaitForAssertion(() => _service.Verify(s => s.DeleteAsync(message.Id, It.IsAny<CancellationToken>()), Times.Once));
        var toast = Services.GetRequiredService<IToastService>();
        cut.WaitForAssertion(() => toast.Toasts.Should().Contain(t => t.Level == ToastLevel.Success));
    }
}
