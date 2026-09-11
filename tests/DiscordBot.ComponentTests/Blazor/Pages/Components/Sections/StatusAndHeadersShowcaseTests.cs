using Bunit;
using DiscordBot.Bot.Blazor.Pages.Components.Sections;
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
}
