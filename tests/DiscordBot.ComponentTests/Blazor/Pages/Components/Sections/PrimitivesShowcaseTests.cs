using Bunit;
using DiscordBot.Bot.Blazor.Pages.Components.Sections;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Pages.Components.Sections;

/// <summary>
/// Smoke test for the Tier 1a showcase section - proves every primitive renders together without
/// throwing and that each section is present, mirroring what <c>Pages/Components.cshtml</c> shows
/// for the same partials (docs/articles/blazor-components.md "Component contract", point 10).
/// </summary>
public class PrimitivesShowcaseTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersEverySection()
    {
        var cut = Render<PrimitivesShowcase>();

        foreach (var testId in new[]
                 {
                     "showcase-buttons", "showcase-badges", "showcase-alerts", "showcase-cards",
                     "showcase-skeletons", "showcase-spinners", "showcase-empty-states", "showcase-kbd"
                 })
        {
            cut.Find($"[data-testid='{testId}']").Should().NotBeNull();
        }
    }

    [Fact]
    public void RemovableBadge_UpdatesCounter_OnRemoveClick()
    {
        var cut = Render<PrimitivesShowcase>();

        cut.Find("[data-testid='badge-removed-count']").TextContent.Should().Contain("0");

        cut.Find("button.badge-remove").Click();

        cut.Find("[data-testid='badge-removed-count']").TextContent.Should().Contain("1");
    }

    [Fact]
    public void CollapsibleCard_TogglesOnHeaderClick()
    {
        var cut = Render<PrimitivesShowcase>();

        cut.Find("button[aria-expanded='true']").Click();

        cut.Find("button[aria-expanded='false']").Should().NotBeNull();
    }

    [Fact]
    public void IconButtons_RenderRealPathData()
    {
        // Regression guard: the Icon Buttons section's IconLeft="IconPaths.X" values previously
        // lacked the "@" prefix a string-typed component parameter needs to be evaluated as C#
        // (Blazor treats an unprefixed value as a literal string for string parameters), so
        // every icon rendered the literal text "IconPaths.X" as the SVG `d` instead of path data.
        var cut = Render<PrimitivesShowcase>();

        var iconButtons = cut.Find("[data-testid='showcase-buttons']");
        var paths = iconButtons.QuerySelectorAll("svg path");

        paths.Should().NotBeEmpty();
        foreach (var path in paths)
        {
            var d = path.GetAttribute("d");
            d.Should().StartWith("M");
            d.Should().NotContain("IconPaths");
        }

        // Spot-check the first Icon Buttons entry (Save, IconLeft="IconPaths.CheckCircle").
        iconButtons.QuerySelector("button svg path")!.GetAttribute("d").Should().Be(IconPaths.CheckCircle);
    }
}
