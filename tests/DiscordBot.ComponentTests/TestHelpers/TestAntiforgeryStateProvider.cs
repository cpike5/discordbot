using Microsoft.AspNetCore.Components.Forms;

namespace DiscordBot.ComponentTests.TestHelpers;

/// <summary>
/// Minimal <see cref="AntiforgeryStateProvider"/> for tests that render <c>&lt;AntiforgeryToken /&gt;</c>
/// (<c>MainNavbarTests</c>, <c>MainLayoutTests</c>). The real host gets an equivalent
/// registration from <c>AddRazorComponents()</c> via the framework's own (internal, so not usable
/// directly here) <c>DefaultAntiforgeryStateProvider</c>; this returns a fixed token rather than a
/// real ASP.NET Core antiforgery token since no request/response exists in a bUnit test.
/// </summary>
public sealed class TestAntiforgeryStateProvider : AntiforgeryStateProvider
{
    public override AntiforgeryRequestToken GetAntiforgeryToken()
        => new("test-antiforgery-token-value", "__RequestVerificationToken");
}
