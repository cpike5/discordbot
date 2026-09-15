using System.Security.Claims;
using Bunit;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Configuration;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using DetailsPage = DiscordBot.Bot.Blazor.Pages.Guilds.FeatureRequests.Details;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds.FeatureRequests;

/// <summary>
/// Component tests for <see cref="DetailsPage"/>, the routable replacement for
/// <c>Pages/Guilds/FeatureRequests/Details.cshtml</c> + <c>DetailsModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b). Covers the review-action gating by
/// status and the Approve/Reject service calls.
/// </summary>
public class DetailsTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;
    private static readonly Guid RequestId = Guid.NewGuid();

    private readonly Mock<IFeatureRequestService> _service = new();

    public DetailsTests()
    {
        Services.AddSingleton(_service.Object);
    }

    private static GuildContext Context() => new(
        Guild: new GuildDto { Id = GuildId, Name = "Test Guild" },
        GuildId: GuildId,
        GuildIdString: GuildId.ToString(),
        IsAppAdmin: true,
        IsGuildAdmin: false,
        CanEdit: true,
        AudioEnabled: true,
        RatWatchEnabled: true,
        Tabs: GuildNavigationConfig.GetTabs());

    private void RegisterMockProvider()
    {
        var mock = new Mock<IGuildContextProvider>();
        mock.Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Ok(Context()));
        Services.AddScoped(_ => mock.Object);
    }

    private static FeatureRequest BuildRequest(FeatureRequestStatus status) => new()
    {
        Id = RequestId,
        GuildId = GuildId,
        SubmittedByUserId = 111,
        Description = "Add a /vote command",
        Status = status,
        CreatedAt = DateTime.UtcNow
    };

    private IRenderedComponent<DetailsPage> RenderDetails(FeatureRequestStatus status)
    {
        AddAuthorization().SetAuthorized("admin").SetClaims(
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("discord:user_id", "999"));
        AddBunitPersistentComponentState();
        RegisterMockProvider();
        _service.Setup(s => s.GetByIdAsync(RequestId)).ReturnsAsync(BuildRequest(status));
        SetInteractiveRendererInfo();

        return Render<DetailsPage>(p => p
            .Add(x => x.GuildId, (long)GuildId)
            .Add(x => x.Id, RequestId));
    }

    [Fact]
    public void Submitted_ShowsReviewNotesAndApproveReject()
    {
        var cut = RenderDetails(FeatureRequestStatus.Submitted);

        cut.Markup.Should().Contain("Approve").And.Contain("Reject").And.Contain("Review Notes");
    }

    [Fact]
    public void DocGenFailed_ShowsApproveAnyway()
    {
        var cut = RenderDetails(FeatureRequestStatus.DocGenFailed);

        cut.Markup.Should().Contain("Approve Anyway");
    }

    [Fact]
    public void Approved_ShowsReadOnlyMessage_NoActions()
    {
        var cut = RenderDetails(FeatureRequestStatus.Approved);

        cut.FindAll("button").Should().BeEmpty();
        cut.Markup.Should().Contain("approved");
    }

    [Fact]
    public void Approve_CallsUpdateStatusAsync_WithApprovedAndReviewerClaim()
    {
        var cut = RenderDetails(FeatureRequestStatus.Submitted);
        _service.Setup(s => s.UpdateStatusAsync(RequestId, FeatureRequestStatus.Approved, 999UL, null))
            .Returns(Task.CompletedTask);
        _service.Setup(s => s.GetByIdAsync(RequestId)).ReturnsAsync(BuildRequest(FeatureRequestStatus.Approved));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Approve").Click();

        cut.WaitForAssertion(() => _service.Verify(
            s => s.UpdateStatusAsync(RequestId, FeatureRequestStatus.Approved, 999UL, null), Times.Once));
    }

    [Fact]
    public void Reject_CallsUpdateStatusAsync_WithRejected()
    {
        var cut = RenderDetails(FeatureRequestStatus.Submitted);
        _service.Setup(s => s.UpdateStatusAsync(RequestId, FeatureRequestStatus.Rejected, 999UL, null))
            .Returns(Task.CompletedTask);
        _service.Setup(s => s.GetByIdAsync(RequestId)).ReturnsAsync(BuildRequest(FeatureRequestStatus.Rejected));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Reject").Click();

        cut.WaitForAssertion(() => _service.Verify(
            s => s.UpdateStatusAsync(RequestId, FeatureRequestStatus.Rejected, 999UL, null), Times.Once));
    }

    [Fact]
    public void GuildMismatch_RendersNotFound()
    {
        AddAuthorization().SetAuthorized("admin").SetClaims(new Claim(ClaimTypes.Role, "Admin"));
        AddBunitPersistentComponentState();
        RegisterMockProvider();
        var mismatched = BuildRequest(FeatureRequestStatus.Submitted);
        mismatched.GuildId = GuildId + 1;
        _service.Setup(s => s.GetByIdAsync(RequestId)).ReturnsAsync(mismatched);
        SetInteractiveRendererInfo();

        var cut = Render<DetailsPage>(p => p.Add(x => x.GuildId, (long)GuildId).Add(x => x.Id, RequestId));

        cut.Markup.Should().Contain("not found");
    }
}
