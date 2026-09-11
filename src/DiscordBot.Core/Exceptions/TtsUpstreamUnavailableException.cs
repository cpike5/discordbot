namespace DiscordBot.Core.Exceptions;

/// <summary>
/// Exception thrown when the text-to-speech provider could not be reached.
/// Distinguishes a transient upstream network failure (DNS, TCP, TLS, service timeout)
/// from a misconfigured service, so callers can report it as a temporary outage
/// rather than a configuration problem.
/// </summary>
/// <remarks>
/// Derives from <see cref="InvalidOperationException"/> so existing callers that
/// catch synthesis failures generically keep working; callers that want to
/// surface the distinction catch this type first.
/// </remarks>
public class TtsUpstreamUnavailableException : InvalidOperationException
{
    /// <summary>
    /// Gets the number of synthesis attempts made before giving up.
    /// </summary>
    public int Attempts { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TtsUpstreamUnavailableException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="attempts">The number of synthesis attempts made before giving up.</param>
    public TtsUpstreamUnavailableException(string message, int attempts)
        : base(message)
    {
        Attempts = attempts;
    }
}
