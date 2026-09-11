using Microsoft.Playwright;

namespace DiscordBot.E2E;

// System.Linq (OrderByDescending/FirstOrDefault below) comes in via implicit usings
// (<ImplicitUsings>enable</ImplicitUsings> in DiscordBot.E2E.csproj).

/// <summary>
/// Collection fixture that launches one headless Chromium instance shared by every test in
/// <see cref="E2ECollection"/>; each test opens its own <see cref="IBrowserContext"/> off of it
/// so sessions (cookies, storage) never leak between tests.
///
/// Chromium's executable is resolved, in order: <c>E2E_CHROMIUM_PATH</c> (explicit override);
/// then, under <c>PLAYWRIGHT_BROWSERS_PATH</c>, a top-level "chromium" entry if one exists (this
/// repo's remote sessions lay that out as a symlink straight to the executable, e.g.
/// /opt/pw-browsers/chromium - see the Playwright skill notes in this environment) or else the
/// newest "chromium-&lt;build&gt;/chrome-linux/chrome" (or headless_shell) directory, the layout a
/// plain `playwright install chromium` produces; otherwise Playwright's own default resolution
/// (e.g. in CI after `playwright.ps1 install chromium --with-deps`). See
/// <see cref="FindChromiumExecutable"/> for the exact rules.
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
        if (string.IsNullOrWhiteSpace(browsersPath) || !Directory.Exists(browsersPath))
        {
            // Let Playwright resolve its own pinned-version default (the normal path when
            // `playwright install chromium` has populated ~/.cache/ms-playwright).
            return null;
        }

        return FindChromiumExecutable(browsersPath);
    }

    /// <summary>
    /// Resolves a Chromium executable somewhere under <paramref name="browsersPath"/>. Handles
    /// two layouts:
    /// - This repo's remote sessions pre-install Chromium at /opt/pw-browsers with a top-level
    ///   "chromium" convenience symlink straight to the executable (not a directory - a plain
    ///   `File.Exists(Path.Combine(browsersPath, "chromium"))` check, which .NET resolves through
    ///   symlinks, is enough here).
    /// - A plain Playwright browser cache (e.g. after `playwright install chromium`, as CI's own
    ///   `e2e` job does) has no such top-level "chromium" entry at all, only a versioned
    ///   "chromium-&lt;build&gt;/chrome-linux/chrome" (or headless_shell) directory - so fall back to
    ///   the newest matching "chromium-*" directory and look inside it.
    /// </summary>
    private static string? FindChromiumExecutable(string browsersPath)
    {
        var topLevelCandidate = Path.Combine(browsersPath, "chromium");
        if (File.Exists(topLevelCandidate))
        {
            return topLevelCandidate;
        }

        if (!Directory.Exists(topLevelCandidate))
        {
            // No plain "chromium" entry at all (neither a file nor a directory) - fall back to
            // the versioned directory layout `playwright install` produces.
            var versionedDir = Directory.GetDirectories(browsersPath, "chromium-*")
                .OrderByDescending(d => d, StringComparer.Ordinal)
                .FirstOrDefault();

            return versionedDir is null ? null : FindExecutableUnder(versionedDir);
        }

        // "chromium" exists but is a directory (not this environment's symlink-to-file
        // convenience layout) - the executable lives inside it.
        return FindExecutableUnder(topLevelCandidate);
    }

    private static string? FindExecutableUnder(string directory)
    {
        foreach (var name in new[] { "chrome", "headless_shell" })
        {
            var candidate = Path.Combine(directory, "chrome-linux", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
