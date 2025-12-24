namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for general application settings.
/// </summary>
public class ApplicationOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "Application";

    /// <summary>
    /// Gets or sets the application title displayed in the admin UI.
    /// Default is "Discord Bot".
    /// </summary>
    public string Title { get; set; } = "Discord Bot";

    /// <summary>
    /// Gets or sets the base URL of the application for generating links and redirects.
    /// Default is "https://localhost:5001".
    /// </summary>
    public string BaseUrl { get; set; } = "https://localhost:5001";

    /// <summary>
    /// Gets or sets the contact email for application support and notifications.
    /// Default is empty string.
    /// </summary>
    public string ContactEmail { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the application version displayed in the UI.
    /// Default is "0.1.0".
    /// </summary>
    public string Version { get; set; } = "0.1.0";
}
