using System.Text.RegularExpressions;
using DiscordBot.Core.Entities;
using DiscordBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DiscordBot.Tests.TestHelpers;

/// <summary>
/// Test classes that boot an <see cref="OfflineAppHost"/> share this collection, so their
/// hosts start one at a time: Program.cs installs Serilog's static bootstrap logger, and two
/// hosts starting together both try to freeze it.
/// </summary>
[CollectionDefinition(Name)]
public sealed class OfflineAppHostCollection
{
    public const string Name = "Offline app host";
}

/// <summary>
/// The real application, booted in-process in offline mode (no Discord login) on a fresh
/// PostgreSQL database, with an HTTP client already signed in as the seeded admin through
/// the login form. One guild with audio enabled is seeded so guild routes have a valid id;
/// callers can seed more rows before the app starts.
/// </summary>
public sealed class OfflineAppHost : IAsyncDisposable
{
    public const ulong GuildId = 123456789012345678UL;
    public const string AdminEmail = "smoke-admin@example.com";
    private const string AdminPassword = "Smoke-Test-Pass-123!";

    private readonly TestDatabase _database;
    private readonly WebApplicationFactory<Program> _factory;

    private OfflineAppHost(TestDatabase database, WebApplicationFactory<Program> factory, HttpClient client)
    {
        _database = database;
        _factory = factory;
        Client = client;
    }

    /// <summary>A client signed in as the seeded SuperAdmin; it follows redirects and keeps cookies.</summary>
    public HttpClient Client { get; }

    /// <summary>The application's root service provider.</summary>
    public IServiceProvider Services => _factory.Services;

    /// <summary>
    /// Creates the database, seeds the guild plus whatever <paramref name="seed"/> adds,
    /// starts the app and signs in.
    /// </summary>
    public static async Task<OfflineAppHost> StartAsync(Func<BotDbContext, Task>? seed = null)
    {
        var database = TestDbContextFactory.CreateDatabase();
        await using (var context = database.CreateContext())
        {
            context.Guilds.Add(new Guild { Id = GuildId, Name = "Smoke Test Guild", JoinedAt = DateTime.UtcNow, IsActive = true });

            // The member portal pages answer 404 for a guild without audio enabled
            context.GuildAudioSettings.Add(new GuildAudioSettings
            {
                GuildId = GuildId,
                AudioEnabled = true,
                EnableMemberPortal = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            if (seed != null)
            {
                await seed(context);
                await context.SaveChangesAsync();
            }
        }

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", database.ConnectionString);
            builder.UseSetting("Database:Provider", "PostgreSql");
            builder.UseSetting("Discord:OfflineMode", "true");
            builder.UseSetting("Identity:DefaultAdmin:Email", AdminEmail);
            builder.UseSetting("Identity:DefaultAdmin:Password", AdminPassword);
            builder.UseSetting("ElasticApm:Enabled", "false");
        });

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            HandleCookies = true
        });

        var host = new OfflineAppHost(database, factory, client);
        await SignInAsync(client, AdminEmail, AdminPassword);
        return host;
    }

    /// <summary>
    /// Creates an active user with one role and returns a new client signed in as them.
    /// The caller disposes the client.
    /// </summary>
    public async Task<HttpClient> CreateSignedInClientAsync(string email, string role)
    {
        const string password = "Other-User-Pass-123!";
        using (var scope = Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, IsActive = true };
            (await users.CreateAsync(user, password)).Succeeded.Should().BeTrue();
            (await users.AddToRoleAsync(user, role)).Succeeded.Should().BeTrue();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            HandleCookies = true
        });
        await SignInAsync(client, email, password);
        return client;
    }

    /// <summary>The Identity id of the seeded admin.</summary>
    public async Task<string> GetAdminUserIdAsync()
    {
        using var scope = Services.CreateScope();
        var admin = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(AdminEmail);
        return admin!.Id;
    }

    /// <summary>Reads the antiforgery token from a page's form.</summary>
    public static string ReadAntiforgeryToken(string html)
    {
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        token.Should().NotBeEmpty("the page's form carries an antiforgery token");
        return token;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
        _database.Dispose();
    }

    private static async Task SignInAsync(HttpClient client, string email, string password)
    {
        var token = ReadAntiforgeryToken(await client.GetStringAsync("/Account/Login"));

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["__RequestVerificationToken"] = token
        }));

        response.RequestMessage!.RequestUri!.AbsolutePath.Should().NotStartWith("/Account/Login", $"{email} should sign in");
    }
}
