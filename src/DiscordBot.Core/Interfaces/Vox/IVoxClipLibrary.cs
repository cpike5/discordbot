using DiscordBot.Core.DTOs.Vox;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces.Vox;

/// <summary>
/// Service interface for the VOX clip library that manages and provides access to audio clips.
/// </summary>
public interface IVoxClipLibrary
{
    /// <summary>
    /// Initializes the clip library by scanning directories and extracting audio metadata.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all clips for a specific group.
    /// </summary>
    /// <param name="group">The clip group to retrieve.</param>
    /// <returns>A read-only list of clip information.</returns>
    IReadOnlyList<VoxClipInfo> GetClips(VoxClipGroup group);

    /// <summary>
    /// Gets a specific clip by name from a group.
    /// </summary>
    /// <param name="group">The clip group to search.</param>
    /// <param name="clipName">The name of the clip (without extension).</param>
    /// <returns>The clip information if found, otherwise null.</returns>
    VoxClipInfo? GetClip(VoxClipGroup group, string clipName);

    /// <summary>
    /// Searches for clips matching a query string.
    /// </summary>
    /// <param name="group">The clip group to search.</param>
    /// <param name="query">The search query.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <returns>A read-only list of matching clip information.</returns>
    IReadOnlyList<VoxClipInfo> SearchClips(VoxClipGroup group, string query, int maxResults = 25);

    /// <summary>
    /// Gets the full file path for a clip.
    /// </summary>
    /// <param name="group">The clip group.</param>
    /// <param name="clipName">The name of the clip (without extension).</param>
    /// <returns>The full file path to the clip.</returns>
    string GetClipFilePath(VoxClipGroup group, string clipName);

    /// <summary>
    /// Gets the total number of clips in a group.
    /// </summary>
    /// <param name="group">The clip group.</param>
    /// <returns>The count of clips.</returns>
    int GetClipCount(VoxClipGroup group);

    /// <summary>
    /// Gets a value indicating whether the library has been initialized.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Waits for the library to be initialized.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for initialization.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if initialized within timeout, false otherwise.</returns>
    Task<bool> WaitForInitializationAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
