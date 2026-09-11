using Discord;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using System.Text;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// The presentation half of the wallet commands: picking which currency a command meant, turning
/// a service error code into something a user can act on, and building the embeds.
/// <para>
/// This lives outside the command modules because Discord.NET interaction modules cannot be
/// exercised in unit tests — <c>Context</c> and <c>RespondAsync</c> both need a live client — so
/// everything worth asserting on is here instead.
/// </para>
/// </summary>
public static class CurrencyFormatting
{
    /// <summary>Renders an amount with its currency symbol, e.g. <c>5 🪙</c>.</summary>
    public static string Amount(long amount, string symbol) => $"{amount:N0} {symbol}".Trim();

    /// <summary>
    /// Picks the currency a command should act on from the ones visible in the guild.
    /// </summary>
    /// <param name="visible">Currencies visible in the guild, in display order.</param>
    /// <param name="selector">
    /// The user's <c>currency</c> option: a currency ID from autocomplete, or a name typed by
    /// hand. Null or empty means "pick for me", which only works when there is exactly one.
    /// </param>
    /// <returns>The chosen currency, or the message explaining why none was chosen.</returns>
    public static CurrencyResolution ResolveCurrency(IReadOnlyList<CurrencyDto> visible, string? selector)
    {
        ArgumentNullException.ThrowIfNull(visible);

        if (visible.Count == 0)
        {
            return CurrencyResolution.Failed(
                "There are no currencies in this server yet. An administrator can create one with `/currency create`.");
        }

        if (string.IsNullOrWhiteSpace(selector))
        {
            // One currency is the common case, and making people name it every time is noise.
            return visible.Count == 1
                ? CurrencyResolution.Chosen(visible[0])
                : CurrencyResolution.Failed(
                    "There is more than one currency here. Pick one with the `currency` option: " +
                    string.Join(", ", visible.Select(c => $"**{c.Name}**")) + ".");
        }

        var trimmed = selector.Trim();

        // Autocomplete sends the ID; a hand-typed value is a name.
        var match = Guid.TryParse(trimmed, out var currencyId)
            ? visible.FirstOrDefault(c => c.Id == currencyId)
            : visible.FirstOrDefault(c => c.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));

        return match != null
            ? CurrencyResolution.Chosen(match)
            : CurrencyResolution.Failed($"No currency named `{trimmed}` is available in this server.");
    }

    /// <summary>
    /// Turns a <see cref="CurrencyErrors"/> code into a sentence, using the balance and symbol
    /// when the code is about money.
    /// </summary>
    public static string DescribeError(string? error, long balance = 0, string symbol = "", long amount = 0) => error switch
    {
        CurrencyErrors.CurrencyNotFound => "That currency no longer exists.",
        CurrencyErrors.CurrencyInactive => "That currency has been deactivated.",
        CurrencyErrors.InsufficientFunds =>
            $"You need {Amount(amount, symbol)} and you have {Amount(balance, symbol)}.",
        CurrencyErrors.InDebt =>
            $"You owe {Amount(Math.Abs(balance), symbol)}. You can't spend or send until you're back above zero.",
        CurrencyErrors.InvalidAmount => "The amount must be a whole number greater than zero.",
        CurrencyErrors.TransfersDisabled => "That currency can't be sent between users.",
        CurrencyErrors.SelfTransfer => "You can't send currency to yourself.",
        CurrencyErrors.FineRequiresGuildCurrency => "Fines only work on this server's own currencies.",
        CurrencyErrors.ReasonRequired => "A reason is required.",
        CurrencyErrors.DuplicateName => "A currency with that name already exists here.",
        CurrencyErrors.InvalidCurrencyRules => "Those currency rules aren't valid. A debt floor must be negative and is required when debt is allowed.",
        CurrencyErrors.NotAuthorizedToMint => "You aren't a mint authority for that currency.",
        CurrencyErrors.WalletNotFound => "That wallet doesn't exist.",
        _ => "The request could not be completed."
    };

    /// <summary>
    /// Turns a refused <see cref="ChargeHoldStatus"/> into the sentence a priced feature shows
    /// instead of doing the thing. Every caller renders the same wording, so a refusal reads the
    /// same in Discord and in the portal.
    /// </summary>
    /// <param name="status">Why the charge seam said no.</param>
    /// <param name="price">What the feature costs here.</param>
    /// <param name="balance">What the user had when the decision was made.</param>
    /// <param name="symbol">Symbol of the priced currency.</param>
    /// <param name="subject">
    /// What is being paid for, as the subject of the sentence: "This sound" reads better on a
    /// soundboard refusal than the generic "This".
    /// </param>
    public static string DescribeChargeRefusal(
        ChargeHoldStatus status,
        long price,
        long balance,
        string? symbol,
        string subject = "This")
    {
        var currency = symbol ?? string.Empty;

        return status switch
        {
            ChargeHoldStatus.InsufficientFunds =>
                $"{subject} costs {Amount(price, currency)}. You have {Amount(balance, currency)}.",
            ChargeHoldStatus.InDebt =>
                $"You owe {Amount(Math.Abs(balance), currency)}. Priced features are locked until you're back above zero.",
            ChargeHoldStatus.CurrencyInactive =>
                "The currency this is priced in has been deactivated, so it can't be paid for right now.",
            ChargeHoldStatus.NoWallet =>
                "The currency this is priced in is no longer available, so it can't be paid for right now.",
            _ => "This could not be paid for right now."
        };
    }

    /// <summary>Builds the balance card for one or more wallets.</summary>
    public static Embed BalanceEmbed(string username, IReadOnlyList<WalletDto> wallets)
    {
        ArgumentNullException.ThrowIfNull(wallets);

        var embed = new EmbedBuilder()
            .WithTitle($"💰 {username}'s Wallet")
            .WithColor(wallets.Any(w => w.IsInDebt) ? Color.Red : Color.Gold)
            .WithCurrentTimestamp();

        if (wallets.Count == 0)
        {
            embed.WithDescription("No balances yet.");
            return embed.Build();
        }

        foreach (var wallet in wallets)
        {
            var value = Amount(wallet.Balance, wallet.CurrencySymbol);
            embed.AddField(
                wallet.CurrencyName,
                wallet.IsInDebt ? $"{value} — **in debt**" : value,
                inline: true);
        }

        if (wallets.Any(w => w.IsInDebt))
        {
            embed.WithFooter("Priced features are locked while a balance is below zero.");
        }

        return embed.Build();
    }

    /// <summary>Builds one page of a wallet's ledger.</summary>
    public static Embed HistoryEmbed(
        string username,
        CurrencyDto currency,
        PagedResult<LedgerTransactionDto> page)
    {
        ArgumentNullException.ThrowIfNull(currency);
        ArgumentNullException.ThrowIfNull(page);

        var totalPages = TotalPages(page.TotalCount, page.PageSize);

        var embed = new EmbedBuilder()
            .WithTitle($"📜 {username} — {currency.Name}")
            .WithColor(Color.Blue)
            .WithFooter($"Page {page.Page} of {totalPages} • {page.TotalCount} entries")
            .WithCurrentTimestamp();

        if (page.Items.Count == 0)
        {
            embed.WithDescription("No transactions yet.");
            return embed.Build();
        }

        var description = new StringBuilder();

        foreach (var row in page.Items)
        {
            var timestamp = new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc)).ToUnixTimeSeconds();
            var sign = row.Amount >= 0 ? "+" : "−";
            var detail = row.Reason ?? row.FeatureKey;

            description
                .Append(TypeEmoji(row.Type))
                .Append(" **")
                .Append(sign)
                .Append(Amount(Math.Abs(row.Amount), currency.Symbol))
                .Append("** — ")
                .Append(row.Type)
                .Append(" • <t:")
                .Append(timestamp)
                .Append(":R>");

            if (!string.IsNullOrWhiteSpace(detail))
            {
                description.Append("\n> ").Append(Truncate(detail, 120));
            }

            description.Append("\nBalance: ").Append(Amount(row.BalanceAfter, currency.Symbol)).Append('\n');
        }

        embed.WithDescription(description.ToString());
        return embed.Build();
    }

    /// <summary>Builds the confirmation shown before a transfer is sent.</summary>
    public static Embed PayConfirmationEmbed(ulong recipientId, long amount, CurrencyDto currency, string? note)
    {
        ArgumentNullException.ThrowIfNull(currency);

        var embed = new EmbedBuilder()
            .WithTitle("Confirm Payment")
            .WithDescription($"Send **{Amount(amount, currency.Symbol)}** to <@{recipientId}>?")
            .WithColor(Color.Orange)
            .AddField("Currency", currency.Name, inline: true)
            .WithCurrentTimestamp();

        if (!string.IsNullOrWhiteSpace(note))
        {
            embed.AddField("Note", Truncate(note, 512));
        }

        return embed.Build();
    }

    /// <summary>Builds the receipt for a completed transfer.</summary>
    public static Embed PayReceiptEmbed(ulong senderId, ulong recipientId, long amount, CurrencyDto currency, long senderBalance)
    {
        ArgumentNullException.ThrowIfNull(currency);

        return new EmbedBuilder()
            .WithTitle("✅ Payment Sent")
            .WithDescription($"<@{senderId}> sent **{Amount(amount, currency.Symbol)}** to <@{recipientId}>.")
            .WithColor(Color.Green)
            .AddField("Your balance", Amount(senderBalance, currency.Symbol), inline: true)
            .WithCurrentTimestamp()
            .Build();
    }

    /// <summary>Builds the receipt for a mint.</summary>
    public static Embed MintReceiptEmbed(ulong recipientId, long amount, CurrencyDto currency, string reason, long balanceAfter)
    {
        ArgumentNullException.ThrowIfNull(currency);

        return new EmbedBuilder()
            .WithTitle("🪙 Minted")
            .WithDescription($"Created **{Amount(amount, currency.Symbol)}** for <@{recipientId}>.")
            .WithColor(Color.Gold)
            .AddField("Reason", Truncate(reason, 512))
            .AddField("New balance", Amount(balanceAfter, currency.Symbol), inline: true)
            .WithCurrentTimestamp()
            .Build();
    }

    /// <summary>
    /// Builds the fine receipt. A fine that hit zero or the debt floor says so, because the
    /// number written to the ledger is not the number the moderator typed.
    /// </summary>
    public static Embed FineReceiptEmbed(
        ulong targetUserId,
        long requestedAmount,
        FineResult result,
        CurrencyDto currency,
        string reason,
        long? moderationCaseNumber)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(currency);

        var applied = result.ClampedAmount ?? requestedAmount;
        var balanceAfter = result.Transaction?.BalanceAfter ?? 0;

        var embed = new EmbedBuilder()
            .WithTitle("⚖️ Fine Issued")
            .WithDescription($"Fined <@{targetUserId}> **{Amount(applied, currency.Symbol)}**.")
            .WithColor(Color.Red)
            .AddField("Reason", Truncate(reason, 512))
            .AddField("New balance", Amount(balanceAfter, currency.Symbol), inline: true)
            .WithCurrentTimestamp();

        if (result.WasClamped)
        {
            embed.AddField(
                "Clamped",
                $"Requested {Amount(requestedAmount, currency.Symbol)}; {(currency.AllowNegative ? "the debt floor" : "a balance of zero")} stopped it at {Amount(applied, currency.Symbol)}.");
        }

        if (moderationCaseNumber.HasValue)
        {
            embed.AddField("Mod case", $"#{moderationCaseNumber.Value}", inline: true);
        }

        return embed.Build();
    }

    /// <summary>Builds the list of currencies visible in a guild.</summary>
    public static Embed CurrencyListEmbed(string guildName, IReadOnlyList<CurrencyDto> currencies)
    {
        ArgumentNullException.ThrowIfNull(currencies);

        var embed = new EmbedBuilder()
            .WithTitle($"🪙 Currencies in {guildName}")
            .WithColor(Color.Gold)
            .WithCurrentTimestamp();

        if (currencies.Count == 0)
        {
            embed.WithDescription("No currencies here yet. An administrator can create one with `/currency create`.");
            return embed.Build();
        }

        foreach (var currency in currencies)
        {
            var rules = new List<string>
            {
                currency.Scope == CurrencyScope.Global ? "global" : "this server",
                currency.IsTransferable ? "transferable" : "not transferable"
            };

            if (currency.AllowNegative)
            {
                rules.Add($"debt to {currency.DebtFloor ?? 0}");
            }

            embed.AddField($"{currency.Symbol} {currency.Name}", string.Join(" • ", rules));
        }

        return embed.Build();
    }

    /// <summary>Number of pages a ledger of this size spans, never less than one.</summary>
    public static int TotalPages(int totalCount, int pageSize) =>
        pageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

    private static string TypeEmoji(LedgerTransactionType type) => type switch
    {
        LedgerTransactionType.Mint => "🪙",
        LedgerTransactionType.TransferIn => "📥",
        LedgerTransactionType.TransferOut => "📤",
        LedgerTransactionType.Spend => "🛒",
        LedgerTransactionType.Refund => "↩️",
        LedgerTransactionType.Fine => "⚖️",
        LedgerTransactionType.Adjustment => "🔧",
        _ => "•"
    };

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
}

/// <summary>
/// The outcome of picking a currency for a command: one currency, or the message to show instead.
/// </summary>
public record CurrencyResolution
{
    /// <summary>The chosen currency, null when none could be chosen.</summary>
    public CurrencyDto? Currency { get; init; }

    /// <summary>The message to show the user when no currency could be chosen.</summary>
    public string? Error { get; init; }

    /// <summary>True when exactly one currency was chosen.</summary>
    public bool Success => Currency != null;

    /// <summary>Creates a successful resolution.</summary>
    public static CurrencyResolution Chosen(CurrencyDto currency) => new() { Currency = currency };

    /// <summary>Creates a failed resolution carrying the message to show.</summary>
    public static CurrencyResolution Failed(string error) => new() { Error = error };
}
