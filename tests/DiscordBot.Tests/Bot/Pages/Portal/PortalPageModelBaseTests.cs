using System.Security.Claims;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Pages.Portal;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Portal;

/// <summary>
/// The portal page base must make the same call as <c>PortalGuildMemberAuthorizationHandler</c>:
/// an admin gets in whether or not their account has a linked Discord account.
/// </summary>
public class PortalPageModelBaseTests
{
    private const ulong GuildId = 123456789UL;

    private readonly Mock<IGuildService> _guilds = new();
    private readonly Mock<IPortalGuildDirectory> _directory = new();
    private readonly Mock<IGuildAudioSettingsRepository> _audioSettings = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManager;

    public PortalPageModelBaseTests()
    {
        _userManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);

        _guilds.Setup(g => g.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildDto { Id = GuildId, Name = "Test" });
        _directory.Setup(d => d.IsGuildAvailableAsync(GuildId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _audioSettings.Setup(a => a.GetByGuildIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAudioSettings { GuildId = GuildId, AudioEnabled = true, EnableMemberPortal = true });
    }

    private TestPage Page(params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "identity-user"), new(ClaimTypes.Name, "someone") };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var page = new TestPage(_guilds.Object, _directory.Object, _audioSettings.Object, _userManager.Object);
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
        page.PageContext = new PageContext(new ActionContext(http, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary()));
        return page;
    }

    [Theory]
    [InlineData(IdentitySeeder.Roles.SuperAdmin)]
    [InlineData(IdentitySeeder.Roles.Admin)]
    public async Task AnAdminWithoutALinkedDiscordAccount_IsAuthorized(string role)
    {
        _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new ApplicationUser { Id = "identity-user", DiscordUserId = null });
        var page = Page(role);

        var result = await page.Check();

        result.Should().Be("Authorized");
        page.IsAuthorized.Should().BeTrue();
        page.IsAuthenticated.Should().BeTrue();
        _directory.Verify(d => d.IsMemberAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ANonAdminWithoutALinkedDiscordAccount_SeesTheLandingPage()
    {
        _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new ApplicationUser { Id = "identity-user", DiscordUserId = null });
        var page = Page(IdentitySeeder.Roles.Moderator);

        var result = await page.Check();

        result.Should().Be("ShowLandingPage");
        page.IsAuthorized.Should().BeFalse();
        page.IsAuthenticated.Should().BeFalse();
    }

    private sealed class TestPage : PortalPageModelBase
    {
        public TestPage(
            IGuildService guilds,
            IPortalGuildDirectory directory,
            IGuildAudioSettingsRepository audioSettings,
            UserManager<ApplicationUser> userManager)
            : base(guilds, directory, audioSettings, userManager, Mock.Of<ILogger>())
        {
        }

        public async Task<string> Check()
        {
            var (result, _) = await CheckPortalAuthorizationAsync(PortalPageModelBaseTests.GuildId, "Test");
            return result.ToString();
        }
    }
}
