using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace DiscordBot.E2E;

/// <summary>
/// End-to-end browser checks for the Phase 1 Blazor hosting foundation (plan §5 Phase 1
/// deliverable 6, §6 "Testing strategy"). Boots the real host via <see cref="BotHostFixture"/>
/// and drives it with the shared headless Chromium from <see cref="PlaywrightFixture"/>.
///
/// Each test opens its own <see cref="IBrowserContext"/> (and logs in again if it needs to be
/// authenticated) rather than sharing session state between tests - xUnit constructs a fresh
/// instance of this class per test method, so there is nothing to share safely across them
/// besides the two collection fixtures. <see cref="AlphabeticalOrderer"/> still runs them
/// A/B/C/D so failures read top-to-bottom as "login -> smoke page -> nested route -> auth guard".
/// </summary>
[Collection(E2ECollection.Name)]
[TestCaseOrderer("DiscordBot.E2E.AlphabeticalOrderer", "DiscordBot.E2E")]
public sealed class BrowserTests
{
    private readonly BotHostFixture _host;
    private readonly PlaywrightFixture _playwright;

    public BrowserTests(BotHostFixture host, PlaywrightFixture playwright)
    {
        _host = host;
        _playwright = playwright;
    }

    [E2EFact]
    public async Task Test_A_Login_WithSeededAdmin_LandsOnDashboardWithSidebar()
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);

        await page.GotoAsync("/Account/Login");
        await Expect(page.Locator("#login-form")).ToBeVisibleAsync();
        await Expect(page.Locator("#email")).ToBeVisibleAsync();
        await Expect(page.Locator("#password")).ToBeVisibleAsync();

        await LoginAsync(page, _host);

        await Expect(page).ToHaveURLAsync($"{_host.BaseUrl}/");
        await Expect(page.Locator("#sidebar")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task Test_B_BlazorSmoke_CounterButton_IncrementsAfterCircuitBoots()
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);

        await LoginAsync(page, _host);

        await page.GotoAsync("/blazor-smoke");
        await AssertCounterIncrementsAsync(page);
    }

    [E2EFact]
    public async Task Test_C_NestedRoute_CircuitBoots_WithNoBlazorInitializer404()
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);

        var blazorFailures = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error" && message.Text.Contains("_blazor", StringComparison.OrdinalIgnoreCase))
            {
                blazorFailures.Add($"console error: {message.Text}");
            }
        };
        page.RequestFailed += (_, request) =>
        {
            if (request.Url.Contains("_blazor", StringComparison.OrdinalIgnoreCase))
            {
                blazorFailures.Add($"request failed: {request.Url} ({request.Failure})");
            }
        };
        page.Response += (_, response) =>
        {
            // Covers both _blazor/initializers (the SignalR negotiation/initializers path) and
            // _framework/blazor.web.js itself - a relative <script src> for the latter is what
            // actually 404s on a nested route (see the comment on that tag in App.razor); either
            // failure leaves the circuit dead the same way.
            var isBlazorAsset = response.Url.Contains("_blazor", StringComparison.OrdinalIgnoreCase)
                || response.Url.Contains("_framework/blazor", StringComparison.OrdinalIgnoreCase);
            if (response.Status == 404 && isBlazorAsset)
            {
                blazorFailures.Add($"404: {response.Url}");
            }
        };

        await LoginAsync(page, _host);

        // The regression this guards: anything under App.razor/blazor.web.js resolved relative
        // to the current (nested) path instead of the app base 404s here and leaves the circuit
        // dead - this test caught exactly that (App.razor's <script> tag was missing its leading
        // "/", so "/admin/blazor-smoke" requested ".../admin/_framework/blazor.web.js"; fixed
        // alongside this test - see the comment on that tag). /admin/blazor-smoke is the same
        // BlazorSmoke component as /blazor-smoke, reachable at a nested path specifically to
        // keep catching that class of bug (see BlazorSmoke.razor).
        await page.GotoAsync("/admin/blazor-smoke");
        await AssertCounterIncrementsAsync(page);

        blazorFailures.Should().BeEmpty(
            "blazor.web.js and _blazor/initializers must resolve relative to the app base, not the current route, even from a nested route");
    }

    [E2EFact]
    public async Task Test_D_UnauthenticatedNestedRoute_RedirectsToLogin()
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);

        await page.GotoAsync("/admin/blazor-smoke");

        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/Account/Login(\?|$)"));
        await Expect(page.Locator("#login-form")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Covers the Phase 2 /components showcase page end to end (plan §5 Phase 2 step 4): logs in,
    /// opens the page, exercises the toast and confirm-modal demos (proof the circuit actually
    /// booted, the same way <see cref="AssertCounterIncrementsAsync"/> proves it for the smoke
    /// page - there is no other DOM signal for "the circuit is connected"), and asserts no
    /// _blazor/_framework request failed anywhere along the way.
    /// </summary>
    [E2EFact]
    public async Task Test_E_ComponentsShowcase_TogglesToastAndConfirmModal_WithNoBlazorAssetFailures()
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);

        var blazorFailures = new List<string>();
        page.RequestFailed += (_, request) =>
        {
            if (request.Url.Contains("_blazor", StringComparison.OrdinalIgnoreCase)
                || request.Url.Contains("_framework/blazor", StringComparison.OrdinalIgnoreCase))
            {
                blazorFailures.Add($"request failed: {request.Url} ({request.Failure})");
            }
        };
        page.Response += (_, response) =>
        {
            var isBlazorAsset = response.Url.Contains("_blazor", StringComparison.OrdinalIgnoreCase)
                || response.Url.Contains("_framework/blazor", StringComparison.OrdinalIgnoreCase);
            if (response.Status == 404 && isBlazorAsset)
            {
                blazorFailures.Add($"404: {response.Url}");
            }
        };

        await LoginAsync(page, _host);

        await page.GotoAsync("/components");
        await Expect(page.Locator("[data-testid='components-nav']")).ToBeVisibleAsync();

        // Toast demo: the "Success" button inside the Toasts section, scoped there since several
        // other sections (badges, alerts) also render text/labels called "Success".
        var toastSection = page.Locator("[data-testid='showcase-toasts']");
        var successButton = toastSection.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Success", Exact = true });
        var toast = page.Locator("[role='alert']").Filter(new LocatorFilterOptions { HasTextString = "Saved successfully." });

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (true)
        {
            await successButton.ClickAsync();
            try
            {
                await Expect(toast).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 1_000 });
                break;
            }
            catch (PlaywrightException) when (DateTime.UtcNow < deadline)
            {
                // Circuit still not connected (or this particular click didn't land) - try again,
                // same retry shape AssertCounterIncrementsAsync uses for the smoke page.
            }
        }

        // Confirm modal demo: open it, then cancel - the modal must close and the result readout
        // must reflect a cancelled (false) confirmation, not a lingering "none".
        var confirmSection = page.Locator("[data-testid='showcase-confirm-modal']");
        await confirmSection.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete item", Exact = true }).ClickAsync();

        var dialog = page.Locator("#showcase-confirm-plain");
        await Expect(dialog).ToBeVisibleAsync();
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Cancel", Exact = true }).ClickAsync();

        await Expect(dialog).ToBeHiddenAsync();
        await Expect(confirmSection.Locator("[data-testid='confirm-result']")).ToContainTextAsync("False");

        blazorFailures.Should().BeEmpty(
            "no _blazor/_framework request should fail while driving the components showcase page");
    }

    /// <summary>
    /// Covers <c>Blazor/Layout/MainLayout.razor</c> (plan §5 Phase 3): the static shell
    /// (<c>MainSidebar</c>/<c>MainNavbar</c>) now wraps <c>/components</c> via
    /// <c>@@layout MainLayout</c>, so this is also proof that wrapping the page in real shell
    /// chrome did not disturb <see cref="Test_E_ComponentsShowcase_TogglesToastAndConfirmModal_WithNoBlazorAssetFailures"/>'s
    /// toast/modal path - both exercise the same route, ordered to run after it (naming keeps
    /// <see cref="AlphabeticalOrderer"/>'s top-to-bottom story: login -> smoke -> nested route ->
    /// auth guard -> components interactivity -> components shell chrome).
    /// </summary>
    [E2EFact]
    public async Task Test_F_ComponentsShowcase_ShowsShellChrome_ForSeededSuperAdmin()
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);

        await LoginAsync(page, _host);

        await page.GotoAsync("/components");
        await Expect(page.Locator("#sidebar")).ToBeVisibleAsync();
        await Expect(page.Locator("#topbar")).ToBeVisibleAsync();

        // The seeded admin is granted the SuperAdmin role (IdentitySeeder), which satisfies both
        // RequireAdmin and RequireSuperAdmin - the whole Administration group, not just the
        // Admin-only links within it, must be visible.
        var administrationGroup = page.Locator(".sidebar-group", new PageLocatorOptions
        {
            Has = page.Locator("#admin-nav-heading")
        });
        await Expect(administrationGroup).ToBeVisibleAsync();
        await Expect(administrationGroup.Locator("a[title='Users']")).ToBeVisibleAsync();
        await Expect(administrationGroup.Locator("a[title='Bulk Purge']")).ToBeVisibleAsync();

        // Sidebar collapse toggle: wwwroot/js/blazor/shell.js adds/removes "sidebar-collapsed" on
        // <html> and persists it to localStorage (the same key App.razor's pre-paint FOUC-guard
        // script reads) - only meaningful at the lg: breakpoint the toggle button itself is
        // visible at (hidden below 1024px, see MainNavbar.razor), so widen the viewport first.
        await page.SetViewportSizeAsync(1280, 800);
        var html = page.Locator("html");
        await Expect(html).Not.ToHaveClassAsync(new Regex(@"(^|\s)sidebar-collapsed(\s|$)"));

        await page.Locator("#sidebarCollapseToggle").ClickAsync();
        await Expect(html).ToHaveClassAsync(new Regex(@"(^|\s)sidebar-collapsed(\s|$)"));

        await page.Locator("#sidebarCollapseToggle").ClickAsync();
        await Expect(html).Not.ToHaveClassAsync(new Regex(@"(^|\s)sidebar-collapsed(\s|$)"));
    }

    /// <summary>
    /// Covers <c>Blazor/Layout/GuildLayout.razor</c> + <c>GuildProbe.razor</c> (plan §5 Phase 3):
    /// seeds a bare <c>Guilds</c> row directly into the fixture's throwaway SQLite database (the
    /// host runs web-only, so there is no live Discord guild to join and no UI flow that creates
    /// one - <see cref="BotHostFixture.DatabasePath"/> is the fixture's own escape hatch for
    /// exactly this), then asserts the full guild shell renders: breadcrumb, header with the
    /// guild's name, the desktop tab strip (Overview through Feature Requests), and the probe
    /// page's own resolved-context fields. The seeded admin carries the SuperAdmin role
    /// (IdentitySeeder), which <c>GuildAccessHandler</c> short-circuits - so this needs no guild
    /// membership setup beyond the bare row <c>IGuildService.GetGuildByIdAsync</c> requires.
    /// </summary>
    [E2EFact]
    public async Task Test_I_GuildProbe_RendersGuildShell_ForSeededGuild()
    {
        const ulong guildId = 900000000000000001UL;
        SeedGuild(guildId, "E2E Probe Guild");

        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);

        await LoginAsync(page, _host);

        await page.GotoAsync($"/Guilds/{guildId}/blazor-probe");

        await Expect(page.Locator("nav[aria-label='Breadcrumb']")).ToContainTextAsync("E2E Probe Guild");
        // GuildHeader's <h1> precedes GuildProbe's own "Guild Context Probe" <h1> in document
        // order - both are real headings, so .First disambiguates rather than narrowing by a
        // class/testid GuildHeader doesn't carry. The probe route matches none of
        // GuildNavigationConfig's tabs, so GuildLayout falls back to the guild's own name as the
        // header title (see GuildLayout.razor's "headerTitle" comment) rather than a tab label.
        await Expect(page.Locator("h1").First).ToHaveTextAsync("E2E Probe Guild");

        var tabNav = page.Locator("#guildNav");
        await Expect(tabNav).ToBeVisibleAsync();
        foreach (var label in new[]
                 {
                     "Overview", "Members", "Moderation", "Messages", "Audio", "Rat Watch",
                     "Currency", "Reminders", "Welcome", "Assistant", "Feature Requests"
                 })
        {
            await Expect(tabNav.GetByText(label, new LocatorGetByTextOptions { Exact = true })).ToBeVisibleAsync();
        }

        await Expect(page.Locator("[data-testid='probe-guild-context']")).ToBeVisibleAsync();
        await Expect(page.Locator("[data-testid='probe-guild-name']")).ToHaveTextAsync("E2E Probe Guild");
        await Expect(page.Locator("[data-testid='probe-guild-id']")).ToHaveTextAsync(guildId.ToString());
        await Expect(page.Locator("[data-testid='probe-can-edit']")).ToHaveTextAsync("True");
    }

    /// <summary>
    /// Covers <c>GuildContextGate</c>'s not-found state for a guild id with no matching
    /// <c>Guilds</c> row: the gate's default "Server Not Found" content renders and
    /// <c>GuildLayout</c> omits the breadcrumb/header/tab chrome around it - still an HTTP 200
    /// (this route doesn't wire <c>NavigationManager.NotFound()</c>; the 404 status-code page is
    /// a different route owned elsewhere), just asserting the rendered content here.
    /// </summary>
    [E2EFact]
    public async Task Test_J_GuildProbe_UnknownGuild_ShowsNotFoundState()
    {
        const ulong unknownGuildId = 900000000000000099UL;

        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);

        await LoginAsync(page, _host);

        var response = await page.GotoAsync($"/Guilds/{unknownGuildId}/blazor-probe");

        response.Should().NotBeNull();
        response!.Status.Should().Be(200);
        await Expect(page.GetByText("Server Not Found")).ToBeVisibleAsync();
        await Expect(page.Locator("nav[aria-label='Breadcrumb']")).ToHaveCountAsync(0);
        await Expect(page.Locator("[data-testid='probe-guild-context']")).ToHaveCountAsync(0);
    }

    /// <summary>
    /// Covers <c>Blazor/Layout/PortalLayout.razor</c> in web-only mode (plan §5 Phase 3): no live
    /// Discord gateway connection exists, so <c>PortalAccessService.DiscordGuildExists</c> always
    /// returns false and every guild resolves
    /// to <c>PortalAccessOutcome.GuildNotFound</c> regardless of whether a database row exists -
    /// this is the documented, existing behavior the Phase 3 brief says to keep, not a bug this
    /// page needs to work around. Anonymous (no login), since <c>PortalProbe</c> is
    /// <c>[AllowAnonymous]</c>: the whole point of the three-state gate is that an anonymous
    /// visitor gets a real, non-crashing render. Asserts the GuildNotFound content renders with no
    /// _blazor/_framework asset failures, the same guard <see cref="Test_C_NestedRoute_CircuitBoots_WithNoBlazorInitializer404"/>
    /// uses for a different route.
    /// </summary>
    [E2EFact]
    public async Task Test_K_PortalProbe_WebOnly_ShowsGuildNotFound()
    {
        const ulong guildId = 900000000000000002UL;
        SeedGuild(guildId, "E2E Portal Probe Guild");

        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);

        var blazorFailures = new List<string>();
        page.RequestFailed += (_, request) =>
        {
            if (request.Url.Contains("_blazor", StringComparison.OrdinalIgnoreCase)
                || request.Url.Contains("_framework/blazor", StringComparison.OrdinalIgnoreCase))
            {
                blazorFailures.Add($"request failed: {request.Url} ({request.Failure})");
            }
        };
        page.Response += (_, response) =>
        {
            var isBlazorAsset = response.Url.Contains("_blazor", StringComparison.OrdinalIgnoreCase)
                || response.Url.Contains("_framework/blazor", StringComparison.OrdinalIgnoreCase);
            if (response.Status == 404 && isBlazorAsset)
            {
                blazorFailures.Add($"404: {response.Url}");
            }
        };

        await page.GotoAsync($"/Portal/{guildId}/blazor-probe");

        await Expect(page.Locator("[data-testid='portal-guild-not-found']")).ToBeVisibleAsync();
        await Expect(page.GetByText("Server Not Found")).ToBeVisibleAsync();

        blazorFailures.Should().BeEmpty(
            "no _blazor/_framework request should fail rendering PortalLayout's GuildNotFound state");
    }

    /// <summary>
    /// Inserts a bare row into the fixture's throwaway SQLite <c>Guilds</c> table directly - the
    /// host runs web-only (no Discord gateway), so there is no UI flow or API call that could
    /// create one otherwise, and <c>IGuildService.GetGuildByIdAsync</c> only ever needs this one
    /// row to exist (see the .csproj comment on why <c>Microsoft.Data.Sqlite</c> is a direct
    /// package reference here rather than a transitive one, this project having no reference to
    /// DiscordBot.Bot/.Infrastructure at all). Opens and closes its own short-lived connection
    /// rather than holding one open alongside the host's own pooled connections to the same file.
    /// </summary>
    private void SeedGuild(ulong guildId, string name)
    {
        using var connection = new SqliteConnection($"Data Source={_host.DatabasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO Guilds (Id, Name, JoinedAt, IsActive) VALUES ($id, $name, $joinedAt, 1)";
        command.Parameters.AddWithValue("$id", (long)guildId);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$joinedAt", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Opens a browser context pointed at the running host, with requests to Google Fonts
    /// short-circuited. Every page in the app (App.razor and the legacy _Layout.cshtml alike)
    /// references fonts.googleapis.com/fonts.gstatic.com; those are unrelated to anything under
    /// test here, and a render-blocking &lt;link rel="stylesheet"&gt; to a host that is slow or
    /// unreachable (offline CI runners, a locked-down sandbox) can stall the page load well past
    /// what a login round trip or a circuit boot should ever take. Aborting them keeps the tests
    /// fast and deterministic regardless of outbound network conditions.
    /// </summary>
    private async Task<IBrowserContext> NewContextAsync()
    {
        var context = await _playwright.Browser!.NewContextAsync(
            new BrowserNewContextOptions { BaseURL = _host.BaseUrl });

        await context.RouteAsync("https://fonts.googleapis.com/**", route => route.AbortAsync());
        await context.RouteAsync("https://fonts.gstatic.com/**", route => route.AbortAsync());

        return context;
    }

    /// <summary>
    /// Opens a page off <paramref name="context"/> and, when <see cref="BotHostFixture.LogDirectory"/>
    /// is set (i.e. E2E_ENABLED=1), appends its console messages and uncaught page errors to
    /// <c>browser-console.log</c> in that same per-run directory alongside host.log - so a
    /// failure in CI leaves both the server's and the browser's own account of what happened.
    /// </summary>
    private async Task<IPage> NewPageAsync(IBrowserContext context)
    {
        var page = await context.NewPageAsync();

        if (!string.IsNullOrEmpty(_host.LogDirectory))
        {
            var consoleLogPath = Path.Combine(_host.LogDirectory, "browser-console.log");
            page.Console += (_, message) => AppendConsoleLogLine(consoleLogPath, $"[{message.Type}] {message.Text}");
            page.PageError += (_, error) => AppendConsoleLogLine(consoleLogPath, $"[pageerror] {error}");
        }

        return page;
    }

    private static void AppendConsoleLogLine(string path, string line)
    {
        try
        {
            File.AppendAllText(path, $"[{DateTime.UtcNow:HH:mm:ss.fff}] {line}{Environment.NewLine}");
        }
        catch
        {
            // Best-effort: never fail a test because its debug log couldn't be written.
        }
    }

    [E2EFact]
    public async Task Test_G_Landing_Anonymous_RendersHero_WithoutLoginRedirect()
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);

        await page.GotoAsync("/landing");
        await Expect(page).ToHaveURLAsync(new Regex(@"/landing$"));
        await Expect(page.Locator("h1")).ToHaveTextAsync(new Regex(@"^\s*Discord Bot\s*$"));
        await Expect(page.Locator("[data-landing-page]")).ToHaveAttributeAsync("data-landing-js-ready", "true");

        // The "/" -> "/landing" redirect (Program.cs) for an anonymous visitor - not touched this
        // round, but the page it lands on is now this component instead of the old cshtml.
        await page.GotoAsync("/");
        await Expect(page).ToHaveURLAsync(new Regex(@"/landing$"));
    }

    /// <summary>
    /// Covers Routes.razor's new <c>NotFoundPage="typeof(NotFound)"</c> (plan §5 Phase 3 step 3):
    /// for a SIGNED-IN visitor, an unmatched URL renders Blazor/Pages/Error/NotFound.razor's
    /// markup AND returns an actual HTTP 404 - the .NET 10 Router.NotFoundPage fix over the older
    /// NotFound-render-fragment-only behaviour, which always returned 200 (verified directly:
    /// curling the same build pre-fix-adoption always returns 200 for an unmatched route).
    ///
    /// There is deliberately no anonymous case here: an anonymous visitor hitting an unmatched
    /// URL never reaches the Router at all. MapRazorComponents' generic "no @page matched"
    /// fallback endpoint carries no [Authorize]/[AllowAnonymous] metadata of its own (there is no
    /// matched page component to derive it from), so IdentityServiceExtensions'
    /// pre-existing/out-of-scope global `FallbackPolicy` (RequireAuthenticatedUser, applied to
    /// any endpoint without explicit authorization metadata) intercepts it in UseAuthorization()
    /// before Blazor's Router ever runs, 302-redirecting to /Account/Login - confirmed by curling
    /// the running host directly: every matched [AllowAnonymous] page (/landing, /Error/403,
    /// /Error/404, /Error/500) returns 200, but /this/does/not/exist always 302s regardless. This
    /// is a pre-existing interaction between Program.cs's endpoint registration and
    /// IdentityServiceExtensions' FallbackPolicy, neither of which this round may touch - flagged
    /// for the orchestrator rather than worked around here (see the PR/handoff notes).
    /// </summary>
    [E2EFact]
    public async Task Test_H_UnknownRoute_Returns404Page()
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        var response = await page.GotoAsync("/this/does/not/exist");
        response.Should().NotBeNull();
        response!.Status.Should().Be(404, "an unmatched route must return a real 404 for a signed-in user");
        await Expect(page.Locator("h1")).ToHaveTextAsync("Page Not Found");
        await Expect(page.Locator("body")).ToContainTextAsync("/this/does/not/exist");
    }

    /// <summary>
    /// Covers Blazor/Pages/Error/{Forbidden,ServerError}.razor rendering anonymously (plan §5
    /// Phase 3) - both carry [AllowAnonymous] against Routes.razor's authenticated-by-default
    /// FallbackPolicy, and Program.cs's UseExceptionHandler("/Error/500") reaches ServerError the
    /// same way it reached the old cshtml.
    /// </summary>
    [E2EFact]
    public async Task Test_N_ErrorPages_RenderAnonymously()
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);

        await page.GotoAsync("/Error/403");
        await Expect(page.Locator("h1")).ToHaveTextAsync("Access Forbidden");
        await Expect(page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Sign In", Exact = true })).ToBeVisibleAsync();

        await page.GotoAsync("/Error/500?requestId=e2e-request-abc");
        await Expect(page.Locator("h1")).ToHaveTextAsync("Something Went Wrong");
        await Expect(page.Locator("body")).ToContainTextAsync("e2e-request-abc");
    }
    /// <summary>
    /// Covers <c>Search.razor</c>'s Pages results section rendering, with the search term
    /// highlighted in the results - the Blazor replacement for the deleted
    /// <c>HighlightTagHelper</c> (docs/plans/blazor-port-plan.md §5 Phase 3).
    /// </summary>
    [E2EFact]
    public async Task Test_L_Search_LoggedIn_RendersPagesSection()
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);

        await LoginAsync(page, _host);

        await page.GotoAsync("/Search?q=settings");

        await Expect(page.Locator("[data-testid='search-section-pages']")).ToBeVisibleAsync();
        await Expect(page.Locator("mark.search-highlight").First).ToBeVisibleAsync();
    }

    /// <summary>
    /// Covers <c>Search.razor</c>'s short-query validation state: a term entered but shorter than
    /// the 2-character minimum shows the validation message instead of silently looking identical
    /// to the "Start searching" empty state (see the "Deviation" comment on that branch in
    /// <c>Search.razor</c>).
    /// </summary>
    [E2EFact]
    public async Task Test_M_Search_ShortQuery_ShowsValidation()
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);

        await LoginAsync(page, _host);

        await page.GotoAsync("/Search?q=a");

        await Expect(page.Locator("[data-testid='search-validation']")).ToContainTextAsync("at least 2 characters");
    }

    /// <summary>
    /// Regression coverage for the Phase 3 review finding that <c>Pages/Account/Logout.cshtml.cs</c>'s
    /// <c>OnPostAsync</c> 500'd on sign-out with no <c>returnUrl</c> - it called
    /// <c>RedirectToPage("/Landing")</c>, a Razor Page deleted by this same round, and now uses
    /// <c>LocalRedirect("/landing")</c> instead. Drives the real navbar logout form
    /// (<c>MainNavbar.razor</c>'s <c>#userMenuButton</c> opens the dropdown, then its plain POST
    /// form with no <c>returnUrl</c> field submits) end to end and asserts the browser lands on
    /// <c>/landing</c> with a 200, not a 500.
    /// </summary>
    [E2EFact]
    public async Task Test_O_Logout_FromShell_LandsOnLanding()
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);

        await LoginAsync(page, _host);

        await page.GotoAsync("/components");
        await Expect(page.Locator("#userMenuButton")).ToBeVisibleAsync();

        // The logout <form> gets no data-enhance-nav opt-out, so Blazor Web App's default
        // enhanced navigation intercepts this same-origin POST as a fetch rather than a full
        // browser navigation - the URL bar updates via history.pushState with no Frame "Load"
        // event ever firing, which is why RunAndWaitForNavigationAsync (confirmed empirically:
        // it reliably times out waiting for that event here) can't be used to capture the
        // response. The POST and its redirect still go out as real HTTP requests either way, so
        // recording every response's status during the click - the same approach Test_E uses for
        // its own _blazor/_framework failure check - proves the redirect chain didn't 500 without
        // depending on which navigation mode actually ran.
        var failedResponses = new List<string>();
        page.Response += (_, response) =>
        {
            if (response.Status >= 500
                && (response.Url.Contains("/Account/Logout", StringComparison.OrdinalIgnoreCase)
                    || response.Url.Contains("/landing", StringComparison.OrdinalIgnoreCase)))
            {
                failedResponses.Add($"{response.Status}: {response.Url}");
            }
        };

        await page.Locator("#userMenuButton").ClickAsync();
        var userMenu = page.Locator("#userMenu");
        await Expect(userMenu).ToHaveClassAsync(new Regex(@"(^|\s)active(\s|$)"));

        // The submit button carries an explicit role="menuitem" (it's an item inside the
        // role="menu" dropdown), which overrides its implicit <button> role - GetByRole needs the
        // accessible role actually in effect, not the element's default one.
        await userMenu.GetByRole(AriaRole.Menuitem, new LocatorGetByRoleOptions { Name = "Sign out" }).ClickAsync();

        await Expect(page).ToHaveURLAsync(new Regex(@"/landing$"));
        await Expect(page.Locator("h1")).ToHaveTextAsync(new Regex(@"^\s*Discord Bot\s*$"));

        failedResponses.Should().BeEmpty("the sign-out form POST's redirect chain must not end in a 500");
    }

    /// <summary>
    /// Covers the static SSR port of Pages/Account/Profile.cshtml + ProfileModel end to end
    /// (docs/plans/blazor-port-plan.md Phase 4 cluster 4a): identity display, the "Not Linked"
    /// Discord badge (the seeded SuperAdmin carries no Discord link), the client-side
    /// <c>&lt;LocalTime&gt;</c> conversion this cluster adds, and a real theme save round trip -
    /// select a different theme, submit the plain POST form, land on <c>?status=saved</c>, then
    /// reload and confirm both the select and the document's <c>data-theme</c> persisted.
    /// </summary>
    [E2EFact]
    public async Task Test_P_Profile_RendersAndSavesTheme()
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);

        await LoginAsync(page, _host);

        await page.GotoAsync("/Account/Profile");
        await Expect(page.Locator("h1")).ToHaveTextAsync("Profile");
        // Scoped past MainLayout's own sidebar section headings (also <h2>s - "Overview",
        // "Administration", "Developer").
        await Expect(page.Locator("#main-content h2")).ToHaveTextAsync("System Administrator");
        await Expect(page.Locator("span.badge", new PageLocatorOptions { HasTextString = "Not Linked" })).ToBeVisibleAsync();

        // Proof wwwroot/js/blazor/localtime.js actually ran: the "Member Since" <LocalTime>
        // element is marked converted and its text no longer matches the server-rendered UTC
        // fallback ("MMM d, yyyy" - see LocalTime.razor's FallbackText).
        var memberSinceTime = page.Locator("time[data-utc]").First;
        await Expect(memberSinceTime).ToHaveAttributeAsync("data-localtime-converted", "1");

        // Two themes are seeded (AddThemeSupport migration): "Discord Dark" (key "discord-dark",
        // the system default) and "Purple Dusk" (key "purple-dusk") - pick whichever option isn't
        // already selected so this test doesn't need to guess numeric theme ids.
        var select = page.Locator("select#SelectedThemeId");
        var selectedText = await select.Locator("option:checked").TextContentAsync();
        var targetLabel = selectedText?.Trim() == "Purple Dusk" ? "Discord Dark" : "Purple Dusk";
        var targetKey = targetLabel == "Purple Dusk" ? "purple-dusk" : "discord-dark";
        var targetOption = select.Locator("option", new LocatorLocatorOptions { HasText = targetLabel });
        var targetValue = await targetOption.GetAttributeAsync("value");
        targetValue.Should().NotBeNullOrEmpty();

        await select.SelectOptionAsync(new SelectOptionValue { Value = targetValue });
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save Preferences" }).ClickAsync();

        await Expect(page).ToHaveURLAsync(new Regex(@"/Account/Profile\?status=saved$"));
        await Expect(page.GetByText("Theme preference saved successfully.")).ToBeVisibleAsync();

        await page.ReloadAsync();
        await Expect(page.Locator("select#SelectedThemeId")).ToHaveValueAsync(targetValue!);
        await Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", targetKey);
    }

    /// <summary>
    /// Covers the static SSR ports of Pages/Account/AccessDenied.cshtml and Lockout.cshtml
    /// (docs/plans/blazor-port-plan.md Phase 4 cluster 4a), both [AllowAnonymous] and reachable
    /// without signing in - <c>AccessDenied</c> additionally showing the "Attempted URL" line
    /// when a <c>?returnUrl=</c> is present, matching <c>ForbiddenTests</c>'/Test_N's coverage of
    /// the sibling error pages.
    /// </summary>
    [E2EFact]
    public async Task Test_Q_AccessDenied_And_Lockout_RenderAnonymously()
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);

        await page.GotoAsync("/Account/AccessDenied?returnUrl=/x");
        await Expect(page.Locator("h1")).ToHaveTextAsync("Access Denied");
        await Expect(page.Locator("body")).ToContainTextAsync("Attempted URL:");
        await Expect(page.Locator("body")).ToContainTextAsync("/x");

        await page.GotoAsync("/Account/Lockout");
        await Expect(page.Locator("h1")).ToHaveTextAsync("Account Locked");
        await Expect(page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Return to Login" })).ToBeVisibleAsync();
    }

    /// <summary>
    /// Covers the three cluster 4a Details pages end to end
    /// (docs/plans/blazor-port-plan.md Phase 4 cluster 4a): one row each is seeded directly into
    /// the fixture's throwaway SQLite <c>AuditLogs</c>, <c>MessageLogs</c> and <c>CommandLogs</c>
    /// tables (the same <see cref="BotHostFixture.DatabasePath"/> escape hatch
    /// <see cref="SeedGuild"/> uses - there is no UI flow that creates these rows in web-only
    /// mode), then each Details page is opened and asserted to render its key fields plus a
    /// client-side-converted <c>&lt;time data-localtime-converted="1"&gt;</c> - proof
    /// <c>localtime.js</c>/<c>BrowserInterop.ConvertLocalTimesAsync</c> actually ran, the same
    /// assertion shape <see cref="Test_P_Profile_RendersAndSavesTheme"/> uses.
    /// </summary>
    [E2EFact]
    public async Task Test_S_LogDetails_RenderForSeededRows()
    {
        var (guildId, auditLogId, messageLogId, commandLogId) = SeedLogRows();

        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        await page.GotoAsync($"/Admin/AuditLogs/Details/{auditLogId}");
        await Expect(page.Locator("h1")).ToHaveTextAsync("Audit Entry Details");
        await Expect(page.Locator("body")).ToContainTextAsync("System");
        await Expect(page.Locator("body")).ToContainTextAsync("BotStarted");
        await Expect(page.Locator("time[data-utc]").First).ToHaveAttributeAsync("data-localtime-converted", "1");

        await page.GotoAsync($"/Admin/MessageLogs/Details/{messageLogId}");
        await Expect(page.Locator("h1")).ToHaveTextAsync("Message Details");
        await Expect(page.Locator("body")).ToContainTextAsync("Hello from the E2E seed");
        await Expect(page.Locator("body")).ToContainTextAsync("e2e-log-user");
        await Expect(page.Locator("body")).ToContainTextAsync("E2E Log Details Guild");
        await Expect(page.Locator("time[data-utc]").First).ToHaveAttributeAsync("data-localtime-converted", "1");

        await page.GotoAsync($"/CommandLogs/Details/{commandLogId}");
        await Expect(page.Locator("h1")).ToContainTextAsync("ping");
        await Expect(page.Locator("body")).ToContainTextAsync("E2E Log Details Guild");
        await Expect(page.Locator("body")).ToContainTextAsync("e2e-log-user");
        await Expect(page.Locator("time[data-utc]").First).ToHaveAttributeAsync("data-localtime-converted", "1");
    }

    /// <summary>
    /// Covers the AuditLogs Details "Export JSON" client-side download end to end (cluster 4a):
    /// <c>BrowserInterop.DownloadFileAsync</c>/<c>browser.js</c>'s <c>downloadFile</c>
    /// (Blob + object URL + synthetic <c>&lt;a download&gt;</c>) via Playwright's download event,
    /// asserting the file name and that its content parses as JSON containing the entry id.
    /// </summary>
    [E2EFact]
    public async Task Test_T_AuditLogDetails_ExportDownloadsJson()
    {
        var (_, auditLogId, _, _) = SeedLogRows();

        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        await page.GotoAsync($"/Admin/AuditLogs/Details/{auditLogId}");

        var downloadTask = page.RunAndWaitForDownloadAsync(async () =>
        {
            await page.GetByLabel("Export this entry as JSON").ClickAsync();
        });
        var download = await downloadTask;

        download.SuggestedFilename.Should().Be($"audit-entry-{auditLogId}.json");

        var path = await download.PathAsync();
        path.Should().NotBeNullOrEmpty();
        var content = await File.ReadAllTextAsync(path!);
        var json = System.Text.Json.JsonDocument.Parse(content);
        json.RootElement.GetProperty("entryId").GetInt64().Should().Be(auditLogId);
        json.RootElement.GetProperty("category").GetString().Should().Be("System");
    }

    /// <summary>
    /// Seeds one <c>Guilds</c> row (via <see cref="SeedGuild"/>), one <c>Users</c> row, and one
    /// row each in <c>AuditLogs</c>, <c>MessageLogs</c> and <c>CommandLogs</c> referencing them -
    /// the FK rows <see cref="Test_S_LogDetails_RenderForSeededRows"/> and
    /// <see cref="Test_T_AuditLogDetails_ExportDownloadsJson"/> both need. The audit log entry
    /// uses a <c>System</c> actor (no Identity user lookup involved) and no guild id - guild-name
    /// enrichment for audit logs goes through the live Discord gateway cache
    /// (<c>Services/Audit/AuditLogService.cs</c>'s <c>EnrichDtosAsync</c>), which is never
    /// populated in this web-only host, so a real assertion on that field would be testing
    /// something this host can never produce.
    /// </summary>
    /// <remarks>
    /// Every numeric id is offset by a random salt so two callers in the same test run (both
    /// <see cref="Test_S_LogDetails_RenderForSeededRows"/> and
    /// <see cref="Test_T_AuditLogDetails_ExportDownloadsJson"/> call this) never collide on the
    /// shared <see cref="BotHostFixture.DatabasePath"/> - xUnit constructs a fresh
    /// <see cref="BrowserTests"/> instance per test method, but the collection fixture's database
    /// file is one file for the whole run. The <c>CommandLogs.Id</c> parameter is bound as the
    /// <see cref="Guid"/> value itself, not <c>.ToString()</c> - Microsoft.Data.Sqlite's native
    /// GUID support serializes a <see cref="Guid"/> parameter as the same 16-byte blob EF Core's
    /// own <c>TEXT</c>-affinity GUID column mapping writes, so a hex-string parameter would bind
    /// as a different SQLite storage class and never match <c>CommandLogService.GetByIdAsync</c>'s
    /// own EF-issued lookup.
    /// </remarks>
    private (ulong GuildId, long AuditLogId, long MessageLogId, Guid CommandLogId) SeedLogRows()
    {
        var salt = (ulong)Random.Shared.NextInt64(1, 1_000_000);
        var guildId = 900000000000000010UL + salt;
        SeedGuild(guildId, "E2E Log Details Guild");

        var userId = 900000000000000011UL + salt;
        var now = DateTime.UtcNow;
        var auditLogId = 900000000000000012L + (long)salt;
        var messageLogId = 900000000000000013L + (long)salt;
        var commandLogId = Guid.NewGuid();

        using var connection = new SqliteConnection($"Data Source={_host.DatabasePath}");
        connection.Open();

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "INSERT INTO Users (Id, Username, Discriminator, FirstSeenAt, LastSeenAt) VALUES ($id, $username, '0', $now, $now)";
            command.Parameters.AddWithValue("$id", (long)userId);
            command.Parameters.AddWithValue("$username", "e2e-log-user");
            command.Parameters.AddWithValue("$now", now.ToString("O"));
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO AuditLogs (Id, Timestamp, Category, Action, ActorId, ActorType, TargetType, TargetId, GuildId, Details, IpAddress, CorrelationId)
                VALUES ($id, $timestamp, 7, 16, NULL, 2, NULL, NULL, NULL, NULL, NULL, NULL)
                """;
            command.Parameters.AddWithValue("$id", auditLogId);
            command.Parameters.AddWithValue("$timestamp", now.ToString("O"));
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO MessageLogs (Id, DiscordMessageId, AuthorId, ChannelId, ChannelName, GuildId, Source, Content, Timestamp, LoggedAt, HasAttachments, HasEmbeds, ReplyToMessageId)
                VALUES ($id, $discordMessageId, $authorId, $channelId, 'general', $guildId, 2, $content, $timestamp, $loggedAt, 0, 0, NULL)
                """;
            command.Parameters.AddWithValue("$id", messageLogId);
            command.Parameters.AddWithValue("$discordMessageId", unchecked((long)(900000000000000014UL + salt)));
            command.Parameters.AddWithValue("$authorId", unchecked((long)userId));
            command.Parameters.AddWithValue("$channelId", unchecked((long)(900000000000000015UL + salt)));
            command.Parameters.AddWithValue("$guildId", unchecked((long)guildId));
            command.Parameters.AddWithValue("$content", "Hello from the E2E seed");
            command.Parameters.AddWithValue("$timestamp", now.ToString("O"));
            command.Parameters.AddWithValue("$loggedAt", now.ToString("O"));
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO CommandLogs (Id, GuildId, UserId, CommandName, Parameters, ExecutedAt, ResponseTimeMs, Success, ErrorMessage, CorrelationId)
                VALUES ($id, $guildId, $userId, 'ping', NULL, $executedAt, 42, 1, NULL, NULL)
                """;
            command.Parameters.AddWithValue("$id", commandLogId);
            command.Parameters.AddWithValue("$guildId", (long)guildId);
            command.Parameters.AddWithValue("$userId", (long)userId);
            command.Parameters.AddWithValue("$executedAt", now.ToString("O"));
            command.ExecuteNonQuery();
        }

        return (guildId, auditLogId, messageLogId, commandLogId);
    }

    /// <summary>Fills and submits the email/password form on /Account/Login and waits for the redirect to complete.</summary>
    [E2EFact]
    public async Task Test_R_Users_CreateEditDetails_RoundTrip()
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        await page.GotoAsync("/Admin/Users");
        await Expect(page.Locator("[data-testid='users-row']").Filter(new LocatorFilterOptions { HasTextString = _host.SeededAdminEmail })).ToBeVisibleAsync();

        // Create. An EditForm's submit is only handled by the Interactive Server circuit once
        // that circuit has actually attached to this freshly-navigated-to page's DOM - a real,
        // observed race (a click fired earlier is either a silent no-op with no FormName set, as
        // here, or - with one set - routes into Blazor Web App's static/antiforgery-protected
        // form-post fallback instead, which this page does not implement). The explicit settle
        // wait below is a pragmatic stand-in for a reliable "the circuit is attached" signal,
        // which this codebase does not expose (see AssertCounterIncrementsAsync's own doc comment
        // on the same underlying gap for a plain button click). Every "submit"-shaped click after
        // this one gets the same treatment.
        await page.GotoAsync("/Admin/Users/Create");
        await page.WaitForTimeoutAsync(1_500);
        const string email = "e2e-user@example.test";
        await page.Locator("#Input_Email").FillAsync(email);
        await page.Locator("#Input_Password").FillAsync("E2eStrongP@ss1");
        await page.Locator("#Input_ConfirmPassword").FillAsync("E2eStrongP@ss1");
        await page.Locator("#Input_Role").SelectOptionAsync("Viewer");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create User" }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex(@"/Admin/Users$"), new PageAssertionsToHaveURLOptions { Timeout = 20_000 });

        await Expect(page.Locator(".toast-success")).ToBeVisibleAsync();
        var newRow = page.Locator("[data-testid='users-row']").Filter(new LocatorFilterOptions { HasTextString = email });
        await Expect(newRow).ToBeVisibleAsync();

        // Details.
        await newRow.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "View" }).ClickAsync();
        await Expect(page.Locator("h1")).ToHaveTextAsync("User Details");
        // GetByText(email) alone is ambiguous here - it also matches the activity-log JSON blob
        // and the success toast's still-lingering text - so scope to the profile heading.
        await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = email })).ToBeVisibleAsync();

        // Edit: change display name, save.
        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Edit User" }).ClickAsync();
        await Expect(page.Locator("h1")).ToHaveTextAsync("Edit User");
        await page.WaitForTimeoutAsync(1_500);
        await page.Locator("#Input_DisplayName").FillAsync("E2E Test User");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save Changes" }).ClickAsync();
        await Expect(page.Locator(".toast-success")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 20_000 });

        // Reset password via the confirm modal.
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Reset Password", Exact = true }).ClickAsync();
        var resetModal = page.Locator("#resetPasswordModal");
        await Expect(resetModal).ToBeVisibleAsync();
        await resetModal.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Reset Password" }).ClickAsync();
        // "temporary password" alone is ambiguous (it also matches the Reset Password row's
        // static help text), so assert it scoped to the generated-password alert specifically.
        var generatedPassword = page.Locator("[data-testid='generated-password']");
        await Expect(generatedPassword).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 20_000 });
        await Expect(generatedPassword).ToContainTextAsync("temporary password");

        // Toggle active from Index. The button itself is a plain @onclick, not a <form>, but it
        // is still the first interaction on this freshly (hard-)navigated page, so it needs the
        // same settle wait as the two form submits above.
        await page.GotoAsync("/Admin/Users");
        var toggleRow = page.Locator("[data-testid='users-row']").Filter(new LocatorFilterOptions { HasTextString = email });
        await Expect(toggleRow.GetByText("Active", new LocatorGetByTextOptions { Exact = true })).ToBeVisibleAsync();
        await page.WaitForTimeoutAsync(1_500);
        await toggleRow.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Disable" }).ClickAsync();
        var toggleModal = page.Locator("#toggle-active-modal");
        await Expect(toggleModal).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 20_000 });
        await toggleModal.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Disable" }).ClickAsync();
        await Expect(toggleRow.GetByText("Inactive", new LocatorGetByTextOptions { Exact = true })).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 20_000 });
    }

    /// <summary>Fills and submits the email/password form on /Account/Login and waits for the redirect to complete.</summary>

    private static async Task LoginAsync(IPage page, BotHostFixture host)
    {
        await page.GotoAsync("/Account/Login");
        await page.Locator("#email").FillAsync(host.SeededAdminEmail);
        await page.Locator("#password").FillAsync(host.SeededAdminPassword);
        await page.Locator("#login-form button[type=submit]").ClickAsync();

        // The submit is a plain (non-AJAX) form POST that 302-redirects to "/". Expect's own
        // polling (not a fixed page-load wait) is what actually waits out the redirect and
        // render here - a "Load"/NetworkIdle wait is unreliable on this page because of the
        // external Google Fonts <link> tags, which this sandbox has no route to and which can
        // stall the page-load lifecycle well past a login round trip's own, much shorter, time.
        await Expect(page.Locator("#sidebar")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions
        {
            Timeout = 20_000
        });
    }

    /// <summary>
    /// Asserts a click moves the counter off 0 - proof the Interactive Server circuit is live,
    /// not just that the prerendered HTML shipped. The button is plain HTML and reports as
    /// enabled the moment the (statically prerendered) markup exists, well before blazor.web.js
    /// has actually negotiated the SignalR circuit behind it, and there is no DOM signal this
    /// page exposes for "the circuit is connected". Waiting for <c>window.Blazor</c> to exist
    /// only confirms the script has loaded, not that the circuit is up, and a click fired before
    /// the circuit connects is simply dropped rather than queued and replayed - so this still
    /// retries the click on a timer (confirmed empirically: a single click sent right after
    /// <c>window.Blazor</c> appears reliably does nothing). What changed from the original
    /// version of this helper is the assertion: because more than one of those retried clicks
    /// can land together the instant the circuit connects, the exact "Current count: 1" text it
    /// used to wait for can be skipped entirely (e.g. straight to "Current count: 3") and the
    /// loop then never succeeds before its deadline - so this waits for the count to move off 0
    /// by any amount instead, via a regex, and stops retrying the moment it does.
    /// </summary>
    private static async Task AssertCounterIncrementsAsync(IPage page)
    {
        // Scoped to the "Current count" text specifically: since Phase 3, /blazor-smoke and
        // /admin/blazor-smoke render under MainLayout (plan §5 Phase 3), whose sidebar footer is
        // also role="status" ("Bot status") - a plain GetByRole(Status) now resolves to two
        // elements and Playwright's strict mode rejects the ambiguous locator.
        var status = page.GetByRole(AriaRole.Status).Filter(new LocatorFilterOptions { HasTextString = "Current count" });
        var button = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Click me" });
        var countChanged = new Regex(@"Current count: [1-9]\d*");

        await Expect(status).ToContainTextAsync("Current count: 0");

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (true)
        {
            await button.ClickAsync();
            try
            {
                await Expect(status).ToContainTextAsync(countChanged, new LocatorAssertionsToContainTextOptions
                {
                    Timeout = 1_000
                });
                return;
            }
            catch (PlaywrightException) when (DateTime.UtcNow < deadline)
            {
                // Circuit still not connected (or this particular click didn't land) - try again.
            }
        }
    }
}
