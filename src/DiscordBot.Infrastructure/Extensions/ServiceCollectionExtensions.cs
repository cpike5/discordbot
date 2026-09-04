using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Configuration;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Interceptors;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Serilog;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("DiscordBot.Tests")]

namespace DiscordBot.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering Infrastructure services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Repository classes in this assembly that must NOT be picked up by the convention-based
    /// scan in <see cref="AddRepositories"/>, because they are registered elsewhere (feature-module
    /// extension methods in DiscordBot.Bot) or need non-default handling.
    /// </summary>
    internal static readonly IReadOnlyCollection<Type> RepositoryScanExclusions =
    [
        typeof(AssistantGuildSettingsRepository),
        typeof(AssistantInteractionLogRepository),
        typeof(AssistantUsageMetricsRepository),
        typeof(DmAssistantInteractionLogRepository),
        typeof(DmAssistantNoteRepository),
        typeof(DmAssistantUsageMetricsRepository),
        typeof(DmConversationMessageRepository),
        typeof(FeatureRequestRepository),
    ];

    /// <summary>
    /// Adds Infrastructure services including DbContext and repositories.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration settings
        services.Configure<DatabaseSettings>(configuration.GetSection(DatabaseSettings.SectionName));

        // Register query performance interceptor as singleton
        services.AddSingleton<QueryPerformanceInterceptor>();

        // Register DbContext with provider detection
        // Primary: explicit Database:Provider config key; Fallback: connection string heuristic
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=data/discordbot.db";

        var dbSettings = configuration.GetSection(DatabaseSettings.SectionName).Get<DatabaseSettings>()
            ?? new DatabaseSettings();
        var isPostgreSql = dbSettings.IsPostgreSql(connectionString);

        var providerName = dbSettings.GetProviderDisplayName(connectionString);
        Log.Information("Database provider: {Provider}", providerName);

        if (isPostgreSql)
        {
            // Ensure TCP keepalive is set to detect dead connections early
            var csBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            if (csBuilder.KeepAlive == 0)
                csBuilder.KeepAlive = 30;
            var npgsqlConnectionString = csBuilder.ToString();

            services.AddBotDbContext<PostgresBotDbContext>(options =>
                options.UseNpgsql(npgsqlConnectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly("DiscordBot.Infrastructure");
                    npgsql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                }));
            // Forward BotDbContext to resolve as PostgresBotDbContext
            services.AddScoped<BotDbContext>(sp => sp.GetRequiredService<PostgresBotDbContext>());
        }
        else
        {
            services.AddBotDbContext<BotDbContext>(options =>
                options.UseSqlite(connectionString, sqlite =>
                    sqlite.MigrationsAssembly("DiscordBot.Infrastructure")));
        }

        // Register repositories (generic repository + convention-scanned concrete repositories)
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddRepositories();

        // Register services
        // SettingsService is registered as Singleton to maintain restart pending flag across requests
        services.AddSingleton<ISettingsService, SettingsService>();
        // CommandModuleConfigurationService is registered as Singleton to maintain restart pending flag across requests
        services.AddSingleton<ICommandModuleConfigurationService, CommandModuleConfigurationService>();

        return services;
    }

    /// <summary>
    /// Registers a provider-specific <typeparamref name="TContext"/> DbContext with the shared
    /// options (query performance interceptor) applied on top of the provider configuration
    /// supplied by <paramref name="configureProvider"/>.
    /// </summary>
    private static void AddBotDbContext<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureProvider)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            configureProvider(options);
            var interceptor = serviceProvider.GetRequiredService<QueryPerformanceInterceptor>();
            options.AddInterceptors(interceptor);
        });
    }

    /// <summary>
    /// Scans the Infrastructure assembly for concrete, non-abstract, non-generic classes whose
    /// name ends in "Repository" and registers each as Scoped against every interface it
    /// implements whose name also ends in "Repository". Classes listed in
    /// <see cref="RepositoryScanExclusions"/> are skipped because they are registered elsewhere.
    /// </summary>
    private static void AddRepositories(this IServiceCollection services)
    {
        var repositoryTypes = typeof(ServiceCollectionExtensions).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(t => t.Name.EndsWith("Repository", StringComparison.Ordinal))
            .Where(t => !RepositoryScanExclusions.Contains(t));

        foreach (var implementationType in repositoryTypes)
        {
            var repositoryInterfaces = implementationType.GetInterfaces()
                .Where(i => i.Name.EndsWith("Repository", StringComparison.Ordinal));

            foreach (var serviceType in repositoryInterfaces)
            {
                services.AddScoped(serviceType, implementationType);
            }
        }
    }
}
