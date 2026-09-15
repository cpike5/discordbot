using Bunit;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.ComponentTests.Blazor.Guilds;

/// <summary>
/// Component tests for <see cref="GuildContextGate"/>: renders one of four states for a
/// <see cref="GuildContextResult"/> - loading, not found, forbidden, or the guild page content via
/// <c>ChildContent</c>. See "GuildContext" in <c>docs/architecture/patterns.md</c>.
/// </summary>
public class GuildContextGateTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;

    private static readonly GuildContext Context = new(
        Guild: new GuildDto { Id = GuildId, Name = "Test Guild" },
        GuildId: GuildId,
        GuildIdString: GuildId.ToString(),
        IsAppAdmin: false,
        IsGuildAdmin: false,
        CanEdit: false,
        AudioEnabled: false,
        RatWatchEnabled: false,
        Tabs: Array.Empty<DiscordBot.Bot.ViewModels.Components.GuildNavItem>());

    private static readonly RenderFragment<GuildContext> ChildTemplate = ctx => builder =>
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "data-testid", "child-content");
        builder.AddContent(2, ctx.GuildIdString);
        builder.CloseElement();
    };

    [Fact]
    public void Result_Null_RendersDefaultLoadingFragment()
    {
        var cut = Render<GuildContextGate>(p => p
            .Add(x => x.Result, null)
            .Add(x => x.ChildContent, ChildTemplate));

        cut.FindComponent<DiscordBot.Bot.Blazor.Shared.LoadingSpinner>().Should().NotBeNull();
        cut.FindAll("[data-testid='child-content']").Should().BeEmpty();
    }

    [Fact]
    public void Result_NotFound_RendersDefaultEmptyState()
    {
        var cut = Render<GuildContextGate>(p => p
            .Add(x => x.Result, GuildContextResult.NotFound())
            .Add(x => x.ChildContent, ChildTemplate));

        cut.Markup.Should().Contain("Server Not Found");
        cut.FindAll("[data-testid='child-content']").Should().BeEmpty();
    }

    [Fact]
    public void Result_Forbidden_RendersDefaultEmptyState()
    {
        var cut = Render<GuildContextGate>(p => p
            .Add(x => x.Result, GuildContextResult.Forbidden())
            .Add(x => x.ChildContent, ChildTemplate));

        cut.Markup.Should().Contain("Access Denied");
        cut.FindAll("[data-testid='child-content']").Should().BeEmpty();
    }

    [Fact]
    public void Result_Ok_RendersChildContent_WithTheResolvedContext()
    {
        var cut = Render<GuildContextGate>(p => p
            .Add(x => x.Result, GuildContextResult.Ok(Context))
            .Add(x => x.ChildContent, ChildTemplate));

        cut.Find("[data-testid='child-content']").TextContent.Should().Be(GuildId.ToString());
        cut.Markup.Should().NotContain("Server Not Found");
        cut.Markup.Should().NotContain("Access Denied");
    }

    [Fact]
    public void Result_NotFound_UsesCustomNotFoundContent_WhenProvided()
    {
        RenderFragment custom = builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-testid", "custom-not-found");
            builder.CloseElement();
        };

        var cut = Render<GuildContextGate>(p => p
            .Add(x => x.Result, GuildContextResult.NotFound())
            .Add(x => x.ChildContent, ChildTemplate)
            .Add(x => x.NotFoundContent, custom));

        cut.Find("[data-testid='custom-not-found']").Should().NotBeNull();
        cut.Markup.Should().NotContain("Server Not Found");
    }

    [Fact]
    public void Result_Forbidden_UsesCustomForbiddenContent_WhenProvided()
    {
        RenderFragment custom = builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-testid", "custom-forbidden");
            builder.CloseElement();
        };

        var cut = Render<GuildContextGate>(p => p
            .Add(x => x.Result, GuildContextResult.Forbidden())
            .Add(x => x.ChildContent, ChildTemplate)
            .Add(x => x.ForbiddenContent, custom));

        cut.Find("[data-testid='custom-forbidden']").Should().NotBeNull();
    }

    [Fact]
    public void Result_Null_UsesCustomLoadingContent_WhenProvided()
    {
        RenderFragment custom = builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-testid", "custom-loading");
            builder.CloseElement();
        };

        var cut = Render<GuildContextGate>(p => p
            .Add(x => x.Result, null)
            .Add(x => x.ChildContent, ChildTemplate)
            .Add(x => x.LoadingContent, custom));

        cut.Find("[data-testid='custom-loading']").Should().NotBeNull();
        cut.FindComponents<DiscordBot.Bot.Blazor.Shared.LoadingSpinner>().Should().BeEmpty();
    }

    [Fact]
    public void Class_And_AdditionalAttributes_PassThrough_ToRootElement()
    {
        var cut = Render<GuildContextGate>(p => p
            .Add(x => x.Result, GuildContextResult.Ok(Context))
            .Add(x => x.ChildContent, ChildTemplate)
            .Add(x => x.Class, "extra-class")
            .AddUnmatched("data-testid", "gate-root"));

        var root = cut.Find("[data-testid='gate-root']");
        root.ClassList.Should().Contain("extra-class");
    }
}
