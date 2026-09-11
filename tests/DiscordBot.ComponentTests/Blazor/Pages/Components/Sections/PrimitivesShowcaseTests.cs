using Bunit;
using DiscordBot.Bot.Blazor.Pages.Components.Sections;
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
}
