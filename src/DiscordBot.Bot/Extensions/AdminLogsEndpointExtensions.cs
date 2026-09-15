using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Minimal-API replacement for <c>Pages/Admin/Logs/Index.cshtml.cs</c>'s <c>OnGetExportAsync</c>
/// page handler (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d "CSV exports become
/// minimal-API GET endpoints"). Mapped from <c>Program.cs</c> next to <c>MapAccountEndpoints()</c>.
/// Same filter query names as <c>Blazor/Pages/Admin/Logs/Tabs/AuditTab.razor.cs</c> builds its
/// export link with, including <c>UserTimezone</c> for the same local-day-boundary conversion the
/// audit tab's own load uses.
/// </summary>
public static class AdminLogsEndpointExtensions
{
    public const string AuditLogsExportRoute = "/api/admin/audit-logs/export";

    public static IEndpointRouteBuilder MapAdminLogsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(AuditLogsExportRoute, HandleExportAsync)
            .RequireAuthorization("RequireAdmin");

        return app;
    }

    internal static async Task<IResult> HandleExportAsync(
        [FromQuery] AuditLogCategory? category,
        [FromQuery] AuditLogAction? action,
        [FromQuery] string? actorId,
        [FromQuery] string? targetType,
        [FromQuery] ulong? auditGuildId,
        [FromQuery] DateTime? auditStartDate,
        [FromQuery] DateTime? auditEndDate,
        [FromQuery] string? auditSearchTerm,
        [FromQuery] string? userTimezone,
        [FromServices] IAuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        DateTime? queryStartDate = auditStartDate.HasValue
            ? TimezoneHelper.ConvertToUtc(auditStartDate.Value.Date, userTimezone)
            : null;
        DateTime? queryEndDate = auditEndDate.HasValue
            ? TimezoneHelper.ConvertToUtc(auditEndDate.Value.Date.AddDays(1).AddTicks(-1), userTimezone)
            : null;

        var query = new AuditLogQueryDto
        {
            Category = category,
            Action = action,
            ActorId = actorId,
            TargetType = targetType,
            GuildId = auditGuildId,
            StartDate = queryStartDate,
            EndDate = queryEndDate,
            SearchTerm = auditSearchTerm,
            Page = 1,
            PageSize = int.MaxValue
        };

        var (items, _) = await auditLogService.GetLogsAsync(query, cancellationToken);

        var bytes = AdminLogsCsvExporter.BuildCsv(items);
        return Results.File(bytes, "text/csv", AdminLogsCsvExporter.BuildFileName(DateTime.UtcNow));
    }
}
