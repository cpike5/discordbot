using Bunit;
using DiscordBot.Bot.Blazor.Pages.Error;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace DiscordBot.ComponentTests.Blazor.Pages.Error;

/// <summary>
/// Covers the static SSR port of Pages/Error/404.cshtml + NotFoundModel
/// (docs/plans/blazor-port-plan.md §5 Phase 3), including the fidelity fix described on
/// <see cref="NotFound"/>: NotFoundModel.OnGet read HttpContext.Request.Path directly, which
/// under UseStatusCodePagesWithReExecute is always "/Error/404" itself, not the URL the visitor
/// actually typed - this component prefers IStatusCodeReExecuteFeature.OriginalPath instead when
/// present.
/// </summary>
public class NotFoundTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersHeadingAndMessage()
    {
        var cut = Render<NotFound>(parameters => parameters
            .AddCascadingValue(new DefaultHttpContext()));

        cut.Find("h1").TextContent.Should().Be("Page Not Found");
        cut.Markup.Should().Contain("404 Not Found");
    }

    [Fact]
    public void WithReExecuteFeature_ShowsOriginalPathAndQueryString_NotTheErrorRoute()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/Error/404";
        httpContext.Features.Set<IStatusCodeReExecuteFeature>(new StatusCodeReExecuteFeature
        {
            OriginalPath = "/guilds/999/does-not-exist",
            OriginalQueryString = "?tab=settings"
        });

        var cut = Render<NotFound>(parameters => parameters
            .AddCascadingValue(httpContext));

        cut.Markup.Should().Contain("/guilds/999/does-not-exist?tab=settings");
        cut.Markup.Should().NotContain("Requested URL: /Error/404");
    }

    [Fact]
    public void WithoutReExecuteFeature_FallsBackToCurrentRequestPath()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/some/unmatched/route";

        var cut = Render<NotFound>(parameters => parameters
            .AddCascadingValue(httpContext));

        cut.Markup.Should().Contain("/some/unmatched/route");
    }

    [Fact]
    public void WithoutHttpContext_RendersWithoutThrowing_AndOmitsRequestedUrlLine()
    {
        var cut = Render<NotFound>();

        cut.Find("h1").TextContent.Should().Be("Page Not Found");
        cut.Markup.Should().NotContain("Requested URL:");
    }
}
