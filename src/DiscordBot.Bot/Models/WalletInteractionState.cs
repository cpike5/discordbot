namespace DiscordBot.Bot.Models;

/// <summary>
/// State behind the <c>/wallet pay</c> confirmation button. The idempotency key is created with
/// the state, not when the button is clicked, so a double click pays once.
/// </summary>
public class WalletPayState
{
    /// <summary>Gets or sets the currency being sent.</summary>
    public Guid CurrencyId { get; set; }

    /// <summary>Gets or sets the Discord user snowflake ID receiving the payment.</summary>
    public ulong RecipientId { get; set; }

    /// <summary>Gets or sets the amount to send, in whole units.</summary>
    public long Amount { get; set; }

    /// <summary>Gets or sets the optional note written to both ledger rows.</summary>
    public string? Note { get; set; }

    /// <summary>Gets or sets the idempotency key the transfer is written under.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;
}

/// <summary>
/// State behind the <c>/wallet history</c> pagination buttons.
/// </summary>
public class WalletHistoryState
{
    /// <summary>Gets or sets the wallet whose ledger is being paged.</summary>
    public Guid WalletId { get; set; }

    /// <summary>Gets or sets the currency the wallet belongs to, for rendering amounts.</summary>
    public Guid CurrencyId { get; set; }
}
