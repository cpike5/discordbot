using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Seeds Identity roles and optionally a default admin user.
/// </summary>
public static class IdentitySeeder
{
    /// <summary>
    /// Role names used in the application.
    /// </summary>
    public static class Roles
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string Admin = "Admin";
        public const string Moderator = "Moderator";
        public const string Viewer = "Viewer";
    }

    /// <summary>
    /// Seeds the database with default roles and optionally a default admin user.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving Identity services.</param>
    /// <param name="logger">Logger for logging seed operations.</param>
    public static async Task SeedIdentityAsync(IServiceProvider serviceProvider, ILogger logger)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var identityOptions = serviceProvider.GetRequiredService<IOptions<IdentityConfigOptions>>();

        logger.LogInformation("Starting Identity seeding...");

        // Seed roles
        await SeedRolesAsync(roleManager, logger);

        // Optionally seed default admin user from configuration
        await SeedDefaultAdminAsync(userManager, identityOptions, logger);

        logger.LogInformation("Identity seeding completed successfully");
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
    {
        string[] roles = { Roles.SuperAdmin, Roles.Admin, Roles.Moderator, Roles.Viewer };

        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                logger.LogInformation("Creating role: {RoleName}", roleName);
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));

                if (result.Succeeded)
                {
                    logger.LogInformation("Role {RoleName} created successfully", roleName);
                }
                else
                {
                    logger.LogError("Failed to create role {RoleName}: {Errors}",
                        roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                logger.LogDebug("Role {RoleName} already exists, skipping", roleName);
            }
        }
    }

    private static async Task SeedDefaultAdminAsync(
        UserManager<ApplicationUser> userManager,
        IOptions<IdentityConfigOptions> identityOptions,
        ILogger logger)
    {
        var adminEmail = identityOptions.Value.DefaultAdmin?.Email;
        var adminPassword = identityOptions.Value.DefaultAdmin?.Password;

        // Only seed if both email and password are configured
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogInformation("Default admin credentials not configured in appsettings, skipping admin user creation");
            return;
        }

        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin != null)
        {
            logger.LogDebug("Admin user {Email} already exists, skipping", adminEmail);
            return;
        }

        logger.LogInformation("Creating default admin user: {Email}", adminEmail);

        var adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            DisplayName = "System Administrator",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(adminUser, adminPassword);

        if (createResult.Succeeded)
        {
            logger.LogInformation("Admin user {Email} created successfully", adminEmail);

            // Assign SuperAdmin role
            var roleResult = await userManager.AddToRoleAsync(adminUser, Roles.SuperAdmin);

            if (roleResult.Succeeded)
            {
                logger.LogInformation("SuperAdmin role assigned to {Email}", adminEmail);
            }
            else
            {
                logger.LogError("Failed to assign SuperAdmin role to {Email}: {Errors}",
                    adminEmail, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            logger.LogError("Failed to create admin user {Email}: {Errors}",
                adminEmail, string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }
    }
}
