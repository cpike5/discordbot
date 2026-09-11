using System.Globalization;
using System.Text.Json;

namespace DiscordBot.Agents;

/// <summary>
/// Reading a model's arguments, and describing them. Replaces the
/// <c>TryGetProperty</c> + null-check + empty-check triplet that every tool otherwise repeats per
/// parameter, and the hand-written JSON Schema string that goes with it.
/// </summary>
/// <remarks>
/// Every accessor returns null rather than throwing when the property is missing, null, or of the
/// wrong kind, because all three are the same thing to a tool: the model did not give me this. A
/// number that arrives as a string is still read as a number — models do that, and refusing it
/// teaches them nothing.
/// </remarks>
public static class ToolInput
{
    /// <summary>
    /// The string at <paramref name="key"/>, or null when it is missing, null, not a string, or
    /// blank. Blank counts as missing: an empty argument is the model omitting the parameter in a
    /// more expensive way.
    /// </summary>
    public static string? GetString(JsonElement input, string key)
    {
        if (!TryGetValue(input, key, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    /// <summary>The 32-bit integer at <paramref name="key"/>, or null when it is missing or unreadable.</summary>
    public static int? GetInt(JsonElement input, string key) =>
        GetLong(input, key) is { } value && value is >= int.MinValue and <= int.MaxValue
            ? (int)value
            : null;

    /// <summary>
    /// The 32-bit integer at <paramref name="key"/> clamped to <paramref name="min"/>..<paramref name="max"/>,
    /// or <paramref name="fallback"/> when it is missing or unreadable. The shape every
    /// <c>limit</c> parameter wants.
    /// </summary>
    public static int GetInt(JsonElement input, string key, int fallback, int min, int max) =>
        Math.Clamp(GetInt(input, key) ?? fallback, min, max);

    /// <summary>The 64-bit integer at <paramref name="key"/>, or null when it is missing or unreadable.</summary>
    public static long? GetLong(JsonElement input, string key)
    {
        if (!TryGetValue(input, key, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(
                value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    /// <summary>
    /// The unsigned 64-bit integer at <paramref name="key"/>, or null when it is missing or
    /// unreadable. Discord snowflakes exceed JavaScript's safe integer range and models routinely
    /// quote them, so the string form is read too.
    /// </summary>
    public static ulong? GetUInt64(JsonElement input, string key)
    {
        if (!TryGetValue(input, key, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetUInt64(out var number) => number,
            JsonValueKind.String when ulong.TryParse(
                value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    /// <summary>
    /// The boolean at <paramref name="key"/>, or null when it is missing or unreadable. The strings
    /// <c>"true"</c> and <c>"false"</c> count.
    /// </summary>
    public static bool? GetBool(JsonElement input, string key)
    {
        if (!TryGetValue(input, key, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    /// <summary>
    /// The strings in the array at <paramref name="key"/>, or null when it is missing or not an
    /// array. Non-string and blank entries are dropped; an array of nothing usable comes back empty
    /// rather than null, so "the model sent an empty list" stays distinguishable from "the model
    /// sent nothing".
    /// </summary>
    public static IReadOnlyList<string>? GetStringArray(JsonElement input, string key)
    {
        if (!TryGetValue(input, key, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = new List<string>();
        foreach (var element in value.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var text = element.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                items.Add(text);
            }
        }

        return items;
    }

    /// <summary>
    /// One property of an input schema: <c>{"type": …, "description": …}</c> plus whichever of the
    /// optional keywords are supplied.
    /// </summary>
    /// <param name="type">JSON Schema type — <c>"string"</c>, <c>"integer"</c>, <c>"boolean"</c>, <c>"array"</c>.</param>
    /// <param name="description">
    /// What the parameter means, written for the model. Every property needs one; a schema with a
    /// bare type is a parameter the model will guess at.
    /// </param>
    /// <param name="default">Value advertised as the default when the parameter is omitted.</param>
    /// <param name="minimum">Lower bound, for a numeric parameter.</param>
    /// <param name="maximum">Upper bound, for a numeric parameter.</param>
    /// <param name="itemType">Element type, for an array parameter.</param>
    /// <returns>A fragment to use as a member of the object passed to <see cref="ObjectSchema"/>.</returns>
    public static IReadOnlyDictionary<string, object> Schema(
        string type,
        string description,
        object? @default = null,
        double? minimum = null,
        double? maximum = null,
        string? itemType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        // A Dictionary<string, object> preserves insertion order on serialization, which is what
        // keeps a schema's bytes stable across edits - and the tool array is the prompt cache's
        // prefix, so its bytes are worth being deliberate about.
        var schema = new Dictionary<string, object>
        {
            ["type"] = type,
            ["description"] = description
        };

        if (@default is not null)
        {
            schema["default"] = @default;
        }

        if (minimum.HasValue)
        {
            schema["minimum"] = minimum.Value;
        }

        if (maximum.HasValue)
        {
            schema["maximum"] = maximum.Value;
        }

        if (itemType is not null)
        {
            schema["items"] = new Dictionary<string, object> { ["type"] = itemType };
        }

        return schema;
    }

    /// <summary>
    /// An object input schema: <c>{"type": "object", "properties": …, "required": […]}</c>.
    /// </summary>
    /// <param name="properties">
    /// An anonymous object whose members are the parameter names and whose values are
    /// <see cref="Schema"/> fragments. Member names are written verbatim, so spell them the way the
    /// model should — <c>note_id</c>, not <c>noteId</c>.
    /// </param>
    /// <param name="required">Names of the required parameters. Always emitted, empty when there are none.</param>
    public static JsonElement ObjectSchema(object properties, params string[] required)
    {
        ArgumentNullException.ThrowIfNull(properties);

        return ToolJson.Element(new
        {
            type = "object",
            properties,
            required = required ?? Array.Empty<string>()
        });
    }

    /// <summary>
    /// The non-null property at <paramref name="key"/> of a JSON object.
    /// </summary>
    private static bool TryGetValue(JsonElement input, string key, out JsonElement value)
    {
        value = default;

        if (input.ValueKind != JsonValueKind.Object || string.IsNullOrEmpty(key))
        {
            return false;
        }

        return input.TryGetProperty(key, out value) && value.ValueKind != JsonValueKind.Null;
    }
}
