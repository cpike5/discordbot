namespace DiscordBot.E2E;

/// <summary>
/// A <see cref="FactAttribute"/> that only runs when the <c>E2E_ENABLED</c> environment
/// variable is set to <c>1</c>. Without it, the test is reported as Skipped (not failed), so a
/// plain <c>dotnet test DiscordBot.sln</c> stays green on machines without Chromium - see
/// CLAUDE.md "Build and test" and docs/articles/testing-guide.md "Browser (Playwright) tests".
/// </summary>
public sealed class E2EFactAttribute : FactAttribute
{
    public E2EFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("E2E_ENABLED") != "1")
        {
            Skip = "Browser (Playwright) tests are gated behind E2E_ENABLED=1. " +
                   "See docs/articles/testing-guide.md \"Browser (Playwright) tests\".";
        }
    }
}
