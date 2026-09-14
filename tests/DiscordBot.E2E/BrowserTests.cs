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
        // other sections (badges, alerts) also render text/labels called "Success". The button is
        // disabled="@(!RendererInfo.IsInteractive)" (NavigationAndOverlaysShowcase.razor) until the
        // circuit actually attaches, so waiting for it to become enabled first rules out a click
        // landing in the prerender-only window (see
        // docs/lessons-learned/blazor-editform-formname-race.md's "Test-side consequence"). That
        // alone isn't quite enough, though: even once enabled, a click sent the instant the
        // "disabled" attribute is removed can still race Blazor Server's own client-side event
        // listener attachment and land in nothing (confirmed empirically against this exact
        // button - not a hypothesis) - the same class of "a click fired too early is dropped, not
        // queued" gap <see cref="AssertCounterIncrementsAsync"/> documents and retries around for
        // the smoke-page counter, so this retries on the same bounded-deadline shape rather than
        // trusting a single click.
        // .First: a retried click can land more than once (the toast host stacks them rather than
        // replacing), and under load a retry is exactly what happens - without .First, a second
        // stacked toast turns every subsequent assertion into a strict-mode violation (Playwright
        // refuses to resolve a bare locator to >1 element), which this loop would then misread as
        // "click didn't land" and retry again, compounding rather than recovering.
        var toastSection = page.Locator("[data-testid='showcase-toasts']");
        var successButton = toastSection.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Success", Exact = true });
        var toast = page.Locator("[role='alert']").Filter(new LocatorFilterOptions { HasTextString = "Saved successfully." }).First;

        await Expect(successButton).ToBeEnabledAsync();
        await ClickUntilVisibleAsync(successButton, toast);

        // Confirm modal demo: open it, then cancel - the modal must close and the result readout
        // must reflect a cancelled (false) confirmation, not a lingering "none". Same
        // disabled-until-interactive gating plus click-retry as the toast button above.
        var confirmSection = page.Locator("[data-testid='showcase-confirm-modal']");
        var deleteItemButton = confirmSection.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete item", Exact = true });
        var dialog = page.Locator("#showcase-confirm-plain");

        await Expect(deleteItemButton).ToBeEnabledAsync();
        await ClickUntilVisibleAsync(deleteItemButton, dialog);
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
        // element is marked converted (data-localtime-converted holds the data-utc value it was
        // converted from - see localtime.js's idempotency note) and its text no longer matches
        // the server-rendered UTC fallback ("MMM d, yyyy" - see LocalTime.razor's FallbackText).
        var memberSinceTime = page.Locator("time[data-utc]").First;
        await Expect(memberSinceTime).ToHaveAttributeAsync("data-localtime-converted", new Regex(".+"));

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
    /// client-side-converted <c>&lt;time data-localtime-converted&gt;</c> (the marker holds the
    /// <c>data-utc</c> value it was converted from, not a bare <c>"1"</c> - see localtime.js) -
    /// proof <c>localtime.js</c>/<c>BrowserInterop.ConvertLocalTimesAsync</c> actually ran, the
    /// same assertion shape <see cref="Test_P_Profile_RendersAndSavesTheme"/> uses.
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
        // Scoped to the Actor Information card's actor-type cell rather than the whole page body:
        // "System" (the seeded row's ActorType) is a common enough word that an unscoped
        // body-text match would pass even if this field rendered something else entirely.
        await Expect(page.Locator("[data-testid='actor-type']")).ToContainTextAsync("System");
        await Expect(page.Locator("body")).ToContainTextAsync("BotStarted");
        await Expect(page.Locator("time[data-utc]").First).ToHaveAttributeAsync("data-localtime-converted", new Regex(".+"));

        await page.GotoAsync($"/Admin/MessageLogs/Details/{messageLogId}");
        await Expect(page.Locator("h1")).ToHaveTextAsync("Message Details");
        await Expect(page.Locator("body")).ToContainTextAsync("Hello from the E2E seed");
        await Expect(page.Locator("body")).ToContainTextAsync("e2e-log-user");
        await Expect(page.Locator("body")).ToContainTextAsync("E2E Log Details Guild");
        await Expect(page.Locator("time[data-utc]").First).ToHaveAttributeAsync("data-localtime-converted", new Regex(".+"));

        await page.GotoAsync($"/CommandLogs/Details/{commandLogId}");
        await Expect(page.Locator("h1")).ToContainTextAsync("ping");
        await Expect(page.Locator("body")).ToContainTextAsync("E2E Log Details Guild");
        await Expect(page.Locator("body")).ToContainTextAsync("e2e-log-user");
        await Expect(page.Locator("time[data-utc]").First).ToHaveAttributeAsync("data-localtime-converted", new Regex(".+"));
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
    /// <see cref="Guid"/> value itself, not <c>.ToString()</c> - Microsoft.Data.Sqlite binds a
    /// <see cref="Guid"/> parameter as <c>TEXT</c> (its canonical hyphenated string form), the
    /// same storage class and format EF Core's own <c>TEXT</c>-affinity GUID column mapping
    /// writes, so passing the value directly matches what <c>CommandLogService.GetByIdAsync</c>'s
    /// own EF-issued lookup reads back. A hand-rolled <c>.ToString()</c> would very likely produce
    /// the identical string and also work here - the point is to bind the typed value and let the
    /// driver own the conversion, not to work around some other serialization on either side.
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

    /// <summary>
    /// Covers the Admin/Users Blazor cluster end to end (docs/plans/blazor-port-plan.md Phase 4
    /// cluster 4a): create a user, view its Details, edit its display name, reset its password via
    /// the confirm modal, then toggle it inactive from the Index list - the full round trip a real
    /// admin would drive through the UI, including every "first interaction on a freshly navigated
    /// page" EditForm submit/button race described in
    /// docs/lessons-learned/blazor-editform-formname-race.md.
    /// </summary>
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

    /// <summary>
    /// Covers cluster 4b's first real <c>GuildLayout</c> consumer end to end (plan §5 Phase 4):
    /// <c>Guilds/Edit</c> renders the full guild shell (breadcrumb, header, desktop tab strip) for
    /// a seeded guild, a toggle-and-save round trip lands on <c>/Guilds/Details/{id}</c> (a Razor
    /// Page, unaffected by this cluster) with a success toast, and an unknown guild id renders the
    /// not-found gate with no chrome - replacing the retired <c>Test_I_GuildProbe_...</c>/
    /// <c>Test_J_GuildProbe_...</c> pair (both retired with <c>GuildProbe.razor</c> itself) now
    /// that a real page proves the same shell.
    /// </summary>
    [E2EFact]
    public async Task Test_U_GuildEdit_RendersShell_SavesAndRedirects()
    {
        const ulong guildId = 900000000000000003UL;
        SeedGuild(guildId, "E2E Edit Guild");

        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        await page.GotoAsync($"/Guilds/Edit/{guildId}");

        await Expect(page.Locator("nav[aria-label='Breadcrumb']")).ToContainTextAsync("E2E Edit Guild");
        var tabNav = page.Locator("#guildNav");
        await Expect(tabNav).ToBeVisibleAsync();
        await Expect(tabNav.GetByText("Overview", new LocatorGetByTextOptions { Exact = true })).ToBeVisibleAsync();

        await page.WaitForTimeoutAsync(1_500);
        await page.Locator("label[for='Input_IsActive']").ClickAsync();
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save Settings" }).ClickAsync();

        await Expect(page).ToHaveURLAsync(new Regex($@"/Guilds/Details/{guildId}$"), new PageAssertionsToHaveURLOptions { Timeout = 20_000 });
        await Expect(page.Locator(".toast-success")).ToBeVisibleAsync();

        // Unknown guild id: the gate's not-found content renders, with the breadcrumb/header/tab
        // chrome omitted around it - same shape GuildProbe's Test_J proved for the retired probe.
        const ulong unknownGuildId = 900000000000000098UL;
        var response = await page.GotoAsync($"/Guilds/Edit/{unknownGuildId}");
        response.Should().NotBeNull();
        response!.Status.Should().Be(200);
        await Expect(page.GetByText("Server Not Found")).ToBeVisibleAsync();
        await Expect(page.Locator("nav[aria-label='Breadcrumb']")).ToHaveCountAsync(0);
    }

    /// <summary>
    /// Smoke-covers all five of the other cluster 4b guild pages (plan §5 Phase 4) in one pass:
    /// each renders its real main heading and its content for a seeded guild in web-only mode (no
    /// Discord gateway, so channel lists are empty and Discord names fall back to raw ids), then
    /// exercises AssistantSettings' save round trip and Welcome's "channel required when enabled"
    /// field-error rule plus its success-toast/stay-in-place save path.
    /// </summary>
    /// <remarks>
    /// Earlier versions of this test tolerated an <c>ErrorBoundary</c> fallback or a raw ASP.NET
    /// exception page in place of each page's real content (via a since-deleted
    /// <c>ExpectHeadingOrKnownDbGapAsync</c> helper), skipped AssistantSettings entirely, and
    /// worked around a from-scratch SQLite database intermittently missing tables/columns at app
    /// startup even though the migration set itself was correct. That gap was in how
    /// <c>Program.cs</c> resolved the base <c>BotDbContext</c> rather than the provider-specific
    /// <c>SqliteBotDbContext</c> at startup - fixed by "fix(infra): resolve SqliteBotDbContext at
    /// startup, not the base BotDbContext" - so a fresh database (exactly what this fixture's
    /// fresh-tempdir-per-run db is) no longer hits it, and this test asserts strictly again.
    /// </remarks>
    [E2EFact]
    public async Task Test_V_GuildPages_RenderWebOnly()
    {
        const ulong guildId = 900000000000000004UL;
        SeedGuild(guildId, "E2E Cluster Guild");

        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        // Welcome - matches its own guild-nav tab, so GuildLayout's header shows the tab label.
        await page.GotoAsync($"/Guilds/Welcome/{guildId}");
        await Expect(page.Locator("h1")).ToHaveTextAsync("Welcome");
        await Expect(page.GetByText("Enable Welcome Messages")).ToBeVisibleAsync();
        await Expect(page.GetByText("Live Preview")).ToBeVisibleAsync();

        // AssistantSettings - matches its own "assistant" guild-nav tab. The channel checklist
        // renders empty in this web-only host (no live Discord gateway for
        // IDiscordChannelResolver.GetTextChannels to read from); saving as-is (assistant disabled
        // by default on a freshly-created settings row, so there is no channel-required rule in
        // play) round-trips to a success toast.
        await page.GotoAsync($"/Guilds/AssistantSettings/{guildId}");
        await Expect(page.Locator("h1")).ToHaveTextAsync("Assistant");
        await Expect(page.GetByText("No text channels available")).ToBeVisibleAsync();
        await page.WaitForTimeoutAsync(1_500);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save Settings" }).ClickAsync();
        await Expect(page.Locator(".toast-success")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 20_000 });

        // AssistantMetrics - route matches no guild-nav tab (its tab points at AssistantSettings'
        // URL instead), so GuildLayout falls back to the guild's own name as the header title.
        // This host has no OpenRouter:ApiKey configured (BotHostFixture), so IAssistantService is
        // never registered and the daily-metrics panel renders its "not configured" state, not
        // "No usage data yet" - see AssistantMetrics.razor.cs's class remarks.
        await page.GotoAsync($"/Guilds/AssistantMetrics/{guildId}");
        await Expect(page.Locator("h1")).ToHaveTextAsync("E2E Cluster Guild");
        // Exact match: the Prompt Surface panel further down this same page also renders text
        // starting with this same sentence ("...on this deployment, so there is no tool array to
        // measure."), so a substring match resolves to two elements.
        await Expect(page.GetByText("The assistant is not configured on this deployment", new PageGetByTextOptions { Exact = true })).ToBeVisibleAsync();

        // AudioModerationLog - same "no matching tab" case as AssistantMetrics.
        await page.GotoAsync($"/Guilds/AudioModerationLog/{guildId}");
        await Expect(page.Locator("h1")).ToHaveTextAsync("E2E Cluster Guild");
        await Expect(page.Locator("#audioTabs")).ToBeVisibleAsync();
        await Expect(page.GetByText("No audio playback events found")).ToBeVisibleAsync();

        // RatWatch - matches its own guild-nav tab.
        await page.GotoAsync($"/Guilds/RatWatch/{guildId}");
        await Expect(page.Locator("h1")).ToHaveTextAsync("Rat Watch");
        await Expect(page.GetByText("No Rat Watches Yet")).ToBeVisibleAsync();

        // Welcome: enable with no channel selected (web-only mode resolves no Discord channels at
        // all, so there is nothing to pick even if a selector were used) - the manual
        // "channel required when enabled" rule refuses the save with a field error, not a toast.
        await page.GotoAsync($"/Guilds/Welcome/{guildId}");
        await page.WaitForTimeoutAsync(1_500);
        await page.Locator("label[for='Input_IsEnabled']").ClickAsync();
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save Configuration" }).ClickAsync();
        await Expect(page.Locator("#Input_WelcomeChannelId-error")).ToContainTextAsync("A welcome channel must be selected");
        await Expect(page.Locator(".toast-success")).ToHaveCountAsync(0);

        // Disabling it again removes the reason for that rule, so the same save now succeeds.
        await page.Locator("label[for='Input_IsEnabled']").ClickAsync();
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save Configuration" }).ClickAsync();
        await Expect(page.Locator(".toast-success")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 20_000 });
    }

    /// <summary>Fills and submits the email/password form on /Account/Login and waits for the redirect to complete.</summary>

    /// <summary>
    /// Covers the ScheduledMessages Blazor cluster (docs/plans/blazor-port-plan.md Phase 4 cluster
    /// 4b) end to end: list, edit (prefill + save), pause/resume toggle, and delete. The channel
    /// select renders empty in this web-only host (no live Discord gateway for
    /// <c>IDiscordChannelResolver.GetTextChannels</c> to read from), so - per the cluster brief -
    /// the message itself is seeded directly via SQLite (the same <see cref="BotHostFixture.DatabasePath"/>
    /// escape hatch <see cref="SeedGuild"/> uses) rather than created through the UI; leaving the
    /// channel select untouched on Edit still round-trips correctly since Blazor's two-way binding
    /// keeps <c>Input.ChannelId</c> in C# memory regardless of what the empty dropdown displays.
    /// </summary>
    [E2EFact]
    public async Task Test_W_ScheduledMessages_CreateListEditDelete_RoundTrip()
    {
        var (guildId, messageId) = SeedScheduledMessage();

        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        // Index shows the seeded row.
        await page.GotoAsync($"/Guilds/ScheduledMessages/{guildId}");
        var row = page.Locator("[data-testid='scheduled-message-row']");
        await Expect(row).ToHaveCountAsync(1);
        await Expect(row).ToContainTextAsync("E2E nightly digest");
        await Expect(row).ToContainTextAsync("Active");

        // Edit: prefill shows the seeded title/content and a populated local datetime, then save
        // an edited title.
        await row.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Edit" }).ClickAsync();
        await page.WaitForTimeoutAsync(1_500);
        await Expect(page.Locator("#Input_Title")).ToHaveValueAsync("E2E nightly digest");
        await Expect(page.Locator("#Input_Content")).ToHaveValueAsync("Here's what happened today.");
        await Expect(page.Locator("#Input_NextExecutionAt")).Not.ToHaveValueAsync(string.Empty);

        await page.Locator("#Input_Title").FillAsync("E2E nightly digest (edited)");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save Changes" }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex($@"/Guilds/ScheduledMessages/{guildId}$"), new PageAssertionsToHaveURLOptions { Timeout = 20_000 });
        await Expect(page.Locator(".toast-success")).ToBeVisibleAsync();
        await Expect(row).ToContainTextAsync("E2E nightly digest (edited)");

        // Pause/resume toggle. A fresh page load (rather than trusting the server-side redirect
        // above to have left a settled, already-attached circuit behind) gives this the same
        // predictable "just navigated, wait for the circuit" shape Test_R's Index toggle uses.
        await page.GotoAsync($"/Guilds/ScheduledMessages/{guildId}");
        await page.WaitForTimeoutAsync(1_500);
        await row.Locator("button[title='Pause']").ClickAsync();
        await Expect(row).ToContainTextAsync("Paused", new LocatorAssertionsToContainTextOptions { Timeout = 20_000 });

        // A fresh page load (rather than a second click straight on the row the Pause toggle just
        // re-rendered) for the same reason as above: toggling is not idempotent, so a retry-click
        // loop here (the AssertCounterIncrementsAsync shape) risks flipping the state back and
        // forth if an earlier click actually landed before its own assertion observed it - unlike
        // a pure counter increment, a missed-vs-landed click can't be told apart from the outside.
        // A clean reload avoids the ambiguity entirely.
        await page.GotoAsync($"/Guilds/ScheduledMessages/{guildId}");
        await page.WaitForTimeoutAsync(1_500);
        await row.Locator("button[title='Resume']").ClickAsync();
        await Expect(row).ToContainTextAsync("Active", new LocatorAssertionsToContainTextOptions { Timeout = 20_000 });

        // Delete via the confirm modal -> empty state, straight off the same circuit the Edit save
        // and both toggles just used (no reload first): every mutation on this page (save, toggle,
        // delete) - and the reload that follows it - resolves its IScheduledMessageService through
        // a fresh IServiceScopeFactory scope (Blazor/Common/ScopedOperations.cs) instead of the
        // page's injected, circuit-scoped instance, so there is no stale tracked entity left behind
        // for a later call in this circuit to collide with. See
        // docs/lessons-learned/scheduled-message-repeated-update-tracking.md.
        await row.Locator("button[title='Delete']").ClickAsync();
        var deleteModal = page.Locator("#delete-scheduled-message-modal");
        await Expect(deleteModal).ToBeVisibleAsync();
        await deleteModal.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete" }).ClickAsync();
        await Expect(page.GetByText("No Scheduled Messages")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 20_000 });

        _ = messageId; // asserted indirectly via the row content above
    }

    /// <summary>
    /// Covers the Reminders Blazor cluster (cluster 4b) end to end: a seeded Pending reminder for
    /// an unresolvable Discord user id renders "Unknown (id)" (there is no live gateway/REST user
    /// in this web-only host - <see cref="DiscordBot.Bot.Services.Reminders.DiscordReminderUserResolver"/>'s
    /// documented fallback), and can be cancelled via the confirm modal.
    /// </summary>
    [E2EFact]
    public async Task Test_X_Reminders_ListAndCancel()
    {
        var (guildId, reminderId, userId) = SeedReminder();

        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        await page.GotoAsync($"/Guilds/Reminders/{guildId}");
        var row = page.Locator("[data-testid='reminder-row']");
        await Expect(row).ToHaveCountAsync(1);
        await Expect(row).ToContainTextAsync($"Unknown ({userId})");
        await Expect(row).ToContainTextAsync("Pending");

        await page.WaitForTimeoutAsync(1_500);
        await row.Locator("button[title='Cancel Reminder']").ClickAsync();
        var cancelModal = page.Locator("#cancel-reminder-modal");
        await Expect(cancelModal).ToBeVisibleAsync();
        await cancelModal.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Cancel Reminder" }).ClickAsync();

        await Expect(row).ToContainTextAsync("Cancelled", new LocatorAssertionsToContainTextOptions { Timeout = 20_000 });
        await Expect(row.Locator("button[title='Cancel Reminder']")).ToHaveCountAsync(0);

        _ = reminderId; // asserted indirectly via the row content above
    }

    /// <summary>
    /// Covers the FeatureRequests Blazor cluster (cluster 4b) end to end: a seeded Submitted
    /// request lists, opens to Details, and Approve flips it to the Approved badge (reviewer id
    /// comes from the seeded SuperAdmin's <c>discord:user_id</c> claim - see
    /// <c>IdentitySeeder</c>/<c>BotHostFixture</c>).
    /// </summary>
    [E2EFact]
    public async Task Test_Y_FeatureRequests_ListDetailsApprove()
    {
        var (guildId, requestId) = SeedFeatureRequest();

        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        await page.GotoAsync($"/Guilds/FeatureRequests/{guildId}");
        var row = page.Locator("[data-testid='feature-request-row']");
        await Expect(row).ToHaveCountAsync(1);
        await Expect(row).ToContainTextAsync("Submitted");
        await Expect(row).ToContainTextAsync("Add an /export command");

        await row.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "View Details" }).ClickAsync();
        await Expect(page.Locator("[data-testid='feature-request-details']")).ToBeVisibleAsync();
        await page.WaitForTimeoutAsync(1_500);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Approve", Exact = true }).ClickAsync();

        await Expect(page.Locator("[data-testid='feature-request-details']")).ToContainTextAsync("Approved", new LocatorAssertionsToContainTextOptions { Timeout = 20_000 });

        await page.GotoAsync($"/Guilds/FeatureRequests/{guildId}");
        await Expect(row).ToContainTextAsync("Approved");
        _ = requestId; // asserted indirectly via the row content above
    }

    /// <summary>
    /// Covers the static SSR port of Pages/Account/Login.cshtml + LoginModel end to end
    /// (docs/plans/blazor-port-plan.md Phase 4 cluster 4c): a wrong password re-renders the same
    /// page with an error alert and no redirect (not a Blazor <c>NavigationException</c> gone
    /// wrong), a correct password with <c>?returnUrl=/components</c> lands there instead of the
    /// default dashboard, and an externally-controlled <c>?returnUrl=https://evil.example</c> is
    /// rejected (<c>LocalUrl.IsLocal</c>) and falls back to <c>/</c> rather than leaving the
    /// browser on an attacker-controlled URL.
    /// </summary>
    [E2EFact]
    public async Task Test_Z1_Login_BadPassword_ShowsError_And_ReturnUrl_RoundTrips()
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);

        await page.GotoAsync("/Account/Login");
        await page.Locator("#email").FillAsync(_host.SeededAdminEmail);
        await page.Locator("#password").FillAsync("definitely-the-wrong-password");
        await page.Locator("#login-form button[type=submit]").ClickAsync();

        await Expect(page.GetByText("Sign-in failed")).ToBeVisibleAsync();
        await Expect(page).ToHaveURLAsync(new Regex(@"/Account/Login"));

        await page.GotoAsync("/Account/Login?returnUrl=%2Fcomponents");
        await page.Locator("#email").FillAsync(_host.SeededAdminEmail);
        await page.Locator("#password").FillAsync(_host.SeededAdminPassword);
        await page.Locator("#login-form button[type=submit]").ClickAsync();

        await Expect(page).ToHaveURLAsync($"{_host.BaseUrl}/components", new PageAssertionsToHaveURLOptions { Timeout = 20_000 });

        // Still authenticated from the login above - revisiting /Account/Login now takes the
        // "already authenticated" branch in Login.razor.cs's OnInitialized, which sanitizes and
        // redirects immediately (no credentials needed), exercising the same LocalUrl.IsLocal
        // check the OnValidSubmit path uses.
        await page.GotoAsync("/Account/Login?returnUrl=https%3A%2F%2Fevil.example");

        await Expect(page).ToHaveURLAsync($"{_host.BaseUrl}/", new PageAssertionsToHaveURLOptions { Timeout = 20_000 });
    }

    /// <summary>
    /// Covers the <c>?authError=</c> contract Login.razor.cs preserves from
    /// <c>LoginModel.OnGet</c>: <c>discord_unavailable</c> shows its title plus the Discord status
    /// link, <c>discord_expired</c> shows its own copy with no status link.
    /// </summary>
    [E2EFact]
    public async Task Test_Z2_Login_AuthError_Renders()
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);

        await page.GotoAsync("/Account/Login?authError=discord_unavailable");
        await Expect(page.GetByText("Discord is currently unavailable")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Discord status" })).ToBeVisibleAsync();

        await page.GotoAsync("/Account/Login?authError=discord_expired");
        await Expect(page.GetByText("Login session expired")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Discord status" })).ToHaveCountAsync(0);
    }

    /// <summary>Seeds one <c>Guilds</c> row and one <c>ScheduledMessages</c> row referencing it, for <see cref="Test_W_ScheduledMessages_CreateListEditDelete_RoundTrip"/>.</summary>
    private (ulong GuildId, Guid MessageId) SeedScheduledMessage()
    {
        var salt = (ulong)Random.Shared.NextInt64(1, 1_000_000);
        var guildId = 900000000000000020UL + salt;
        SeedGuild(guildId, "E2E Scheduled Messages Guild");

        var messageId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        using var connection = new SqliteConnection($"Data Source={_host.DatabasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ScheduledMessages (Id, GuildId, ChannelId, Title, Content, CronExpression, Frequency, IsEnabled, LastExecutedAt, NextExecutionAt, CreatedAt, CreatedBy, UpdatedAt)
            VALUES ($id, $guildId, $channelId, $title, $content, NULL, 3, 1, NULL, $nextExecutionAt, $createdAt, 'e2e-seed', $updatedAt)
            """;
        command.Parameters.AddWithValue("$id", messageId);
        command.Parameters.AddWithValue("$guildId", unchecked((long)guildId));
        command.Parameters.AddWithValue("$channelId", unchecked((long)(900000000000000021UL + salt)));
        command.Parameters.AddWithValue("$title", "E2E nightly digest");
        command.Parameters.AddWithValue("$content", "Here's what happened today.");
        command.Parameters.AddWithValue("$nextExecutionAt", now.AddHours(2).ToString("O"));
        command.Parameters.AddWithValue("$createdAt", now.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
        command.ExecuteNonQuery();

        return (guildId, messageId);
    }

    /// <summary>Seeds one <c>Guilds</c> row and one Pending <c>Reminders</c> row referencing it, for <see cref="Test_X_Reminders_ListAndCancel"/>.</summary>
    private (ulong GuildId, Guid ReminderId, ulong UserId) SeedReminder()
    {
        var salt = (ulong)Random.Shared.NextInt64(1, 1_000_000);
        var guildId = 900000000000000030UL + salt;
        SeedGuild(guildId, "E2E Reminders Guild");

        var reminderId = Guid.NewGuid();
        var userId = 900000000000000031UL + salt;
        var now = DateTime.UtcNow;

        using var connection = new SqliteConnection($"Data Source={_host.DatabasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Reminders (Id, GuildId, ChannelId, UserId, Message, TriggerAt, CreatedAt, DeliveredAt, Status, DeliveryAttempts, LastError)
            VALUES ($id, $guildId, $channelId, $userId, $message, $triggerAt, $createdAt, NULL, 0, 0, NULL)
            """;
        command.Parameters.AddWithValue("$id", reminderId);
        command.Parameters.AddWithValue("$guildId", unchecked((long)guildId));
        command.Parameters.AddWithValue("$channelId", unchecked((long)(900000000000000032UL + salt)));
        command.Parameters.AddWithValue("$userId", unchecked((long)userId));
        command.Parameters.AddWithValue("$message", "E2E stand-up reminder");
        command.Parameters.AddWithValue("$triggerAt", now.AddHours(1).ToString("O"));
        command.Parameters.AddWithValue("$createdAt", now.ToString("O"));
        command.ExecuteNonQuery();

        return (guildId, reminderId, userId);
    }

    /// <summary>Seeds one <c>Guilds</c> row and one Submitted <c>FeatureRequests</c> row referencing it, for <see cref="Test_Y_FeatureRequests_ListDetailsApprove"/>.</summary>
    private (ulong GuildId, Guid RequestId) SeedFeatureRequest()
    {
        var salt = (ulong)Random.Shared.NextInt64(1, 1_000_000);
        var guildId = 900000000000000040UL + salt;
        SeedGuild(guildId, "E2E Feature Requests Guild");

        var requestId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        using var connection = new SqliteConnection($"Data Source={_host.DatabasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO FeatureRequests (Id, GuildId, SubmittedByUserId, Title, Description, GatheredRequirements, ConsolidatedSummary, Status, ReviewedByUserId, ReviewedAt, ReviewNotes, DocBranchName, DocPath, DocGenError, CreatedAt, UpdatedAt)
            VALUES ($id, $guildId, $userId, $title, $description, NULL, NULL, 0, NULL, NULL, NULL, NULL, NULL, NULL, $createdAt, $updatedAt)
            """;
        command.Parameters.AddWithValue("$id", requestId);
        command.Parameters.AddWithValue("$guildId", unchecked((long)guildId));
        command.Parameters.AddWithValue("$userId", unchecked((long)(900000000000000041UL + salt)));
        command.Parameters.AddWithValue("$title", "Add an /export command");
        command.Parameters.AddWithValue("$description", "Add an /export command");
        command.Parameters.AddWithValue("$createdAt", now.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
        command.ExecuteNonQuery();

        return (guildId, requestId);
    }

    /// <summary>
    /// Covers <c>Blazor/Pages/Account/LinkDiscord.razor</c> (Phase 4 cluster 4c) against this
    /// fixture's web-only host (<c>Discord:Enabled=false</c>, no OAuth client configured - see
    /// "Discord:Enabled (web-only mode)" in <c>docs/articles/configuration-guide.md</c>): the
    /// page still renders its "OAuth Not Configured" copy, and bot verification (which
    /// authenticates entirely through the Discord bot, never the OAuth client) stays reachable
    /// and working end to end - see the "Deliberately not preserved" remark in
    /// <c>LinkDiscord.razor</c> for why this differs from the legacy page's nesting.
    /// </summary>
    [E2EFact]
    public async Task Test_Z3_LinkDiscord_WebOnly_ShowsNotConfigured_AndVerificationFlow()
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        await page.GotoAsync("/Account/LinkDiscord");
        await Expect(page.Locator("h2", new PageLocatorOptions { HasText = "Discord OAuth Not Configured" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Start Verification" })).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Start Verification" }).ClickAsync();

        await Expect(page.GetByText("Verification pending")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 20_000 });
        await Expect(page.Locator("#VerificationCode")).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Cancel", Exact = true }).ClickAsync();

        await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Start Verification" })).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 20_000 });
        await Expect(page.GetByText("Verification pending")).Not.ToBeVisibleAsync();
    }

    /// <summary>
    /// Covers <c>Blazor/Pages/Account/Privacy.razor</c> (Phase 4 cluster 4c): the seeded
    /// SuperAdmin has no Discord link in this fixture, so the page renders only the
    /// "Discord Account Required" callout - the Data Management card (export/delete) never
    /// reaches the DOM for an unlinked user, matching the legacy page exactly (deleting data is
    /// keyed on the Discord user id, unlike bot verification above, so there is no equivalent
    /// "make it reachable anyway" fix here). To reach the typed-DELETE-confirmation guard this
    /// test also checks, it links the same seeded user directly in the database (the web-only
    /// host has no real Discord OAuth to link through) and reloads - the same raw-SQL seeding
    /// idiom <see cref="SeedGuild"/> and friends use for their own fixtures.
    /// </summary>
    [E2EFact]
    public async Task Test_Z4_Privacy_UnlinkedUser_RendersCallout_AndDeleteRequiresConfirmation()
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        await page.GotoAsync("/Account/Privacy");
        await Expect(page.GetByText("Discord Account Required")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Delete All Data" })).Not.ToBeVisibleAsync();

        var discordUserId = LinkSeededAdminToDiscord();
        // ConsentType.MessageLogging = 1 - granted, RevokedAt NULL - so the privacy-actions form
        // below the delete form actually renders a consent row with a live Grant/Revoke button,
        // needed for the BLOCKING regression check further down.
        SeedGrantedConsent(discordUserId, consentType: 1);
        await page.GotoAsync("/Account/Privacy");
        await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Delete All Data" })).ToBeVisibleAsync();

        await page.Locator("input[placeholder='Type DELETE to confirm']").FillAsync("delete");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Delete All Data" }).ClickAsync();

        await Expect(page.GetByText("Type DELETE to confirm.")).ToBeVisibleAsync();
        // Still on Privacy, still signed in - a real purge would have signed the session out and
        // redirected to /landing (Privacy.razor.cs's HandleDeleteDataAsync).
        await Expect(page).ToHaveURLAsync(new Regex(@"/Account/Privacy$"));
        await Expect(page.Locator("#sidebar")).ToBeVisibleAsync();

        // BLOCKING review finding: "Delete My Data" is now its own <EditForm FormName="privacy-delete">,
        // separate from "privacy-actions" (consent Grant/Revoke + Export) - see Privacy.razor.cs's
        // class remarks. Before that split, this same confirmation input lived in privacy-actions
        // alongside the consent toggle above, so pressing Enter here would implicitly submit the
        // FIRST submit button in that shared form (a consent toggle), not Delete - silently
        // flipping consent instead of asking for a delete. Prove the fix: press Enter with the
        // wrong confirmation text and assert both the same validation error appears AND the seeded
        // consent is untouched (checked directly against the database, not just the DOM, so a
        // regression that flips it without changing the visible badge text can't hide).
        var confirmationInput = page.Locator("input[placeholder='Type DELETE to confirm']");
        await confirmationInput.FillAsync("nope");
        await confirmationInput.PressAsync("Enter");

        await Expect(page.GetByText("Type DELETE to confirm.")).ToBeVisibleAsync();
        await Expect(page).ToHaveURLAsync(new Regex(@"/Account/Privacy$"));
        ConsentIsGranted(discordUserId, consentType: 1).Should().BeTrue(
            "Enter in the delete confirmation box must submit only the delete form, never the consent Grant/Revoke button in the separate privacy-actions form");
    }

    /// <summary>
    /// Covers the unified Admin/Logs page end to end (docs/plans/blazor-port-plan.md Phase 4
    /// cluster 4d): the <c>?tab=audit</c> query selects the Audit tab on load, a seeded row is
    /// visible, and the CSV export link (now a minimal-API GET rather than a page handler)
    /// downloads a file whose header row matches <see cref="DiscordBot.Bot.Services.AdminLogsCsvExporter.Header"/>.
    /// </summary>
    [E2EFact]
    public async Task Test_ZE1_AdminLogs_TabsFiltersAndAuditExport()
    {
        var (_, auditLogId, _, _) = SeedLogRows();

        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        await page.GotoAsync("/Admin/Logs?tab=audit");
        await Expect(page.Locator("[data-testid='audit-filter-form']")).ToBeVisibleAsync();
        await Expect(page.Locator("body")).ToContainTextAsync("BotStarted");

        var download = await page.RunAndWaitForDownloadAsync(async () =>
        {
            await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Export CSV" }).ClickAsync();
        });

        var path = await download.PathAsync();
        path.Should().NotBeNullOrEmpty();
        var lines = await File.ReadAllLinesAsync(path!);
        // Literal copy of Services/AdminLogsCsvExporter.Header - this project has no reference to
        // DiscordBot.Bot (see the .csproj comment SeedGuild's own doc points to).
        lines[0].Should().Be("Timestamp,Category,Action,Actor,Target Type,Target ID,Guild,Details,IP Address,Correlation ID");
        _ = auditLogId;
    }

    /// <summary>
    /// Covers the Admin/Notifications page end to end (cluster 4d): marking one seeded unread
    /// notification read updates its row in place (no navigation - <c>ToggleReadAsync</c> reloads
    /// through <c>ScopedOperations</c>), and deleting the other removes its row.
    /// </summary>
    [E2EFact]
    public async Task Test_ZE2_Notifications_MarkReadInPlace()
    {
        var (id1, id2) = SeedNotificationsForSeededAdmin();

        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        await page.GotoAsync("/Admin/Notifications");
        // Scoped to the two seeded rows by id, not an exact total count: the live host also runs
        // its own performance-threshold monitor, which can add its own real notifications for
        // this same admin (visible below as "Performance Alert" rows) independently of this seed.
        var firstRow = page.Locator($"[data-notification-id='{id1}']");
        var secondRow = page.Locator($"[data-notification-id='{id2}']");
        await Expect(firstRow).ToBeVisibleAsync();
        await Expect(secondRow).ToBeVisibleAsync();

        await Expect(firstRow).ToHaveAttributeAsync("data-read", "false");
        var markReadButton = firstRow.Locator("button[title='Mark read']");
        // Wait for the circuit to actually attach before clicking - a click fired in the
        // prerender-to-circuit handoff window is dropped, not queued (see docs/lessons-learned/
        // blazor-editform-formname-race.md); the button's own disabled="@(!RendererInfo.IsInteractive)"
        // binding is what makes this wait meaningful.
        await Expect(markReadButton).ToBeEnabledAsync();
        await markReadButton.ClickAsync();
        await Expect(firstRow).ToHaveAttributeAsync("data-read", "true");
        await Expect(page).ToHaveURLAsync(new Regex(@"/Admin/Notifications$"));

        var deleteButton = secondRow.Locator("button[title='Delete']");
        await Expect(deleteButton).ToBeEnabledAsync();
        await deleteButton.ClickAsync();
        await Expect(secondRow).Not.ToBeVisibleAsync();
    }

    /// <summary>
    /// Covers the Admin/LlmUsage page end to end (cluster 4d): the hero tiles reflect three
    /// seeded <c>LlmUsageRecords</c> rows, and clicking the user's row in "Cost by User" loads the
    /// per-user drill-down (now a direct paged repository call, replacing <c>llm-usage.js</c>).
    /// </summary>
    [E2EFact]
    public async Task Test_ZE3_LlmUsage_HeroAndDrilldown()
    {
        SeedLlmUsageRecords(3);

        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        await page.GotoAsync("/Admin/LlmUsage");
        var messagesCard = page.Locator(".hero-metric-card").Filter(new LocatorFilterOptions { HasTextString = "Messages" });
        await Expect(messagesCard.Locator(".hero-metric-value")).ToHaveTextAsync("3");

        // The user row itself has no natural disabled state to wait on (it's a <tr>, not a
        // button) - RendererInfo.IsInteractive is circuit-wide, not per-element, so waiting for
        // the filter form's own disabled-gated Apply button to become enabled is a valid proxy
        // for "the circuit has attached and every handler on this page is live", same idea as
        // Test_ZE2's explicit ToBeEnabledAsync wait.
        await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Apply" })).ToBeEnabledAsync();
        await page.Locator("[data-testid='llm-usage-user-row']").First.ClickAsync();
        await Expect(page.Locator("[data-testid='llm-usage-drilldown-row']")).ToHaveCountAsync(3);
    }

    /// <summary>
    /// Covers Admin/UserPurge end to end for the SuperAdmin role the seeded admin already carries
    /// (cluster 4d): a seeded, unlinked Discord member with one <c>ModNotes</c> row previews with
    /// a non-zero count, the typed-confirm button stays disabled until the Discord user id itself
    /// is typed (the dynamic <c>RequiredText</c>, unlike BulkPurge's fixed "CONFIRM"), and purging
    /// shows the success banner.
    /// </summary>
    [E2EFact]
    public async Task Test_ZE4_UserPurge_PreviewAndTypedConfirm()
    {
        var discordUserId = SeedPurgeableMember();

        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        await page.GotoAsync($"/Admin/UserPurge?DiscordUserId={discordUserId}");
        await Expect(page.Locator("[data-testid='user-purge-preview']")).ToBeVisibleAsync();
        await Expect(page.Locator("[data-testid='user-purge-preview-row']").Filter(new LocatorFilterOptions { HasTextString = "ModNotes" })).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Purge This User's Data" }).ClickAsync();
        var confirmButton = page.Locator("#userPurgeModal").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Purge User Data" });
        await Expect(confirmButton).ToBeDisabledAsync();

        await page.Locator("#userPurgeModalInput").FillAsync(discordUserId.ToString());
        await Expect(confirmButton).ToBeEnabledAsync();
        await confirmButton.ClickAsync();

        await Expect(page.Locator("body")).ToContainTextAsync("purged successfully");
    }

    /// <summary>Seeds two unread <c>UserNotifications</c> rows for the seeded SuperAdmin, for <see cref="Test_ZE2_Notifications_MarkReadInPlace"/>.</summary>
    private (Guid Id1, Guid Id2) SeedNotificationsForSeededAdmin()
    {
        var userId = GetSeededAdminUserId();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var now = DateTime.UtcNow;

        using var connection = new SqliteConnection($"Data Source={_host.DatabasePath}");
        connection.Open();
        foreach (var id in new[] { id1, id2 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO UserNotifications (Id, UserId, Type, Title, Message, IsRead, CreatedAt)
                VALUES ($id, $userId, 2, 'E2E Notification', 'Seeded for Test_ZE2', 0, $now)
                """;
            // Bind the Guid value directly, not .ToString() - see the CommandLogs.Id remark on
            // SeedLogRows: Microsoft.Data.Sqlite's own Guid-parameter binding is what matches the
            // TEXT format EF Core's SQLite provider reads back with FindAsync/GetByIdAsync; a
            // hand-formatted string silently fails that lookup ("Notification ... not found").
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$userId", userId);
            // EF Core's SQLite provider stores/compares DateTime as TEXT in its own
            // "yyyy-MM-dd HH:mm:ss.fffffff" format (no 'T'/'Z') - a round-trip ("O") string sorts
            // differently within the same calendar day (the 'T' at position 10 collates after a
            // space), so a same-day upper-bound filter like this page's default date range can
            // silently exclude a row seeded with "O". Match EF's own format instead.
            command.Parameters.AddWithValue("$now", now.ToString("yyyy-MM-dd HH:mm:ss.fffffff"));
            var rows = command.ExecuteNonQuery();
            rows.Should().Be(1, $"the INSERT for notification {id} into UserNotifications (UserId={userId}) should affect exactly one row");
        }

        return (id1, id2);
    }

    /// <summary>Looks up the seeded SuperAdmin's <c>AspNetUsers.Id</c>, for tests that seed rows owned by that account (e.g. <see cref="SeedNotificationsForSeededAdmin"/>).</summary>
    private string GetSeededAdminUserId()
    {
        using var connection = new SqliteConnection($"Data Source={_host.DatabasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM AspNetUsers WHERE Email = $email";
        command.Parameters.AddWithValue("$email", _host.SeededAdminEmail);
        return (string)command.ExecuteScalar()!;
    }

    /// <summary>Seeds <paramref name="count"/> <c>LlmUsageRecords</c> rows for one salted Discord user id, for <see cref="Test_ZE3_LlmUsage_HeroAndDrilldown"/>.</summary>
    private void SeedLlmUsageRecords(int count)
    {
        var userId = 900000000000000060UL + (ulong)Random.Shared.NextInt64(1, 1_000_000);
        var now = DateTime.UtcNow;

        using var connection = new SqliteConnection($"Data Source={_host.DatabasePath}");
        connection.Open();
        for (var i = 0; i < count; i++)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO LlmUsageRecords (Id, Timestamp, Mode, UserId, Model, InputTokens, OutputTokens, CachedTokens, CacheWriteTokens, LlmCalls, ToolCalls, CostUsd, CostSource, LatencyMs, Success)
                VALUES ($id, $timestamp, 1, $userId, 'anthropic/claude-sonnet-4.6', 100, 50, 0, 0, 1, 0, 0.05, 1, 500, 1)
                """;
            command.Parameters.AddWithValue("$id", DateTime.UtcNow.Ticks + i);
            // See the identical note on SeedNotificationsForSeededAdmin's "$now" parameter -
            // EF's SQLite DateTime TEXT format, not round-trip "O".
            command.Parameters.AddWithValue("$timestamp", now.AddMinutes(-i).ToString("yyyy-MM-dd HH:mm:ss.fffffff"));
            command.Parameters.AddWithValue("$userId", unchecked((long)userId));
            command.ExecuteNonQuery();
        }
    }

    /// <summary>Seeds one Discord <c>Users</c> row (no linked <c>AspNetUsers</c> account, so <c>CanPurgeUserAsync</c> allows it) plus a <c>Guilds</c> row and one <c>ModNotes</c> row authored by that user, for <see cref="Test_ZE4_UserPurge_PreviewAndTypedConfirm"/>.</summary>
    private ulong SeedPurgeableMember()
    {
        var salt = (ulong)Random.Shared.NextInt64(1, 1_000_000);
        var discordUserId = 900000000000000070UL + salt;
        var guildId = 900000000000000080UL + salt;
        var now = DateTime.UtcNow;

        SeedGuild(guildId, "E2E Purge Guild");

        using var connection = new SqliteConnection($"Data Source={_host.DatabasePath}");
        connection.Open();

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "INSERT INTO Users (Id, Username, Discriminator, FirstSeenAt, LastSeenAt) VALUES ($id, $username, '0', $now, $now)";
            command.Parameters.AddWithValue("$id", unchecked((long)discordUserId));
            command.Parameters.AddWithValue("$username", "e2e-purge-member");
            command.Parameters.AddWithValue("$now", now.ToString("O"));
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO ModNotes (Id, GuildId, AuthorUserId, TargetUserId, Content, CreatedAt)
                VALUES ($id, $guildId, $authorUserId, $targetUserId, 'E2E seeded note', $now)
                """;
            command.Parameters.AddWithValue("$id", Guid.NewGuid());
            command.Parameters.AddWithValue("$guildId", unchecked((long)guildId));
            command.Parameters.AddWithValue("$authorUserId", unchecked((long)discordUserId));
            command.Parameters.AddWithValue("$targetUserId", unchecked((long)(discordUserId + 1)));
            command.Parameters.AddWithValue("$now", now.ToString("O"));
            command.ExecuteNonQuery();
        }

        return discordUserId;
    }

    /// <summary>Sets the seeded SuperAdmin's <c>DiscordUserId</c>/<c>DiscordUsername</c> directly in the database, for <see cref="Test_Z4_Privacy_UnlinkedUser_RendersCallout_AndDeleteRequiresConfirmation"/> - the web-only host has no real Discord OAuth to link an account through. Returns the generated Discord user id so a caller can seed/verify per-user rows (e.g. <c>UserConsents</c>) keyed on it.</summary>
    private ulong LinkSeededAdminToDiscord()
    {
        var discordUserId = 900000000000000050UL + (ulong)Random.Shared.NextInt64(1, 1_000_000);

        using var connection = new SqliteConnection($"Data Source={_host.DatabasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE AspNetUsers SET DiscordUserId = $discordUserId, DiscordUsername = 'e2e-seed' WHERE Email = $email";
        command.Parameters.AddWithValue("$discordUserId", unchecked((long)discordUserId));
        command.Parameters.AddWithValue("$email", _host.SeededAdminEmail);
        command.ExecuteNonQuery();

        return discordUserId;
    }

    /// <summary>Inserts an active (<c>RevokedAt</c> NULL) <c>UserConsents</c> row directly, for <see cref="Test_Z4_Privacy_UnlinkedUser_RendersCallout_AndDeleteRequiresConfirmation"/> - the web-only host has no bot to grant consent through <c>/consent grant</c>. <c>UserConsents.DiscordUserId</c> has an FK to <c>Users.Id</c> (the domain <c>User</c>, not <c>AspNetUsers</c>), so this also seeds the minimal required row there first.</summary>
    private void SeedGrantedConsent(ulong discordUserId, int consentType)
    {
        using var connection = new SqliteConnection($"Data Source={_host.DatabasePath}");
        connection.Open();

        using (var userCommand = connection.CreateCommand())
        {
            userCommand.CommandText =
                "INSERT OR IGNORE INTO Users (Id, Username, Discriminator, FirstSeenAt, LastSeenAt) " +
                "VALUES ($id, 'e2e-seed', '0', $now, $now)";
            userCommand.Parameters.AddWithValue("$id", unchecked((long)discordUserId));
            userCommand.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
            userCommand.ExecuteNonQuery();
        }

        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO UserConsents (DiscordUserId, ConsentType, GrantedAt, RevokedAt) VALUES ($discordUserId, $consentType, $grantedAt, NULL)";
        command.Parameters.AddWithValue("$discordUserId", unchecked((long)discordUserId));
        command.Parameters.AddWithValue("$consentType", consentType);
        command.Parameters.AddWithValue("$grantedAt", DateTime.UtcNow.ToString("o"));
        command.ExecuteNonQuery();
    }

    /// <summary>Reads whether <paramref name="discordUserId"/> currently has an active (<c>RevokedAt</c> NULL) consent row for <paramref name="consentType"/> - the direct-database counterpart to <see cref="SeedGrantedConsent"/>, used to prove a UI interaction did (or, here, did not) change it.</summary>
    private bool ConsentIsGranted(ulong discordUserId, int consentType)
    {
        using var connection = new SqliteConnection($"Data Source={_host.DatabasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM UserConsents WHERE DiscordUserId = $discordUserId AND ConsentType = $consentType AND RevokedAt IS NULL";
        command.Parameters.AddWithValue("$discordUserId", unchecked((long)discordUserId));
        command.Parameters.AddWithValue("$consentType", consentType);
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    /// <summary>
    /// Covers the Blazor port of <c>Pages/Guilds/Members/Index.cshtml</c> +
    /// <c>_MemberDetailModal.cshtml</c> (docs/plans/blazor-port-plan.md Phase 4 cluster 4d): two
    /// seeded members both list, the search filter narrows to one, and its detail modal opens
    /// with the seeded nickname plus a working "View Moderation Profile" link.
    /// </summary>
    [E2EFact]
    public async Task Test_ZC1_Members_ListSearchModal()
    {
        var (guildId, aliceId, bobId) = SeedTwoMembers();

        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        await page.GotoAsync($"/Guilds/{guildId}/Members");
        var rows = page.Locator("[data-testid='member-row']");
        await Expect(rows).ToHaveCountAsync(2);
        await Expect(rows.Filter(new LocatorFilterOptions { HasTextString = "e2e-alice" })).ToHaveCountAsync(1);
        await Expect(rows.Filter(new LocatorFilterOptions { HasTextString = "e2e-bob" })).ToHaveCountAsync(1);

        await page.WaitForTimeoutAsync(1_500);
        await page.Locator("#memberSearch").FillAsync("alice");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Apply Filters" }).ClickAsync();

        await Expect(rows).ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions { Timeout = 10_000 });
        await Expect(rows).ToContainTextAsync("e2e-alice");

        await rows.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "View" }).ClickAsync();
        var modal = page.Locator("#memberDetailModal");
        await Expect(modal).ToContainTextAsync("Alice Nickname");

        var moderationLink = modal.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "View Moderation Profile" });
        await Expect(moderationLink).ToHaveAttributeAsync("href", $"/Guilds/{guildId}/Members/{aliceId}/Moderation");

        _ = bobId; // asserted indirectly via the row content above
    }

    /// <summary>
    /// Covers the Blazor port of <c>Pages/Guilds/Members/Moderation.cshtml</c>
    /// (docs/plans/blazor-port-plan.md Phase 4 cluster 4d): adding a note appears in the list
    /// without a page navigation, adding a tag renders a chip, and hovering a
    /// <c>UserPreview</c> trigger (the added note's author) opens the popover - this fixture's
    /// host runs web-only (<c>Discord:Enabled=false</c>, no gateway), so, like every other
    /// Discord-dependent surface covered elsewhere in this file, the popover resolves to its
    /// error state rather than a live username; this only asserts that it opens and resolves.
    /// </summary>
    [E2EFact]
    public async Task Test_ZC2_Moderation_AddNoteAndTag()
    {
        var (guildId, userId, tagName) = SeedMemberForModeration();

        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        await page.GotoAsync($"/Guilds/{guildId}/Members/{userId}/Moderation");
        await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "e2e-charlie" })).ToBeVisibleAsync();

        await page.WaitForTimeoutAsync(1_500);
        await page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = "Notes", Exact = false }).ClickAsync();
        await page.Locator("textarea").FillAsync("E2E moderator note");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add Note" }).ClickAsync();

        var noteItem = page.Locator(".note-item", new PageLocatorOptions { HasTextString = "E2E moderator note" });
        await Expect(noteItem).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
        await Expect(page).ToHaveURLAsync(new Regex($"/Guilds/{guildId}/Members/{userId}/Moderation$"));

        await page.Locator("#addTagSelect").SelectOptionAsync(tagName);
        await Expect(page.Locator(".user-tag-removable")).ToContainTextAsync(tagName, new LocatorAssertionsToContainTextOptions { Timeout = 10_000 });

        await noteItem.Locator("[tabindex='0']").First.HoverAsync();
        // The layout's (hidden) mobile search overlay is also role="dialog", so target the popover's own container.
        await Expect(page.Locator(".preview-popup-container[role='dialog']").First).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
    }

    /// <summary>Seeds one guild and two <c>GuildMembers</c> (with backing <c>Users</c> rows), for <see cref="Test_ZC1_Members_ListSearchModal"/>.</summary>
    private (ulong GuildId, ulong AliceId, ulong BobId) SeedTwoMembers()
    {
        var salt = (ulong)Random.Shared.NextInt64(1, 1_000_000);
        var guildId = 900000000000000060UL + salt;
        SeedGuild(guildId, "E2E Members Guild");

        var aliceId = 900000000000000061UL + salt;
        var bobId = 900000000000000062UL + salt;
        var now = DateTime.UtcNow;

        using var connection = new SqliteConnection($"Data Source={_host.DatabasePath}");
        connection.Open();

        void SeedUserAndMember(ulong userId, string username, string? nickname)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "INSERT INTO Users (Id, Username, Discriminator, FirstSeenAt, LastSeenAt) VALUES ($id, $username, '0', $now, $now)";
                command.Parameters.AddWithValue("$id", unchecked((long)userId));
                command.Parameters.AddWithValue("$username", username);
                command.Parameters.AddWithValue("$now", now.ToString("O"));
                command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT INTO GuildMembers (GuildId, UserId, JoinedAt, Nickname, IsActive, LastCachedAt)
                    VALUES ($guildId, $userId, $joinedAt, $nickname, 1, $joinedAt)
                    """;
                command.Parameters.AddWithValue("$guildId", unchecked((long)guildId));
                command.Parameters.AddWithValue("$userId", unchecked((long)userId));
                command.Parameters.AddWithValue("$joinedAt", now.ToString("O"));
                command.Parameters.AddWithValue("$nickname", (object?)nickname ?? DBNull.Value);
                command.ExecuteNonQuery();
            }
        }

        SeedUserAndMember(aliceId, "e2e-alice", "Alice Nickname");
        SeedUserAndMember(bobId, "e2e-bob", null);

        return (guildId, aliceId, bobId);
    }

    /// <summary>Seeds one guild, one member, and one <c>ModTags</c> row, for <see cref="Test_ZC2_Moderation_AddNoteAndTag"/>.</summary>
    private (ulong GuildId, ulong UserId, string TagName) SeedMemberForModeration()
    {
        var salt = (ulong)Random.Shared.NextInt64(1, 1_000_000);
        var guildId = 900000000000000070UL + salt;
        SeedGuild(guildId, "E2E Moderation Guild");

        var userId = 900000000000000071UL + salt;
        var now = DateTime.UtcNow;
        const string tagName = "E2E-Watch";

        using var connection = new SqliteConnection($"Data Source={_host.DatabasePath}");
        connection.Open();

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "INSERT INTO Users (Id, Username, Discriminator, FirstSeenAt, LastSeenAt) VALUES ($id, 'e2e-charlie', '0', $now, $now)";
            command.Parameters.AddWithValue("$id", unchecked((long)userId));
            command.Parameters.AddWithValue("$now", now.ToString("O"));
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO GuildMembers (GuildId, UserId, JoinedAt, IsActive, LastCachedAt)
                VALUES ($guildId, $userId, $joinedAt, 1, $joinedAt)
                """;
            command.Parameters.AddWithValue("$guildId", unchecked((long)guildId));
            command.Parameters.AddWithValue("$userId", unchecked((long)userId));
            command.Parameters.AddWithValue("$joinedAt", now.ToString("O"));
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO ModTags (Id, GuildId, Name, Color, Category, Description, IsFromTemplate, CreatedAt)
                VALUES ($id, $guildId, $name, '#FF5733', 1, NULL, 0, $now)
                """;
            command.Parameters.AddWithValue("$id", Guid.NewGuid());
            command.Parameters.AddWithValue("$guildId", unchecked((long)guildId));
            command.Parameters.AddWithValue("$name", tagName);
            command.Parameters.AddWithValue("$now", now.ToString("O"));
            command.ExecuteNonQuery();
        }

        return (guildId, userId, tagName);
    }

    /// <summary>
    /// Covers <c>Blazor/Pages/Guilds/Index.razor</c> and <c>Details.razor</c> end to end (plan §5
    /// Phase 4, cluster 4d): the top-level guild list renders a seeded guild's row, and clicking
    /// into it lands on the dashboard with the action bar (Sync/Edit Settings/More Actions, all
    /// gated on <c>CanEdit</c> - the seeded SuperAdmin has it) and every widget's empty state (no
    /// scheduled messages/reminders/members/etc. seeded for this guild).
    /// </summary>
    [E2EFact]
    public async Task Test_ZB1_GuildsIndex_ListsSeededGuild_AndOpensDetails()
    {
        const ulong guildId = 900000000000000060UL;
        SeedGuild(guildId, "E2E Guilds Index Guild");

        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        await page.GotoAsync("/Guilds");
        // Both the desktop row and the mobile card render the guild name in the DOM at once
        // (one hidden via CSS per breakpoint, not removed), so a plain GetByText resolves to
        // two elements under Playwright's strict mode - scope to the desktop row.
        var row = page.Locator("[data-testid='guild-row']", new PageLocatorOptions { HasTextString = "E2E Guilds Index Guild" });
        await Expect(row).ToBeVisibleAsync();

        // The row's navigate-on-click handler only fires once the Interactive Server circuit has
        // actually connected - a click sent to the (already-visible, statically prerendered) row
        // before then is simply dropped, not queued (see AssertCounterIncrementsAsync's doc
        // comment for the same gotcha elsewhere in this file).
        await page.WaitForTimeoutAsync(1_500);
        await row.ClickAsync();

        await Expect(page).ToHaveURLAsync(new Regex($@"/Guilds/Details/{guildId}$"), new PageAssertionsToHaveURLOptions { Timeout = 20_000 });
        await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sync", Exact = true })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Edit Settings" })).ToBeVisibleAsync();
        await Expect(page.GetByText("More Actions")).ToBeVisibleAsync();
        await Expect(page.GetByText("No scheduled messages yet")).ToBeVisibleAsync();
        await Expect(page.GetByText("No reminders yet")).ToBeVisibleAsync();
        await Expect(page.GetByText("No command activity yet")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Covers <c>Blazor/Pages/Guilds/FlaggedEvents/{Index,Details}.razor</c> end to end (plan §5
    /// Phase 4, cluster 4d): the list shows a seeded Pending event, its detail page renders the
    /// event, and confirming Dismiss there navigates back to the list with the row's status badge
    /// now reading Dismissed - proving both the fixed post-dismiss redirect (the legacy page's own
    /// inline JS pointed at a route that never existed) and that dismissal actually persisted.
    /// </summary>
    [E2EFact]
    public async Task Test_ZB2_FlaggedEvents_ListDetailsDismiss()
    {
        var (guildId, eventId) = SeedFlaggedEvent();
        // Dismiss resolves the reviewer id from the signed-in user's linked Discord account
        // (discord:user_id claim) and refuses the action otherwise - link the seeded SuperAdmin
        // first, the same escape hatch Test_Z4 uses (the web-only host has no real OAuth to link
        // an account through).
        LinkSeededAdminToDiscord();

        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        await page.GotoAsync($"/Guilds/FlaggedEvents/{guildId}");
        var row = page.Locator("[data-testid='flagged-event-row']");
        await Expect(row).ToHaveCountAsync(1);
        await Expect(row).ToContainTextAsync("Pending");

        await row.GetByTitle("View Details").ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex($@"/Guilds/FlaggedEvents/{guildId}/{eventId}$"), new PageAssertionsToHaveURLOptions { Timeout = 20_000 });
        await Expect(page.GetByText("Repeated identical messages posted in quick succession")).ToBeVisibleAsync();

        await page.WaitForTimeoutAsync(1_500);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Dismiss", Exact = true }).ClickAsync();
        await page.Locator("#flaggedEventConfirmModal").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Confirm" }).ClickAsync();

        await Expect(page).ToHaveURLAsync(new Regex($@"/Guilds/FlaggedEvents/{guildId}$"), new PageAssertionsToHaveURLOptions { Timeout = 20_000 });
        await Expect(page.Locator("[data-testid='flagged-event-row']")).ToContainTextAsync("Dismissed");
    }

    /// <summary>
    /// Covers <c>Blazor/Pages/Guilds/RatWatch/Incidents.razor</c> end to end (plan §5 Phase 4,
    /// cluster 4d): the default last-30-days filter shows a seeded watch, the row's "View" opens
    /// the on-demand detail modal (<c>IRatWatchService.GetByIdAsync</c>, not the legacy
    /// <c>?handler=IncidentDetail</c> JSON endpoint), and CSV export downloads a file whose header
    /// row matches the server-built columns.
    /// </summary>
    [E2EFact]
    public async Task Test_ZB3_RatWatchIncidents_FilterModalExport()
    {
        var (guildId, _) = SeedRatWatch();

        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);
        await LoginAsync(page, _host);

        // Wide, explicit date bounds rather than relying on the page's own "last 30 days"
        // default - the raw-SQL-seeded ScheduledAt below is a plain ISO-8601 string, not run
        // through Microsoft.Data.Sqlite's own DateTime parameter conversion, so a narrow
        // default-range comparison against an EF-generated boundary can be format-sensitive.
        await page.GotoAsync($"/Guilds/RatWatch/Incidents/{guildId}?StartDate=2020-01-01&EndDate=2030-01-01");
        var row = page.Locator("[data-testid='ratwatch-incident-row']");
        await Expect(row).ToHaveCountAsync(1);

        // See ZB1's identical comment - a click before the circuit connects is dropped, not queued.
        await page.WaitForTimeoutAsync(1_500);
        await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "View" }).ClickAsync();
        var modal = page.Locator("#incidentDetailModal");
        await Expect(modal).ToBeVisibleAsync();
        await Expect(modal).ToContainTextAsync("Guilty");
        await modal.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Close" }).ClickAsync();

        var downloadTask = page.RunAndWaitForDownloadAsync(async () =>
        {
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Export CSV" }).ClickAsync();
        });
        var download = await downloadTask;

        var path = await download.PathAsync();
        path.Should().NotBeNullOrEmpty();
        var content = await File.ReadAllTextAsync(path!);
        content.Should().Contain("Date,Accused,Initiator,Status,Votes For,Votes Against,Custom Message");
    }

    /// <summary>Seeds one guild and one Pending <c>FlaggedEvents</c> row, for <see cref="Test_ZB2_FlaggedEvents_ListDetailsDismiss"/>.</summary>
    private (ulong GuildId, Guid EventId) SeedFlaggedEvent()
    {
        var salt = (ulong)Random.Shared.NextInt64(1, 1_000_000);
        var guildId = 900000000000000070UL + salt;
        SeedGuild(guildId, "E2E Flagged Events Guild");

        var eventId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        using var connection = new SqliteConnection($"Data Source={_host.DatabasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO FlaggedEvents (Id, GuildId, UserId, ChannelId, RuleType, Severity, Description, Evidence, Status, ActionTaken, ReviewedByUserId, CreatedAt, ReviewedAt)
            VALUES ($id, $guildId, $userId, NULL, 0, 2, $description, '{}', 0, NULL, NULL, $createdAt, NULL)
            """;
        command.Parameters.AddWithValue("$id", eventId);
        command.Parameters.AddWithValue("$guildId", unchecked((long)guildId));
        command.Parameters.AddWithValue("$userId", unchecked((long)(900000000000000071UL + salt)));
        command.Parameters.AddWithValue("$description", "Repeated identical messages posted in quick succession");
        command.Parameters.AddWithValue("$createdAt", now.ToString("O"));
        command.ExecuteNonQuery();

        return (guildId, eventId);
    }

    /// <summary>Seeds one guild and one Guilty <c>RatWatches</c> row, for <see cref="Test_ZB3_RatWatchIncidents_FilterModalExport"/>.</summary>
    private (ulong GuildId, Guid WatchId) SeedRatWatch()
    {
        var salt = (ulong)Random.Shared.NextInt64(1, 1_000_000);
        var guildId = 900000000000000080UL + salt;
        SeedGuild(guildId, "E2E Rat Watch Incidents Guild");

        var watchId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        using var connection = new SqliteConnection($"Data Source={_host.DatabasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO RatWatches (Id, GuildId, ChannelId, AccusedUserId, InitiatorUserId, OriginalMessageId, CustomMessage, ScheduledAt, CreatedAt, Status, NotificationMessageId, VotingMessageId, ClearedAt, VotingStartedAt, VotingEndedAt)
            VALUES ($id, $guildId, $channelId, $accusedUserId, $initiatorUserId, $messageId, $customMessage, $scheduledAt, $createdAt, 3, NULL, NULL, NULL, $votingStartedAt, $votingEndedAt)
            """;
        command.Parameters.AddWithValue("$id", watchId);
        command.Parameters.AddWithValue("$guildId", unchecked((long)guildId));
        command.Parameters.AddWithValue("$channelId", unchecked((long)(900000000000000081UL + salt)));
        command.Parameters.AddWithValue("$accusedUserId", unchecked((long)(900000000000000082UL + salt)));
        command.Parameters.AddWithValue("$initiatorUserId", unchecked((long)(900000000000000083UL + salt)));
        command.Parameters.AddWithValue("$messageId", unchecked((long)(900000000000000084UL + salt)));
        command.Parameters.AddWithValue("$customMessage", "Said they'd be back in 5 minutes");
        command.Parameters.AddWithValue("$scheduledAt", now.AddMinutes(-10).ToString("O"));
        command.Parameters.AddWithValue("$createdAt", now.AddMinutes(-15).ToString("O"));
        command.Parameters.AddWithValue("$votingStartedAt", now.AddMinutes(-5).ToString("O"));
        command.Parameters.AddWithValue("$votingEndedAt", now.ToString("O"));
        command.ExecuteNonQuery();

        return (guildId, watchId);
    }

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

    /// <summary>
    /// Clicks <paramref name="trigger"/> and waits for <paramref name="target"/> to become
    /// visible, retrying the click on a bounded deadline if it doesn't - the same shape as
    /// <see cref="AssertCounterIncrementsAsync"/>, generalized for any interactive-only trigger
    /// gated <c>disabled="@(!RendererInfo.IsInteractive)"</c> (<c>Test_E</c>'s toast/ConfirmModal
    /// demo buttons). Waiting for the trigger to report enabled first (the caller's job - see
    /// <c>Test_E</c>) rules out a click landing in the prerender-only window; this loop covers the
    /// narrower remaining race confirmed empirically against the ConfirmModal button specifically:
    /// a click sent the instant "disabled" is removed can still beat Blazor Server's own
    /// client-side event listener attachment and be silently dropped rather than queued, the same
    /// gap <see cref="AssertCounterIncrementsAsync"/>'s remarks document for the smoke-page
    /// counter. No blind sleep - each attempt's wait is a short, real "did it work" check.
    /// </summary>
    private static async Task ClickUntilVisibleAsync(ILocator trigger, ILocator target)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (true)
        {
            await trigger.ClickAsync();
            try
            {
                await Expect(target).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 1_000 });
                return;
            }
            catch (PlaywrightException) when (DateTime.UtcNow < deadline)
            {
                // Click didn't land (or produced no visible effect yet) - try again.
            }
        }
    }
}
