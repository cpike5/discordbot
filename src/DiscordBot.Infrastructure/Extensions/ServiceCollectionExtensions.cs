using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering Infrastructure services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Infrastructure services including DbContext and repositories.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register DbContext with SQLite
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=discordbot.db";

        services.AddDbContext<BotDbContext>(options =>
            options.UseSqlite(connectionString));

        // Register repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IGuildRepository, GuildRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICommandLogRepository, CommandLogRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();

        // Register services
        // SettingsService is registered as Singleton to maintain restart pending flag across requests
        services.AddSingleton<ISettingsService, SettingsService>();

        return services;
    }
}
