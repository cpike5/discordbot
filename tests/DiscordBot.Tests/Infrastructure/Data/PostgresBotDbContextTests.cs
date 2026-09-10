using DiscordBot.Core.Entities;
using DiscordBot.Infrastructure.Data;
using FluentAssertions;
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

    [Theory]
    [InlineData(typeof(AuditLog), nameof(AuditLog.Timestamp))]
    [InlineData(typeof(Reminder), nameof(Reminder.TriggerAt))]
    public void Model_NonNullableDateTimeColumn_UsesTimestampWithoutTimeZone(Type entityType, string propertyName)
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(entityType);
        entity.Should().NotBeNull($"{entityType.Name} should be part of the model");

        var property = entity!.FindProperty(propertyName);
        property.Should().NotBeNull($"{entityType.Name}.{propertyName} should be a mapped property");

        property!.GetColumnType().Should().Be("timestamp without time zone");
    }

    [Fact]
    public void Model_NullableDateTimeColumn_UsesTimestampWithoutTimeZone()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(Guild));
        entity.Should().NotBeNull();

        var property = entity!.FindProperty(nameof(Guild.LeftAt));
        property.Should().NotBeNull();

        property!.GetColumnType().Should().Be("timestamp without time zone");
    }
}
