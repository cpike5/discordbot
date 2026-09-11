using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiscordBot.Agents;

/// <summary>
/// The serializer settings every tool result goes through, and the one-liner that applies them.
/// </summary>
/// <remarks>
/// Property names are written exactly as the payload type spells them — no naming policy — because a
/// tool result is a model-facing contract, and the names in the code should be the names on the wire
/// rather than the output of a transform someone has to remember. Write payload members in
/// <c>snake_case</c> to match the tool schemas.
/// </remarks>
public static class ToolJson
{
    /// <summary>
    /// Compact settings for tool payloads: no indentation, and null members omitted entirely so an
    /// optional field costs nothing when it is absent.
    /// </summary>
    public static readonly JsonSerializerOptions Compact = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>
    /// Serializes <paramref name="payload"/> to a <see cref="JsonElement"/> using <see cref="Compact"/>.
    /// </summary>
    /// <param name="payload">The value to serialize. Usually an anonymous object.</param>
    public static JsonElement Element(object? payload) =>
        JsonSerializer.SerializeToElement(payload, Compact);
}
