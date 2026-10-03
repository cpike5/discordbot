using DiscordBot.Bot.Pages.Guilds.FeatureRequests;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Guilds.FeatureRequests;

/// <summary>
/// Tests for the feature request details page: names instead of ids, a required reason on reject,
/// and a retry for a failed documentation run.
/// </summary>
public class DetailsModelTests
{
    private const ulong GuildId = 123456789UL;
    private static readonly Guid RequestId = Guid.NewGuid();

    private readonly Mock<IFeatureRequestService> _service = new();
    private readonly Mock<IGuildService> _guilds = new();
    private readonly Mock<IDiscordUserResolver> _users = new();
    private readonly DetailsModel _model;

    public DetailsModelTests()
    {
        _guilds
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildDto { Id = GuildId, Name = "Test Server", IsActive = true });
        _service
            .Setup(s => s.GetByIdAsync(RequestId))
            .ReturnsAsync(new FeatureRequest
            {
                Id = RequestId,
                GuildId = GuildId,
                SubmittedByUserId = 42UL,
                ReviewedByUserId = 43UL,
                Description = "Add dark mode",
                Status = FeatureRequestStatus.DocGenFailed
            });

        _model = new DetailsModel(_service.Object, _guilds.Object, _users.Object, Mock.Of<ILogger<DetailsModel>>());

        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary());
        _model.PageContext = new PageContext(actionContext);
        _model.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    [Fact]
    public async Task OnGet_ShowsTheSubmitterAndReviewerByName()
    {
        _users
            .Setup(u => u.ResolveUsersAsync(It.IsAny<IEnumerable<ulong>>()))
            .ReturnsAsync(new Dictionary<ulong, (string Username, string? AvatarUrl)>
            {
                [42] = ("alice", null),
                [43] = ("Unknown#43", null)
            });

        await _model.OnGetAsync(GuildId, RequestId);

        _model.SubmitterName.Should().Be("alice");
        _model.ReviewerName.Should().Be("Unknown user");
    }

    [Fact]
    public async Task OnGet_WhenNamesCannotBeResolved_StillRenders()
    {
        _users
            .Setup(u => u.ResolveUsersAsync(It.IsAny<IEnumerable<ulong>>()))
            .ThrowsAsync(new HttpRequestException("discord is down"));

        var result = await _model.OnGetAsync(GuildId, RequestId);

        result.Should().BeOfType<PageResult>();
        _model.SubmitterName.Should().Be("Unknown user");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task OnPostReject_WithoutAReason_DoesNotReject(string? notes)
    {
        _model.ReviewNotes = notes;

        var result = await _model.OnPostRejectAsync(GuildId, RequestId);

        result.Should().BeOfType<RedirectToPageResult>();
        _service.Verify(
            s => s.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<FeatureRequestStatus>(), It.IsAny<ulong?>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task OnPostReject_WithAReason_RejectsAndKeepsTheTrimmedReason()
    {
        _model.ReviewNotes = "  Out of scope for this server.  ";

        await _model.OnPostRejectAsync(GuildId, RequestId);

        _service.Verify(
            s => s.UpdateStatusAsync(RequestId, FeatureRequestStatus.Rejected, It.IsAny<ulong?>(), "Out of scope for this server."),
            Times.Once);
    }

    [Fact]
    public async Task OnPostRetryDocGen_QueuesTheRequestAgain()
    {
        _service.Setup(s => s.RequeueDocGenAsync(RequestId)).ReturnsAsync(true);

        var result = await _model.OnPostRetryDocGenAsync(GuildId, RequestId);

        result.Should().BeOfType<RedirectToPageResult>();
        _service.Verify(s => s.RequeueDocGenAsync(RequestId), Times.Once);
    }

    [Fact]
    public async Task OnPostRetryDocGen_ForARequestInAnotherGuild_IsNotFound()
    {
        var otherId = Guid.NewGuid();
        _service.Setup(s => s.GetByIdAsync(otherId)).ReturnsAsync(new FeatureRequest { Id = otherId, GuildId = 999UL });

        var result = await _model.OnPostRetryDocGenAsync(GuildId, otherId);

        result.Should().BeOfType<NotFoundResult>();
        _service.Verify(s => s.RequeueDocGenAsync(It.IsAny<Guid>()), Times.Never);
    }
}
