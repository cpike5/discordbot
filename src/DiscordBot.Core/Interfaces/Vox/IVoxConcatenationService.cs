namespace DiscordBot.Core.Interfaces.Vox;

/// <summary>
/// Result of a VOX concatenation operation.
/// </summary>
/// <param name="OutputPath">Path to the concatenated PCM output file.</param>
/// <param name="AudioBytes">Size of the output file in bytes.</param>
/// <param name="DurationMs">FFmpeg concatenation processing time in milliseconds.</param>
public record VoxConcatenationResult(string OutputPath, long AudioBytes, double DurationMs);

/// <summary>
/// Service interface for concatenating multiple VOX audio clips into a single output file.
/// </summary>
public interface IVoxConcatenationService
{
    /// <summary>
    /// Concatenates multiple audio clips with specified word gaps.
    /// </summary>
    /// <param name="clipFilePaths">List of clip file paths to concatenate.</param>
    /// <param name="wordGapMs">Word gap in milliseconds to insert between clips.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Concatenation result containing output path, size, and processing time.</returns>
    Task<VoxConcatenationResult> ConcatenateAsync(
        IReadOnlyList<string> clipFilePaths,
        int wordGapMs,
        CancellationToken cancellationToken = default);
}
