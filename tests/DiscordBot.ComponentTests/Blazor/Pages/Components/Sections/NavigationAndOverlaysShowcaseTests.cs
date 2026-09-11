using Bunit;
using DiscordBot.Bot.Blazor.Pages.Components.Sections;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Pages.Components.Sections;

/// <summary>
/// Smoke test for the Tier 3 showcase section - proves every navigation/overlay component renders
/// together without throwing, mirroring PrimitivesShowcaseTests for Tier 1a
/// (docs/articles/blazor-components.md "Component contract", point 10).
/// </summary>
public class NavigationAndOverlaysShowcaseTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersEverySection()
    {
        AddAuthorizedAdmin().SetPolicies("RequireAdmin");
        var cut = Render<NavigationAndOverlaysShowcase>();

        foreach (var testId in new[]
                 {
                     "showcase-tabgroup-inpage", "showcase-tabgroup-navigation", "showcase-modal",
                     "showcase-confirm-modal", "showcase-toasts", "showcase-loading-overlay",
                     "showcase-preview-popover", "showcase-guild-selector", "showcase-highlight",
                     "showcase-restart-banner"
                 })
        {
            cut.Find($"[data-testid='{testId}']").Should().NotBeNull();
        }
    }

    [Fact]
    public void InPageTabGroup_SwitchingTabs_ShowsOnlySelectedPanel()
    {
        AddAuthorizedAdmin().SetPolicies("RequireAdmin");
        var cut = Render<NavigationAndOverlaysShowcase>();

        cut.Find("[data-testid='panel-overview']").Should().NotBeNull();
        cut.FindAll("[data-testid='panel-activity']").Should().BeEmpty();

        cut.Find("button[data-tab-id='activity']").Click();

        cut.FindAll("[data-testid='panel-overview']").Should().BeEmpty();
        cut.Find("[data-testid='panel-activity']").Should().NotBeNull();
    }

    [Fact]
    public void ConfirmButton_OpensPlainConfirmModal()
    {
        AddAuthorizedAdmin().SetPolicies("RequireAdmin");
        var cut = Render<NavigationAndOverlaysShowcase>();

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Delete item").Click();

        cut.Find("#showcase-confirm-plain").Should().NotBeNull();
    }

    [Fact]
    public void ToastButton_AddsToastToHost()
    {
        AddAuthorizedAdmin().SetPolicies("RequireAdmin");
        var cut = Render<NavigationAndOverlaysShowcase>();

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Success").Click();

        cut.WaitForAssertion(() => cut.Find(".toast-success").Should().NotBeNull());
    }

    [Fact]
    public void Highlight_RendersMarkForBasicExample()
    {
        AddAuthorizedAdmin().SetPolicies("RequireAdmin");
        var cut = Render<NavigationAndOverlaysShowcase>();

        cut.Find("[data-testid='highlight-basic'] mark").TextContent.Should().Be("fox");
    }

    [Fact]
    public void NotAdmin_RestartBannerSectionRendersEmptyBanner()
    {
        AddAuthorization().SetAuthorized("member").SetRoles("Member");
        var cut = Render<NavigationAndOverlaysShowcase>();

        cut.Find("[data-testid='showcase-restart-banner']").Should().NotBeNull();
        cut.FindAll("[data-testid='showcase-restart-banner'] div.mb-6").Should().BeEmpty();
    }
}
