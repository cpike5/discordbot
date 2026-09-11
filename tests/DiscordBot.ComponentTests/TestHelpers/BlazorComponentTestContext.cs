using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Services.Realtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordBot.ComponentTests.TestHelpers;

/// <summary>
/// Shared base for every bUnit test in this project. Registers the same Blazor UI-state and
/// interop services <c>AddBlazorWeb</c> wires into the real host (see "Blazor components" in
/// <c>docs/architecture/patterns.md</c>), backed by a real <see cref="DashboardEventBus"/> and
/// bUnit's fake <see cref="Microsoft.JSInterop.IJSRuntime"/>, so a rendered component sees the
/// same DI shape it would inside a circuit.
/// </summary>
/// <remarks>
/// <para>
/// Inherits <see cref="Bunit.BunitContext"/> rather than the older <c>Bunit.TestContext"</c> -
/// bUnit 2.10 marks <c>TestContext</c> obsolete in favour of <c>BunitContext</c> (a rename, same
/// shape), which is what this project targets.
/// </para>
/// <para>
/// <b>Auth is opt-in per test.</b> Call <c>this.AddAuthorization()</c> (a bUnit extension
/// method - note the name: bUnit renamed the old <c>AddTestAuthorization()</c> to
/// <c>AddAuthorization()</c> in v2) and configure it with <c>SetAuthorized(name)</c> /
/// <c>SetRoles(...)</c> / <c>SetNotAuthorized()</c> before rendering a component that reads
/// <c>[CascadingParameter] Task&lt;AuthenticationState&gt;</c>; bUnit supplies that cascading
/// value automatically once authorization is configured, no explicit
/// <c>AddCascadingAuthenticationState()</c> needed.
/// </para>
/// <para>
/// <b><see cref="Bunit.BunitJSInterop"/> defaults to <see cref="JSRuntimeMode.Loose"/></b>: an
/// unconfigured JS call (including a dynamic <c>import</c>) returns a default value instead of
/// throwing, which is enough for every interop call this codebase's Phase 1 components make in
/// <c>OnAfterRenderAsync</c> without per-test setup. Use <c>JSInterop.SetupModule(...)</c> /
/// <c>Setup&lt;T&gt;(...)</c> in a specific test to assert exactly which interop calls a
/// component made (see <see cref="Bunit.JSRuntimeMode.Strict"/> semantics still being available
/// per-handler even in loose mode).
/// </para>
/// </remarks>
public abstract class BlazorComponentTestContext : BunitContext, Xunit.IAsyncLifetime
{
    protected BlazorComponentTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Same registrations AddBlazorWeb performs for the real host (Extensions/BlazorServiceExtensions.cs),
        // minus the ASP.NET Core hosting/circuit pieces bUnit doesn't need.
        Services.AddBlazorUiServices();
        Services.AddBlazorInterop();
        Services.AddScoped<CircuitClientInfoService>();

        // Real bus, not a mock: subscribe/publish/debounce behavior is exactly what the event-bus
        // tests need to exercise, and DashboardEventBus has no dependency other than a logger.
        Services.AddSingleton<IDashboardEventBus>(new DashboardEventBus(NullLogger<DashboardEventBus>.Instance));
    }

    /// <summary>
    /// Configures bUnit's fake auth (<c>this.AddAuthorization()</c>) as a signed-in admin, which
    /// makes <c>[CascadingParameter] Task&lt;AuthenticationState&gt;</c> resolve to that identity
    /// in any component rendered afterwards - equivalent to what <c>RequireAdmin</c> lets through
    /// in the real app (<c>SuperAdmin</c> or <c>Admin</c>; tests use <c>Admin</c>).
    /// </summary>
    protected BunitAuthorizationContext AddAuthorizedAdmin(string userName = "admin")
        => AddAuthorization().SetAuthorized(userName).SetRoles("Admin");

    // xUnit v2 support: ChartInterop/BrowserInterop implement only IAsyncDisposable (see
    // docs/articles/blazor-interop.md, "Disposal"). The DI container's plain, synchronous
    // Dispose() - which is what xUnit calls by default at the end of a test - throws
    // InvalidOperationException the moment it has to dispose a scoped instance of either, because
    // .NET's container refuses to dispose an IAsyncDisposable-only service synchronously.
    // BunitContext's own Dispose()/DisposeAsync() are both sealed, so this can't be fixed by
    // overriding them; instead, implementing Xunit.IAsyncLifetime here makes xUnit await
    // BunitContext's own DisposeAsync() (which disposes the container correctly) before it ever
    // reaches the synchronous path, which then finds the container already torn down and no-ops.
    Task Xunit.IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    Task Xunit.IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();
}
