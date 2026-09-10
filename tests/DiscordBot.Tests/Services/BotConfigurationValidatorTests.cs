using DiscordBot.Bot.Services;
using FluentAssertions;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="BotConfigurationValidator"/>, which makes
/// <see cref="BotConfiguration.Token"/> required only when <see cref="BotConfiguration.Enabled"/>
/// is true, so the web portal can start without a Discord bot token in web-only mode
/// (Discord:Enabled=false).
/// </summary>
public class BotConfigurationValidatorTests
{
    private readonly BotConfigurationValidator _validator = new();

    [Fact]
    public void Validate_WhenEnabledAndTokenMissing_ShouldFail()
    {
        var options = new BotConfiguration { Enabled = true, Token = string.Empty };

        var result = _validator.Validate(name: null, options);

        result.Failed.Should().BeTrue("Token is required when Discord:Enabled is true");
        result.FailureMessage.Should().Contain("Discord:Token is required");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenEnabledAndTokenBlank_ShouldFail(string? token)
    {
        var options = new BotConfiguration { Enabled = true, Token = token! };

        var result = _validator.Validate(name: null, options);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenEnabledAndTokenSet_ShouldSucceed()
    {
        var options = new BotConfiguration { Enabled = true, Token = "test-token" };

        var result = _validator.Validate(name: null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenDisabledAndTokenMissing_ShouldSucceed()
    {
        var options = new BotConfiguration { Enabled = false, Token = string.Empty };

        var result = _validator.Validate(name: null, options);

        result.Succeeded.Should().BeTrue("Token is not required when running web-only (Discord:Enabled=false)");
    }

    [Fact]
    public void Validate_WhenDisabledAndTokenSet_ShouldSucceed()
    {
        var options = new BotConfiguration { Enabled = false, Token = "leftover-token" };

        var result = _validator.Validate(name: null, options);

        result.Succeeded.Should().BeTrue("a configured token is still allowed when Enabled is false");
    }
}
