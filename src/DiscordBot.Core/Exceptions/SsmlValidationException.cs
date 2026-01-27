namespace DiscordBot.Core.Exceptions;

/// <summary>
/// Exception thrown when SSML validation fails.
/// </summary>
public class SsmlValidationException : Exception
{
    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Gets the invalid SSML that caused the validation failure.
    /// </summary>
    public string InvalidSsml { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SsmlValidationException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="errors">The validation errors.</param>
    /// <param name="invalidSsml">The invalid SSML.</param>
    public SsmlValidationException(string message, IReadOnlyList<string> errors, string invalidSsml)
        : base(message)
    {
        Errors = errors;
        InvalidSsml = invalidSsml;
    }
}
