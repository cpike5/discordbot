namespace DiscordBot.Core.Models.FeatureRequests;

/// <summary>
/// Result returned by <c>IInputValidationService.Validate</c> after sanitizing and validating user input.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public string SanitizedText { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }

    public static ValidationResult Success(string sanitizedText) =>
        new() { IsValid = true, SanitizedText = sanitizedText };

    public static ValidationResult Failure(string reason) =>
        new() { IsValid = false, RejectionReason = reason };
}
