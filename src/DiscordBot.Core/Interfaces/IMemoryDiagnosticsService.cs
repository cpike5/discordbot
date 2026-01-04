using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for aggregating and reporting memory diagnostics across all services.
/// </summary>
public interface IMemoryDiagnosticsService
{
    /// <summary>
    /// Gets the complete memory diagnostics report including GC info and service breakdown.
    /// Results are cached for a short duration to avoid expensive GC calls.
    /// </summary>
    /// <returns>Complete memory diagnostics report.</returns>
    MemoryDiagnosticsDto GetDiagnostics();

    /// <summary>
    /// Gets just the GC generation sizes (useful for lightweight polling).
    /// </summary>
    /// <returns>GC generation size information.</returns>
    GcGenerationSizesDto GetGcGenerationSizes();
}
