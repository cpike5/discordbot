using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.Enums;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Widgets;

public class AuditLogCardTests : BlazorComponentTestContext
{
    private static readonly AuditLogItem Log = new(
        Id: 1,
        Timestamp: DateTime.UtcNow.AddMinutes(-5),
        RelativeTime: "5 min ago",
        Category: AuditLogCategory.Configuration,
        CategoryName: "Configuration",
        CategoryIcon: "M1 1h1",
        Action: AuditLogAction.SettingChanged,
        ActionName: "Setting Changed",
        ActorDisplayName: "admin",
        TargetType: null,
        TargetId: null,
        GuildName: "Test Guild",
        Description: "admin changed settings");

    // AuditLogCard always renders an <AuthorizeView Policy="RequireAdmin"> (header link, and the
    // footer link when there are logs), so every test needs bUnit's fake authorization services
    // registered - AddAuthorization() alone (no SetAuthorized) is enough to avoid the
    // "no IAuthorizationPolicyProvider registered" error; it renders as unauthenticated.

    [Fact]
    public void NoLogs_RendersEmptyState()
    {
        AddAuthorization();
        var cut = Render<AuditLogCard>();
        cut.Markup.Should().Contain("No audit logs yet");
    }

    [Fact]
    public void Logs_RendersEachEntry()
    {
        AddAuthorization();
        var cut = Render<AuditLogCard>(p => p.Add(x => x.Logs, new[] { Log }));

        cut.Markup.Should().Contain("Setting Changed");
        cut.Markup.Should().Contain("Test Guild");
        cut.Markup.Should().Contain("5 min ago");
    }

    [Fact]
    public void FromLogs_ViewModelHelper_StillProducesRenderableItems()
    {
        AddAuthorization();
        var dto = new DiscordBot.Core.DTOs.AuditLogDto
        {
            Id = 2,
            Timestamp = DateTime.UtcNow,
            Category = AuditLogCategory.User,
            CategoryName = "User",
            Action = AuditLogAction.Login,
            ActionName = "Login",
            ActorDisplayName = "bob"
        };

        var viewModel = AuditLogCardViewModel.FromLogs(new[] { dto });
        var cut = Render<AuditLogCard>(p => p.Add(x => x.Logs, viewModel.Logs));

        cut.Markup.Should().Contain("bob logged in");
    }

    [Fact]
    public void FooterLink_OnlyVisibleToAdmin()
    {
        AddAuthorization().SetNotAuthorized();
        var cut = Render<AuditLogCard>(p => p.Add(x => x.Logs, new[] { Log }));
        cut.Markup.Should().NotContain("View all audit logs");
    }

    [Fact]
    public void FooterLink_VisibleWhenAuthorizedForRequireAdminPolicy()
    {
        AddAuthorization().SetAuthorized("admin").SetPolicies("RequireAdmin");
        var cut = Render<AuditLogCard>(p => p.Add(x => x.Logs, new[] { Log }));
        cut.Markup.Should().Contain("View all audit logs");
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        AddAuthorization();
        var cut = Render<AuditLogCard>(p => p.Add(x => x.Class, "extra-class").AddUnmatched("data-testid", "audit-card"));
        var root = cut.Find("div");
        root.ClassList.Should().Contain("extra-class");
        root.GetAttribute("data-testid").Should().Be("audit-card");
    }
}
