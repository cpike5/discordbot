using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Handlers;
using DiscordBot.Bot.Hubs;
using DiscordBot.Bot.Middleware;
using DiscordBot.Core.Configuration;
using DiscordBot.Infrastructure.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using Elastic.Apm.NetCoreAll;
using Elastic.Apm.SerilogEnricher;
using Elastic.Serilog.Sinks;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Transport;
using Serilog;
using Serilog.Exceptions;
using System.Reflection;

// Get service version from assembly for consistent use across logging and APM
var serviceVersion = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
    ?? "0.0.0";

// Set environment variable for Elastic APM before it initializes
// This ensures APM uses the same version as logs without hardcoding in appsettings.json
Environment.SetEnvironmentVariable("ELASTIC_APM_SERVICE_VERSION", serviceVersion);

// Configure Serilog bootstrap logger for startup errors
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting Discord bot application");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Log the current environment for configuration debugging
    Log.Information("Environment: {Environment}", builder.Environment.EnvironmentName);
    Log.Information("ContentRootPath: {ContentRootPath}", builder.Environment.ContentRootPath);

    // Configure Serilog from appsettings.json with programmatic Elasticsearch sink
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        // Get service name from config (used by Elastic Observability to identify log source)
        var serviceName = context.Configuration["ElasticApm:ServiceName"] ?? "discordbot";

        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithElasticApmCorrelationInfo()
            .Enrich.WithProperty("service.name", serviceName)
            .Enrich.WithProperty("service.version", serviceVersion)
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithProcessName()
            .Enrich.WithThreadId()
            .Enrich.WithThreadName()
            .Enrich.WithClientIp()
            .Enrich.WithCorrelationId()
            .Enrich.WithExceptionDetails();

        // Add Elasticsearch sink programmatically if configured
        var elasticUrl = context.Configuration["ElasticSearch:Url"];
        if (!string.IsNullOrEmpty(elasticUrl))
        {
            var apiKey = context.Configuration["ElasticSearch:ApiKey"] ?? "";
            var environment = context.HostingEnvironment.EnvironmentName?.ToLower() ?? "development";

            configuration.WriteTo.Elasticsearch(new[] { new Uri(elasticUrl) }, opts =>
            {
                opts.DataStream = new DataStreamName("logs", "discordbot", environment);
                opts.BootstrapMethod = BootstrapMethod.None;
            }, transport =>
            {
                if (!string.IsNullOrEmpty(apiKey))
                {
                    transport.Authentication(new ApiKey(apiKey));
                }
            });
        }
    });

    // Enable systemd integration (only activates when running under systemd)
    builder.Host.UseSystemd();

    // ==========================================
    // Core Infrastructure Services
    // ==========================================

    // Add Discord bot services (client, handlers, interaction framework)
    builder.Services.AddDiscordBot(builder.Configuration);

    // Add Infrastructure services (database and repositories)
    builder.Services.AddInfrastructure(builder.Configuration);

    // Add OpenTelemetry metrics and tracing
    builder.Services.AddOpenTelemetryMetrics(builder.Configuration);
    builder.Services.AddOpenTelemetryTracing(builder.Configuration);

    // Add Elastic APM with priority-based sampling
    builder.Services.AddElasticApmWithPrioritySampling(builder.Configuration);

    // Configure forwarded headers for reverse proxy (nginx, Cloudflare, etc.)
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // Clear known networks/proxies to accept headers from any proxy
        // This is necessary when running behind multiple proxies (e.g., Cloudflare -> nginx)
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // ==========================================
    // Configuration Options
    // ==========================================

    // Register shared configuration options (not owned by specific features)
    builder.Services.Configure<ApplicationOptions>(
        builder.Configuration.GetSection(ApplicationOptions.SectionName));
    builder.Services.Configure<CachingOptions>(
        builder.Configuration.GetSection(CachingOptions.SectionName));
    builder.Services.Configure<BackgroundServicesOptions>(
        builder.Configuration.GetSection(BackgroundServicesOptions.SectionName));
    builder.Services.Configure<ObservabilityOptions>(
        builder.Configuration.GetSection(ObservabilityOptions.SectionName));
    builder.Services.Configure<VerificationOptions>(
        builder.Configuration.GetSection(VerificationOptions.SectionName));

    // ==========================================
    // Identity & Authorization
    // ==========================================

    // Add Identity, Discord OAuth, and authorization policies
    builder.Services.AddIdentityServices(builder.Configuration);

    // ==========================================
    // Application Services
    // ==========================================

    // Add core application services (BotService, GuildService, etc.)
    builder.Services.AddApplicationServices();

    // ==========================================
    // Feature Services
    // ==========================================

    // Verification
    builder.Services.AddVerification();

    // Logging services
    builder.Services.AddMessageLogging(builder.Configuration);
    builder.Services.AddAuditLogging(builder.Configuration);

    // Scheduled operations
    builder.Services.AddScheduledMessages(builder.Configuration);
    builder.Services.AddRatWatch(builder.Configuration);
    builder.Services.AddReminders(builder.Configuration);

    // Voice and audio
    builder.Services.AddVoiceSupport(builder.Configuration);

    // Analytics and metrics
    builder.Services.AddAnalytics(builder.Configuration);

    // Moderation services (includes detection services and handlers)
    builder.Services.AddModerationServices(builder.Configuration);

    // Performance Metrics services (latency, connection state, API tracking, database metrics)
    builder.Services.AddPerformanceMetrics(builder.Configuration);

    // ==========================================
    // Web API & SignalR
    // ==========================================

    // Add HttpClient for Discord API calls with tracing handler
    builder.Services.AddTransient<DiscordApiTracingHandler>();
    builder.Services.AddHttpClient("Discord", client =>
    {
        client.BaseAddress = new Uri("https://discord.com/api/v10/");
        client.DefaultRequestHeaders.Add("User-Agent", "DiscordBot-Admin");
    })
    .AddHttpMessageHandler<DiscordApiTracingHandler>();

    // Add Web API services
    builder.Services.AddControllers();
    builder.Services.AddRazorPages();
    builder.Services.AddEndpointsApiExplorer();

    // Add SignalR for real-time dashboard updates
    builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    });

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

    // ==========================================
    // Middleware Pipeline
    // ==========================================

    // Enable Elastic APM (must be very early in pipeline to capture all requests)
    // Configured via ElasticApm section in appsettings.json
    app.UseAllElasticApm(builder.Configuration);

    // Handle forwarded headers from reverse proxy (must be FIRST in pipeline)
    // Required for SignalR WebSocket connections behind nginx/Cloudflare
    app.UseForwardedHeaders();

    // Add correlation ID middleware (must be before Serilog request logging)
    app.UseCorrelationId();

    // Add API metrics middleware (after correlation ID, before Serilog)
    app.UseApiMetrics();

    app.UseSerilogRequestLogging();

    // Configure error handling
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler("/Error/500");
    }

    // Configure status code pages for common HTTP errors
    app.UseStatusCodePagesWithReExecute("/Error/{0}");

    // Enable static file serving for wwwroot
    app.UseStaticFiles();

    // Enable Swagger in all environments for now (can be restricted to Development later)
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Discord Bot Management API v1");
        c.RoutePrefix = "swagger";
    });

    // Enable authentication and authorization middleware
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapRazorPages();

    // Map SignalR hub for real-time dashboard
    app.MapHub<DashboardHub>("/hubs/dashboard");

    // Map Prometheus metrics endpoint
    app.UseOpenTelemetryPrometheusScrapingEndpoint();

    // ==========================================
    // Startup Tasks
    // ==========================================

    // Seed Identity roles and default admin user
    using (var scope = app.Services.CreateScope())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        try
        {
            await IdentitySeeder.SeedIdentityAsync(scope.ServiceProvider, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding Identity data");
        }
    }

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
