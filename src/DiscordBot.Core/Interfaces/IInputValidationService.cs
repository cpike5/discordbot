using DiscordBot.Core.Models.FeatureRequests;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Validates and sanitizes free-text user input for feature request submissions.
/// </summary>
public interface IInputValidationService
{
    ValidationResult Validate(string input, int minLength, int maxLength);
}
