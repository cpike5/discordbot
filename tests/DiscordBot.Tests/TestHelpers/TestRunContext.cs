using cpike.Agent;
using DiscordBot.Infrastructure.Services.LLM.Tooling;

namespace DiscordBot.Tests.TestHelpers;

/// <summary>
/// Builds the <see cref="DiscordRunContext"/> a tool reads its caller from, seeded the way a
/// pipeline run seeds it.
/// </summary>
public static class TestRunContext
{
    /// <summary>A run context for <paramref name="userId"/>, optionally in a guild or with an active guild.</summary>
    public static DiscordRunContext Create(
        ulong userId = 1,
        ulong guildId = 0,
        bool canMutate = true,
        ulong? activeGuildId = null)
    {
        var scope = new AgentRunScope { CanEditData = canMutate };
        var seed = DiscordRunContext.Seed(
            new DiscordRunIdentity(userId, guildId, CanMutate: canMutate, ActiveGuildId: activeGuildId),
            "test");

        foreach (var (key, value) in seed.Items)
        {
            scope.Items[key] = value;
        }

        return new DiscordRunContext(scope);
    }
}
