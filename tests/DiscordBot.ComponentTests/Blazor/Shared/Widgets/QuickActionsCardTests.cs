using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Widgets;

public class QuickActionsCardTests : BlazorComponentTestContext
{
    private static readonly QuickActionItemViewModel LinkAction = new()
    {
        Id = "link", Label = "Settings", IconPath = "M1 1h1", ActionType = QuickActionType.Link, Href = "/settings"
    };

    private static readonly QuickActionItemViewModel PostAction = new()
    {
        Id = "post", Label = "Sync", IconPath = "M1 1h1", ActionType = QuickActionType.PostAction, Handler = "Sync"
    };

    private static readonly QuickActionItemViewModel ConfirmAction = new()
    {
        Id = "confirm", Label = "Restart", IconPath = "M1 1h1", ActionType = QuickActionType.PostAction,
        Handler = "Restart", RequiresConfirmation = true, IsAdminOnly = true
    };

    [Fact]
    public void LinkAction_RendersAnchor()
    {
        var cut = Render<QuickActionsCard>(p => p.Add(x => x.Actions, new[] { LinkAction }));
        cut.Find("a.quick-action-card").GetAttribute("href").Should().Be("/settings");
    }

    [Fact]
    public void PostAction_WithoutConfirmation_InvokesOnAction()
    {
        QuickActionItemViewModel? invoked = null;
        var cut = Render<QuickActionsCard>(p => p
            .Add(x => x.Actions, new[] { PostAction })
            .Add(x => x.OnAction, a => invoked = a));

        cut.Find("button.quick-action-card").Click();

        invoked.Should().Be(PostAction);
    }

    [Fact]
    public void PostAction_RequiresConfirmation_RaisesOnConfirmRequested_NotOnAction()
    {
        QuickActionItemViewModel? confirmRequested = null;
        var actionInvoked = false;
        var cut = Render<QuickActionsCard>(p => p
            .Add(x => x.Actions, new[] { ConfirmAction })
            .Add(x => x.UserIsAdmin, true)
            .Add(x => x.OnConfirmRequested, a => confirmRequested = a)
            .Add(x => x.OnAction, _ => actionInvoked = true));

        cut.Find("button.quick-action-card").Click();

        confirmRequested.Should().Be(ConfirmAction);
        actionInvoked.Should().BeFalse();
    }

    [Fact]
    public void AdminOnlyAction_HiddenFromNonAdmin()
    {
        var cut = Render<QuickActionsCard>(p => p
            .Add(x => x.Actions, new[] { ConfirmAction })
            .Add(x => x.UserIsAdmin, false));

        cut.FindAll(".quick-action-card").Should().BeEmpty();
    }

    [Fact]
    public void AdminOnlyAction_VisibleToAdmin()
    {
        var cut = Render<QuickActionsCard>(p => p
            .Add(x => x.Actions, new[] { ConfirmAction })
            .Add(x => x.UserIsAdmin, true));

        cut.FindAll(".quick-action-card").Should().ContainSingle();
    }

    [Fact]
    public void Title_DefaultsToQuickActions()
    {
        var cut = Render<QuickActionsCard>();
        cut.Find("h2").TextContent.Should().Be("Quick Actions");
    }
}
