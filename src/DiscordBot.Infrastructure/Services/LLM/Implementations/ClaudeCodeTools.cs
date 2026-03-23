using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;

namespace DiscordBot.Infrastructure.Services.LLM.Implementations;

/// <summary>
/// Static tool definitions for Claude Code CLI integration.
/// </summary>
public static class ClaudeCodeTools
{
    /// <summary>
    /// Tool name for running a Claude Code session.
    /// </summary>
    public const string RunClaudeCode = "run_claude_code";

    /// <summary>
    /// Tool name for checking Claude Code session status.
    /// </summary>
    public const string GetClaudeCodeStatus = "get_claude_code_status";

    /// <summary>
    /// Gets all Claude Code tool definitions.
    /// </summary>
    public static IEnumerable<LlmToolDefinition> GetAllTools()
    {
        yield return CreateRunClaudeCodeTool();
        yield return CreateGetClaudeCodeStatusTool();
    }

    private static LlmToolDefinition CreateRunClaudeCodeTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "prompt": {
                        "type": "string",
                        "description": "The task or question for Claude Code to work on. Be specific and detailed."
                    },
                    "continue_session": {
                        "type": "boolean",
                        "description": "Whether to resume the previous Claude Code session for continuity",
                        "default": true
                    },
                    "working_directory": {
                        "type": "string",
                        "description": "Override the default working directory for this invocation"
                    }
                },
                "required": ["prompt"]
            }
            """);

        return new LlmToolDefinition
        {
            Name = RunClaudeCode,
            Description = "Runs a Claude Code CLI session to perform coding tasks such as reading, writing, and editing files, running commands, and answering questions about the codebase. Use for any task that requires direct interaction with the project repository.",
            InputSchema = schema.RootElement.Clone()
        };
    }

    private static LlmToolDefinition CreateGetClaudeCodeStatusTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {},
                "required": []
            }
            """);

        return new LlmToolDefinition
        {
            Name = GetClaudeCodeStatus,
            Description = "Check if a Claude Code session exists and its cumulative cost.",
            InputSchema = schema.RootElement.Clone()
        };
    }
}
