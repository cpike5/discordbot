using AspNet.Security.OAuth.Discord;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace DiscordBot.Tests.Bot.Extensions;

/// <summary>
/// Tests for the Discord OAuth half of <see cref="IdentityServiceExtensions.AddIdentityServices"/>:
/// credentials are mandatory unless <c>Discord:OfflineMode</c> is on, in which case a missing pair
/// leaves the Discord scheme unregistered and the login page offers password sign-in only.
/// </summary>
public class IdentityServiceExtensionsTests
{
    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        settings.TryAdd("DataProtection:KeyPath", Path.Combine(Path.GetTempPath(), "discordbot-tests-dp-keys"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityServices(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task OfflineMode_WithoutOAuthCredentials_SkipsDiscordScheme()
    {
        using var provider = BuildProvider(new() { ["Discord:OfflineMode"] = "true" });

        provider.GetRequiredService<DiscordOAuthSettings>().IsConfigured.Should().BeFalse();
        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        (await schemes.GetSchemeAsync(DiscordAuthenticationDefaults.AuthenticationScheme)).Should().BeNull();
    }

    [Fact]
    public async Task OfflineMode_WithOAuthCredentials_KeepsDiscordScheme()
    {
        using var provider = BuildProvider(new()
        {
            ["Discord:OfflineMode"] = "true",
            ["Discord:OAuth:ClientId"] = "client-id",
            ["Discord:OAuth:ClientSecret"] = "client-secret"
        });

        provider.GetRequiredService<DiscordOAuthSettings>().IsConfigured.Should().BeTrue();
        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        (await schemes.GetSchemeAsync(DiscordAuthenticationDefaults.AuthenticationScheme)).Should().NotBeNull();
    }

    [Fact]
    public void WithoutOfflineMode_MissingOAuthCredentials_FailValidation()
    {
        using var provider = BuildProvider(new());

        var act = () => provider.GetRequiredService<IOptions<DiscordBot.Core.Configuration.DiscordOAuthOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }
}
