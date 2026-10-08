using System.Net;
using System.Security.Claims;
using DiscordBot.Bot.Extensions;
using DiscordBot.Core.Configuration;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DiscordBot.Tests.Bot.Extensions;

/// <summary>
/// The portal rate-limit policies, run through the real rate-limiting middleware on a minimal host.
/// </summary>
public class RateLimitingServiceExtensionsTests
{
    [Fact]
    public void Defaults_AllowNormalUse()
    {
        var options = new PortalRateLimitOptions();

        options.Upload.PermitLimit.Should().BeGreaterThan(1);
        options.Upload.WindowSeconds.Should().BeGreaterThan(0);
        options.Play.PermitLimit.Should().BeGreaterThan(options.Upload.PermitLimit, "a soundboard is clicked far more often than it is uploaded to");
        options.Play.WindowSeconds.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(PortalRateLimitPolicies.Upload, "PortalRateLimit:Upload")]
    [InlineData(PortalRateLimitPolicies.Play, "PortalRateLimit:Play")]
    public async Task Policy_RefusesTheRequestPastTheLimit_With429ProblemJson(string policy, string section)
    {
        await using var app = await StartAsync(policy, new Dictionary<string, string?>
        {
            [$"{section}:PermitLimit"] = "2",
            [$"{section}:WindowSeconds"] = "600"
        });
        var client = app.GetTestClient();

        (await client.PostAsync("/limited?user=111", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync("/limited?user=111", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var third = await client.PostAsync("/limited?user=111", null);

        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        third.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Policy_CountsEachUserSeparately()
    {
        await using var app = await StartAsync(PortalRateLimitPolicies.Upload, new Dictionary<string, string?>
        {
            ["PortalRateLimit:Upload:PermitLimit"] = "1",
            ["PortalRateLimit:Upload:WindowSeconds"] = "600"
        });
        var client = app.GetTestClient();

        (await client.PostAsync("/limited?user=111", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync("/limited?user=111", null)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await client.PostAsync("/limited?user=222", null)).StatusCode.Should().Be(HttpStatusCode.OK,
            "one member using up their uploads must not lock out another");
    }

    [Fact]
    public void PartitionKey_PrefersTheDiscordUserId_ThenTheIdentityUser_ThenTheAddress()
    {
        var discord = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("discord:user_id", "123456789012345678"),
                new Claim(ClaimTypes.NameIdentifier, "identity-id")
            }, "test"))
        };
        var identityOnly = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "identity-id") }, "test"))
        };
        var anonymous = new DefaultHttpContext();
        anonymous.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.7");

        RateLimitingServiceExtensions.GetPartitionKey(discord).Should().Be("discord:123456789012345678");
        RateLimitingServiceExtensions.GetPartitionKey(identityOnly).Should().Be("user:identity-id");
        RateLimitingServiceExtensions.GetPartitionKey(anonymous).Should().Be("ip:10.0.0.7");
    }

    private static async Task<WebApplication> StartAsync(string policy, Dictionary<string, string?> settings)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddPortalRateLimiting(builder.Configuration);

        var app = builder.Build();

        // Stand-in for authentication: the query string names the signed-in Discord user
        app.Use((context, next) =>
        {
            var user = context.Request.Query["user"].ToString();
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("discord:user_id", user) }, "test"));
            return next();
        });
        app.UseRateLimiter();
        app.MapPost("/limited", () => Results.Ok()).RequireRateLimiting(policy);

        await app.StartAsync();
        return app;
    }
}
