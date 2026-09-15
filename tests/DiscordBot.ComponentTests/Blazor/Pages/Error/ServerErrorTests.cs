using Bunit;
using DiscordBot.Bot.Blazor.Pages.Error;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Error;

/// <summary>
/// Covers the static SSR port of Pages/Error/500.cshtml + ServerErrorModel
/// (docs/plans/blazor-port-plan.md §5 Phase 3): the RequestId fallback to
/// HttpContext.TraceIdentifier, and the dev-vs-prod gating of exception message/stack trace via
/// IWebHostEnvironment.IsDevelopment() + IExceptionHandlerPathFeature, identical to
/// ServerErrorModel.OnGet.
/// </summary>
public class ServerErrorTests : BlazorComponentTestContext
{
    private readonly Mock<IWebHostEnvironment> _environment = new();

    public ServerErrorTests()
    {
        Services.AddSingleton(_environment.Object);
        Services.AddSingleton(NullLogger<ServerError>.Instance as Microsoft.Extensions.Logging.ILogger<ServerError>);
    }

    [Fact]
    public void RendersHeadingAndMessage()
    {
        _environment.Setup(e => e.EnvironmentName).Returns("Production");

        var cut = Render<ServerError>(parameters => parameters
            .AddCascadingValue(new DefaultHttpContext()));

        cut.Find("h1").TextContent.Should().Be("Something Went Wrong");
        cut.Markup.Should().Contain("500 Internal Server Error");
    }

    [Fact]
    public void RequestIdParameter_TakesPrecedenceOverTraceIdentifier()
    {
        _environment.Setup(e => e.EnvironmentName).Returns("Production");
        var httpContext = new DefaultHttpContext { TraceIdentifier = "trace-abc" };

        // [SupplyParameterFromQuery] parameters can't be set via ComponentParameterCollectionBuilder.Add
        // (bUnit throws, directing you here instead) - navigate the fake NavigationManager to a URI
        // carrying the query parameter before rendering, same as the real router would.
        var navMan = Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo(navMan.GetUriWithQueryParameter("RequestId", "explicit-request-id"));

        var cut = Render<ServerError>(parameters => parameters
            .AddCascadingValue(httpContext));

        cut.Markup.Should().Contain("explicit-request-id");
        cut.Markup.Should().NotContain("trace-abc");
    }

    [Fact]
    public void NoRequestIdParameter_FallsBackToTraceIdentifier()
    {
        _environment.Setup(e => e.EnvironmentName).Returns("Production");
        var httpContext = new DefaultHttpContext { TraceIdentifier = "trace-xyz" };

        var cut = Render<ServerError>(parameters => parameters
            .AddCascadingValue(httpContext));

        cut.Markup.Should().Contain("trace-xyz");
    }

    [Fact]
    public void Production_HidesExceptionDetails_EvenWhenFeaturePresent()
    {
        _environment.Setup(e => e.EnvironmentName).Returns("Production");
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<IExceptionHandlerPathFeature>(new ExceptionHandlerFeature
        {
            Error = new InvalidOperationException("boom"),
            Path = "/some/path"
        });

        var cut = Render<ServerError>(parameters => parameters
            .AddCascadingValue(httpContext));

        cut.Markup.Should().NotContain("boom");
        cut.Markup.Should().NotContain("Development Mode");
    }

    [Fact]
    public void Development_ShowsExceptionMessageAndStackTrace()
    {
        _environment.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var httpContext = new DefaultHttpContext();
        Exception exception;
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (InvalidOperationException ex)
        {
            exception = ex;
        }

        httpContext.Features.Set<IExceptionHandlerPathFeature>(new ExceptionHandlerFeature
        {
            Error = exception,
            Path = "/some/path"
        });

        var cut = Render<ServerError>(parameters => parameters
            .AddCascadingValue(httpContext));

        cut.Markup.Should().Contain("Development Mode");
        cut.Markup.Should().Contain("boom");
        cut.Find("pre").TextContent.Should().Contain(nameof(Development_ShowsExceptionMessageAndStackTrace));
    }

    [Fact]
    public void Development_WithNoExceptionFeature_OmitsDevelopmentBlock()
    {
        _environment.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var cut = Render<ServerError>(parameters => parameters
            .AddCascadingValue(new DefaultHttpContext()));

        cut.Markup.Should().NotContain("Development Mode");
    }
}
