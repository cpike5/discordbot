using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.Vox;
using System.Diagnostics;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Hosted service that initializes the VOX clip library at application startup.
/// Logs initialization timing and clip counts per group.
/// </summary>
public class VoxClipLibraryInitializer : IHostedService
{
    private readonly IVoxClipLibrary _clipLibrary;
    private readonly ILogger<VoxClipLibraryInitializer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoxClipLibraryInitializer"/> class.
    /// </summary>
    /// <param name="clipLibrary">The VOX clip library to initialize.</param>
    /// <param name="logger">The logger.</param>
    public VoxClipLibraryInitializer(
        IVoxClipLibrary clipLibrary,
        ILogger<VoxClipLibraryInitializer> logger)
    {
        _clipLibrary = clipLibrary;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("VOX clip library initialization starting");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _clipLibrary.InitializeAsync(cancellationToken);
            stopwatch.Stop();

            var voxCount = _clipLibrary.GetClipCount(VoxClipGroup.Vox);
            var fvoxCount = _clipLibrary.GetClipCount(VoxClipGroup.Fvox);
            var hgruntCount = _clipLibrary.GetClipCount(VoxClipGroup.Hgrunt);

            _logger.LogInformation(
                "VOX library initialized with {VoxCount} VOX, {FvoxCount} FVOX, {HgruntCount} HGrunt clips in {ElapsedMs}ms",
                voxCount,
                fvoxCount,
                hgruntCount,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error initializing VOX clip library after {ElapsedMs}ms",
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("VOX clip library initializer stopping");
        return Task.CompletedTask;
    }
}
