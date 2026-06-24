using DiscordBot.Bot.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Authorization;

/// <summary>
/// Authorization filter that enforces per-guild access scoping on API controllers.
/// <para>
/// When a guild ID is present in the request (route or query string, parameter name
/// <c>guildId</c> by default), the caller must satisfy the <c>GuildAccess</c> policy for
/// that specific guild. This closes the cross-guild IDOR where a user authorized for one
/// guild simply passes a different guild's ID and reads its data.
/// </para>
/// <para>
/// When no guild ID is present (a global / cross-guild aggregate view), the caller must
/// hold a global <see cref="IdentitySeeder.Roles.Admin"/> or
/// <see cref="IdentitySeeder.Roles.SuperAdmin"/> role.
/// </para>
/// <para>
/// This complements — it does not replace — the role policy applied to the controller
/// (e.g. <c>RequireViewer</c>/<c>RequireModerator</c>); both must pass. SuperAdmins bypass
/// the per-guild check inside <see cref="GuildAccessHandler"/>.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireGuildScopeAttribute : Attribute, IAsyncAuthorizationFilter
{
    private const string GuildAccessPolicy = "GuildAccess";

    private readonly string _guildIdParameterName;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequireGuildScopeAttribute"/> class.
    /// </summary>
    /// <param name="guildIdParameterName">
    /// The route/query parameter name carrying the guild ID. Defaults to <c>guildId</c>,
    /// which must match the parameter name configured on the registered <c>GuildAccess</c> policy.
    /// </param>
    public RequireGuildScopeAttribute(string guildIdParameterName = "guildId")
    {
        _guildIdParameterName = guildIdParameterName;
    }

    /// <inheritdoc />
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var httpContext = context.HttpContext;
        var user = httpContext.User;

        // Defense in depth: the controller's role policy should already require this,
        // but never evaluate guild scope for an unauthenticated principal.
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        var guildIdValue = httpContext.Request.RouteValues.TryGetValue(_guildIdParameterName, out var routeValue)
            ? routeValue?.ToString()
            : null;

        if (string.IsNullOrEmpty(guildIdValue))
        {
            guildIdValue = httpContext.Request.Query[_guildIdParameterName].FirstOrDefault();
        }

        // No specific guild was requested: this is a global / cross-guild aggregate view,
        // which must be restricted to a globally elevated role.
        if (string.IsNullOrEmpty(guildIdValue))
        {
            if (!user.IsInRole(IdentitySeeder.Roles.Admin) && !user.IsInRole(IdentitySeeder.Roles.SuperAdmin))
            {
                context.Result = new ForbidResult();
            }

            return;
        }

        // A specific guild was requested: enforce per-guild membership/permission. The
        // GuildAccessHandler reads the same guildId from route/query and verifies the
        // caller's Discord membership (and Administrator permission for the Admin role).
        var authorizationService = httpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        var result = await authorizationService.AuthorizeAsync(user, GuildAccessPolicy);

        if (!result.Succeeded)
        {
            context.Result = new ForbidResult();
        }
    }
}
