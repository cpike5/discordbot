using System.Reflection;
using DiscordBot.Infrastructure.Services.LLM.Tooling;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// The assemblies scanned for the assistant's <c>cpike.Agent.IAgentTool</c> implementations:
/// Infrastructure (tools over the repositories) and Bot (tools over the Discord client).
/// </summary>
internal static class AgentToolAssemblies
{
    internal static readonly IReadOnlyList<Assembly> All = new[]
    {
        typeof(DiscordRunContext).Assembly,
        typeof(AgentToolAssemblies).Assembly
    };
}
