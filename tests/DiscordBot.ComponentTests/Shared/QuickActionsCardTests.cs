using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class QuickActionsCardTests : TestContext
{
    private static QuickActionItemViewModel LinkAction(string label = "Docs") => new()
    {
        Id = "docs",
        Label = label,
        ActionType = QuickActionType.Link,
        Href = "/docs"
    };

    [Fact]
    public void RendersTitle_AndLinkAction()
    {
        var cut = RenderComponent<QuickActionsCard>(p => p
            .Add(x => x.Model, new QuickActionsCardViewModel
            {
                Title = "Quick Actions",
                Actions = new[] { LinkAction() }
            }));

        cut.Find("h2").TextContent.Should().Be("Quick Actions");
        var link = cut.Find("a.quick-action-card");
        link.GetAttribute("href").Should().Be("/docs");
        link.TextContent.Trim().Should().Be("Docs");
    }

    [Fact]
    public void FiltersAdminOnlyActions_ForNonAdmins()
    {
        var actions = new[]
        {
            LinkAction("Public"),
            LinkAction("Secret") with { IsAdminOnly = true }
        };

        var nonAdmin = RenderComponent<QuickActionsCard>(p => p
            .Add(x => x.Model, new QuickActionsCardViewModel { Actions = actions, UserIsAdmin = false }));
        var admin = RenderComponent<QuickActionsCard>(p => p
            .Add(x => x.Model, new QuickActionsCardViewModel { Actions = actions, UserIsAdmin = true }));

        nonAdmin.FindAll("a.quick-action-card").Should().HaveCount(1);
        admin.FindAll("a.quick-action-card").Should().HaveCount(2);
    }

    [Fact]
    public void PostAction_RaisesOnSubmitAction()
    {
        QuickActionItemViewModel? submitted = null;
        var action = new QuickActionItemViewModel
        {
            Id = "sync",
            Label = "Sync",
            ActionType = QuickActionType.PostAction,
            Handler = "SyncGuilds"
        };

        var cut = RenderComponent<QuickActionsCard>(p => p
            .Add(x => x.Model, new QuickActionsCardViewModel { Actions = new[] { action } })
            .Add(x => x.OnSubmitAction, a => submitted = a));

        var button = cut.Find("button.quick-action-card");
        button.GetAttribute("data-handler").Should().Be("SyncGuilds");
        button.Click();

        submitted.Should().Be(action);
    }

    [Fact]
    public void ConfirmationAction_RaisesOnShowConfirmation()
    {
        QuickActionItemViewModel? requested = null;
        var action = new QuickActionItemViewModel
        {
            Id = "restart",
            Label = "Restart",
            ActionType = QuickActionType.PostAction,
            RequiresConfirmation = true,
            ConfirmationModalId = "restartModal"
        };

        var cut = RenderComponent<QuickActionsCard>(p => p
            .Add(x => x.Model, new QuickActionsCardViewModel { Actions = new[] { action } })
            .Add(x => x.OnShowConfirmation, a => requested = a));

        cut.Find("button.quick-action-card").Click();

        requested.Should().Be(action);
    }
}
