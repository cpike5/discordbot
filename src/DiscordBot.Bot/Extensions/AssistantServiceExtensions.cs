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
using System.Reflection;

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
        //
        // CONTRACT: AssistantOptions supports both nested option groups (e.g. "Assistant:Sampling:MaxTokens")
        // and historical flat legacy keys (e.g. "Assistant:MaxTokens") via [Obsolete] forwarding properties
        // that read/write the same nested objects. Whichever the ConfigurationBinder happens to bind last
        // during the plain Configure<T> call above wins by accident of reflection/property-declaration
        // order — that is NOT a reliable contract. To make "flat key wins when both are present" an explicit,
        // robust guarantee (not an accident of binding order), a PostConfigure step re-applies any flat
        // legacy key that is actually present in the "Assistant" configuration section, overwriting whatever
        // the nested binding produced. See AssistantOptions.cs for the mirrored contract comment, and
        // AssistantOptionsBindingTests for coverage of both the raw-binder and the real DI registration path.
        services.Configure<AssistantOptions>(
            configuration.GetSection(AssistantOptions.SectionName));
        services.PostConfigure<AssistantOptions>(options =>
            ApplyFlatLegacyKeyPrecedence(options, configuration.GetSection(AssistantOptions.SectionName)));
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

        // Shared assistant pipeline pieces (also used by the DM assistant)
        services.AddSingleton<IAssistantRateLimiter, AssistantRateLimiter>();
        services.AddScoped<IAssistantAccessGate, AssistantAccessGate>();
        services.AddScoped<IAssistantTelemetryReader, AssistantTelemetryReader>();

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

            // Register the shared assistant message pipeline (agent invocation, pricing, truncation)
            services.AddScoped<IAssistantMessagePipeline, AssistantMessagePipeline>();

            // Register the guild assistant context factory (builds agent context + logs usage)
            services.AddScoped<IGuildAssistantContextFactory, GuildAssistantContextFactory>();

            // Register the main assistant service
            services.AddScoped<IAssistantService, AssistantService>();
        }

        return services;
    }

    /// <summary>
    /// Re-applies any historical flat legacy key (e.g. "Assistant:MaxTokens") that is present in the
    /// "Assistant" configuration section onto <paramref name="options"/>, overwriting whatever value its
    /// nested equivalent (e.g. "Assistant:Sampling:MaxTokens") bound to. Only keys that are actually present
    /// in configuration are touched, so nested-only deployments are unaffected. Applied via
    /// <c>PostConfigure&lt;AssistantOptions&gt;</c> so it always runs after the plain nested+flat binding,
    /// making "flat wins when both are set" an explicit guarantee rather than an accident of the order in
    /// which <see cref="AssistantOptions"/> declares its properties.
    /// </summary>
    private static void ApplyFlatLegacyKeyPrecedence(AssistantOptions options, IConfigurationSection assistantSection)
    {
        foreach (var property in typeof(AssistantOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<ObsoleteAttribute>() is null)
            {
                continue;
            }

            var flatKeySection = assistantSection.GetSection(property.Name);
            if (!flatKeySection.Exists())
            {
                continue;
            }

            var value = flatKeySection.Get(property.PropertyType);
            property.SetValue(options, value);
        }
    }
}
