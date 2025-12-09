using DiscordBot.Bot.Extensions;
using Serilog;

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

    // Add Web API services
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    var app = builder.Build();

    // Configure middleware pipeline
    app.UseSerilogRequestLogging();

    app.UseAuthorization();

    app.MapControllers();

    // Add health check endpoint
    app.MapGet("/health", () => Results.Ok(new
    {
        Status = "Healthy",
        Timestamp = DateTime.UtcNow
    }));

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
