using DiscordBot.Agents.Abstractions;
using DiscordBot.Core.Interfaces.LLM;

namespace DiscordBot.Infrastructure.Abstractions.LLM;

/// <summary>
/// Marker interface for tool providers scoped to DM assistant only.
/// DM tool providers are registered separately from guild assistant providers
/// to prevent DM-only tools from leaking into the guild assistant's tool registry.
/// </summary>
public interface IDmToolProvider : IToolProvider
{
}
