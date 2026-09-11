using System.Reflection;
using DiscordBot.Infrastructure.Services.LLM;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// The assemblies <c>AddAgentTools</c> scans for <c>IAgentTool</c> implementations.
/// </summary>
/// <remarks>
/// Named once and shared by <see cref="AssistantServiceExtensions"/> and
/// <see cref="DmAssistantServiceExtensions"/>, because a tool that is scanned by one surface's
/// registration and not the other's is the kind of asymmetry that takes an afternoon to find.
/// Infrastructure holds tools over domain services; Bot holds the ones that need Discord.NET.
/// </remarks>
internal static class AgentToolAssemblies
{
    /// <summary>Infrastructure and Bot, in that order.</summary>
    internal static readonly IReadOnlyList<Assembly> All = new[]
    {
        typeof(ToolPermissions).Assembly,
        typeof(AgentToolAssemblies).Assembly
    };
}
