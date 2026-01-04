using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Interface for services that can report their memory consumption.
/// Implementing services should calculate their approximate in-memory footprint.
/// </summary>
public interface IMemoryReportable
{
    /// <summary>
    /// Gets the display name for this service in memory reports.
    /// </summary>
    string ServiceName { get; }

    /// <summary>
    /// Gets the estimated memory usage of this service.
    /// </summary>
    /// <returns>Memory report containing usage details.</returns>
    ServiceMemoryReportDto GetMemoryReport();
}
