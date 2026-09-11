using System.Text.RegularExpressions;
using FluentAssertions;
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
        var status = page.GetByRole(AriaRole.Status);
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
