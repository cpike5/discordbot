using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Pages.Admin;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Pages;

/// <summary>
/// Tests for the routed Blazor twin of the old Pages/Admin/Logs/Index.cshtml
/// (Phase F migration). Covers the native tab mechanism that replaced the
/// _TabPanel partial + tab-panel.js stack (lazy per-tab mounting, only the
/// active tab loads data) and the tab bodies' list rendering and live filters.
/// </summary>
public class LogsIndexTests : TestContext
{
    private readonly Mock<IMessageLogService> _messageLogService = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<IMessageLogRepository> _messageLogRepository = new();
    private readonly List<MessageLogQueryDto> _messageQueries = new();
    private readonly List<AuditLogQueryDto> _auditQueries = new();

    public LogsIndexTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _messageLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<MessageLogQueryDto>(), It.IsAny<CancellationToken>()))
            .Callback<MessageLogQueryDto, CancellationToken>((q, _) => _messageQueries.Add(q))
            .ReturnsAsync(new PaginatedResponseDto<MessageLogDto>
            {
                Items = new[]
                {
                    new MessageLogDto
                    {
                        Id = 11,
                        AuthorId = 123456789012345678,
                        AuthorUsername = "alice",
                        GuildId = 987654321098765432,
                        GuildName = "Test Guild",
                        ChannelId = 42,
                        ChannelName = "general",
                        Source = MessageSource.ServerChannel,
                        Content = "hello world",
                        Timestamp = DateTime.UtcNow.AddHours(-1)
                    }
                },
                Page = 1,
                PageSize = 25,
                TotalCount = 1
            });

        _auditLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogQueryDto, CancellationToken>((q, _) => _auditQueries.Add(q))
            .ReturnsAsync((new List<AuditLogDto>
            {
                new()
                {
                    Id = 7,
                    Timestamp = DateTime.UtcNow.AddMinutes(-30),
                    Category = AuditLogCategory.User,
                    CategoryName = "User",
                    Action = AuditLogAction.Updated,
                    ActionName = "Updated",
                    ActorType = AuditLogActorType.User,
                    ActorId = "123456789012345678",
                    ActorDisplayName = "Alice Admin",
                    TargetType = "User",
                    TargetId = "42",
                    GuildId = 987654321098765432,
                    GuildName = "Test Guild",
                    Details = "{\"field\":\"value\"}"
                }
            } as IReadOnlyList<AuditLogDto>, 1));

        _guildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>
            {
                new() { Id = 987654321098765432, Name = "Test Guild" }
            });

        _messageLogRepository
            .Setup(r => r.GetUserMessagesAsync(
                It.IsAny<ulong>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MessageLog>());

        // The page resolves services through IServiceScopeFactory (scope per operation).
        Services.AddSingleton(_messageLogService.Object);
        Services.AddSingleton(_auditLogService.Object);
        Services.AddSingleton(_guildService.Object);
        Services.AddSingleton(_messageLogRepository.Object);
        Services.AddScoped<ToastInterop>();
        Services.AddLogging();

        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("Admin User");
        auth.SetPolicies("RequireAdmin");
    }

    [Fact]
    public void MessagesTab_IsDefault_AndAuditTabIsNotLoaded()
    {
        var cut = RenderComponent<LogsIndex>();

        // Tab strip renders all three tabs; Application is disabled.
        cut.Markup.Should().Contain("Messages");
        cut.Markup.Should().Contain("Audit");
        cut.Markup.Should().Contain("Application");

        // The messages list loads and renders rows.
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("alice");
            cut.Markup.Should().Contain("hello world");
            cut.Markup.Should().Contain("/Admin/MessageLogs/Details/11");
        });

        // Page-model parity: default date range (last 7 days) applied.
        _messageQueries.Should().NotBeEmpty();
        _messageQueries[0].StartDate.Should().Be(DateTime.UtcNow.Date.AddDays(-7));
        _messageQueries[0].PageSize.Should().Be(25);

        // Lazy tabs: only the active tab loads data (page-model parity).
        _auditLogService.Verify(
            s => s.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void SwitchingToAuditTab_LoadsAuditLogs()
    {
        var cut = RenderComponent<LogsIndex>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("hello world"));

        var auditTab = cut.FindAll("button[role=tab]")
            .Single(b => b.GetAttribute("data-tab-id") == "audit");
        auditTab.Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Alice Admin");
            cut.Markup.Should().Contain("Updated");
            cut.Markup.Should().Contain("Test Guild");
        });

        // Page-model parity: default audit date range (last 30 days) applied.
        _auditQueries.Should().NotBeEmpty();
        _auditQueries[0].PageSize.Should().Be(25);
        _auditQueries[0].StartDate.Should().NotBeNull();

        // The messages tab stays mounted (state preserved when switching back).
        cut.Markup.Should().Contain("hello world");
    }

    [Fact]
    public void MessageSearch_AppliesFilterAfterDebounce()
    {
        var cut = RenderComponent<LogsIndex>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("hello world"));
        _messageQueries.Clear();

        cut.Find("#messageSearchTerm").Input("needle");

        // Debounced ~300ms before the query re-fires with the search term.
        cut.WaitForAssertion(
            () => _messageQueries.Should().Contain(q => q.SearchTerm == "needle"),
            timeout: TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void AuditRow_ExpandsInline()
    {
        var cut = RenderComponent<LogsIndex>();

        var auditTab = cut.FindAll("button[role=tab]")
            .Single(b => b.GetAttribute("data-tab-id") == "audit");
        auditTab.Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Alice Admin"));

        // Expandable row content is not rendered until toggled.
        cut.Markup.Should().NotContain("Entry ID:");

        cut.Find("button[aria-controls=details-7]").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Entry ID:");
            cut.Markup.Should().Contain("/Admin/AuditLogs/Details/7");
        });
    }
}
