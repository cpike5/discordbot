using System.Text.RegularExpressions;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// Two endpoints with the same route and verb make every request to it fail with
/// AmbiguousMatchException (a 500). ModTagsController and UserModerationController both once
/// defined POST/DELETE users/{userId}/tags/{tagName}, so adding or removing a tag from a user's
/// moderation profile always failed. This keeps that from coming back, for any controller.
/// </summary>
[Collection(OfflineAppHostCollection.Name)]
public class RouteAmbiguityTests : IClassFixture<RouteAmbiguityTests.AppFixture>
{
    private readonly AppFixture _app;

    public RouteAmbiguityTests(AppFixture app)
    {
        _app = app;
    }

    [Fact]
    public void NoTwoEndpoints_ShareARouteAndVerb()
    {
        var endpoints = _app.Host.Services.GetServices<EndpointDataSource>().SelectMany(d => d.Endpoints).OfType<RouteEndpoint>().ToList();
        endpoints.Should().HaveCountGreaterThan(100, "the check must be looking at the real route table");

        var duplicates = endpoints
            .SelectMany(e =>
            {
                var verbs = e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? new List<string> { "*" };
                return verbs.Select(v => (Key: $"{v} {Normalize(e.RoutePattern)}", Name: e.DisplayName ?? e.RoutePattern.RawText));
            })
            .GroupBy(x => x.Key)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(" | ", g.Select(x => x.Name))}")
            .ToList();

        duplicates.Should().BeEmpty("a duplicated route and verb is an AmbiguousMatchException for every caller");
    }

    private static string Normalize(RoutePattern pattern)
        => Regex.Replace(pattern.RawText ?? string.Empty, @"\{[^}:?*]+", "{").ToLowerInvariant();

    public sealed class AppFixture : IAsyncLifetime
    {
        public OfflineAppHost Host { get; private set; } = null!;

        public async Task InitializeAsync() => Host = await OfflineAppHost.StartAsync();

        public async Task DisposeAsync() => await Host.DisposeAsync();
    }
}
