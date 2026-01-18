using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically cleans up the audio cache,
/// removing expired entries and enforcing size limits.
/// </summary>
public class AudioCacheCleanupService : BackgroundService
{
    private readonly ILogger<AudioCacheCleanupService> _logger;
    private readonly ISoundCacheService _cacheService;
    private readonly AudioCacheOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioCacheCleanupService"/> class.
    /// </summary>
    public AudioCacheCleanupService(
        ILogger<AudioCacheCleanupService> logger,
        ISoundCacheService cacheService,
        IOptions<AudioCacheOptions> options)
    {
        _logger = logger;
        _cacheService = cacheService;
        _options = options.Value;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Audio cache is disabled, cleanup service will not run");
            return;
        }

        _logger.LogInformation("Audio cache cleanup service started (interval: {Interval} minutes)",
            _options.CleanupIntervalMinutes);

        // Initial delay to allow system to stabilize
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Audio cache cleanup service stopped during initial delay");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var removedCount = await _cacheService.CleanupAsync(stoppingToken);

                var stats = _cacheService.GetStatistics();
                _logger.LogDebug(
                    "Audio cache status: {EntryCount} entries, {SizeBytes} bytes, {HitRate:F1}% hit rate",
                    stats.EntryCount, stats.TotalSizeBytes, stats.HitRate);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during audio cache cleanup");
            }

            await Task.Delay(TimeSpan.FromMinutes(_options.CleanupIntervalMinutes), stoppingToken);
        }

        _logger.LogInformation("Audio cache cleanup service stopped");
    }
}
