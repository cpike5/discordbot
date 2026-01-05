namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for Elastic Stack integration (Elasticsearch, APM).
/// </summary>
public class ElasticOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "Elastic";

    /// <summary>
    /// Gets or sets the Elastic Cloud ID (for Elastic Cloud deployments).
    /// When set, takes precedence over Endpoints.
    /// </summary>
    public string? CloudId { get; set; }

    /// <summary>
    /// Gets or sets the API key for authentication.
    /// Use environment variables or user secrets in production.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the Elasticsearch endpoint URLs (for self-hosted deployments).
    /// </summary>
    public string[] Endpoints { get; set; } = [];

    /// <summary>
    /// Gets or sets the index format for logs (supports date placeholders).
    /// Default: discordbot-logs-{0:yyyy.MM.dd}
    /// </summary>
    public string IndexFormat { get; set; } = "discordbot-logs-{0:yyyy.MM.dd}";

    /// <summary>
    /// Gets or sets the Elastic APM server URL.
    /// </summary>
    public string? ApmServerUrl { get; set; }

    /// <summary>
    /// Gets or sets the APM secret token for authentication.
    /// </summary>
    public string? ApmSecretToken { get; set; }

    /// <summary>
    /// Gets or sets the environment name (development, staging, production).
    /// </summary>
    public string Environment { get; set; } = "development";
}
