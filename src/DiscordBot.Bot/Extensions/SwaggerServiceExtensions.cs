using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering Swagger/OpenAPI documentation services.
/// </summary>
public static class SwaggerServiceExtensions
{
    /// <summary>
    /// Adds Swagger/OpenAPI documentation generation services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Discord Bot Management API",
                Version = "v1",
                Description = "API for managing the Discord bot, including guild settings, bot status, and command logs."
            });

            // Include XML comments for Swagger documentation
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }
}
