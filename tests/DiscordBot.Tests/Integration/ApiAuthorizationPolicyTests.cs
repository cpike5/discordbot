using System.Net;
using System.Reflection;
using DiscordBot.Bot.Controllers;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// Authorization wiring checked against the real app (offline mode, PostgreSQL): every API action
/// keyed by a guild in its route carries a guild policy, and every policy name the assembly asks for
/// is registered.
/// </summary>
[Collection(OfflineAppHostCollection.Name)]
public class ApiAuthorizationPolicyTests : IClassFixture<ApiAuthorizationPolicyTests.AppFixture>
{
    /// <summary>
    /// Actions whose route carries a guild id but which deliberately do not use a guild policy.
    /// Each entry is "Controller.Action" and says why. Keep this list short and justified.
    /// </summary>
    private static readonly HashSet<string> GuildRouteExceptions = new(StringComparer.Ordinal)
    {
        // None today. Add "ControllerName.ActionName" with a comment explaining why the action
        // is safe without GuildAccess or PortalGuildMember.
    };

    private static readonly string[] GuildPolicies = { "GuildAccess", "PortalGuildMember" };

    private readonly AppFixture _app;

    public ApiAuthorizationPolicyTests(AppFixture app)
    {
        _app = app;
    }

    [Fact]
    public void EveryApiActionWithAGuildIdInItsRoute_CarriesAGuildPolicy()
    {
        var provider = _app.Host.Services.GetRequiredService<IActionDescriptorCollectionProvider>();

        var missing = provider.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(a => a.AttributeRouteInfo?.Template?.Contains("{guildId", StringComparison.OrdinalIgnoreCase) == true)
            .Where(a => !GuildRouteExceptions.Contains($"{a.ControllerName}.{a.ActionName}"))
            .Where(a => !a.EndpointMetadata.OfType<IAuthorizeData>().Any(d => GuildPolicies.Contains(d.Policy)))
            .Select(a => $"{a.ControllerTypeInfo.Name}.{a.ActionName} ({a.AttributeRouteInfo!.Template})")
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        missing.Should().BeEmpty(
            "an action keyed by a guild must check the caller's access to that guild (GuildAccess) " +
            "or portal membership (PortalGuildMember), or be listed in GuildRouteExceptions with a reason");
    }

    [Fact]
    public async Task EveryAuthorizePolicyNameInTheBotAssembly_IsRegistered()
    {
        var assembly = typeof(AudioController).Assembly;
        const BindingFlags members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var policyNames = assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes<AuthorizeAttribute>(inherit: false)
                .Concat(t.GetMethods(members).SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>(inherit: false))))
            .Select(a => a.Policy)
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct()
            .ToList();

        policyNames.Should().NotBeEmpty();

        var policyProvider = _app.Host.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var unknown = new List<string>();
        foreach (var name in policyNames)
        {
            if (await policyProvider.GetPolicyAsync(name!) == null)
            {
                unknown.Add(name!);
            }
        }

        unknown.Should().BeEmpty("an [Authorize(Policy = ...)] naming an unregistered policy throws on every request");
    }

    [Fact]
    public async Task Viewer_WhoIsNotInTheGuild_CannotDriveTheBotsVoice()
    {
        using var viewer = await _app.Host.CreateSignedInClientAsync("audio-viewer@example.com", "Viewer");

        var response = await viewer.PostAsync($"/api/guilds/{OfflineAppHost.GuildId}/audio/stop", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "Viewer is a global role; the guild itself must be checked");
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    public sealed class AppFixture : IAsyncLifetime
    {
        public OfflineAppHost Host { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            Host = await OfflineAppHost.StartAsync();
        }

        public async Task DisposeAsync()
        {
            await Host.DisposeAsync();
        }
    }
}
