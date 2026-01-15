using System.Text.Json;

namespace DiscordBot.Core.DTOs.LLM;

/// <summary>
/// Result of executing a tool, including success/error status and data.
/// </summary>
public class ToolExecutionResult
{
    /// <summary>
    /// Whether the tool execution was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The result data from the tool (if successful).
    /// </summary>
    public JsonElement? Data { get; set; }

    /// <summary>
    /// Error message if the tool execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Creates a successful execution result.
    /// </summary>
    /// <param name="data">The result data.</param>
    /// <returns>A successful ToolExecutionResult.</returns>
    public static ToolExecutionResult CreateSuccess(JsonElement data)
    {
        return new ToolExecutionResult
        {
            Success = true,
            Data = data
        };
    }

    /// <summary>
    /// Creates a failed execution result.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A failed ToolExecutionResult.</returns>
    public static ToolExecutionResult CreateError(string errorMessage)
    {
        return new ToolExecutionResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
