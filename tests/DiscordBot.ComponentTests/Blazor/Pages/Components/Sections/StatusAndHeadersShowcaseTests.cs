using Bunit;
using DiscordBot.Bot.Blazor.Pages.Components.Sections;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Pages.Components.Sections;

/// <summary>
/// Smoke test for the Tier 1b showcase section - proves every status/header/pagination component
/// renders together without throwing and that each section is present, mirroring what
/// <c>Pages/Components.cshtml</c> shows for StatusIndicator/Pagination and adding equivalent
/// coverage for the rest of the tier (docs/articles/blazor-components.md "Component contract",
/// point 10).
/// </summary>
public class StatusAndHeadersShowcaseTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersEverySection()
    {
        var cut = Render<StatusAndHeadersShowcase>();

        foreach (var testId in new[]
                 {
                     "showcase-status-indicators", "showcase-moderation-badges", "showcase-hero-metric-cards",
                     "showcase-guild-stats-card", "showcase-dashboard-widget", "showcase-breadcrumb",
                     "showcase-page-header", "showcase-guild-header", "showcase-pagination"
                 })
        {
            cut.Find($"[data-testid='{testId}']").Should().NotBeNull();
        }
    }

    [Fact]
    public void PaginationCallbackMode_UpdatesDisplayedPage_OnButtonClick()
    {
        var cut = Render<StatusAndHeadersShowcase>();

        cut.Find("[data-testid='pagination-callback-page']").TextContent.Should().Contain("1");

        var callbackSection = cut.Find("[data-testid='showcase-pagination']");
        callbackSection.QuerySelectorAll("button").First(b => b.TextContent.Trim() == "2").Click();

        cut.Find("[data-testid='pagination-callback-page']").TextContent.Should().Contain("2");
    }

    [Fact]
    public void HeroMetricCardAndDashboardWidget_IconPathUsages_RenderRealPathData()
    {
        // Regression guard: this section's IconPath="IconPaths.X" values (HeroMetricCard,
        // DashboardWidget) previously lacked the "@" prefix a string-typed component parameter
        // needs to be evaluated as C# (Blazor treats an unprefixed value as a literal string for
        // string parameters), so they rendered the literal text "IconPaths.X" as the SVG `d`.
        var cut = Render<StatusAndHeadersShowcase>();

        var heroCards = cut.Find("[data-testid='showcase-hero-metric-cards']");
        heroCards.QuerySelector(".hero-metric-icon svg path")!.GetAttribute("d").Should().Be(IconPaths.Server);

        var widget = cut.Find("[data-testid='showcase-dashboard-widget']");
        var widgetIconPath = widget.QuerySelector("svg path")!.GetAttribute("d");
        widgetIconPath.Should().StartWith("M");
        widgetIconPath.Should().NotContain("IconPaths");
        widgetIconPath.Should().Be(IconPaths.CheckCircle);
    }
}
