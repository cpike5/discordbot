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
    /// Gets or sets the data stream name for logs.
    /// Uses Elastic data streams with ILM for automatic rollover.
    /// Default: logs-discordbot-default
    /// </summary>
    public string DataStream { get; set; } = "logs-discordbot-default";

    /// <summary>
    /// Gets or sets the bootstrap method for index templates.
    /// Options: None, Silent, Failure
    /// Default: Silent
    /// </summary>
    public string BootstrapMethod { get; set; } = "Silent";

    /// <summary>
    /// Gets or sets the ILM policy name to use for data streams.
    /// Default: logs
    /// </summary>
    public string IlmPolicy { get; set; } = "logs";

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

    /// <summary>
    /// Gets or sets whether Elastic APM is enabled.
    /// When false, the APM agent will not initialize.
    /// Default is true.
    /// </summary>
    public bool ApmEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether APM recording is enabled.
    /// When false, the agent runs but does not record or send data.
    /// Useful for temporarily disabling APM without configuration changes.
    /// Default is true.
    /// </summary>
    public bool ApmRecording { get; set; } = true;
}
