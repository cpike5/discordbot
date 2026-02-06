using AspNet.Security.OAuth.Discord;
using DiscordBot.Bot.Authorization;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering ASP.NET Core Identity, Discord OAuth, and authorization services.
/// </summary>
public static class IdentityServiceExtensions
{
    /// <summary>
    /// Adds ASP.NET Core Identity, Discord OAuth authentication, and authorization services.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Load Identity configuration for startup
        var identityConfig = configuration
            .GetSection(IdentityConfigOptions.SectionName)
            .Get<IdentityConfigOptions>() ?? new IdentityConfigOptions();

        // Persist data protection keys to a writable directory so antiforgery tokens
        // and cookies survive restarts (required when systemd ProtectHome=true blocks
        // the default ~/.aspnet/DataProtection-Keys location)
        var dataProtectionPath = configuration.GetValue<string>("DataProtection:KeyPath")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DataProtection-Keys");
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
            .SetApplicationName("DiscordBot");

        // Add ASP.NET Core Identity
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
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
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromDays(identityConfig.CookieExpireDays);
            options.SlidingExpiration = identityConfig.CookieSlidingExpiration;
            options.LoginPath = identityConfig.LoginPath;
            options.LogoutPath = identityConfig.LogoutPath;
            options.AccessDeniedPath = identityConfig.AccessDeniedPath;
        });

        // Add Discord OAuth authentication
        services.AddDiscordOAuth(configuration);

        // Add authorization policies
        services.AddAuthorizationPolicies();

        // Register IHttpContextAccessor for authorization handlers
        services.AddHttpContextAccessor();

        // Register custom authorization handlers
        services.AddScoped<IAuthorizationHandler, GuildAccessHandler>();
        services.AddScoped<IAuthorizationHandler, PortalGuildMemberAuthorizationHandler>();

        // Register claims transformation for Discord-linked users
        services.AddScoped<IClaimsTransformation, DiscordClaimsTransformation>();

        // Add Discord OAuth services
        services.AddScoped<IDiscordTokenService, DiscordTokenService>();
        services.AddScoped<IDiscordUserInfoService, DiscordUserInfoService>();
        services.AddScoped<IGuildMembershipService, GuildMembershipService>();
        services.AddScoped<IUserDiscordGuildService, UserDiscordGuildService>();

        // Add Discord OAuth Token Refresh background service
        services.AddHostedService<DiscordTokenRefreshService>();

        return services;
    }

    /// <summary>
    /// Adds Discord OAuth authentication.
    /// </summary>
    private static IServiceCollection AddDiscordOAuth(this IServiceCollection services, IConfiguration configuration)
    {
        // Register DiscordOAuthOptions with validation
        services.AddOptions<DiscordOAuthOptions>()
            .Bind(configuration.GetSection(DiscordOAuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Add Discord OAuth authentication with minimal initial config
        services.AddAuthentication()
            .AddDiscord(_ => { });

        // Configure Discord authentication options using validated DiscordOAuthOptions via DI
        services.AddOptions<DiscordAuthenticationOptions>(DiscordAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<DiscordOAuthOptions>>((discordOptions, oauthOptions) =>
            {
                discordOptions.ClientId = oauthOptions.Value.ClientId;
                discordOptions.ClientSecret = oauthOptions.Value.ClientSecret;
                discordOptions.Scope.Add("identify");
                discordOptions.Scope.Add("email");
                discordOptions.Scope.Add("guilds"); // Required for fetching user's guild list
                discordOptions.SaveTokens = true;
            });

        // Register that Discord OAuth is configured for UI to consume
        services.AddSingleton(new DiscordOAuthSettings { IsConfigured = true });

        return services;
    }

    /// <summary>
    /// Adds authorization policies for role-based and guild-based access control.
    /// </summary>
    private static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
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

            // Portal guild membership authorization - lighter weight than admin access
            // Only requires Discord OAuth and guild membership, no role checks
            options.AddPolicy("PortalGuildMember", policy =>
                policy.Requirements.Add(new PortalGuildMemberRequirement()));

            // Fallback policy - require authentication for all pages by default
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}
