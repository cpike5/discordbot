using System.Text.Json;
using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Tests.Agents;

/// <summary>
/// Unit tests for <see cref="AgentToolRegistration.AddAgentTools"/> — the scan that makes adding a
/// tool "add a file" rather than "add a file and remember the DI line".
/// </summary>
public class AgentToolRegistrationTests
{
    /// <summary>A minimal scannable tool. Public, so it is a realistic scan target.</summary>
    public abstract class ScanTool : IAgentTool
    {
        public LlmToolDefinition Definition => new()
        {
            Name = GetType().Name,
            Description = "A scannable tool.",
            InputSchema = ToolInput.ObjectSchema(new { })
        };

        public Task<ToolExecutionResult> InvokeAsync(
            JsonElement input, ToolContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(ToolResults.Json(new { ok = true }));
    }

    /// <summary>Always registered.</summary>
    public sealed class ScanToolAlpha : ScanTool;

    /// <summary>Always registered, and sorts after <see cref="ScanToolAlpha"/> by full type name.</summary>
    public sealed class ScanToolBeta : ScanTool;

    /// <summary>Registered only when its flag is set.</summary>
    [OptInTool("Testing:DarkTool:Enabled")]
    public sealed class ScanToolDark : ScanTool;

    /// <summary>Not registered: it cannot be constructed.</summary>
    public sealed class ScanToolGeneric<T> : ScanTool;

    private static readonly System.Reflection.Assembly TestAssembly = typeof(ScanToolAlpha).Assembly;

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static List<Type> RegisteredToolTypes(IServiceCollection services) =>
        services
            .Where(d => d.ServiceType == typeof(IAgentTool))
            .Select(d => d.ImplementationType!)
            .ToList();

    [Fact]
    public void AddAgentTools_RegistersEveryConcreteToolAsScoped()
    {
        var services = new ServiceCollection();

        services.AddAgentTools(new[] { TestAssembly });

        var descriptors = services.Where(d => d.ServiceType == typeof(IAgentTool)).ToList();
        descriptors.Should().NotBeEmpty();
        descriptors.Should().OnlyContain(d => d.Lifetime == ServiceLifetime.Scoped);
        descriptors.Select(d => d.ImplementationType)
            .Should().Contain(new[] { typeof(ScanToolAlpha), typeof(ScanToolBeta) });
    }

    [Fact]
    public void AddAgentTools_SkipsWhatItCannotConstruct()
    {
        var services = new ServiceCollection();

        services.AddAgentTools(new[] { TestAssembly });

        var types = RegisteredToolTypes(services);
        types.Should().NotContain(typeof(ScanTool));
        types.Should().NotContain(t => t.Name.StartsWith("ScanToolGeneric", StringComparison.Ordinal));
    }

    [Fact]
    public void AddAgentTools_IsIdempotent_SoEachSurfaceCanCallIt()
    {
        // Both AddAssistant and AddDmAssistant call it; neither may depend on the other running.
        var services = new ServiceCollection();

        var first = services.AddAgentTools(new[] { TestAssembly });
        services.AddAgentTools(new[] { TestAssembly });

        first.Should().BeGreaterThan(0);
        RegisteredToolTypes(services).Count(t => t == typeof(ScanToolAlpha)).Should().Be(1);
    }

    [Fact]
    public void AddAgentTools_IgnoresADuplicateAssembly()
    {
        var services = new ServiceCollection();

        services.AddAgentTools(new[] { TestAssembly, TestAssembly });

        RegisteredToolTypes(services).Count(t => t == typeof(ScanToolAlpha)).Should().Be(1);
    }

    [Fact]
    public void AddAgentTools_RegistersInFullTypeNameOrder()
    {
        var services = new ServiceCollection();

        services.AddAgentTools(new[] { TestAssembly });

        var types = RegisteredToolTypes(services);
        types.IndexOf(typeof(ScanToolAlpha)).Should().BeLessThan(types.IndexOf(typeof(ScanToolBeta)));
    }

    [Fact]
    public void AddAgentTools_LeavesAnOptInToolDark_WhenThereIsNoConfiguration()
    {
        var services = new ServiceCollection();

        services.AddAgentTools(new[] { TestAssembly });

        RegisteredToolTypes(services).Should().NotContain(typeof(ScanToolDark));
    }

    [Fact]
    public void AddAgentTools_LeavesAnOptInToolDark_WhenItsFlagIsFalse()
    {
        var services = new ServiceCollection();

        services.AddAgentTools(new[] { TestAssembly }, Configuration(("Testing:DarkTool:Enabled", "false")));

        RegisteredToolTypes(services).Should().NotContain(typeof(ScanToolDark));
    }

    [Fact]
    public void AddAgentTools_RegistersAnOptInTool_WhenItsFlagIsSet()
    {
        var services = new ServiceCollection();

        services.AddAgentTools(new[] { TestAssembly }, Configuration(("Testing:DarkTool:Enabled", "true")));

        RegisteredToolTypes(services).Should().Contain(typeof(ScanToolDark));
    }

    [Fact]
    public void AddAgentTools_ReturnsTheNumberItRegistered()
    {
        var services = new ServiceCollection();

        var withoutDark = services.AddAgentTools(new[] { TestAssembly });

        var withDarkServices = new ServiceCollection();
        var withDark = withDarkServices.AddAgentTools(
            new[] { TestAssembly }, Configuration(("Testing:DarkTool:Enabled", "true")));

        withDark.Should().Be(withoutDark + 1);
    }

    [Fact]
    public void AddAgentTools_ResolvesTheToolsItRegistered()
    {
        // The registration has to survive container validation, not just look right in the
        // collection - which also means every IAgentTool in this assembly has to be constructible
        // with no arguments, because resolving the enumerable constructs all of them.
        var services = new ServiceCollection();
        services.AddAgentTools(new[] { TestAssembly });

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IEnumerable<IAgentTool>>()
            .Should().Contain(t => t is ScanToolAlpha);
    }
}
