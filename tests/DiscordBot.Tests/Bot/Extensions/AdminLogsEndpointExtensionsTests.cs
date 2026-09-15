using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace DiscordBot.Tests.Bot.Extensions;

/// <summary>
/// Unit tests for <see cref="AdminLogsEndpointExtensions"/> (docs/plans/blazor-port-plan.md §5
/// Phase 4, cluster 4d) - the minimal-API replacement for
/// <c>Pages/Admin/Logs/Index.cshtml.cs.OnGetExportAsync</c>. Route/policy metadata is checked
/// against the real endpoint the way <c>AccountEndpointExtensionsTests</c> exercises its handlers
/// directly; the handler itself is called directly too, since it is <see langword="internal"/>.
/// </summary>
public class AdminLogsEndpointExtensionsTests
{
    [Fact]
    public void MapAdminLogsEndpoints_RegistersExportRoute_RequiringAdmin()
    {
        var app = WebApplication.CreateBuilder().Build();
        app.MapAdminLogsEndpoints();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(d => d.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == AdminLogsEndpointExtensions.AuditLogsExportRoute);

        var authData = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
        authData.Should().ContainSingle(a => a.Policy == "RequireAdmin");
    }

    [Fact]
    public async Task HandleExportAsync_ReturnsCsvFile_WithTimestampedName()
    {
        var service = new Mock<IAuditLogService>();
        service.Setup(s => s.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<AuditLogDto>(), 0));

        var result = await AdminLogsEndpointExtensions.HandleExportAsync(
            category: null, action: null, actorId: null, targetType: null, auditGuildId: null,
            auditStartDate: null, auditEndDate: null, auditSearchTerm: null, userTimezone: null,
            auditLogService: service.Object, cancellationToken: CancellationToken.None);

        var fileResult = result.Should().BeAssignableTo<FileContentHttpResult>().Subject;
        fileResult.ContentType.Should().Be("text/csv");
        fileResult.FileDownloadName.Should().StartWith("audit-logs-").And.EndWith(".csv");
    }

    [Fact]
    public async Task HandleExportAsync_PassesFiltersThrough_AndRequestsEveryMatchingRow()
    {
        AuditLogQueryDto? captured = null;
        var service = new Mock<IAuditLogService>();
        service.Setup(s => s.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogQueryDto, CancellationToken>((q, _) => captured = q)
            .ReturnsAsync((Array.Empty<AuditLogDto>(), 0));

        await AdminLogsEndpointExtensions.HandleExportAsync(
            category: DiscordBot.Core.Enums.AuditLogCategory.Security,
            action: DiscordBot.Core.Enums.AuditLogAction.Login,
            actorId: "actor-1", targetType: "User", auditGuildId: 123UL,
            auditStartDate: new DateTime(2026, 1, 1), auditEndDate: new DateTime(2026, 1, 31),
            auditSearchTerm: "term", userTimezone: "UTC",
            auditLogService: service.Object, cancellationToken: CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Category.Should().Be(DiscordBot.Core.Enums.AuditLogCategory.Security);
        captured.Action.Should().Be(DiscordBot.Core.Enums.AuditLogAction.Login);
        captured.ActorId.Should().Be("actor-1");
        captured.TargetType.Should().Be("User");
        captured.GuildId.Should().Be(123UL);
        captured.SearchTerm.Should().Be("term");
        captured.Page.Should().Be(1);
        captured.PageSize.Should().Be(int.MaxValue);
        captured.StartDate.Should().Be(new DateTime(2026, 1, 1));
        captured.EndDate.Should().Be(new DateTime(2026, 1, 31, 23, 59, 59, 999).AddTicks(9999));
    }
}
