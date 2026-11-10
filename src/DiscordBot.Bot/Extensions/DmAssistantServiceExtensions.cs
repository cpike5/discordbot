using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Infrastructure.Services;
using DiscordBot.Infrastructure.Services.LLM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DiscordBot.Infrastructure.Abstractions.LLM;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering DM assistant services.
/// </summary>
public static class DmAssistantServiceExtensions
{
    /// <summary>
    /// Adds DM assistant services including repositories, service, and configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDmAssistant(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<DmAssistantOptions>(
            configuration.GetSection(DmAssistantOptions.SectionName));

        // Register repositories (always needed for admin/cleanup)
        services.AddScoped<IDmConversationMessageRepository, DmConversationMessageRepository>();
        services.AddScoped<IDmAssistantInteractionLogRepository, DmAssistantInteractionLogRepository>();
        services.AddScoped<IDmAssistantUsageMetricsRepository, DmAssistantUsageMetricsRepository>();

        // Register bot owner resolver (singleton — caches owner ID)
        services.AddSingleton<IBotOwnerResolver, DiscordBotOwnerResolver>();

        // Register DM tool provider repositories
        services.AddScoped<IDmAssistantNoteRepository, DmAssistantNoteRepository>();

        // Remembers which skills this user has loaded, so an activation carries into the next turn.
        // Ungated: it is a memory-cache entry, and it must exist wherever the DM context is built.
        services.TryAddSingleton<IDmSkillActivationStore, DmSkillActivationStore>();


        // The DM tools themselves are found by the assembly scan in AddAssistantAgent; the
        // ToolCatalog decides which reach the DM assistant. fetch_url reads through this client.
        services.AddHttpClient("DmAssistantWebFetch", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DiscordBot-DmAssistant/1.0");
            client.MaxResponseContentBufferSize = 512 * 1024;
        });

        // Only register LLM-dependent service if API key is configured
        // This prevents DI validation failures when running migrations without API key
        var apiKey = configuration.GetValue<string>("OpenRouter:ApiKey");
        if (!string.IsNullOrEmpty(apiKey))
        {
            // The framework and the shared pipeline are registered by AddAssistant (AddAssistantAgent),
            // which Program.cs calls first; the pipeline is TryAdd'd here too so this extension does
            // not depend on that order for its own service.
            services.TryAddScoped<IAssistantMessagePipeline, AssistantMessagePipeline>();

            services.AddScoped<IDmAssistantContextFactory, DmAssistantContextFactory>();
            services.AddScoped<IDmAssistantService, DmAssistantService>();
        }

        return services;
    }
}
