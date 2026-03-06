using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Interfaces.LLM;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM.Providers;

/// <summary>
/// DM-scoped wrapper around <see cref="DocumentationToolProvider"/>.
/// Copies <see cref="ToolContext.ActiveGuildId"/> into <see cref="ToolContext.GuildId"/>
/// before delegating so that guild-specific URL substitution works in DM context.
/// </summary>
public class DmDocumentationToolProvider : IDmToolProvider
{
    private readonly DocumentationToolProvider _inner;
    private readonly ILogger<DmDocumentationToolProvider> _logger;

    /// <inheritdoc />
    public string Name => _inner.Name;

    /// <inheritdoc />
    public string Description => _inner.Description;

    public DmDocumentationToolProvider(
        DocumentationToolProvider inner,
        ILogger<DmDocumentationToolProvider> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IEnumerable<LlmToolDefinition> GetTools() => _inner.GetTools();

    /// <inheritdoc />
    public async Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        // In DM context there is no GuildId, but ActiveGuildId may be set
        // via the set_active_guild tool. Copy it so documentation URL substitution works.
        if (context.GuildId == 0 && context.ActiveGuildId is > 0)
        {
            _logger.LogDebug("DM documentation: copying ActiveGuildId {ActiveGuildId} to GuildId", context.ActiveGuildId);
            context.GuildId = context.ActiveGuildId.Value;
        }

        return await _inner.ExecuteToolAsync(toolName, input, context, cancellationToken);
    }
}
