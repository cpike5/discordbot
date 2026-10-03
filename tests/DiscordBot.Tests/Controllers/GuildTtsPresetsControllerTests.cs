using System.Reflection;
using System.Security.Claims;
using DiscordBot.Bot.Controllers;
using DiscordBot.Bot.Services.Tts;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DiscordBot.Tests.Controllers;

/// <summary>
/// The admin Text-to-Speech page saves custom presets here instead of at the member portal
/// endpoint, which refuses everyone while the guild's member portal is off.
/// </summary>
public class GuildTtsPresetsControllerTests
{
    private const ulong AdminDiscordId = 111222333444555666;
    private const ulong GuildId = 987654321098765432;

    private readonly Mock<IUserTtsPresetRepository> _repository = new();
    private readonly GuildTtsPresetsController _controller;

    public GuildTtsPresetsControllerTests()
    {
        var service = new CustomTtsPresetService(_repository.Object, NullLogger<CustomTtsPresetService>.Instance);
        _controller = new GuildTtsPresetsController(service);
        SignInAs(AdminDiscordId);
    }

    private void SignInAs(ulong? discordId)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "admin@example.com") };
        if (discordId.HasValue) claims.Add(new Claim("discord:user_id", discordId.Value.ToString()));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };
    }

    [Fact]
    public void Controller_RequiresAnAdminWithAccessToTheGuild_NotTheMemberPortalPolicy()
    {
        var policies = typeof(GuildTtsPresetsController).GetCustomAttributes<AuthorizeAttribute>()
            .Select(a => a.Policy).ToList();

        policies.Should().Contain("RequireAdmin").And.Contain("GuildAccess");
        policies.Should().NotContain("PortalGuildMember");
    }

    [Fact]
    public void Controller_RoutesOnTheGuildId_SoGuildAccessCanCheckIt()
    {
        var route = typeof(GuildTtsPresetsController).GetCustomAttribute<RouteAttribute>();

        route!.Template.Should().Be("api/guilds/{guildId}/tts/presets/custom");
    }

    [Fact]
    public async Task Create_SavesThePresetUnderTheAdminsDiscordAccount()
    {
        UserTtsPreset? saved = null;
        _repository.Setup(r => r.GetCountByUserIdAsync(AdminDiscordId, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _repository.Setup(r => r.AddAsync(It.IsAny<UserTtsPreset>(), It.IsAny<CancellationToken>()))
            .Callback<UserTtsPreset, CancellationToken>((p, _) => { saved = p; p.Id = 7; })
            .ReturnsAsync((UserTtsPreset p, CancellationToken _) => p);

        var result = await _controller.CreateCustomPreset(GuildId,
            new PortalTtsPresetsController.CreateCustomPresetRequest { Name = "  Narrator  ", VoiceName = "en-US-JennyNeural", Speed = 5, Pitch = 1 },
            CancellationToken.None);

        var created = result.Should().BeOfType<ObjectResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        saved.Should().NotBeNull();
        saved!.UserId.Should().Be(AdminDiscordId);
        saved.Name.Should().Be("Narrator");
        saved.Speed.Should().Be(2.0m, "the speed is clamped to the supported range");
    }

    [Fact]
    public async Task Create_WithoutALinkedDiscordAccount_SaysSoInPlainWords()
    {
        SignInAs(null);

        var result = await _controller.CreateCustomPreset(GuildId,
            new PortalTtsPresetsController.CreateCustomPresetRequest { Name = "x", VoiceName = "v" }, CancellationToken.None);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = bad.Value.Should().BeOfType<ApiErrorDto>().Subject;
        error.ErrorCode.Should().Be("discord_link_required");
        error.Message.Should().Contain("Link your Discord account");
        _repository.Verify(r => r.AddAsync(It.IsAny<UserTtsPreset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_AtTheLimit_IsRefusedWithTheLimitCode()
    {
        _repository.Setup(r => r.GetCountByUserIdAsync(AdminDiscordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CustomTtsPresetService.MaxPresetsPerUser);

        var result = await _controller.CreateCustomPreset(GuildId,
            new PortalTtsPresetsController.CreateCustomPresetRequest { Name = "one more", VoiceName = "v" }, CancellationToken.None);

        var error = result.Should().BeOfType<BadRequestObjectResult>().Subject.Value.Should().BeOfType<ApiErrorDto>().Subject;
        error.ErrorCode.Should().Be("preset_limit_reached");
    }

    [Theory]
    [InlineData("", "v", "Preset name is required")]
    [InlineData("   ", "v", "Preset name is required")]
    [InlineData("ok", "", "Voice name is required")]
    public async Task Create_WithAMissingField_ExplainsWhich(string name, string voice, string message)
    {
        var result = await _controller.CreateCustomPreset(GuildId,
            new PortalTtsPresetsController.CreateCustomPresetRequest { Name = name, VoiceName = voice }, CancellationToken.None);

        var error = result.Should().BeOfType<BadRequestObjectResult>().Subject.Value.Should().BeOfType<ApiErrorDto>().Subject;
        error.Message.Should().Be(message);
    }

    [Fact]
    public async Task Get_ListsTheAdminsPresets()
    {
        _repository.Setup(r => r.GetByUserIdAsync(AdminDiscordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserTtsPreset> { new() { Id = 1, UserId = AdminDiscordId, Name = "A", VoiceName = "v", Speed = 1m, Pitch = 1m } });

        var result = await _controller.GetCustomPresets(GuildId, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IEnumerable<object>>().Which.Should().HaveCount(1);
    }

    [Fact]
    public async Task Delete_OnlyRemovesThePresetsTheAdminOwns()
    {
        _repository.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserTtsPreset { Id = 5, UserId = 42, Name = "someone else's", VoiceName = "v" });

        var result = await _controller.DeleteCustomPreset(GuildId, 5, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(r => r.DeleteAsync(It.IsAny<UserTtsPreset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_RemovesTheAdminsOwnPreset()
    {
        var preset = new UserTtsPreset { Id = 6, UserId = AdminDiscordId, Name = "mine", VoiceName = "v" };
        _repository.Setup(r => r.GetByIdAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(preset);

        var result = await _controller.DeleteCustomPreset(GuildId, 6, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _repository.Verify(r => r.DeleteAsync(preset, It.IsAny<CancellationToken>()), Times.Once);
    }
}
