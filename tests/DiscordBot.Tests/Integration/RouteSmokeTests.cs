using System.Net;
using System.Text.RegularExpressions;
using DiscordBot.Bot.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// Boots the real application (offline mode, PostgreSQL) once, signs in as the seeded admin
/// through the login form, and requests every Razor Page route and every guild navigation
/// URL. Hand-built URLs and wrong redirect targets show up here as 404s and 500s.
/// </summary>
public class RouteSmokeTests : IClassFixture<RouteSmokeTests.AppFixture>
{
    /// <summary>
    /// Routes known to fail, each tied to the plan item that fixes it
    /// (docs/plans/ux-polish-audit-and-plan.md). Remove the entry with the fix; the sweep
    /// fails if a listed route starts passing, so this list cannot go stale.
    /// </summary>
    private static readonly Dictionary<string, string> KnownFailures = new(StringComparer.OrdinalIgnoreCase)
    {
        [$"/Guilds/AssistantMetrics/{AppFixture.GuildId}"] =
            "B-29 (Phase 8): 500 without OpenRouter:ApiKey because IAssistantService is not registered",
        [$"/Portal/Soundboard/{AppFixture.GuildId}"] = OfflinePortal,
        [$"/Portal/TTS/{AppFixture.GuildId}"] = OfflinePortal,
        [$"/Portal/VOX/{AppFixture.GuildId}"] = OfflinePortal
    };

    private const string OfflinePortal =
        "D15 (Phase 9): portal pages 404 when the guild is not in the Discord client, which it never is offline";

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
    /// One application host for the class. It owns a fresh database cloned from the migrated
    /// template, with one guild seeded so guild routes have a valid id.
    /// </summary>
    public sealed class AppFixture : IAsyncLifetime
    {
        public const ulong GuildId = 123456789012345678UL;
        private const string AdminEmail = "smoke-admin@example.com";
        private const string AdminPassword = "Smoke-Test-Pass-123!";

        /// <summary>
        /// Route parameters the fixture can fill. A route with any other required parameter
        /// (a log id, a currency id, a member id...) needs seeded rows and is not swept.
        /// </summary>
        private static readonly Dictionary<string, string> KnownParameters = new(StringComparer.OrdinalIgnoreCase)
        {
            ["guildId"] = GuildId.ToString()
        };

        /// <summary>Pages whose own route parameter named <c>id</c> is a guild id.</summary>
        private static readonly HashSet<string> PagesWhoseIdIsAGuild = new(StringComparer.OrdinalIgnoreCase)
        {
            "/Guilds/Edit"
        };

        /// <summary>Pages that read a required <c>id</c> from the query string, and the id to send.</summary>
        private readonly Dictionary<string, string> _queryIds = new(StringComparer.OrdinalIgnoreCase);

        private TestDatabase? _database;
        private WebApplicationFactory<Program>? _factory;

        public HttpClient Client { get; private set; } = null!;

        public IReadOnlyList<string> PageUrls { get; private set; } = Array.Empty<string>();

        public async Task InitializeAsync()
        {
            _database = TestDbContextFactory.CreateDatabase();
            await using (var context = _database.CreateContext())
            {
                context.Guilds.Add(new Guild { Id = GuildId, Name = "Smoke Test Guild", JoinedAt = DateTime.UtcNow, IsActive = true });

                // The member portal pages answer 404 for a guild without audio enabled
                context.GuildAudioSettings.Add(new GuildAudioSettings
                {
                    GuildId = GuildId,
                    AudioEnabled = true,
                    EnableMemberPortal = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }

            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:DefaultConnection", _database.ConnectionString);
                builder.UseSetting("Database:Provider", "PostgreSql");
                builder.UseSetting("Discord:OfflineMode", "true");
                builder.UseSetting("Identity:DefaultAdmin:Email", AdminEmail);
                builder.UseSetting("Identity:DefaultAdmin:Password", AdminPassword);
                builder.UseSetting("ElasticApm:Enabled", "false");
            });

            Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = true,
                HandleCookies = true
            });

            await SignInAsync();

            // A few pages take their id from the query string rather than the route.
            using (var scope = _factory.Services.CreateScope())
            {
                var admin = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(AdminEmail);
                _queryIds["/Admin/Users/Details"] = admin!.Id;
                _queryIds["/Admin/Users/Edit"] = admin.Id;
            }

            PageUrls = BuildPageUrls(_factory.Services);
        }

        public async Task DisposeAsync()
        {
            Client?.Dispose();
            if (_factory != null)
            {
                await _factory.DisposeAsync();
            }

            _database?.Dispose();
        }

        private async Task SignInAsync()
        {
            var loginPage = await Client.GetStringAsync("/Account/Login");
            var token = Regex.Match(loginPage, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
            token.Should().NotBeEmpty("the login form carries an antiforgery token");

            var response = await Client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = AdminEmail,
                ["Input.Password"] = AdminPassword,
                ["__RequestVerificationToken"] = token
            }));

            response.RequestMessage!.RequestUri!.AbsolutePath.Should().NotStartWith("/Account/Login", "the seeded admin should sign in");
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
