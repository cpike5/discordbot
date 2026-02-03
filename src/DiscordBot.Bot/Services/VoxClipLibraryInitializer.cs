using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.Vox;
using System.Diagnostics;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that initializes the VOX clip library after application startup.
/// Runs asynchronously to avoid blocking the application startup process.
/// </summary>
public class VoxClipLibraryInitializer : BackgroundService
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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VOX clip library background initialization starting");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _clipLibrary.InitializeAsync(stoppingToken);
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
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("VOX clip library initialization cancelled");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error initializing VOX clip library after {ElapsedMs}ms",
                stopwatch.ElapsedMilliseconds);
        }
    }
}
