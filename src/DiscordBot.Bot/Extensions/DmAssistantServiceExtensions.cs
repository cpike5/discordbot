using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.LLM.Providers;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Infrastructure.Services;
using DiscordBot.Infrastructure.Services.LLM.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        // Register DM tool providers
        services.AddScoped<IDmToolProvider, MemoryToolProvider>();
        services.AddScoped<IDmToolProvider, ConversationToolProvider>();
        services.AddScoped<IDmToolProvider, BotManagementToolProvider>();
        services.AddScoped<IDmToolProvider, DmModerationToolProvider>();
        services.AddScoped<IDmToolProvider, DmAnalyticsToolProvider>();
        services.AddScoped<DocumentationToolProvider>();
        services.AddScoped<IDmToolProvider, DmDocumentationToolProvider>();
        services.AddScoped<IDmToolProvider, CodeExecutionToolProvider>();
        services.AddHttpClient("DmAssistantWebFetch", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DiscordBot-DmAssistant/1.0");
            client.MaxResponseContentBufferSize = 512 * 1024;
        });
        services.AddScoped<IDmToolProvider, WebFetchToolProvider>();

        // Only register LLM-dependent service if API key is configured
        // This prevents DI validation failures when running migrations without API key
        var apiKey = configuration.GetValue<string>("Anthropic:ApiKey");
        if (!string.IsNullOrEmpty(apiKey))
        {
            services.AddScoped<IDmAssistantService, DmAssistantService>();
        }

        return services;
    }
}
