using Anthropic;
using Anthropic.Core;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Infrastructure.Services.LLM.Anthropic;
using DiscordBot.Infrastructure.Services.LLM.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering AI assistant services.
/// </summary>
public static class AssistantServiceExtensions
{
    /// <summary>
    /// Adds AI assistant services including LLM client, agent runner, and tool registry.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAssistant(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<AnthropicOptions>(
            configuration.GetSection(AnthropicOptions.SectionName));

        // Get API key from configuration
        var apiKey = configuration.GetValue<string>("Anthropic:ApiKey");

        // Only register Anthropic services if API key is configured
        if (!string.IsNullOrEmpty(apiKey))
        {
            // Register Anthropic client as singleton (thread-safe, expensive to create)
            // The SDK reads from ANTHROPIC_API_KEY environment variable by default,
            // but we can pass options to configure it explicitly
            services.AddSingleton<AnthropicClient>(sp =>
            {
                var clientOptions = new ClientOptions { ApiKey = apiKey };
                return new AnthropicClient(clientOptions);
            });

            // Register LLM client implementation
            services.AddSingleton<ILlmClient, AnthropicLlmClient>();
        }

        // Register tool registry as singleton (manages tool providers)
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        // Register built-in tool providers
        services.AddSingleton<IToolProvider, DocumentationToolProvider>();

        // Register agent runner (depends on ILlmClient and ILogger)
        services.AddScoped<IAgentRunner, AgentRunner>();

        return services;
    }
}
