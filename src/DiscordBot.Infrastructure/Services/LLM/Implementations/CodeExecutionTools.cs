using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;

namespace DiscordBot.Infrastructure.Services.LLM.Implementations;

/// <summary>
/// Static tool definitions for code execution.
/// </summary>
public static class CodeExecutionTools
{
    /// <summary>
    /// Tool name for executing Python code.
    /// </summary>
    public const string ExecutePython = "execute_python";

    /// <summary>
    /// Gets all code execution tool definitions.
    /// </summary>
    public static IEnumerable<LlmToolDefinition> GetAllTools()
    {
        yield return CreateExecutePythonTool();
    }

    private static LlmToolDefinition CreateExecutePythonTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "code": {
                        "type": "string",
                        "description": "The Python code to execute. Has access to the standard library. Print output to stdout for results."
                    }
                },
                "required": ["code"]
            }
            """);

        return new LlmToolDefinition
        {
            Name = ExecutePython,
            Description = "Executes Python code and returns stdout/stderr output. Use for calculations, data processing, text manipulation, or any task that benefits from scripting. The code runs in an isolated process with a timeout. Print results to stdout.",
            InputSchema = schema.RootElement.Clone()
        };
    }
}
