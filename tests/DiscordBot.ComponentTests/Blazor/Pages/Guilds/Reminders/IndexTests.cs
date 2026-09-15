using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.Services.Reminders;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using IndexPage = DiscordBot.Bot.Blazor.Pages.Guilds.Reminders.Index;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds.Reminders;

/// <summary>
/// Component tests for <see cref="IndexPage"/>, the routable replacement for
/// <c>Pages/Guilds/Reminders/Index.cshtml</c> + <c>IndexModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b). Ports the behaviours of the deleted
/// <c>tests/DiscordBot.Tests/Bot/Pages/Guilds/Reminders/IndexModelTests.cs</c> to bUnit.
/// </summary>
public class IndexTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;

    private readonly Mock<IReminderRepository> _repository = new();
    private readonly Mock<IReminderUserResolver> _userResolver = new();

    public IndexTests()
    {
        Services.AddSingleton(_repository.Object);
        Services.AddSingleton(_userResolver.Object);
        _userResolver.Setup(r => r.ResolveAsync(GuildId, It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReminderUserInfo("Alice", null));
        _repository.Setup(r => r.GetGuildStatsAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((5, 2, 1, 0));
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

    private void NavigateTo(string path)
        => ((BunitNavigationManager)Services.GetRequiredService<NavigationManager>()).NavigateTo(path);

    private static Reminder BuildReminder(ReminderStatus status = ReminderStatus.Pending) => new()
    {
        Id = Guid.NewGuid(),
        GuildId = GuildId,
        UserId = 42,
        Message = "Stand up",
        TriggerAt = DateTime.UtcNow.AddHours(1),
        CreatedAt = DateTime.UtcNow,
        Status = status
    };

    private void SetUp()
    {
        AddAuthorization().SetAuthorized("viewer").SetClaims(new Claim(ClaimTypes.NameIdentifier, "viewer-1"));
        AddBunitPersistentComponentState();
        RegisterMockProvider();
        SetInteractiveRendererInfo();
    }

    [Fact]
    public void List_RendersRow_WithResolvedUsername()
    {
        SetUp();
        _repository.Setup(r => r.GetByGuildAsync(GuildId, 1, 20, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { BuildReminder() }, 1));
        NavigateTo($"/Guilds/Reminders/{GuildId}");

        var cut = Render<IndexPage>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.FindAll("[data-testid='reminder-row']").Should().HaveCount(1);
        cut.Markup.Should().Contain("Alice").And.Contain("Stand up");
    }

    [Fact]
    public void UnresolvedUser_ShowsUnknownPlaceholder()
    {
        SetUp();
        _userResolver.Setup(r => r.ResolveAsync(GuildId, 42UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReminderUserInfo("Unknown (42)", null));
        _repository.Setup(r => r.GetByGuildAsync(GuildId, 1, 20, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { BuildReminder() }, 1));
        NavigateTo($"/Guilds/Reminders/{GuildId}");

        var cut = Render<IndexPage>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.Markup.Should().Contain("Unknown (42)");
    }

    [Fact]
    public void StatCards_RenderRepositoryStats()
    {
        SetUp();
        _repository.Setup(r => r.GetByGuildAsync(GuildId, 1, 20, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Reminder>(), 0));
        NavigateTo($"/Guilds/Reminders/{GuildId}");

        var cut = Render<IndexPage>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.Markup.Should().Contain("5").And.Contain("2").And.Contain("1");
    }

    [Fact]
    public void PendingReminder_CanCancel_ButtonPresent()
    {
        SetUp();
        _repository.Setup(r => r.GetByGuildAsync(GuildId, 1, 20, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { BuildReminder(ReminderStatus.Pending) }, 1));
        NavigateTo($"/Guilds/Reminders/{GuildId}");

        var cut = Render<IndexPage>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.FindAll("button[title='Cancel Reminder']").Should().HaveCount(1);
    }

    [Fact]
    public void DeliveredReminder_CannotCancel_NoButton()
    {
        SetUp();
        _repository.Setup(r => r.GetByGuildAsync(GuildId, 1, 20, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { BuildReminder(ReminderStatus.Delivered) }, 1));
        NavigateTo($"/Guilds/Reminders/{GuildId}");

        var cut = Render<IndexPage>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.FindAll("button[title='Cancel Reminder']").Should().BeEmpty();
    }

    [Fact]
    public void CancelPending_Confirmed_UpdatesStatus_AndToasts()
    {
        SetUp();
        var reminder = BuildReminder(ReminderStatus.Pending);
        _repository.Setup(r => r.GetByGuildAsync(GuildId, 1, 20, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { reminder }, 1));
        _repository.Setup(r => r.GetByIdAsync(reminder.Id, It.IsAny<CancellationToken>())).ReturnsAsync(reminder);
        NavigateTo($"/Guilds/Reminders/{GuildId}");

        var cut = Render<IndexPage>(p => p.Add(x => x.GuildId, (long)GuildId));
        cut.Find("button[title='Cancel Reminder']").Click();
        cut.Find("#cancel-reminder-modal").QuerySelectorAll("button").Last().Click();

        cut.WaitForAssertion(() => _repository.Verify(
            r => r.UpdateAsync(It.Is<Reminder>(x => x.Id == reminder.Id && x.Status == ReminderStatus.Cancelled), It.IsAny<CancellationToken>()),
            Times.Once));
        var toast = Services.GetRequiredService<IToastService>();
        cut.WaitForAssertion(() => toast.Toasts.Should().Contain(t => t.Level == ToastLevel.Success));
    }

    [Fact]
    public void StatusFilter_Change_NavigatesWithStatusQuery()
    {
        SetUp();
        _repository.Setup(r => r.GetByGuildAsync(GuildId, 1, 20, It.IsAny<ReminderStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Reminder>(), 0));
        NavigateTo($"/Guilds/Reminders/{GuildId}");
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        var cut = Render<IndexPage>(p => p.Add(x => x.GuildId, (long)GuildId));
        cut.Find("#status").Change(((int)ReminderStatus.Failed).ToString());

        navMan.Uri.Should().Contain($"status={(int)ReminderStatus.Failed}");
    }

    [Fact]
    public void PageFallback_LegacyPageQuery_IsUsed_WhenPageNumberAbsent()
    {
        SetUp();
        _repository.Setup(r => r.GetByGuildAsync(GuildId, 2, 20, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Reminder>(), 0));
        NavigateTo($"/Guilds/Reminders/{GuildId}?page=2");

        Render<IndexPage>(p => p.Add(x => x.GuildId, (long)GuildId));

        _repository.Verify(r => r.GetByGuildAsync(GuildId, 2, 20, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
