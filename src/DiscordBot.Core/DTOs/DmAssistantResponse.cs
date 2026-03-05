namespace DiscordBot.Core.DTOs;

/// <summary>
/// Result of a DM assistant message processing operation.
/// </summary>
public class DmAssistantResponse
{
    public bool Success { get; set; }
    public string? Response { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsOwner { get; set; }
    public bool IsPlaceholder { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CachedTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public int LatencyMs { get; set; }

    public static DmAssistantResponse PlaceholderResult(string message)
    {
        return new DmAssistantResponse
        {
            Success = true,
            Response = message,
            IsPlaceholder = true
        };
    }

    public static DmAssistantResponse ErrorResult(string errorMessage)
    {
        return new DmAssistantResponse
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
