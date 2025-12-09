namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for standardized API error responses.
/// </summary>
public class ApiErrorDto
{
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional error details.
    /// </summary>
    public string? Detail { get; set; }

    /// <summary>
    /// Gets or sets the HTTP status code.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Gets or sets the trace ID for correlation.
    /// </summary>
    public string? TraceId { get; set; }
}
