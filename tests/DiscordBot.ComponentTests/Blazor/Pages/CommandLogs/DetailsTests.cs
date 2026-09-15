using Bunit;
using DiscordBot.Bot.Blazor.Pages.CommandLogs;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.CommandLogs;

/// <summary>
/// Component tests for <see cref="Details"/>, the routable replacement for
/// <c>Pages/CommandLogs/Details.cshtml</c> + <c>DetailsModel</c>
/// (docs/plans/blazor-port-plan.md Phase 4 cluster 4a). Covers: every card, the not-found
/// EmptyState, the error alert, the guild link fidelity fix, and copy-to-clipboard toasts.
/// </summary>
public class DetailsTests : BlazorComponentTestContext
{
    private readonly Mock<ICommandLogService> _commandLogService = new();

    public DetailsTests()
    {
        Services.AddSingleton(_commandLogService.Object);
    }

    private static CommandLogDto BuildLog(Guid id, bool success = true, ulong? guildId = 42, string? errorMessage = null) => new()
    {
        Id = id,
        GuildId = guildId,
        GuildName = guildId.HasValue ? "Test Guild" : null,
        UserId = 555666777UL,
        Username = "alice",
        CommandName = "ping",
        Parameters = "{\"foo\":\"bar\"}",
        ExecutedAt = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
        ResponseTimeMs = 42,
        Success = success,
        ErrorMessage = errorMessage
    };

    private IRenderedComponent<Details> RenderDetails(Guid id)
    {
        AddBunitPersistentComponentState();
        return Render<Details>(p => p.Add(c => c.Id, id));
    }

    [Fact]
    public void SuccessfulLog_RendersAllCardsWithSuccessBadge()
    {
        var id = Guid.NewGuid();
        _commandLogService.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(BuildLog(id));

        var cut = RenderDetails(id);

        cut.Markup.Should().Contain("Success");
        cut.Markup.Should().Contain("Basic Information");
        cut.Markup.Should().Contain("Server Information");
        cut.Markup.Should().Contain("User Information");
        cut.Markup.Should().Contain("Command Details");
        cut.Markup.Should().Contain("Test Guild");
        cut.Markup.Should().Contain("alice");
        cut.Markup.Should().Contain("/Guilds/Details/42");
    }

    [Fact]
    public void FailedLog_RendersErrorAlertAndFailedBadge()
    {
        var id = Guid.NewGuid();
        _commandLogService.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(BuildLog(id, success: false, errorMessage: "Boom"));

        var cut = RenderDetails(id);

        cut.Markup.Should().Contain("Failed");
        cut.Markup.Should().Contain("Boom");
        cut.Find("[role='alert']").Should().NotBeNull();
    }

    [Fact]
    public void LogNotFound_RendersEmptyState()
    {
        var id = Guid.NewGuid();
        _commandLogService.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((CommandLogDto?)null);

        var cut = RenderDetails(id);

        cut.Markup.Should().Contain("Command Log Not Found");
        cut.Markup.Should().NotContain("Basic Information");
    }

    [Fact]
    public void CopyLogId_InvokesClipboardInterop()
    {
        var id = Guid.NewGuid();
        _commandLogService.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(BuildLog(id));
        var copyHandler = JSInterop.SetupModule("./js/blazor/browser.js").Setup<bool>("copyToClipboard", _ => true);
        copyHandler.SetResult(true);

        var cut = RenderDetails(id);
        cut.Find("[aria-label='Copy Log ID']").Click();

        copyHandler.Invocations.Should().ContainSingle(inv => (string)inv.Arguments[0]! == id.ToString());
    }

    /// <summary>
    /// Declared route authorization: kept at <c>RequireModerator</c>, broader than the
    /// <c>RequireAdmin</c> the other two cluster 4a Details pages use - see the sibling note on
    /// <c>AuditLogs.DetailsTests</c>.
    /// </summary>
    [Fact]
    public void Page_RequiresModeratorPolicy()
    {
        var attribute = typeof(Details).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should().ContainSingle().Subject;

        attribute.Policy.Should().Be("RequireModerator");
    }
}
