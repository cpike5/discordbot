using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Extensions;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;

// Configure Serilog bootstrap logger for startup errors
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting Discord bot application");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog from appsettings.json
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Add Discord bot services
    builder.Services.AddDiscordBot(builder.Configuration);

    // Add Infrastructure services (database and repositories)
    builder.Services.AddInfrastructure(builder.Configuration);

    // Add application services
    builder.Services.AddScoped<IBotService, BotService>();
    builder.Services.AddScoped<IGuildService, GuildService>();
    builder.Services.AddScoped<ICommandLogService, CommandLogService>();

    // Add Web API services
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // Configure Swagger/OpenAPI
    builder.Services.AddSwaggerGen(c =>
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

    var app = builder.Build();

    // Configure middleware pipeline
    app.UseSerilogRequestLogging();

    // Enable Swagger in all environments for now (can be restricted to Development later)
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Discord Bot Management API v1");
        c.RoutePrefix = "swagger";
    });

    app.UseAuthorization();

    app.MapControllers();

    Log.Information("Application configured successfully, starting web host");

    await app.RunAsync();

    Log.Information("Application shut down gracefully");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start or encountered a fatal error");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
