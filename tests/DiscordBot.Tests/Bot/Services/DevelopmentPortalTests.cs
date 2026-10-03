using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services.Portal;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DiscordBot.Tests.Bot.Services;

/// <summary>
/// The development portal (D15) must be unreachable outside Development + Discord:OfflineMode.
/// The positive path is exercised end to end by <c>MemberPortalTests</c> and <c>RouteSmokeTests</c>.
/// </summary>
public class DevelopmentPortalTests
{
    [Theory]
    [InlineData("Development", true, true)]
    [InlineData("Development", false, false)]
    [InlineData("Production", true, false)]
    [InlineData("Production", false, false)]
    [InlineData("Staging", true, false)]
    public void IsEnabled_NeedsBothDevelopmentAndOfflineMode(string environment, bool offline, bool expected)
    {
        DevelopmentPortal.IsEnabled(Environment(environment), offline).Should().Be(expected);
    }

    [Theory]
    [InlineData("Development", true, typeof(DevelopmentPortalGuildDirectory))]
    [InlineData("Development", false, typeof(DiscordPortalGuildDirectory))]
    [InlineData("Production", true, typeof(DiscordPortalGuildDirectory))]
    [InlineData("Production", false, typeof(DiscordPortalGuildDirectory))]
    public void Registration_PicksTheDevelopmentDirectoryOnlyWhenBothHold(string environment, bool offline, Type expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Discord:OfflineMode"] = offline.ToString() })
            .Build();
        var services = new ServiceCollection();

        services.AddPortalGuildDirectory(Environment(environment), configuration);

        var registration = services.Single(d => d.ServiceType == typeof(IPortalGuildDirectory));
        registration.ImplementationType.Should().Be(expected);
        registration.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Development", false)]
    [InlineData("Production", false)]
    public async Task Seeder_SeedsNothing_UnlessDevelopmentAndOffline(string environment, bool offline)
    {
        using var host = new SeedHost();

        var seeded = await DevelopmentPortal.SeedAsync(
            host.Services, Environment(environment), offline, NullLogger.Instance);

        seeded.Should().BeFalse();
        (await host.FindAsync(host.AdminEmail))!.DiscordUserId.Should().BeNull();
        (await host.FindAsync(DevelopmentPortal.MemberEmail)).Should().BeNull();
    }

    [Fact]
    public async Task Seeder_LinksAdminAndCreatesMember_WhenAdminHasNoLink()
    {
        using var host = new SeedHost();

        var seeded = await DevelopmentPortal.SeedAsync(
            host.Services, Environment("Development"), true, NullLogger.Instance);

        seeded.Should().BeTrue();
        (await host.FindAsync(host.AdminEmail))!.DiscordUserId.Should().Be(DevelopmentPortal.AdminDiscordUserId);
        var member = await host.FindAsync(DevelopmentPortal.MemberEmail);
        member!.DiscordUserId.Should().Be(DevelopmentPortal.MemberDiscordUserId);
        member.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Seeder_SeedsNothing_WhenAdminAlreadyHasARealLink()
    {
        using var host = new SeedHost();
        const ulong realId = 123456789012345678UL;
        await host.SetLinkAsync(host.AdminEmail, realId);

        var seeded = await DevelopmentPortal.SeedAsync(
            host.Services, Environment("Development"), true, NullLogger.Instance);

        seeded.Should().BeFalse();
        (await host.FindAsync(host.AdminEmail))!.DiscordUserId.Should().Be(realId);
        (await host.FindAsync(DevelopmentPortal.MemberEmail)).Should().BeNull();
    }

    [Fact]
    public async Task Seeder_IsIdempotent_WhenAdminIsAlreadyLinkedToTheFakeId()
    {
        using var host = new SeedHost();
        var development = Environment("Development");

        await DevelopmentPortal.SeedAsync(host.Services, development, true, NullLogger.Instance);
        var again = await DevelopmentPortal.SeedAsync(host.Services, development, true, NullLogger.Instance);

        again.Should().BeTrue();
        (await host.FindAsync(host.AdminEmail))!.DiscordUserId.Should().Be(DevelopmentPortal.AdminDiscordUserId);
    }

    [Fact]
    public async Task Seeder_RemovesTheSeed_WhenThePortalIsOff()
    {
        using var host = new SeedHost();
        await DevelopmentPortal.SeedAsync(host.Services, Environment("Development"), true, NullLogger.Instance);

        // The same database later starts as Production
        var seeded = await DevelopmentPortal.SeedAsync(
            host.Services, Environment("Production"), false, NullLogger.Instance);

        seeded.Should().BeFalse();
        var admin = await host.FindAsync(host.AdminEmail);
        admin.Should().NotBeNull("users are never deleted");
        admin!.DiscordUserId.Should().BeNull();
        var member = await host.FindAsync(DevelopmentPortal.MemberEmail);
        member.Should().NotBeNull("users are never deleted");
        member!.DiscordUserId.Should().BeNull();
        member.IsActive.Should().BeFalse();
        member.LockoutEnabled.Should().BeTrue();
        member.LockoutEnd.Should().Be(DateTimeOffset.MaxValue);
    }

    [Fact]
    public async Task Seeder_LeavesRealLinksAlone_WhenThePortalIsOff()
    {
        using var host = new SeedHost();
        const ulong realId = 123456789012345678UL;
        await host.SetLinkAsync(host.AdminEmail, realId);

        await DevelopmentPortal.SeedAsync(host.Services, Environment("Production"), false, NullLogger.Instance);

        (await host.FindAsync(host.AdminEmail))!.DiscordUserId.Should().Be(realId);
    }

    /// <summary>Real Identity stores on a fresh PostgreSQL database, with the default admin created.</summary>
    private sealed class SeedHost : IDisposable
    {
        private readonly TestDatabase _database = TestDbContextFactory.CreateDatabase();
        private readonly ServiceProvider _root;
        private readonly IServiceScope _scope;

        public SeedHost()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<BotDbContext>(o => o.UseNpgsql(_database.ConnectionString));
            services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<BotDbContext>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddSingleton<IOptions<IdentityConfigOptions>>(Options.Create(new IdentityConfigOptions
            {
                DefaultAdmin = new DefaultAdminOptions { Email = AdminEmail, Password = "Passw0rd!Passw0rd" }
            }));
            _root = services.BuildServiceProvider();
            _scope = _root.CreateScope();

            var userManager = Services.GetRequiredService<UserManager<ApplicationUser>>();
            var created = userManager
                .CreateAsync(new ApplicationUser { UserName = AdminEmail, Email = AdminEmail }, "Passw0rd!Passw0rd")
                .GetAwaiter().GetResult();
            created.Succeeded.Should().BeTrue();
        }

        public string AdminEmail => "admin@example.com";

        public IServiceProvider Services => _scope.ServiceProvider;

        public async Task<ApplicationUser?> FindAsync(string email)
        {
            // A fresh scope, so nothing is served from the change tracker
            using var scope = _root.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
        }

        public async Task SetLinkAsync(string email, ulong discordUserId)
        {
            var userManager = Services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await userManager.FindByEmailAsync(email))!;
            user.DiscordUserId = discordUserId;
            (await userManager.UpdateAsync(user)).Succeeded.Should().BeTrue();
        }

        public void Dispose()
        {
            _scope.Dispose();
            _root.Dispose();
            _database.Dispose();
        }
    }

    private static IHostEnvironment Environment(string name)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(name);
        return environment.Object;
    }
}
