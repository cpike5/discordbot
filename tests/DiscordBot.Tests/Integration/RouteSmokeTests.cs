using System.Net;
using DiscordBot.Bot.Configuration;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// Boots the real application (offline mode, PostgreSQL) once, signs in as the seeded admin
/// through the login form, and requests every Razor Page route and every guild navigation
/// URL. Hand-built URLs and wrong redirect targets show up here as 404s and 500s.
/// </summary>
[Collection(OfflineAppHostCollection.Name)]
public class RouteSmokeTests : IClassFixture<RouteSmokeTests.AppFixture>
{
    /// <summary>
    /// Routes known to fail, each tied to the plan item that fixes it
    /// (docs/plans/ux-polish-audit-and-plan.md). Remove the entry with the fix; the sweep
    /// fails if a listed route starts passing, so this list cannot go stale.
    /// </summary>
    private static readonly Dictionary<string, string> KnownFailures = new(StringComparer.OrdinalIgnoreCase)
    {
    };

    private readonly AppFixture _app;

    public RouteSmokeTests(AppFixture app)
    {
        _app = app;
    }

    [Fact]
    public async Task EveryPageRoute_WithValidIds_IsNeitherNotFoundNorServerError()
    {
        var failures = new List<string>();
        var fixedKnownFailures = new List<string>();

        foreach (var url in _app.PageUrls)
        {
            var response = await _app.Client.GetAsync(url);
            var status = (int)response.StatusCode;
            var failed = status >= 500 || response.StatusCode == HttpStatusCode.NotFound;

            if (KnownFailures.ContainsKey(url))
            {
                if (!failed)
                {
                    fixedKnownFailures.Add(url);
                }
            }
            else if (failed)
            {
                failures.Add($"{url} -> {status} (ended at {response.RequestMessage?.RequestUri?.PathAndQuery})");
            }
        }

        _app.PageUrls.Should().NotBeEmpty();
        failures.Should().BeEmpty("every page route should render or redirect somewhere that renders");
        fixedKnownFailures.Should().BeEmpty("these routes now work, so remove them from KnownFailures");
    }

    [Fact]
    public async Task EveryGuildNavigationUrl_Renders()
    {
        var failures = new List<string>();

        foreach (var tab in GuildNavigationConfig.GetTabs())
        {
            var url = tab.GetUrl(AppFixture.GuildId);
            var response = await _app.Client.GetAsync(url);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                failures.Add($"{tab.Id}: {url} -> {(int)response.StatusCode}");
            }
        }

        failures.Should().BeEmpty("every guild navigation tab should open its page");
    }

    [Theory]
    [InlineData("/Admin/AuditLogs", "/Admin/Logs?tab=audit")]
    [InlineData("/Admin/MessageLogs", "/Admin/Logs?tab=messages")]
    public async Task LegacyLogPages_RedirectToTheirTab(string url, string expectedTarget)
    {
        var response = await _app.Client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.RequestMessage!.RequestUri!.PathAndQuery.Should().Be(expectedTarget);
    }

    [Fact]
    public async Task CommandsTabAlias_OpensExecutionLogsWithSearchTerm()
    {
        var html = await _app.Client.GetStringAsync("/Commands?tab=logs&search=ping");

        html.Should().MatchRegex("data-tab-id=\"execution-logs\"[^>]*aria-selected=\"true\"|aria-selected=\"true\"[^>]*data-tab-id=\"execution-logs\"");
        html.Should().Contain("id=\"logs-SearchTerm\"").And.Contain("value=\"ping\"");
    }

    /// <summary>
    /// One application host for the class (<see cref="OfflineAppHost"/>), plus the list of
    /// page URLs to sweep, built from the app's own route table.
    /// </summary>
    public sealed class AppFixture : IAsyncLifetime
    {
        public const ulong GuildId = OfflineAppHost.GuildId;

        /// <summary>
        /// Route parameters the fixture can fill. A route with any other required parameter
        /// (a log id, a currency id, a member id...) needs seeded rows and is not swept.
        /// </summary>
        private static readonly Dictionary<string, string> KnownParameters = new(StringComparer.OrdinalIgnoreCase)
        {
            ["guildId"] = GuildId.ToString()
        };

        /// <summary>
        /// Pages whose own route parameter named <c>id</c> is a guild id. Empty now: Guilds/Edit
        /// takes <c>{guildId}</c> like every other guild page, which the authorization policy needs.
        /// </summary>
        private static readonly HashSet<string> PagesWhoseIdIsAGuild = new(StringComparer.OrdinalIgnoreCase)
        {
        };

        /// <summary>Pages that read a required <c>id</c> from the query string, and the id to send.</summary>
        private readonly Dictionary<string, string> _queryIds = new(StringComparer.OrdinalIgnoreCase);

        private OfflineAppHost? _host;

        public HttpClient Client => _host!.Client;

        public IReadOnlyList<string> PageUrls { get; private set; } = Array.Empty<string>();

        public async Task InitializeAsync()
        {
            _host = await OfflineAppHost.StartAsync();

            // A few pages take their id from the query string rather than the route.
            var adminId = await _host.GetAdminUserIdAsync();
            _queryIds["/Admin/Users/Details"] = adminId;
            _queryIds["/Admin/Users/Edit"] = adminId;

            PageUrls = BuildPageUrls(_host.Services);
        }

        public async Task DisposeAsync()
        {
            if (_host != null)
            {
                await _host.DisposeAsync();
            }
        }

        private IReadOnlyList<string> BuildPageUrls(IServiceProvider services)
        {
            var pages = services.GetRequiredService<IActionDescriptorCollectionProvider>()
                .ActionDescriptors.Items
                .OfType<PageActionDescriptor>()
                .Where(p => p.AttributeRouteInfo?.Template != null);

            var urls = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var page in pages)
            {
                var url = Fill(page.ViewEnginePath, RoutePatternFactory.Parse(page.AttributeRouteInfo!.Template!));
                if (url == null)
                {
                    continue;
                }

                if (_queryIds.TryGetValue(page.ViewEnginePath, out var queryId))
                {
                    url += "?id=" + Uri.EscapeDataString(queryId);
                }

                urls.Add(url);
            }

            // Index pages are also routed with an explicit "/Index" segment; sweep the canonical one.
            urls.RemoveWhere(u => u.Split('/').Contains("Index", StringComparer.OrdinalIgnoreCase));

            // Signing out mid-sweep would turn every later request into a login redirect.
            urls.RemoveWhere(u => u.StartsWith("/Account/Logout", StringComparison.OrdinalIgnoreCase));

            // The error pages answer with their own status code by design.
            urls.RemoveWhere(u => u.StartsWith("/Error/", StringComparison.OrdinalIgnoreCase));

            return urls.ToList();
        }

        private static string? Fill(string pagePath, RoutePattern pattern)
        {
            var segments = new List<string>();
            foreach (var segment in pattern.PathSegments)
            {
                var text = "";
                foreach (var part in segment.Parts)
                {
                    switch (part)
                    {
                        case RoutePatternLiteralPart literal:
                            text += literal.Content;
                            break;
                        case RoutePatternSeparatorPart separator:
                            text += separator.Content;
                            break;
                        case RoutePatternParameterPart parameter:
                            if (KnownParameters.TryGetValue(parameter.Name, out var value))
                            {
                                text += value;
                            }
                            else if (parameter.Name == "id" && PagesWhoseIdIsAGuild.Contains(pagePath))
                            {
                                text += GuildId.ToString();
                            }
                            else if (parameter.IsOptional)
                            {
                                continue;
                            }
                            else
                            {
                                return null;
                            }

                            break;
                    }
                }

                if (text.Length > 0)
                {
                    segments.Add(text);
                }
            }

            return "/" + string.Join('/', segments);
        }
    }
}
