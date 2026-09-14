using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Overlays;

/// <summary>
/// Tests for <see cref="GuildPreviewContent"/> - the <see cref="GuildPreviewDto"/>-facing adapter
/// onto <see cref="GuildPreviewPopoverContent"/> (docs/plans/blazor-port-plan.md Phase 4 cluster 4d).
/// </summary>
public class GuildPreviewContentTests : BlazorComponentTestContext
{
    private static readonly GuildPreviewDto Model = new()
    {
        GuildId = 789,
        Name = "Test Guild",
        MemberCount = 42,
        OwnerUsername = "owner",
        BotJoinedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public void RendersName_AndLiteralGuildRouteLinks()
    {
        var cut = Render<GuildPreviewContent>(p => p.Add(x => x.Model, Model));

        cut.Find(".preview-username").TextContent.Should().Be("Test Guild");
        cut.Find("a[href='/Guilds/Details/789']").Should().NotBeNull();
        cut.Find("a[href='/Guilds/Edit/789']").Should().NotBeNull();
    }
}
