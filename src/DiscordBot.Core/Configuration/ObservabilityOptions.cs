namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for external observability tools.
/// </summary>
public class ObservabilityOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "Observability";

    /// <summary>
    /// Gets or sets the URL to the Kibana dashboard.
    /// When configured, a link to Kibana will appear in the admin sidebar.
    /// Leave null or empty to hide the link.
    /// </summary>
    public string? KibanaUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL to the Seq log aggregation dashboard.
    /// When configured, a link to Seq will appear in the admin sidebar.
    /// Leave null or empty to hide the link.
    /// </summary>
    public string? SeqUrl { get; set; }

    /// <summary>
    /// Gets a value indicating whether any observability URLs are configured.
    /// </summary>
    public bool HasAnyUrls => !string.IsNullOrEmpty(KibanaUrl)
                            || !string.IsNullOrEmpty(SeqUrl);
}
