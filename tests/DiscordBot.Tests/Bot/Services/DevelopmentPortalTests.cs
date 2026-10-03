using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services.Portal;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task Seeder_DoesNothing_UnlessDevelopmentAndOffline(string environment, bool offline)
    {
        // An empty provider throws if the seeder asks it for anything, which proves it bailed out first
        var services = new ServiceCollection().BuildServiceProvider();

        var seeded = await DevelopmentPortal.SeedAsync(services, Environment(environment), offline, NullLogger.Instance);

        seeded.Should().BeFalse();
    }

    private static IHostEnvironment Environment(string name)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(name);
        return environment.Object;
    }
}
