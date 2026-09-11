using DiscordBot.Agents.Abstractions;
using DiscordBot.Bot.Extensions;
using DiscordBot.Infrastructure.Abstractions.LLM;
using DiscordBot.Infrastructure.Services.LLM.Providers;
using DiscordBot.Infrastructure.Services.LLM.Tools;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Asserts the real DI wiring for individually authored tools: that the scan runs from both
/// assistant registrations, that each surface gets its adapter, and that a tool is registered once
/// however many times the scan is called.
/// </summary>
/// <remarks>
/// Descriptor-level assertions on purpose. Resolving these would want a live <c>DbContext</c> and a
/// Discord client; what can actually go wrong here is the registration, so that is what is checked.
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

    private static IConfiguration Configuration(bool withApiKey = false)
    {
        var data = new Dictionary<string, string?>();
        if (withApiKey)
        {
            data["OpenRouter:ApiKey"] = "test-key";
        }

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    private static List<Type?> Implementations(IServiceCollection services, Type serviceType) =>
        services.Where(d => d.ServiceType == serviceType).Select(d => d.ImplementationType).ToList();

    [Fact]
    public void AddAssistant_ScansTheToolsEvenWithNoApiKey()
    {
        // A tool object costs nothing to register, and migrations run without a key.
        var services = new ServiceCollection();

        services.AddAssistant(Configuration());

        Implementations(services, typeof(IAgentTool)).Should().Contain(MemoryTools);
    }

    [Fact]
    public void AddDmAssistant_ScansTheToolsAndRegistersTheDmAdapter()
    {
        var services = new ServiceCollection();

        services.AddDmAssistant(Configuration());

        Implementations(services, typeof(IAgentTool)).Should().Contain(MemoryTools);
        Implementations(services, typeof(IDmToolProvider)).Should().Contain(typeof(DmAgentToolProvider));
    }

    [Fact]
    public void AddDmAssistant_NoLongerRegistersAProviderOfItsOwnForTheMemoryTools()
    {
        // The conversion's point: five tools, no provider, no DI line per tool.
        var services = new ServiceCollection();

        services.AddDmAssistant(Configuration());

        Implementations(services, typeof(IDmToolProvider))
            .Should().NotContain(t => t!.Name.Contains("Memory", StringComparison.Ordinal));
    }

    [Fact]
    public void AddAssistant_RegistersTheGuildAdapterOnceTheApiKeyIsPresent()
    {
        var services = new ServiceCollection();

        services.AddAssistant(Configuration(withApiKey: true));

        Implementations(services, typeof(IToolProvider)).Should().Contain(typeof(GuildAgentToolProvider));
    }

    [Fact]
    public void TheTwoRegistrationsTogetherRegisterEachToolExactlyOnce()
    {
        // Both call AddAgentTools over the same assemblies; TryAddEnumerable is what keeps that from
        // advertising every tool twice.
        var services = new ServiceCollection();

        services.AddAssistant(Configuration(withApiKey: true));
        services.AddDmAssistant(Configuration(withApiKey: true));

        var registered = Implementations(services, typeof(IAgentTool));

        foreach (var tool in MemoryTools)
        {
            registered.Count(t => t == tool).Should().Be(1, $"{tool.Name} must be registered once");
        }
    }

    [Fact]
    public void EveryScannedToolIsRegisteredScoped()
    {
        // Tools depend on repositories, which are scoped; a singleton tool would capture one.
        var services = new ServiceCollection();

        services.AddDmAssistant(Configuration());

        services.Where(d => d.ServiceType == typeof(IAgentTool))
            .Should().OnlyContain(d => d.Lifetime == ServiceLifetime.Scoped);
    }
}
