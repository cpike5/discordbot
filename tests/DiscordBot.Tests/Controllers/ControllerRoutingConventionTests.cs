using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace DiscordBot.Tests.Controllers;

/// <summary>
/// Guards against action routes that escape their controller's prefix.
/// </summary>
public class ControllerRoutingConventionTests
{
    /// <summary>
    /// An action carrying both <c>[Route("...")]</c> and an HTTP-method attribute with its own
    /// template (e.g. <c>[HttpDelete("{name}")]</c>) registers two routes. On a controller with no
    /// class-level route the second one lands at the site root, so <c>DELETE /{name}</c> matches every
    /// single-segment path and routing claims <c>GET /sw.js</c> or <c>/manifest.webmanifest</c> as a
    /// 405 endpoint before the static file middleware can serve it.
    /// </summary>
    [Fact]
    public void NoAction_CombinesRouteAttributeWithTemplatedHttpMethodAttribute()
    {
        var offenders = typeof(DiscordBot.Bot.Controllers.ApiControllerBase).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<RouteAttribute>().Any()
                && m.GetCustomAttributes<HttpMethodAttribute>().Any(a => a.Template != null))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();

        offenders.Should().BeEmpty(
            "an action with [Route] should use a bare [HttpGet]/[HttpPost]/... so it does not register a second, unprefixed route");
    }
}
