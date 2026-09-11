namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the virtual currency feature.
/// </summary>
public class CurrencyOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "Currency";

    /// <summary>
    /// Gets or sets whether the feature is registered at all. With this off, no currency service
    /// is resolvable, the commands and pages are hidden, and every priced feature is free.
    /// Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets how long a hold reserves funds before other holds may use them again.
    /// An expired hold still commits; expiry only frees the reservation.
    /// Default is 120 seconds.
    /// </summary>
    public int HoldExpirySeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets the rate limit applied to user-to-user transfers, in transfers per minute.
    /// Default is 5.
    /// </summary>
    public int MaxTransferPerMinute { get; set; } = 5;

    /// <summary>
    /// Gets or sets the debt floor pre-filled when an admin turns debt on for a currency.
    /// Stored negative. Default is -100.
    /// </summary>
    public long DefaultDebtFloor { get; set; } = -100;

    /// <summary>
    /// Gets or sets the number of ledger rows shown per page in wallet history.
    /// Default is 10.
    /// </summary>
    public int HistoryPageSize { get; set; } = 10;
}
