using DiscordBot.Bot.Blazor.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Tests.Bot.Blazor.Common;

/// <summary>
/// Unit tests for <see cref="ScopedOperations"/> - the per-operation-scope helpers interactive
/// Blazor pages use for every mutation and post-mutation reload instead of the circuit-scoped
/// service they were injected with (docs/plans/blazor-port-plan.md §4.1 "Data access in
/// components", docs/architecture/patterns.md "Blazor Components" § Per-operation scopes).
/// </summary>
public class ScopedOperationsTests
{
    private interface ICounter
    {
        int InstanceId { get; }

        Task<int> GetValueAsync();
    }

    /// <summary>Scoped so each <see cref="IServiceScopeFactory.CreateAsyncScope"/> hands back a distinct instance - the same lifetime a real repository/service has.</summary>
    private sealed class Counter : ICounter, IDisposable
    {
        private static int _nextInstanceId;

        public Counter() => InstanceId = System.Threading.Interlocked.Increment(ref _nextInstanceId);

        public int InstanceId { get; }

        public bool Disposed { get; private set; }

        public Task<int> GetValueAsync() => Task.FromResult(InstanceId);

        public void Dispose() => Disposed = true;
    }

    private interface IOther
    {
        string Name { get; }
    }

    private sealed class Other : IOther
    {
        public string Name => "other";
    }

    private static IServiceScopeFactory BuildScopeFactory(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<ICounter, Counter>();
        services.AddScoped<IOther, Other>();
        configure?.Invoke(services);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    [Fact]
    public async Task RunAsync_Action_ResolvesFromAFreshScopeAndDisposesIt()
    {
        var services = new ServiceCollection();
        services.AddScoped<ICounter, Counter>();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        Counter? captured = null;
        await scopeFactory.RunAsync<ICounter>(counter =>
        {
            captured = (Counter)counter;
            return Task.CompletedTask;
        });

        captured.Should().NotBeNull();
        captured!.Disposed.Should().BeTrue("the scope created for the call should be disposed once the action completes");
    }

    [Fact]
    public async Task RunAsync_Action_EachCallUsesItsOwnScope()
    {
        var scopeFactory = BuildScopeFactory();
        var instanceIds = new List<int>();

        await scopeFactory.RunAsync<ICounter>(async counter => instanceIds.Add(await counter.GetValueAsync()));
        await scopeFactory.RunAsync<ICounter>(async counter => instanceIds.Add(await counter.GetValueAsync()));

        instanceIds.Should().HaveCount(2);
        instanceIds[0].Should().NotBe(instanceIds[1], "two separate calls should each get a fresh scoped instance, not the same one held across calls");
    }

    [Fact]
    public async Task RunAsync_Func_ReturnsTheResultAndResolvesFromAFreshScope()
    {
        var scopeFactory = BuildScopeFactory();

        var value = await scopeFactory.RunAsync<ICounter, int>(counter => counter.GetValueAsync());

        value.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RunAsync_Action_PropagatesExceptions()
    {
        var scopeFactory = BuildScopeFactory();

        var act = () => scopeFactory.RunAsync<ICounter>(_ => throw new InvalidOperationException("boom"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public async Task RunAsync_Func_PropagatesExceptions()
    {
        var scopeFactory = BuildScopeFactory();

        var act = () => scopeFactory.RunAsync<ICounter, int>(_ => throw new InvalidOperationException("boom"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public async Task RunAsync_TwoServiceAction_ResolvesBothFromTheSameScope()
    {
        var scopeFactory = BuildScopeFactory();

        ICounter? resolvedCounter = null;
        IOther? resolvedOther = null;
        await scopeFactory.RunAsync<ICounter, IOther>((counter, other) =>
        {
            resolvedCounter = counter;
            resolvedOther = other;
            return Task.CompletedTask;
        });

        resolvedCounter.Should().NotBeNull();
        resolvedOther.Should().NotBeNull();
        resolvedOther!.Name.Should().Be("other");
    }

    [Fact]
    public async Task RunAsync_TwoServiceFunc_ReturnsTheResult()
    {
        var scopeFactory = BuildScopeFactory();

        var result = await scopeFactory.RunAsync<ICounter, IOther, string>((counter, other) => Task.FromResult($"{other.Name}-{counter.InstanceId}"));

        result.Should().StartWith("other-");
    }
}
