using System.Reflection;
using System.Text.RegularExpressions;
using DiscordBot.Bot.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace DiscordBot.Tests.Bot.Controllers;

/// <summary>
/// Regression guard for the pre-existing bug found while porting <c>Members/Moderation</c> to
/// Blazor (docs/plans/blazor-port-plan.md Phase 4 cluster 4d): <c>ModTagsController.ApplyTag</c>/
/// <c>RemoveTag</c> and <c>UserModerationController.ApplyTag</c>/<c>RemoveTag</c> both mapped
/// <c>POST</c>/<c>DELETE api/guilds/{guildId}/users/{userId}/tags/{tagName}</c> - an ambiguous
/// match ASP.NET Core only discovers at request time, never at build time, since MVC action
/// selection doesn't fail fast on a duplicate template. Reflects over every controller action in
/// the Bot assembly and asserts no two actions - in the same controller or different ones - share
/// an HTTP method and route template. Fixed here by deleting <c>UserModerationController</c>
/// outright (its only consumer, <c>wwwroot/js/user-moderation-profile.js</c>, retired with the
/// same port).
/// </summary>
public class ControllerRouteConflictTests
{
    private static readonly Regex RouteParameterPattern = new(@"\{[^{}]*\}", RegexOptions.Compiled);

    [Fact]
    public void NoTwoControllerActions_ShareAnHttpMethodAndRouteTemplate()
    {
        var assembly = typeof(PreviewController).Assembly;

        var controllerTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsPublic: true } && typeof(ControllerBase).IsAssignableFrom(t))
            .ToList();

        controllerTypes.Should().NotBeEmpty("the Bot assembly should contain at least one controller");

        var routes = new List<(string Method, string Template, string ActionDescription)>();

        foreach (var controllerType in controllerTypes)
        {
            var controllerRoutes = controllerType
                .GetCustomAttributes<RouteAttribute>()
                .Select(r => r.Template)
                .DefaultIfEmpty(null)
                .ToList();

            foreach (var method in controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName || !typeof(IActionResult).IsAssignableFrom(UnwrapTaskType(method.ReturnType)))
                {
                    continue;
                }

                var httpMethodAttributes = method.GetCustomAttributes()
                    .OfType<IActionHttpMethodProvider>()
                    .Cast<Attribute>()
                    .ToList();

                if (httpMethodAttributes.Count == 0)
                {
                    // Not a conventionally-routed action in this codebase (every controller here
                    // is attribute-routed) - skip rather than guess a convention route.
                    continue;
                }

                var methodRouteTemplates = method.GetCustomAttributes()
                    .OfType<IRouteTemplateProvider>()
                    .Select(r => r.Template)
                    .DefaultIfEmpty(null)
                    .ToList();

                foreach (var httpAttr in httpMethodAttributes)
                {
                    var httpMethods = ((IActionHttpMethodProvider)httpAttr).HttpMethods;
                    var attrTemplate = (httpAttr as IRouteTemplateProvider)?.Template;

                    // A method can carry [HttpPost]/[HttpDelete] etc. plus separate [Route(...)]
                    // attributes (ModTagsController's pattern) - combine each controller route
                    // with each applicable method-level template exactly as routing would.
                    var effectiveMethodTemplates = attrTemplate is not null
                        ? new List<string?> { attrTemplate }
                        : (methodRouteTemplates.Count > 0 ? methodRouteTemplates : new List<string?> { null });

                    foreach (var controllerRoute in controllerRoutes)
                    {
                        foreach (var methodTemplate in effectiveMethodTemplates)
                        {
                            var combined = CombineRoute(controllerRoute, methodTemplate);
                            if (combined is null)
                            {
                                continue;
                            }

                            var normalized = Normalize(combined);
                            foreach (var httpMethod in httpMethods)
                            {
                                routes.Add((httpMethod, normalized, $"{controllerType.Name}.{method.Name}"));
                            }
                        }
                    }
                }
            }
        }

        var duplicates = routes
            .GroupBy(r => (r.Method, r.Template))
            .Where(g => g.Select(x => x.ActionDescription).Distinct().Count() > 1)
            .ToList();

        duplicates.Should().BeEmpty(
            "two controller actions mapping the same HTTP method + route template are an " +
            $"ambiguous match at runtime: {string.Join("; ", duplicates.Select(d => $"{d.Key.Method} {d.Key.Template} -> [{string.Join(", ", d.Select(x => x.ActionDescription).Distinct())}]"))}");
    }

    private static Type UnwrapTaskType(Type returnType)
    {
        if (returnType is { IsGenericType: true } && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return returnType.GetGenericArguments()[0];
        }

        return returnType == typeof(Task) ? typeof(IActionResult) : returnType;
    }

    private static string? CombineRoute(string? controllerRoute, string? methodTemplate)
    {
        if (string.IsNullOrEmpty(methodTemplate))
        {
            return controllerRoute is null ? null : "/" + controllerRoute.Trim('/');
        }

        if (methodTemplate.StartsWith('/'))
        {
            return "/" + methodTemplate.Trim('/');
        }

        var parts = new[] { controllerRoute?.Trim('/'), methodTemplate.Trim('/') }
            .Where(p => !string.IsNullOrEmpty(p));

        return "/" + string.Join('/', parts);
    }

    /// <summary>Replaces every <c>{param}</c> segment with a placeholder and lowercases the
    /// template - routing treats two differently-named parameters in the same position
    /// (<c>{id}</c> vs <c>{tagName}</c>) as an identical, ambiguous match.</summary>
    private static string Normalize(string template) =>
        RouteParameterPattern.Replace(template, "{}").ToLowerInvariant();
}
