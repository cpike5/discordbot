using System.Text.Json;
using cpike.Agent;

namespace DiscordBot.Infrastructure.Services.LLM.Tooling;

/// <summary>Builds the framework's tool definitions from the house schema helpers.</summary>
public static class AgentTools
{
    /// <summary>
    /// A tool definition from an object schema built with <see cref="ToolInput.ObjectSchema"/>.
    /// </summary>
    /// <param name="name">The tool's lower_snake_case name, as the model calls it.</param>
    /// <param name="description">What the model reads to decide whether to call it.</param>
    /// <param name="inputSchema">The <c>{ type, properties, required }</c> object schema.</param>
    /// <param name="requiresEdit">
    /// The tool always changes data, so the loop refuses it for a caller without edit access
    /// (<see cref="AgentRunScope.CanEditData"/>) before it is entered.
    /// </param>
    public static AgentToolDefinition Define(
        string name, string description, JsonElement inputSchema, bool requiresEdit = false) => new()
    {
        Name = name,
        Description = description,
        InputSchema = SchemaFrom(inputSchema),
        RequiresEdit = requiresEdit,
    };

    /// <summary>Converts a <c>{ type, properties, required }</c> JSON schema into the framework's shape.</summary>
    public static AgentInputSchema SchemaFrom(JsonElement schema)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (schema.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in props.EnumerateObject())
            {
                properties[property.Name] = property.Value.Clone();
            }
        }

        var required = schema.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.Array
            ? req.EnumerateArray().Select(r => r.GetString()!).ToList()
            : [];

        return new AgentInputSchema
        {
            Type = schema.TryGetProperty("type", out var type) ? type.Clone() : JsonSerializer.SerializeToElement("object"),
            Properties = properties,
            Required = required,
        };
    }
}
