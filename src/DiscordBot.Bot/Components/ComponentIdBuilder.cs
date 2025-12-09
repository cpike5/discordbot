namespace DiscordBot.Bot.Components;

/// <summary>
/// Utility class for building and parsing Discord component custom IDs.
/// Format: {handler}:{action}:{userId}:{correlationId}:{data}
/// </summary>
public static class ComponentIdBuilder
{
    private const char Separator = ':';
    private const int MinParts = 4; // handler, action, userId, correlationId (data is optional)

    /// <summary>
    /// Builds a component custom ID from the provided parts.
    /// </summary>
    /// <param name="handler">The handler name (e.g., "shutdown", "guilds")</param>
    /// <param name="action">The action name (e.g., "confirm", "cancel", "page")</param>
    /// <param name="userId">The user ID who can interact with this component</param>
    /// <param name="correlationId">The correlation ID for state lookup</param>
    /// <param name="data">Optional additional data</param>
    /// <returns>A formatted component custom ID string</returns>
    public static string Build(string handler, string action, ulong userId, string correlationId, string? data = null)
    {
        if (string.IsNullOrWhiteSpace(handler))
            throw new ArgumentException("Handler cannot be null or whitespace.", nameof(handler));
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action cannot be null or whitespace.", nameof(action));
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("Correlation ID cannot be null or whitespace.", nameof(correlationId));

        var parts = new List<string> { handler, action, userId.ToString(), correlationId };

        if (!string.IsNullOrEmpty(data))
        {
            parts.Add(data);
        }
        else
        {
            parts.Add(string.Empty);
        }

        return string.Join(Separator, parts);
    }

    /// <summary>
    /// Parses a component custom ID into its constituent parts.
    /// </summary>
    /// <param name="customId">The component custom ID to parse</param>
    /// <returns>The parsed component ID parts</returns>
    /// <exception cref="FormatException">Thrown when the custom ID format is invalid</exception>
    public static ComponentIdParts Parse(string customId)
    {
        if (!TryParse(customId, out var parts))
        {
            throw new FormatException($"Invalid component custom ID format: {customId}");
        }

        return parts;
    }

    /// <summary>
    /// Attempts to parse a component custom ID into its constituent parts.
    /// </summary>
    /// <param name="customId">The component custom ID to parse</param>
    /// <param name="parts">The parsed component ID parts, or default if parsing fails</param>
    /// <returns>True if parsing succeeded, false otherwise</returns>
    public static bool TryParse(string customId, out ComponentIdParts parts)
    {
        parts = default!;

        if (string.IsNullOrWhiteSpace(customId))
            return false;

        var segments = customId.Split(Separator);
        if (segments.Length < MinParts)
            return false;

        if (!ulong.TryParse(segments[2], out var userId))
            return false;

        parts = new ComponentIdParts
        {
            Handler = segments[0],
            Action = segments[1],
            UserId = userId,
            CorrelationId = segments[3],
            Data = segments.Length > 4 && !string.IsNullOrEmpty(segments[4]) ? segments[4] : null
        };

        return true;
    }
}

/// <summary>
/// Represents the parsed parts of a component custom ID.
/// </summary>
public record ComponentIdParts
{
    /// <summary>
    /// The handler name (e.g., "shutdown", "guilds")
    /// </summary>
    public required string Handler { get; init; }

    /// <summary>
    /// The action name (e.g., "confirm", "cancel", "page")
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// The user ID who can interact with this component
    /// </summary>
    public required ulong UserId { get; init; }

    /// <summary>
    /// The correlation ID for state lookup
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Optional additional data
    /// </summary>
    public string? Data { get; init; }
}
