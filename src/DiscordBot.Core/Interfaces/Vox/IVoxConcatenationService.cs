namespace DiscordBot.Core.Interfaces.Vox;

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
    /// <returns>The path to the concatenated output file.</returns>
    Task<string> ConcatenateAsync(
        IReadOnlyList<string> clipFilePaths,
        int wordGapMs,
        CancellationToken cancellationToken = default);
}
