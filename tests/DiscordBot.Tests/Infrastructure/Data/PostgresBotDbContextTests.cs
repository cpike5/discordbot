using DiscordBot.Infrastructure.Data;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Tests.Infrastructure.Data;

/// <summary>
/// Guards the <see cref="PostgresBotDbContext.ConfigureConventions"/> override that pins
/// DateTime columns to <c>timestamp without time zone</c>. Npgsql 10 changed its design-time
/// default for <see cref="DateTime"/> to <c>timestamp with time zone</c>, which does not match
/// the existing Postgres schema or the <c>Npgsql.EnableLegacyTimestampBehavior</c> runtime
/// switch set in Program.cs. Building the model does not require a live database connection,
/// so these tests never open one.
/// </summary>
public class PostgresBotDbContextTests
{
    private static PostgresBotDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PostgresBotDbContext>()
            .UseNpgsql("Host=localhost;Database=x;Username=x;Password=x")
            .Options;

        return new PostgresBotDbContext(options);
    }

    /// <summary>
    /// Walks every entity type in the model (including owned types) and every
    /// <see cref="DateTime"/>/<see cref="DateTime?"/> scalar property on it, asserting the
    /// convention pinned every one of them - rather than a hard-coded sample of three
    /// properties that would miss a newly added <see cref="DateTime"/> column silently
    /// reverting to Npgsql 10's <c>timestamp with time zone</c> default.
    /// </summary>
    [Fact]
    public void Model_EveryDateTimeColumn_UsesTimestampWithoutTimeZone()
    {
        using var context = CreateContext();

        var dateTimeProperties = context.Model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetProperties(),
                (entityType, property) => (entityType, property))
            .Where(x => x.property.ClrType == typeof(DateTime) || x.property.ClrType == typeof(DateTime?))
            .ToList();

        // Sanity check the walk itself found something - an empty result would make every
        // assertion below vacuously true and silently stop guarding anything.
        dateTimeProperties.Should().NotBeEmpty("the model should contain DateTime columns to guard");

        using var scope = new AssertionScope();
        foreach (var (entityType, property) in dateTimeProperties)
        {
            property.GetColumnType().Should().Be(
                "timestamp without time zone",
                "{0}.{1} is a {2} column and must stay pinned to timestamp without time zone",
                entityType.ClrType.Name, property.Name, property.ClrType.Name);
        }
    }
}
