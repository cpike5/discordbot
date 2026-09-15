using Microsoft.Extensions.DependencyInjection;
using Bunit;
using Discord.WebSocket;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Moq;
using MembersIndex = DiscordBot.Bot.Blazor.Pages.Guilds.Members.Index;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds.Members;

/// <summary>
/// Component tests for <see cref="Index"/> (Guilds/Members), the routable replacement for
/// <c>Pages/Guilds/Members/Index.cshtml</c> + <c>IndexModel</c> + <c>_MemberDetailModal.cshtml</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d).
/// </summary>
public class IndexTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;

    private readonly Mock<IGuildContextProvider> _contextProvider = new();
    private readonly Mock<IGuildMemberService> _memberService = new();
    private readonly Mock<DiscordSocketClient> _discordClient = new();

    public IndexTests()
    {
        Services.AddScoped(_ => _contextProvider.Object);
        Services.AddSingleton(_memberService.Object);
        Services.AddSingleton(_discordClient.Object);

        AddBunitPersistentComponentState();
        AddAuthorizedAdmin();

        _contextProvider.Setup(p => p.GetAsync(GuildId, It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
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

        _memberService.Setup(s => s.GetMemberCountAsync(GuildId, It.IsAny<GuildMemberQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    private static GuildMemberDto Member(ulong userId, string username) => new()
    {
        UserId = userId,
        Username = username,
        JoinedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private void SetupMembers(params GuildMemberDto[] members)
    {
        _memberService.Setup(s => s.GetMembersAsync(GuildId, It.IsAny<GuildMemberQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginatedResponseDto<GuildMemberDto>
            {
                Items = members,
                Page = 1,
                PageSize = 25,
                TotalCount = members.Length
            });
    }

    private IRenderedComponent<MembersIndex> RenderPage()
    {
        SetInteractiveRendererInfo();
        return Render<MembersIndex>(p => p.Add(x => x.GuildId, (long)GuildId));
    }

    [Fact]
    public void NoMembers_RendersEmptyState()
    {
        SetupMembers();

        var cut = RenderPage();

        cut.Markup.Should().Contain("No members yet");
    }

    [Fact]
    public void Members_RenderInTable()
    {
        SetupMembers(Member(1, "alice"), Member(2, "bob"));

        var cut = RenderPage();

        cut.Markup.Should().Contain("alice").And.Contain("bob");
        cut.FindAll("[data-testid='member-row']").Should().HaveCount(2);
    }

    [Fact]
    public void SelectingAMemberCheckbox_ShowsBulkToolbar()
    {
        SetupMembers(Member(1, "alice"));
        var cut = RenderPage();

        var checkbox = cut.Find("[data-testid='member-row'] input[type=checkbox]");
        checkbox.Change(true);

        cut.Markup.Should().Contain("1 member(s) selected");
    }

    [Fact]
    public void ViewButton_OpensModal_AndLoadsMemberViaService()
    {
        SetupMembers(Member(1, "alice"));
        _memberService.Setup(s => s.GetMemberAsync(GuildId, 1UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildMemberDto { UserId = 1, Username = "alice", Nickname = "Al", JoinedAt = DateTime.UtcNow });

        var cut = RenderPage();
        cut.Find("[data-testid='member-row'] button").Click();

        cut.Find("#memberDetailModal").Should().NotBeNull();
        cut.Markup.Should().Contain("Al");
        _memberService.Verify(s => s.GetMemberAsync(GuildId, 1UL, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void LoadFailure_RendersAlert_NotEmptyState()
    {
        _memberService.Setup(s => s.GetMembersAsync(GuildId, It.IsAny<GuildMemberQueryDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = RenderPage();

        cut.Markup.Should().Contain("Couldn't load members");
    }
}
