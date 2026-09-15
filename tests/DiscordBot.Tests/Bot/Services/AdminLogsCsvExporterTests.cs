using System.Text;
using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Services;

/// <summary>
/// Unit tests for <see cref="AdminLogsCsvExporter"/>, the CSV-building helper behind
/// <c>GET /api/admin/audit-logs/export</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d).
/// Ported from the equivalent behaviours of the deleted
/// <c>Pages/Admin/Logs/Index.cshtml.cs.OnGetExportAsync</c>.
/// </summary>
public class AdminLogsCsvExporterTests
{
    [Fact]
    public void BuildCsv_EmptyItems_WritesOnlyTheHeader()
    {
        var bytes = AdminLogsCsvExporter.BuildCsv(Array.Empty<AuditLogDto>());

        var text = Encoding.UTF8.GetString(bytes);
        text.Trim().Should().Be(AdminLogsCsvExporter.Header);
    }

    [Fact]
    public void BuildCsv_OneRow_WritesEveryColumn()
    {
        var log = new AuditLogDto
        {
            Timestamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            CategoryName = "User",
            ActionName = "Created",
            ActorDisplayName = "alice",
            TargetType = "User",
            TargetId = "u1",
            GuildName = "Test Guild",
            Details = "some details",
            IpAddress = "127.0.0.1",
            CorrelationId = "corr-1"
        };

        var text = Encoding.UTF8.GetString(AdminLogsCsvExporter.BuildCsv([log]));

        text.Should().Contain("\"2026-01-02 03:04:05\"");
        text.Should().Contain("\"User\"");
        text.Should().Contain("\"Created\"");
        text.Should().Contain("\"alice\"");
        text.Should().Contain("\"Test Guild\"");
        text.Should().Contain("\"127.0.0.1\"");
        text.Should().Contain("\"corr-1\"");
    }

    [Fact]
    public void BuildCsv_ActorDisplayNameMissing_FallsBackToActorId()
    {
        var log = new AuditLogDto { ActorDisplayName = null, ActorId = "u-guid-1", CategoryName = "System", ActionName = "SettingChanged" };

        var text = Encoding.UTF8.GetString(AdminLogsCsvExporter.BuildCsv([log]));

        text.Should().Contain("\"u-guid-1\"");
    }

    [Fact]
    public void EscapeCsv_DoublesEmbeddedQuotes()
    {
        AdminLogsCsvExporter.EscapeCsv("say \"hi\"").Should().Be("say \"\"hi\"\"");
    }

    [Fact]
    public void EscapeCsv_NullOrEmpty_ReturnsEmptyString()
    {
        AdminLogsCsvExporter.EscapeCsv(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void BuildFileName_UsesTimestampedAuditLogsPrefix()
    {
        var name = AdminLogsCsvExporter.BuildFileName(new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc));

        name.Should().Be("audit-logs-20260304-050607.csv");
    }
}
