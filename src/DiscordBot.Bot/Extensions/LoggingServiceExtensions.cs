using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering audit log and message log services.
/// </summary>
public static class LoggingServiceExtensions
{
    /// <summary>
    /// Adds audit logging services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAuditLogging(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind options
        services.Configure<AuditLogRetentionOptions>(
            configuration.GetSection("AuditLogRetention"));

        // Audit log services
        services.AddSingleton<IAuditLogQueue, AuditLogQueue>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        // Background services for queue processing and retention
        services.AddHostedService<AuditLogQueueProcessor>();
        services.AddHostedService<AuditLogRetentionService>();

        return services;
    }

    /// <summary>
    /// Adds message logging services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessageLogging(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind options
        services.Configure<MessageLogRetentionOptions>(
            configuration.GetSection("MessageLogRetention"));

        // Message log service (scoped for per-request)
        services.AddScoped<IMessageLogService, MessageLogService>();

        // Background service for cleanup
        services.AddHostedService<MessageLogCleanupService>();

        return services;
    }
}
