using System.Text;
using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Builds the audit-log export CSV - split out from <c>Extensions/AdminLogsEndpointExtensions.cs</c>'s
/// minimal-API handler so the formatting itself (header shape, escaping, filename) is unit-testable
/// without a running host. Column set and escaping are an exact port of the legacy
/// <c>Pages/Admin/Logs/Index.cshtml.cs</c> <c>OnGetExportAsync</c>/<c>EscapeCsv</c>.
/// </summary>
public static class AdminLogsCsvExporter
{
    public const string Header = "Timestamp,Category,Action,Actor,Target Type,Target ID,Guild,Details,IP Address,Correlation ID";

    /// <summary>Builds the CSV body (header + one row per log) as UTF-8 bytes.</summary>
    public static byte[] BuildCsv(IEnumerable<AuditLogDto> items)
    {
        var csv = new StringBuilder();
        csv.AppendLine(Header);

        foreach (var log in items)
        {
            csv.AppendLine($"\"{log.Timestamp:yyyy-MM-dd HH:mm:ss}\"," +
                $"\"{EscapeCsv(log.CategoryName)}\"," +
                $"\"{EscapeCsv(log.ActionName)}\"," +
                $"\"{EscapeCsv(log.ActorDisplayName ?? log.ActorId ?? string.Empty)}\"," +
                $"\"{EscapeCsv(log.TargetType ?? string.Empty)}\"," +
                $"\"{EscapeCsv(log.TargetId ?? string.Empty)}\"," +
                $"\"{EscapeCsv(log.GuildName ?? string.Empty)}\"," +
                $"\"{EscapeCsv(log.Details ?? string.Empty)}\"," +
                $"\"{EscapeCsv(log.IpAddress ?? string.Empty)}\"," +
                $"\"{EscapeCsv(log.CorrelationId ?? string.Empty)}\"");
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    /// <summary>Builds the download filename for a CSV generated "now".</summary>
    public static string BuildFileName(DateTime utcNow) => $"audit-logs-{utcNow:yyyyMMdd-HHmmss}.csv";

    /// <summary>Escapes CSV field values (doubles embedded quotes) to prevent injection and formatting issues.</summary>
    public static string EscapeCsv(string value) => string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\"", "\"\"");
}
