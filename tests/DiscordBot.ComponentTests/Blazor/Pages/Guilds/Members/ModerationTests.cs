using System.Security.Claims;
using Bunit;
using Discord.WebSocket;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Pages.Guilds.Members;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds.Members;

/// <summary>
/// Component tests for <see cref="Moderation"/> (Guilds/Members/{userId}/Moderation), the
/// routable replacement for <c>Pages/Guilds/Members/Moderation.cshtml</c> + <c>ModerationModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d).
/// </summary>
public class ModerationTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;
    private const ulong UserId = 987654321098765432UL;
    private const ulong CurrentUserId = 111111111111111111UL;

    private readonly Mock<IGuildContextProvider> _contextProvider = new();
    private readonly Mock<IGuildMemberService> _memberService = new();
    private readonly Mock<IModerationService> _moderationService = new();
    private readonly Mock<IModNoteService> _modNoteService = new();
    private readonly Mock<IModTagService> _modTagService = new();
    private readonly Mock<IFlaggedEventService> _flaggedEventService = new();
    private readonly Mock<DiscordSocketClient> _discordClient = new();

    public ModerationTests()
    {
        Services.AddScoped(_ => _contextProvider.Object);
        Services.AddSingleton(_memberService.Object);
        Services.AddSingleton(_moderationService.Object);
        Services.AddSingleton(_modNoteService.Object);
        Services.AddSingleton(_modTagService.Object);
        Services.AddSingleton(_flaggedEventService.Object);
        Services.AddSingleton(_discordClient.Object);

        AddBunitPersistentComponentState();
        AddAuthorization().SetAuthorized("admin").SetClaims(
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("discord:user_id", CurrentUserId.ToString()));

        _contextProvider.Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Ok(new GuildContext(
                Guild: new GuildDto { Id = GuildId, Name = "Test Guild" },
                GuildId: GuildId,
                GuildIdString: GuildId.ToString(),
                IsAppAdmin: true,
                IsGuildAdmin: false,
                CanEdit: true,
                AudioEnabled: true,
                RatWatchEnabled: true,
                Tabs: DiscordBot.Bot.Configuration.GuildNavigationConfig.GetTabs())));

        _moderationService.Setup(s => s.GetUserCasesAsync(GuildId, UserId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<ModerationCaseDto>(), 0));
        _flaggedEventService.Setup(s => s.GetUserEventsAsync(GuildId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FlaggedEventDto>());
        _modTagService.Setup(s => s.GetGuildTagsAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ModTagDto { Id = Guid.NewGuid(), GuildId = GuildId, Name = "VIP", Category = TagCategory.Positive }]);
    }

    private void SetupMember(GuildMemberDto? member) =>
        _memberService.Setup(s => s.GetMemberAsync(GuildId, UserId, It.IsAny<CancellationToken>())).ReturnsAsync(member);

    private void SetupNotes(params ModNoteDto[] notes) =>
        _modNoteService.Setup(s => s.GetNotesAsync(GuildId, UserId, It.IsAny<CancellationToken>())).ReturnsAsync(notes);

    private void SetupTags(params UserModTagDto[] tags) =>
        _modTagService.Setup(s => s.GetUserTagsAsync(GuildId, UserId, It.IsAny<CancellationToken>())).ReturnsAsync(tags);

    private IRenderedComponent<Moderation> RenderPage()
    {
        SetInteractiveRendererInfo();
        return Render<Moderation>(p => p
            .Add(x => x.GuildId, (long)GuildId)
            .Add(x => x.UserId, (long)UserId));
    }

    [Fact]
    public void MemberNotFound_RendersEmptyState()
    {
        SetupMember(null);
        SetupNotes();
        SetupTags();

        var cut = RenderPage();

        cut.Markup.Should().Contain("Member not found");
    }

    [Fact]
    public void MemberFound_RendersNameAndTabCounts()
    {
        SetupMember(new GuildMemberDto { UserId = UserId, Username = "alice", Nickname = "Al", JoinedAt = DateTime.UtcNow });
        SetupNotes(new ModNoteDto { Id = Guid.NewGuid(), AuthorUserId = CurrentUserId, AuthorUsername = "admin", Content = "hi", CreatedAt = DateTime.UtcNow });
        SetupTags();

        var cut = RenderPage();

        cut.Markup.Should().Contain("Al");
        cut.Find("[data-tab-id='notes']").TextContent.Should().Contain("1");
    }

    [Fact]
    public void AddNote_CallsServiceWithCurrentUserId_AndRefreshesListInPlace()
    {
        SetupMember(new GuildMemberDto { UserId = UserId, Username = "alice", JoinedAt = DateTime.UtcNow });
        SetupNotes();
        SetupTags();
        _modNoteService.Setup(s => s.AddNoteAsync(GuildId, UserId, "A new note", CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModNoteDto { Id = Guid.NewGuid(), AuthorUserId = CurrentUserId, Content = "A new note", CreatedAt = DateTime.UtcNow });

        var cut = RenderPage();
        cut.Find("[data-tab-id='notes']").Click();
        cut.Find("textarea").Input("A new note");
        cut.FindAll("button").First(b => b.TextContent.Contains("Add Note")).Click();

        cut.WaitForAssertion(() => _modNoteService.Verify(
            s => s.AddNoteAsync(GuildId, UserId, "A new note", CurrentUserId, It.IsAny<CancellationToken>()), Times.Once));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("A new note"));
    }

    [Fact]
    public void OwnNote_ShowsDeleteButton_OtherAuthorNote_DoesNot()
    {
        SetupMember(new GuildMemberDto { UserId = UserId, Username = "alice", JoinedAt = DateTime.UtcNow });
        SetupNotes(
            new ModNoteDto { Id = Guid.NewGuid(), AuthorUserId = CurrentUserId, AuthorUsername = "admin", Content = "mine", CreatedAt = DateTime.UtcNow },
            new ModNoteDto { Id = Guid.NewGuid(), AuthorUserId = 42, AuthorUsername = "other-mod", Content = "theirs", CreatedAt = DateTime.UtcNow });
        SetupTags();

        var cut = RenderPage();
        cut.Find("[data-tab-id='notes']").Click();

        cut.FindAll(".note-item button").Should().HaveCount(1);
    }

    [Fact]
    public void AddTag_CallsServiceWithAppliedByCurrentUserId()
    {
        SetupMember(new GuildMemberDto { UserId = UserId, Username = "alice", JoinedAt = DateTime.UtcNow });
        SetupNotes();
        SetupTags();
        _modTagService.Setup(s => s.ApplyTagAsync(GuildId, UserId, "VIP", CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserModTagDto { Id = Guid.NewGuid(), UserId = UserId, TagName = "VIP", AppliedByUserId = CurrentUserId });

        var cut = RenderPage();
        cut.Find("#addTagSelect").Change("VIP");

        cut.WaitForAssertion(() => _modTagService.Verify(
            s => s.ApplyTagAsync(GuildId, UserId, "VIP", CurrentUserId, It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public void RemoveTag_RequiresConfirmation_ThenCallsService()
    {
        SetupMember(new GuildMemberDto { UserId = UserId, Username = "alice", JoinedAt = DateTime.UtcNow });
        SetupNotes();
        SetupTags(new UserModTagDto { Id = Guid.NewGuid(), UserId = UserId, TagName = "VIP", TagCategory = TagCategory.Positive, AppliedByUserId = CurrentUserId });
        _modTagService.Setup(s => s.RemoveTagAsync(GuildId, UserId, "VIP", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var cut = RenderPage();
        cut.Find(".user-tag-removable").Click();
        cut.Find("#confirmRemoveTagModal").QuerySelectorAll("button").Last().Click();

        cut.WaitForAssertion(() => _modTagService.Verify(
            s => s.RemoveTagAsync(GuildId, UserId, "VIP", It.IsAny<CancellationToken>()), Times.Once));
    }
}
