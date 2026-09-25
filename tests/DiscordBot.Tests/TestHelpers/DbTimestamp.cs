namespace DiscordBot.Tests.TestHelpers;

/// <summary>
/// Bounds for asserting on a timestamp the code under test stamped with <c>DateTime.UtcNow</c> and
/// wrote to the test database. Comparing such a value against <c>DateTime.UtcNow</c> read after the
/// call with <c>BeCloseTo</c> measures how long the database took, not whether the value is right:
/// on PostgreSQL under a loaded CI runner a single test can spend several seconds between the two
/// reads. Read <see cref="LowerBound"/> before the value can be stamped and assert
/// <c>value.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow)</c> instead.
/// </summary>
internal static class DbTimestamp
{
    /// <summary>
    /// The current UTC time truncated to whole microseconds. PostgreSQL keeps microseconds and
    /// Npgsql drops the remaining ticks on write, so a value stamped within the same microsecond as
    /// an untruncated lower bound could read back just below it.
    /// </summary>
    public static DateTime LowerBound()
    {
        var now = DateTime.UtcNow;
        return now.AddTicks(-(now.Ticks % 10));
    }
}
