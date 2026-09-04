using System.Reflection;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Tests.Infrastructure.Extensions;

/// <summary>
/// Verifies that <see cref="ServiceCollectionExtensions.AddInfrastructure"/> registers every
/// repository interface in DiscordBot.Core.Interfaces, so a silently missed registration
/// (e.g. a repository excluded from the convention scan but not registered elsewhere) fails fast.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    /// <summary>
    /// Repository interfaces that are intentionally registered by feature-module extension
    /// methods in DiscordBot.Bot rather than by <c>AddInfrastructure</c>, and therefore are not
    /// expected to resolve from a service provider built with <c>AddInfrastructure</c> alone.
    /// Derived from the same exclusion set the production scan uses, so the two never drift.
    /// </summary>
    private static readonly HashSet<Type> RegisteredElsewhere = DiscordBot.Infrastructure.Extensions.ServiceCollectionExtensions
        .RepositoryScanExclusions
        .SelectMany(t => t.GetInterfaces())
        .Where(i => i.Name.StartsWith('I') && i.Name.EndsWith("Repository", StringComparison.Ordinal))
        .ToHashSet();

    private static ServiceProvider BuildProvider(string connectionString = "Data Source=:memory:")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Fact]
    public void AddInfrastructure_RegistersGenericRepository()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider.GetService<IRepository<object>>();

        repository.Should().NotBeNull();
    }

    public static IEnumerable<object[]> AllRepositoryInterfaces()
    {
        return typeof(IRepository<>).Assembly
            .GetTypes()
            .Where(t => t.IsInterface)
            .Where(t => t.Name.StartsWith('I') && t.Name.EndsWith("Repository", StringComparison.Ordinal))
            .Where(t => !t.IsGenericTypeDefinition)
            .Where(t => !RegisteredElsewhere.Contains(t))
            .Select(t => new object[] { t });
    }

    [Theory]
    [MemberData(nameof(AllRepositoryInterfaces))]
    public void AddInfrastructure_RegistersEveryCoreRepositoryInterface(Type repositoryInterface)
    {
        using var provider = BuildProvider();

        using var scope = provider.CreateScope();
        var resolved = scope.ServiceProvider.GetService(repositoryInterface);

        resolved.Should().NotBeNull($"because {repositoryInterface.Name} should be registered by AddInfrastructure");
    }

    [Fact]
    public void AddInfrastructure_WithSqliteConnectionString_ResolvesSqliteBotDbContext()
    {
        using var provider = BuildProvider("Data Source=data/discordbot.db");
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        context.Should().BeOfType<BotDbContext>();
    }

    [Fact]
    public void AddInfrastructure_WithPostgresConnectionString_RegistersPostgresBotDbContext()
    {
        // Resolving BotDbContext against a Postgres connection string would attempt to open a
        // connection (or at least construct a live NpgsqlDataSource), so assert against the
        // registered ServiceDescriptor instead of resolving from the container.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=x;Username=u;Password=p",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        var postgresDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(PostgresBotDbContext));
        postgresDescriptor.Should().NotBeNull("PostgresBotDbContext should be registered for a Postgres connection string");
        postgresDescriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);

        var forwardingDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(BotDbContext));
        forwardingDescriptor.Should().NotBeNull("BotDbContext should be forwarded to PostgresBotDbContext");
        forwardingDescriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
        forwardingDescriptor.ImplementationFactory.Should().NotBeNull(
            "BotDbContext should be registered via a factory that resolves PostgresBotDbContext");
    }

    [Fact]
    public void AddInfrastructure_RegistersEveryScannedRepositoryInterface_AsScoped()
    {
        var scannedInterfaces = typeof(DiscordBot.Infrastructure.Extensions.ServiceCollectionExtensions).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(t => t.Name.EndsWith("Repository", StringComparison.Ordinal))
            .Where(t => !DiscordBot.Infrastructure.Extensions.ServiceCollectionExtensions.RepositoryScanExclusions.Contains(t))
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.Name.EndsWith("Repository", StringComparison.Ordinal))
            .ToList();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        var repositoryDescriptors = services
            .Where(d => !d.ServiceType.IsGenericType && d.ServiceType.Name.EndsWith("Repository", StringComparison.Ordinal))
            .ToList();

        repositoryDescriptors.Should().HaveCount(scannedInterfaces.Count);
        repositoryDescriptors.Should().OnlyContain(d => d.Lifetime == ServiceLifetime.Scoped);
    }
}
