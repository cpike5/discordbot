using DiscordBot.Bot.Authorization;
using DiscordBot.Bot.Extensions;
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
using Microsoft.AspNetCore.Identity;
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

    // Log the current environment for configuration debugging
    Log.Information("Environment: {Environment}", builder.Environment.EnvironmentName);
    Log.Information("ContentRootPath: {ContentRootPath}", builder.Environment.ContentRootPath);

    // Configure Serilog from appsettings.json
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Add Discord bot services
    builder.Services.AddDiscordBot(builder.Configuration);

    // Add Infrastructure services (database and repositories)
    builder.Services.AddInfrastructure(builder.Configuration);

    // Add OpenTelemetry metrics
    builder.Services.AddOpenTelemetryMetrics(builder.Configuration);

    // Add OpenTelemetry tracing
    builder.Services.AddOpenTelemetryTracing(builder.Configuration);

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
    builder.Services.AddScoped<IBotService, BotService>();
    builder.Services.AddScoped<IGuildService, GuildService>();
    builder.Services.AddScoped<ICommandLogService, CommandLogService>();
    builder.Services.AddScoped<ICommandAnalyticsService, CommandAnalyticsService>();
    builder.Services.AddScoped<ICommandMetadataService, CommandMetadataService>();
    builder.Services.AddScoped<IUserManagementService, UserManagementService>();
    builder.Services.AddScoped<IWelcomeService, WelcomeService>();

    // Add Discord OAuth services
    builder.Services.AddScoped<IDiscordTokenService, DiscordTokenService>();
    builder.Services.AddScoped<IDiscordUserInfoService, DiscordUserInfoService>();
    builder.Services.AddScoped<IGuildMembershipService, GuildMembershipService>();

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

    // Add Metrics update background services
    builder.Services.AddHostedService<MetricsUpdateService>();
    builder.Services.AddHostedService<BusinessMetricsUpdateService>();

    // Add HttpClient for Discord API calls
    builder.Services.AddHttpClient("Discord", client =>
    {
        client.BaseAddress = new Uri("https://discord.com/api/v10/");
        client.DefaultRequestHeaders.Add("User-Agent", "DiscordBot-Admin");
    });

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

    // Configure middleware pipeline
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
