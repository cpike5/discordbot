using Anthropic;
using Anthropic.Core;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Infrastructure.Services;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Infrastructure.Services.LLM.Anthropic;
using DiscordBot.Infrastructure.Services.LLM.Providers;
using DiscordBot.Bot.Services.LLM.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering AI assistant services.
/// </summary>
public static class AssistantServiceExtensions
{
    /// <summary>
    /// Adds AI assistant services including LLM client, agent runner, tool registry, and assistant service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAssistant(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<AssistantOptions>(
            configuration.GetSection(AssistantOptions.SectionName));
        services.Configure<AnthropicOptions>(
            configuration.GetSection(AnthropicOptions.SectionName));

        // Get API key from configuration
        var apiKey = configuration.GetValue<string>("Anthropic:ApiKey");

        // Register assistant repositories (always needed for settings management)
        services.AddScoped<IAssistantUsageMetricsRepository, AssistantUsageMetricsRepository>();
        services.AddScoped<IAssistantInteractionLogRepository, AssistantInteractionLogRepository>();
        services.AddScoped<IAssistantGuildSettingsRepository, AssistantGuildSettingsRepository>();
        services.AddScoped<AssistantGuildSettingsRepository>();

        // Register assistant guild settings service (always needed for admin UI)
        services.AddScoped<IAssistantGuildSettingsService, AssistantGuildSettingsService>();

        // Only register LLM-dependent services if API key is configured
        // This prevents DI validation failures when running migrations without API key
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

            // Register prompt template service
            services.AddSingleton<IPromptTemplate, PromptTemplate>();

            // Register built-in tool providers (scoped to support scoped dependencies like ICommandMetadataService)
            services.AddScoped<IToolProvider, DocumentationToolProvider>();
            services.AddScoped<IToolProvider, UserGuildInfoToolProvider>();
            services.AddScoped<IToolProvider, RatWatchToolProvider>();

            // Register tool registry as scoped (auto-registers injected IToolProvider instances)
            services.AddScoped<IToolRegistry, ToolRegistry>();

            // Register agent runner (depends on ILlmClient and ILogger)
            services.AddScoped<IAgentRunner, AgentRunner>();

            // Register the main assistant service
            services.AddScoped<IAssistantService, AssistantService>();
        }

        return services;
    }
}
