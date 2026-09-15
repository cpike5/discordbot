using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Overlays;

/// <summary>
/// Tests for <see cref="UserPreviewContent"/> - the <see cref="UserPreviewDto"/>-facing adapter
/// onto <see cref="UserPreviewPopoverContent"/> (docs/plans/blazor-port-plan.md Phase 4 cluster
/// 4d). Covers the DTO-to-ViewModel mapping this component owns, especially the guild-context-
/// dependent Profile/Moderation-history URLs <see cref="UserPreviewPopoverContent"/> itself has
/// no opinion on.
/// </summary>
public class UserPreviewContentTests : BlazorComponentTestContext
{
    private static readonly UserPreviewDto Model = new()
    {
        UserId = 123,
        Username = "exampleuser",
        DisplayName = "Example User",
        Roles = ["Member"],
        IsVerified = true
    };

    [Fact]
    public void NoGuildContext_ProfileUrl_PointsAtAdminUsersDirectory_AndHasNoModHistoryLink()
    {
        var cut = Render<UserPreviewContent>(p => p.Add(x => x.Model, Model));

        cut.Find(".preview-username").TextContent.Should().Be("exampleuser");
        cut.Find("a[href='/Admin/Users/Details?id=123']").Should().NotBeNull();
        cut.FindAll("a").Should().HaveCount(1);
    }

    [Fact]
    public void WithGuildContext_ProfileAndModHistoryUrls_PointAtGuildModerationProfile()
    {
        var cut = Render<UserPreviewContent>(p => p
            .Add(x => x.Model, Model)
            .Add(x => x.GuildId, 456UL));

        var links = cut.FindAll("a");
        links.Should().HaveCount(2);
        links.Should().OnlyContain(a => a.GetAttribute("href") == "/Guilds/456/Members/123/Moderation");
    }
}
