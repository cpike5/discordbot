using System.Security.Claims;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Middleware;
using DiscordBot.Core.Authorization;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DiscordBot.Tests.Controllers.Currency;

/// <summary>
/// Shared setup for the currency controller tests: the signed-in user, the mocked services, and
/// the currencies the routes are exercised against.
/// </summary>
public static class CurrencyControllerTestContext
{
    /// <summary>Discord snowflake of the signed-in portal user in these tests.</summary>
    public const ulong ActorId = 100000000000000001UL;

    /// <summary>Guild the guild-scoped currency belongs to.</summary>
    public const ulong GuildId = 222333444555UL;

    /// <summary>Builds a signed-in user with a linked Discord account and the given portal roles.</summary>
    public static ClaimsPrincipal User(ulong discordUserId = ActorId, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "portal-user") };

        if (discordUserId != 0)
        {
            claims.Add(new Claim("discord:user_id", discordUserId.ToString()));
        }

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    /// <summary>An admin user, the common case for these routes.</summary>
    public static ClaimsPrincipal AdminUser(ulong discordUserId = ActorId) =>
        User(discordUserId, Roles.Admin);

    /// <summary>Gives a controller an HttpContext carrying the signed-in user.</summary>
    public static T WithUser<T>(this T controller, ClaimsPrincipal user)
        where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        controller.HttpContext.Items[CorrelationIdMiddleware.ItemKey] = "test-correlation-id";
        return controller;
    }

    /// <summary>A guild-owned currency.</summary>
    public static CurrencyDto GuildCurrency(Guid? id = null, bool isActive = true) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Scope = CurrencyScope.Guild,
        GuildId = GuildId,
        Name = "Rat Coin",
        Symbol = "🪙",
        IsTransferable = true,
        IsActive = isActive,
        CreatedById = ActorId,
        CreatedAt = DateTime.UtcNow
    };

    /// <summary>A bot-wide currency.</summary>
    public static CurrencyDto GlobalCurrency(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Scope = CurrencyScope.Global,
        GuildId = null,
        Name = "Bot Credit",
        Symbol = "💳",
        IsTransferable = false,
        IsActive = true,
        CreatedById = ActorId,
        CreatedAt = DateTime.UtcNow
    };

    /// <summary>An audit log service whose builder swallows every call.</summary>
    public static Mock<IAuditLogService> AuditLog()
    {
        var builder = new Mock<IAuditLogBuilder>();
        builder.Setup(b => b.ForCategory(It.IsAny<AuditLogCategory>())).Returns(builder.Object);
        builder.Setup(b => b.WithAction(It.IsAny<AuditLogAction>())).Returns(builder.Object);
        builder.Setup(b => b.ByUser(It.IsAny<string>())).Returns(builder.Object);
        builder.Setup(b => b.BySystem()).Returns(builder.Object);
        builder.Setup(b => b.ByBot()).Returns(builder.Object);
        builder.Setup(b => b.InGuild(It.IsAny<ulong>())).Returns(builder.Object);
        builder.Setup(b => b.OnTarget(It.IsAny<string>(), It.IsAny<string>())).Returns(builder.Object);
        builder.Setup(b => b.WithDetails(It.IsAny<object>())).Returns(builder.Object);
        builder.Setup(b => b.WithDetails(It.IsAny<Dictionary<string, object?>>())).Returns(builder.Object);
        builder.Setup(b => b.FromIpAddress(It.IsAny<string>())).Returns(builder.Object);
        builder.Setup(b => b.WithCorrelationId(It.IsAny<string>())).Returns(builder.Object);
        builder.Setup(b => b.LogAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var auditLog = new Mock<IAuditLogService>();
        auditLog.Setup(a => a.CreateBuilder()).Returns(builder.Object);
        return auditLog;
    }

    /// <summary>An access service that answers one level for every currency.</summary>
    public static Mock<ICurrencyAccessService> Access(CurrencyAccessLevel level)
    {
        var access = new Mock<ICurrencyAccessService>();

        access
            .Setup(a => a.GetAccessAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CurrencyDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(level);

        access
            .Setup(a => a.GetGuildRoleIdsAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ulong>());

        return access;
    }

    /// <summary>Reads the status code off whatever result kind an action returned.</summary>
    public static int StatusOf(IActionResult? result) => result switch
    {
        ObjectResult objectResult => objectResult.StatusCode ?? 200,
        StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
        null => 0,
        _ => 200
    };
}
