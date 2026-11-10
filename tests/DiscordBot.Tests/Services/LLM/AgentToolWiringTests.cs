using cpike.Agent;
using cpike.Agent.Prompting;
using DiscordBot.Bot.Extensions;
using DiscordBot.Infrastructure.Abstractions.LLM;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Infrastructure.Services.LLM.Tools;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Asserts the real DI wiring of the assistant on the cpike.Agent framework: what the two assistant
/// registrations put in the container, and that the prompt store and loop actually resolve from it.
/// </summary>
/// <remarks>
/// The registrations are the production extension methods (<c>AddAssistant</c>, <c>AddDmAssistant</c>)
/// run over a bare <see cref="ServiceCollection"/>. The framework services are resolved for real; the
/// tools are checked at descriptor level here (they want a live database and Discord client), and
/// resolved for real by <see cref="AgentToolResolutionTests"/> inside the booted application.
/// </remarks>
public class AgentToolWiringTests
{
    private static readonly Type[] MemoryTools =
    [
        typeof(SaveNoteTool),
        typeof(SearchNotesTool),
        typeof(GetNoteTool),
        typeof(ListNotesTool),
        typeof(DeleteNoteTool)
    ];

    private static IConfiguration Configuration(bool withApiKey = true)
    {
        var data = new Dictionary<string, string?>();
        if (withApiKey)
        {
            data["OpenRouter:ApiKey"] = "test-key";
        }

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    private static ServiceCollection Registered(bool withApiKey = true)
    {
        var configuration = Configuration(withApiKey);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(configuration);
        services.AddAssistant(configuration);
        services.AddDmAssistant(configuration);
        return services;
    }

    private static List<Type?> Implementations(IServiceCollection services, Type serviceType) =>
        services.Where(d => d.ServiceType == serviceType).Select(d => d.ImplementationType).ToList();

    [Fact]
    public void WithoutAnApiKey_NoToolAndNoLoopIsRegistered()
    {
        // Migrations run without a key, and nothing that calls a model may be in the container then.
        var services = Registered(withApiKey: false);

        services.Should().NotContain(d => d.ServiceType == typeof(IAgentTool));
        services.Should().NotContain(d => d.ServiceType == typeof(AgentService));
        services.Should().NotContain(d => d.ServiceType == typeof(IAgentPromptStore));
    }

    [Fact]
    public void EveryHouseToolIsRegisteredExactlyOnce()
    {
        // Both assistant registrations run in Program.cs; the scan and TryAddEnumerable are what keep
        // that from advertising every tool twice.
        var registered = Implementations(Registered(), typeof(IAgentTool));

        foreach (var tool in RegisteredAgentTools.Types)
        {
            registered.Count(t => t == tool).Should().Be(1, $"{tool.Name} must be registered once");
        }

        registered.Should().Contain(MemoryTools);
    }

    [Fact]
    public void EveryToolIsRegisteredScoped()
    {
        // Tools depend on repositories and on the run's DiscordRunContext, which are scoped; a
        // singleton tool would capture one run's caller for every run after it.
        Registered().Where(d => d.ServiceType == typeof(IAgentTool))
            .Should().OnlyContain(d => d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void TheFrameworkSkillLoaderIsRegisteredOnce_AndNoHouseToolTakesItsName()
    {
        // The engine had a load_skill of its own; the framework's is the only one now. A second tool
        // answering to that name would be resolved by registration order.
        var registered = Implementations(Registered(), typeof(IAgentTool));

        registered.Count(t => t == typeof(cpike.Agent.Tools.LoadSkillTool)).Should().Be(1);
        registered.Should().OnlyHaveUniqueItems();
        RegisteredAgentTools.All.Select(t => t.Name).Should().OnlyHaveUniqueItems()
            .And.NotContain("load_skill");
    }

    [Fact]
    public void ThePipelineIsRegisteredOnce()
    {
        Implementations(Registered(), typeof(IAssistantMessagePipeline))
            .Should().ContainSingle().Which.Should().Be(typeof(AssistantMessagePipeline));
    }

    [Fact]
    public void AgentServiceIsRegisteredScoped()
    {
        // Resolved for real in AgentToolResolutionTests: it takes every tool, and the tools want the
        // application's database and Discord client.
        Registered().Should().ContainSingle(d => d.ServiceType == typeof(AgentService))
            .Which.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void ThePromptStoreIsTheCatalogStoreWithEverySurfaceMode()
    {
        using var provider = Registered().BuildServiceProvider();

        var store = provider.GetRequiredService<IAgentPromptStore>();

        store.Should().BeOfType<CatalogPromptStore>();
        store.GetDefault().Key.Should().Be("bot", "every run layers its mode on the base persona");
        store.TryGet(GuildAssistantContext.ModeName, out _).Should().BeTrue();
        store.TryGet(DmAssistantContext.ModeName, out _).Should().BeTrue();
        store.TryGet(CatalogPromptStore.FeatureRequestModeName, out _).Should().BeTrue();
    }
}

/// <summary>
/// Every house tool resolved for real, from the booted application with an API key configured: the
/// check that no tool depends on a service only some host registers.
/// </summary>
[Collection(OfflineAppHostCollection.Name)]
public class AgentToolResolutionTests
{
    [Fact]
    public async Task EveryHouseToolResolvesExactlyOnceFromTheApplication()
    {
        await using var host = await OfflineAppHost.StartAsync(
            settings: new Dictionary<string, string> { ["OpenRouter:ApiKey"] = "test-key" });

        using var scope = host.Services.CreateScope();
        var tools = scope.ServiceProvider.GetServices<IAgentTool>().ToList();

        foreach (var type in RegisteredAgentTools.Types)
        {
            tools.Count(t => t.GetType() == type).Should().Be(1, $"{type.Name} must resolve once");
        }

        tools.Select(t => t.Definition.Name).Should().OnlyHaveUniqueItems();
        scope.ServiceProvider.GetRequiredService<AgentService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IAssistantMessagePipeline>().Should().BeOfType<AssistantMessagePipeline>();
    }
}
