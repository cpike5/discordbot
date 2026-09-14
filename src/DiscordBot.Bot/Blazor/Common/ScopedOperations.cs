using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Blazor.Common;

/// <summary>
/// Per-operation-scope helpers for interactive Blazor pages (docs/plans/blazor-port-plan.md §4.1
/// "Data access in components": services and <c>BotDbContext</c> are scoped, and a circuit is one
/// scope, so a page that injects a service directly holds the same instance - and the same
/// <c>DbContext</c>, with everything it has ever tracked - for the circuit's entire life. A
/// mutation handler resolves its service through <see cref="IServiceScopeFactory"/> instead, via
/// the extension methods here, so it gets a scope (and a <c>DbContext</c>) that starts empty and
/// is thrown away the moment the call returns - see docs/architecture/patterns.md "Blazor
/// Components" § Per-operation scopes and
/// docs/lessons-learned/scheduled-message-repeated-update-tracking.md for why the circuit-lived
/// alternative doesn't work.
/// </summary>
public static class ScopedOperations
{
    /// <summary>
    /// Runs <paramref name="action"/> against a <typeparamref name="TService"/> resolved from a
    /// fresh scope, disposing that scope (and therefore its <c>DbContext</c>, if any) as soon as
    /// <paramref name="action"/> completes or throws.
    /// </summary>
    public static async Task RunAsync<TService>(this IServiceScopeFactory scopeFactory, Func<TService, Task> action)
        where TService : notnull
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<TService>();
        await action(service);
    }

    /// <summary>Same as <see cref="RunAsync{TService}(IServiceScopeFactory, Func{TService, Task})"/>, returning a result.</summary>
    public static async Task<TResult> RunAsync<TService, TResult>(this IServiceScopeFactory scopeFactory, Func<TService, Task<TResult>> func)
        where TService : notnull
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<TService>();
        return await func(service);
    }

    /// <summary>
    /// Same as <see cref="RunAsync{TService}(IServiceScopeFactory, Func{TService, Task})"/> but for
    /// a handler that needs two services from the same scope (e.g. two repositories, or a
    /// repository plus an audit service) - resolving both from one scope instead of nesting two
    /// separate <see cref="RunAsync{TService}(IServiceScopeFactory, Func{TService, Task})"/> calls
    /// keeps them on the same <c>DbContext</c> for that one operation.
    /// </summary>
    public static async Task RunAsync<TService1, TService2>(this IServiceScopeFactory scopeFactory, Func<TService1, TService2, Task> action)
        where TService1 : notnull
        where TService2 : notnull
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service1 = scope.ServiceProvider.GetRequiredService<TService1>();
        var service2 = scope.ServiceProvider.GetRequiredService<TService2>();
        await action(service1, service2);
    }

    /// <summary>Two-service overload of <see cref="RunAsync{TService, TResult}(IServiceScopeFactory, Func{TService, Task{TResult}})"/>.</summary>
    public static async Task<TResult> RunAsync<TService1, TService2, TResult>(this IServiceScopeFactory scopeFactory, Func<TService1, TService2, Task<TResult>> func)
        where TService1 : notnull
        where TService2 : notnull
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service1 = scope.ServiceProvider.GetRequiredService<TService1>();
        var service2 = scope.ServiceProvider.GetRequiredService<TService2>();
        return await func(service1, service2);
    }
}
