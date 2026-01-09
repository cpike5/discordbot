using DiscordBot.Bot.Authorization;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Handlers;
using DiscordBot.Bot.Hubs;
using DiscordBot.Bot.Middleware;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Extensions;
using DiscordBot.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
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

    // Add Discord bot services
    builder.Services.AddDiscordBot(builder.Configuration);

    // Add Infrastructure services (database and repositories)
    builder.Services.AddInfrastructure(builder.Configuration);

    // Add OpenTelemetry metrics
    builder.Services.AddOpenTelemetryMetrics(builder.Configuration);

    // Add OpenTelemetry tracing
    builder.Services.AddOpenTelemetryTracing(builder.Configuration);

    // Add Elastic APM with priority-based sampling (dual-write during validation)
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

    // Register configuration options classes
    builder.Services.Configure<ApplicationOptions>(
        builder.Configuration.GetSection(ApplicationOptions.SectionName));
    builder.Services.Configure<DiscordOAuthOptions>(
        builder.Configuration.GetSection(DiscordOAuthOptions.SectionName));
    builder.Services.Configure<CachingOptions>(
        builder.Configuration.GetSection(CachingOptions.SectionName));
    builder.Services.Configure<VerificationOptions>(
        builder.Configuration.GetSection(VerificationOptions.SectionName));
    builder.Services.Configure<BackgroundServicesOptions>(
        builder.Configuration.GetSection(BackgroundServicesOptions.SectionName));
    builder.Services.Configure<IdentityConfigOptions>(
        builder.Configuration.GetSection(IdentityConfigOptions.SectionName));
    builder.Services.Configure<ObservabilityOptions>(
        builder.Configuration.GetSection(ObservabilityOptions.SectionName));

    // Load Identity configuration for startup
    var identityConfig = builder.Configuration
        .GetSection(IdentityConfigOptions.SectionName)
        .Get<IdentityConfigOptions>() ?? new IdentityConfigOptions();

    // Add ASP.NET Core Identity
    builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Password settings
        options.Password.RequireDigit = identityConfig.RequireDigit;
        options.Password.RequireLowercase = identityConfig.RequireLowercase;
        options.Password.RequireUppercase = identityConfig.RequireUppercase;
        options.Password.RequireNonAlphanumeric = identityConfig.RequireNonAlphanumeric;
        options.Password.RequiredLength = identityConfig.RequiredLength;
        options.Password.RequiredUniqueChars = identityConfig.RequiredUniqueChars;

        // Lockout settings
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(identityConfig.LockoutTimeSpanMinutes);
        options.Lockout.MaxFailedAccessAttempts = identityConfig.MaxFailedAccessAttempts;
        options.Lockout.AllowedForNewUsers = identityConfig.LockoutAllowedForNewUsers;

        // User settings
        options.User.RequireUniqueEmail = identityConfig.RequireUniqueEmail;

        // Sign-in settings
        options.SignIn.RequireConfirmedAccount = identityConfig.RequireConfirmedAccount;
        options.SignIn.RequireConfirmedEmail = identityConfig.RequireConfirmedEmail;
    })
    .AddEntityFrameworkStores<BotDbContext>()
    .AddDefaultTokenProviders();

    // Configure application cookie
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(identityConfig.CookieExpireDays);
        options.SlidingExpiration = identityConfig.CookieSlidingExpiration;
        options.LoginPath = identityConfig.LoginPath;
        options.LogoutPath = identityConfig.LogoutPath;
        options.AccessDeniedPath = identityConfig.AccessDeniedPath;
    });

    // Load Discord OAuth configuration
    var oauthOptions = builder.Configuration
        .GetSection(DiscordOAuthOptions.SectionName)
        .Get<DiscordOAuthOptions>() ?? new DiscordOAuthOptions();

    // Add Discord OAuth authentication (only if configured)
    var isDiscordOAuthConfigured = !string.IsNullOrEmpty(oauthOptions.ClientId) && !string.IsNullOrEmpty(oauthOptions.ClientSecret);

    if (isDiscordOAuthConfigured)
    {
        builder.Services.AddAuthentication()
            .AddDiscord(options =>
            {
                options.ClientId = oauthOptions.ClientId!;
                options.ClientSecret = oauthOptions.ClientSecret!;
                options.Scope.Add("identify");
                options.Scope.Add("email");
                options.Scope.Add("guilds"); // Required for fetching user's guild list
                options.SaveTokens = true;
            });
    }

    // Register whether Discord OAuth is configured for UI to consume
    builder.Services.AddSingleton(new DiscordOAuthSettings { IsConfigured = isDiscordOAuthConfigured });

    // Add Authorization Policies
    builder.Services.AddAuthorization(options =>
    {
        // Hierarchical role policies
        options.AddPolicy("RequireSuperAdmin", policy =>
            policy.RequireRole(IdentitySeeder.Roles.SuperAdmin));

        options.AddPolicy("RequireAdmin", policy =>
            policy.RequireRole(IdentitySeeder.Roles.SuperAdmin, IdentitySeeder.Roles.Admin));

        options.AddPolicy("RequireModerator", policy =>
            policy.RequireRole(
                IdentitySeeder.Roles.SuperAdmin,
                IdentitySeeder.Roles.Admin,
                IdentitySeeder.Roles.Moderator));

        options.AddPolicy("RequireViewer", policy =>
            policy.RequireRole(
                IdentitySeeder.Roles.SuperAdmin,
                IdentitySeeder.Roles.Admin,
                IdentitySeeder.Roles.Moderator,
                IdentitySeeder.Roles.Viewer));

        // Guild-specific authorization (handler will be added later)
        options.AddPolicy("GuildAccess", policy =>
            policy.Requirements.Add(new GuildAccessRequirement()));

        // Fallback policy - require authentication for all pages by default
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });

    // Register IHttpContextAccessor for authorization handlers
    builder.Services.AddHttpContextAccessor();

    // Register custom authorization handlers
    builder.Services.AddScoped<IAuthorizationHandler, GuildAccessHandler>();

    // Register claims transformation for Discord-linked users
    builder.Services.AddScoped<IClaimsTransformation, DiscordClaimsTransformation>();

    // Add application services
    builder.Services.AddSingleton<IVersionService, VersionService>();
    builder.Services.AddSingleton<IDashboardNotifier, DashboardNotifier>();
    builder.Services.AddSingleton<IDashboardUpdateService, DashboardUpdateService>();
    builder.Services.AddScoped<IBotService, BotService>();
    builder.Services.AddScoped<IGuildService, GuildService>();
    builder.Services.AddScoped<ICommandLogService, CommandLogService>();
    builder.Services.AddScoped<ICommandAnalyticsService, CommandAnalyticsService>();
    builder.Services.AddScoped<ICommandMetadataService, CommandMetadataService>();
    builder.Services.AddSingleton<IPageMetadataService, PageMetadataService>();
    builder.Services.AddScoped<ICommandRegistrationService, CommandRegistrationService>();
    builder.Services.AddScoped<IUserManagementService, UserManagementService>();
    builder.Services.AddScoped<IWelcomeService, WelcomeService>();
    builder.Services.AddScoped<ISearchService, SearchService>();

    // Add Time Parsing service
    builder.Services.AddScoped<ITimeParsingService, TimeParsingService>();

    // Add Discord OAuth services
    builder.Services.AddScoped<IDiscordTokenService, DiscordTokenService>();
    builder.Services.AddScoped<IDiscordUserInfoService, DiscordUserInfoService>();
    builder.Services.AddScoped<IGuildMembershipService, GuildMembershipService>();

    // Add Guild Member services
    builder.Services.AddScoped<IGuildMemberService, GuildMemberService>();

    // Add Discord OAuth Token Refresh background service
    builder.Services.AddHostedService<DiscordTokenRefreshService>();

    // Add Verification services
    builder.Services.AddScoped<IVerificationService, VerificationService>();
    builder.Services.AddHostedService<VerificationCleanupService>();

    // Add Consent services
    builder.Services.AddScoped<IConsentService, ConsentService>();

    // Add Message Log services and cleanup
    builder.Services.AddScoped<IMessageLogService, MessageLogService>();
    builder.Services.Configure<DiscordBot.Core.Configuration.MessageLogRetentionOptions>(
        builder.Configuration.GetSection("MessageLogRetention"));
    builder.Services.AddHostedService<MessageLogCleanupService>();

    // Add Audit Log services
    builder.Services.AddSingleton<IAuditLogQueue, AuditLogQueue>();
    builder.Services.AddScoped<IAuditLogService, AuditLogService>();
    builder.Services.AddHostedService<AuditLogQueueProcessor>();
    builder.Services.Configure<DiscordBot.Core.Configuration.AuditLogRetentionOptions>(
        builder.Configuration.GetSection("AuditLogRetention"));
    builder.Services.AddHostedService<AuditLogRetentionService>();

    // Add Metrics update background services
    builder.Services.AddHostedService<MetricsUpdateService>();
    builder.Services.AddHostedService<BusinessMetricsUpdateService>();

    // Add Scheduled Messages services
    builder.Services.Configure<ScheduledMessagesOptions>(
        builder.Configuration.GetSection(ScheduledMessagesOptions.SectionName));
    builder.Services.AddScoped<IScheduledMessageService, ScheduledMessageService>();
    builder.Services.AddHostedService<ScheduledMessageExecutionService>();

    // Add Bot Status service (must be registered before RatWatchStatusService)
    builder.Services.AddSingleton<IBotStatusService, BotStatusService>();

    // Add Rat Watch services
    builder.Services.Configure<RatWatchOptions>(
        builder.Configuration.GetSection(RatWatchOptions.SectionName));
    builder.Services.AddScoped<IRatWatchService, RatWatchService>();
    builder.Services.AddSingleton<IRatWatchStatusService, RatWatchStatusService>();
    builder.Services.AddHostedService<RatWatchExecutionService>();

    // Add Reminder services
    builder.Services.Configure<ReminderOptions>(
        builder.Configuration.GetSection(ReminderOptions.SectionName));
    builder.Services.AddScoped<IReminderService, ReminderService>();
    builder.Services.AddHostedService<ReminderExecutionService>();

    // Add Soundboard services
    builder.Services.Configure<SoundboardOptions>(
        builder.Configuration.GetSection(SoundboardOptions.SectionName));
    builder.Services.AddScoped<ISoundService, SoundService>();
    builder.Services.AddScoped<ISoundFileService, SoundFileService>();
    builder.Services.AddScoped<IGuildAudioSettingsService, GuildAudioSettingsService>();

    // Add Analytics Aggregation services
    builder.Services.Configure<AnalyticsRetentionOptions>(
        builder.Configuration.GetSection(AnalyticsRetentionOptions.SectionName));
    builder.Services.AddHostedService<MemberActivityAggregationService>();
    builder.Services.AddHostedService<ChannelActivityAggregationService>();
    builder.Services.AddHostedService<GuildMetricsAggregationService>();
    builder.Services.AddHostedService<AnalyticsRetentionService>();

    // Add Historical Metrics configuration
    builder.Services.Configure<HistoricalMetricsOptions>(
        builder.Configuration.GetSection(HistoricalMetricsOptions.SectionName));

    // Add Soundboard configuration (audio services added when implemented)
    builder.Services.Configure<SoundboardOptions>(
        builder.Configuration.GetSection(SoundboardOptions.SectionName));

    // Add Moderation services (includes detection services and handlers)
    builder.Services.AddModerationServices(builder.Configuration);

    // Add Performance Metrics services (latency, connection state, API tracking, database metrics)
    builder.Services.AddPerformanceMetrics(builder.Configuration);

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

    // Enable Elastic APM (must be very early in pipeline to capture all requests)
    // Configured via ElasticApm section in appsettings.json
    app.UseAllElasticApm(builder.Configuration);

    // Configure middleware pipeline
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
