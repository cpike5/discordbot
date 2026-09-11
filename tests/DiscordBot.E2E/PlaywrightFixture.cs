using Microsoft.Playwright;

namespace DiscordBot.E2E;

/// <summary>
/// Collection fixture that launches one headless Chromium instance shared by every test in
/// <see cref="E2ECollection"/>; each test opens its own <see cref="IBrowserContext"/> off of it
/// so sessions (cookies, storage) never leak between tests.
///
/// Chromium's executable is resolved, in order: <c>E2E_CHROMIUM_PATH</c> (explicit override);
/// <c>PLAYWRIGHT_BROWSERS_PATH</c>/chromium (the layout this repo's remote sessions pre-install
/// Chromium under, e.g. /opt/pw-browsers/chromium - see the Playwright skill notes in this
/// environment); otherwise Playwright's own default resolution (a browser installed the normal
/// way via the `playwright install` CLI, e.g. in CI after `playwright.ps1 install chromium`).
///
/// Does nothing when <c>E2E_ENABLED</c> is not "1", matching <see cref="BotHostFixture"/> - see
/// its remarks for why an inert fixture matters even though every test is Skip-marked.
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;

    public IBrowser? Browser { get; private set; }

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("E2E_ENABLED") != "1")
        {
            return;
        }

        _playwright = await Microsoft.Playwright.Playwright.CreateAsync().ConfigureAwait(false);

        var launchOptions = new BrowserTypeLaunchOptions { Headless = true };

        var executablePath = ResolveChromiumExecutablePath();
        if (executablePath is not null)
        {
            launchOptions.ExecutablePath = executablePath;
        }

        Browser = await _playwright.Chromium.LaunchAsync(launchOptions).ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync().ConfigureAwait(false);
        }

        _playwright?.Dispose();
    }

    private static string? ResolveChromiumExecutablePath()
    {
        var explicitPath = Environment.GetEnvironmentVariable("E2E_CHROMIUM_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        var browsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        if (!string.IsNullOrWhiteSpace(browsersPath))
        {
            var candidate = Path.Combine(browsersPath, "chromium");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // Let Playwright resolve its own pinned-version default (the normal path when
        // `playwright install chromium` has populated ~/.cache/ms-playwright).
        return null;
    }
}
