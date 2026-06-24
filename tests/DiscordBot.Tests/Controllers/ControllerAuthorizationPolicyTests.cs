using System.Reflection;
using DiscordBot.Bot.Controllers;
using DiscordBot.Bot.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Tests.Controllers;

/// <summary>
/// Guards against authorization policy references that do not resolve — e.g. a typo'd
/// <c>[Authorize(Policy = "...")]</c> that would throw at request time (HTTP 500) rather than
/// at startup. Builds the real policy registration and asserts every policy referenced by any
/// controller is registered.
/// </summary>
public class ControllerAuthorizationPolicyTests
{
    [Fact]
    public async Task AllControllerAuthorizePolicies_AreRegistered()
    {
        // Arrange: build the actual authorization policy registration used by the app.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationPolicies();
        var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        var referencedPolicies = CollectReferencedControllerPolicies();
        referencedPolicies.Should().NotBeEmpty("controllers declare authorization policies");

        // Assert: every referenced policy resolves to a registered policy.
        foreach (var policyName in referencedPolicies)
        {
            var policy = await policyProvider.GetPolicyAsync(policyName);
            policy.Should().NotBeNull(
                $"controller policy '{policyName}' must be registered in AddAuthorizationPolicies");
        }
    }

    private static HashSet<string> CollectReferencedControllerPolicies()
    {
        var referenced = new HashSet<string>(StringComparer.Ordinal);

        var controllerTypes = typeof(MessagesController).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract);

        foreach (var type in controllerTypes)
        {
            AddPolicies(type.GetCustomAttributes<AuthorizeAttribute>(inherit: true), referenced);

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                AddPolicies(method.GetCustomAttributes<AuthorizeAttribute>(inherit: true), referenced);
            }
        }

        return referenced;
    }

    private static void AddPolicies(IEnumerable<AuthorizeAttribute> attributes, HashSet<string> sink)
    {
        foreach (var attribute in attributes)
        {
            if (!string.IsNullOrEmpty(attribute.Policy))
            {
                sink.Add(attribute.Policy);
            }
        }
    }
}
