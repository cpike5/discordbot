using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Autocomplete;
using DiscordBot.Bot.Components;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.Models;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// The <c>/wallet</c> command group: balances, history, transfers, mints and fines.
/// Everything here reads the currencies visible in the guild — the guild's own plus the active
/// global ones — and refuses with the service's own reason when a balance rule says no.
/// </summary>
[Group("wallet", "Virtual currency wallet commands")]
[RequireGuildActive]
[RequireCurrencyEnabled]
public class WalletModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ICurrencyService _currencyService;
    private readonly IWalletService _walletService;
    private readonly IMintService _mintService;
    private readonly IModerationService _moderationService;
    private readonly IInteractionStateService _stateService;
    private readonly CurrencyOptions _options;
    private readonly ILogger<WalletModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WalletModule"/> class.
    /// </summary>
    public WalletModule(
        ICurrencyService currencyService,
        IWalletService walletService,
        IMintService mintService,
        IModerationService moderationService,
        IInteractionStateService stateService,
        IOptions<CurrencyOptions> options,
        ILogger<WalletModule> logger)
    {
        _currencyService = currencyService;
        _walletService = walletService;
        _mintService = mintService;
        _moderationService = moderationService;
        _stateService = stateService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Shows the caller's balances, or the balance in one currency.
    /// </summary>
    [SlashCommand("balance", "Show your balances")]
    public async Task BalanceAsync(
        [Summary("currency", "Which currency to show (defaults to all of them)")]
        [Autocomplete(typeof(CurrencyAutocompleteHandler))] string? currency = null)
    {
        _logger.LogInformation(
            "Wallet balance requested by {UserId} in guild {GuildId} (currency {Currency})",
            Context.User.Id, Context.Guild.Id, currency ?? "all");

        await DeferAsync(ephemeral: true);

        var visible = await _currencyService.GetVisibleInGuildAsync(Context.Guild.Id);

        if (visible.Count == 0)
        {
            await FollowupAsync(
                embed: EmbedHelper.EmptyState(
                    "No Currencies",
                    "There are no currencies in this server yet. An administrator can create one with `/currency create`."),
                ephemeral: true);
            return;
        }

        // Without a currency option this shows every wallet; with one it narrows to that currency.
        var chosen = string.IsNullOrWhiteSpace(currency)
            ? null
            : CurrencyFormatting.ResolveCurrency(visible, currency);

        if (chosen is { Success: false })
        {
            await FollowupAsync(embed: EmbedHelper.Error("Currency Not Found", chosen.Error!), ephemeral: true);
            return;
        }

        var wallets = await _walletService.GetWalletsForUserAsync(Context.User.Id, Context.Guild.Id);

        // A user with no wallet in a currency has a balance of zero, which is worth showing.
        var rows = visible
            .Where(c => chosen?.Currency == null || c.Id == chosen.Currency.Id)
            .Select(c => wallets.FirstOrDefault(w => w.CurrencyId == c.Id) ?? new WalletDto
            {
                CurrencyId = c.Id,
                CurrencyName = c.Name,
                CurrencySymbol = c.Symbol,
                GuildId = c.GuildId,
                UserId = Context.User.Id,
                Balance = 0
            })
            .ToList();

        await FollowupAsync(
            embed: CurrencyFormatting.BalanceEmbed(Context.User.Username, rows),
            ephemeral: true);
    }

    /// <summary>
    /// Shows a page of the caller's ledger in one currency, with pagination buttons.
    /// </summary>
    [SlashCommand("history", "Show your recent transactions")]
    public async Task HistoryAsync(
        [Summary("currency", "Which currency to show")]
        [Autocomplete(typeof(CurrencyAutocompleteHandler))] string? currency = null)
    {
        await DeferAsync(ephemeral: true);

        var visible = await _currencyService.GetVisibleInGuildAsync(Context.Guild.Id);
        var resolution = CurrencyFormatting.ResolveCurrency(visible, currency);

        if (!resolution.Success)
        {
            await FollowupAsync(embed: EmbedHelper.Error("Pick a Currency", resolution.Error!), ephemeral: true);
            return;
        }

        var chosen = resolution.Currency!;
        var wallet = await _walletService.GetWalletAsync(chosen.Id, Context.User.Id);

        if (wallet == null)
        {
            await FollowupAsync(
                embed: EmbedHelper.EmptyState("No History", $"You have no {chosen.Name} transactions yet."),
                ephemeral: true);
            return;
        }

        var page = await _walletService.GetHistoryAsync(wallet.Id, 1, _options.HistoryPageSize);

        var correlationId = _stateService.CreateState(
            Context.User.Id,
            new WalletHistoryState { WalletId = wallet.Id, CurrencyId = chosen.Id },
            TimeSpan.FromMinutes(15));

        await FollowupAsync(
            embed: CurrencyFormatting.HistoryEmbed(Context.User.Username, chosen, page),
            components: BuildHistoryComponents(Context.User.Id, correlationId, 1, page.TotalCount, _options.HistoryPageSize),
            ephemeral: true);
    }

    /// <summary>
    /// Sends currency to another member, behind a confirmation button.
    /// </summary>
    [SlashCommand("pay", "Send currency to another member")]
    [RateLimitTransfers]
    public async Task PayAsync(
        [Summary("user", "Who to pay")] IUser user,
        [Summary("amount", "How much to send")] long amount,
        [Summary("currency", "Which currency to send")]
        [Autocomplete(typeof(CurrencyAutocompleteHandler))] string? currency = null,
        [Summary("note", "An optional note for both ledgers")] string? note = null)
    {
        await DeferAsync(ephemeral: true);

        if (amount <= 0)
        {
            await FollowupAsync(
                embed: EmbedHelper.Error("Invalid Amount", CurrencyFormatting.DescribeError(CurrencyErrors.InvalidAmount)),
                ephemeral: true);
            return;
        }

        if (user.Id == Context.User.Id)
        {
            await FollowupAsync(
                embed: EmbedHelper.Error("Invalid Recipient", CurrencyFormatting.DescribeError(CurrencyErrors.SelfTransfer)),
                ephemeral: true);
            return;
        }

        if (user.IsBot)
        {
            await FollowupAsync(
                embed: EmbedHelper.Error("Invalid Recipient", "Bots don't hold wallets."),
                ephemeral: true);
            return;
        }

        var visible = await _currencyService.GetVisibleInGuildAsync(Context.Guild.Id);
        var resolution = CurrencyFormatting.ResolveCurrency(visible, currency);

        if (!resolution.Success)
        {
            await FollowupAsync(embed: EmbedHelper.Error("Pick a Currency", resolution.Error!), ephemeral: true);
            return;
        }

        var chosen = resolution.Currency!;

        if (!chosen.IsTransferable)
        {
            await FollowupAsync(
                embed: EmbedHelper.Error("Transfers Disabled", CurrencyFormatting.DescribeError(CurrencyErrors.TransfersDisabled)),
                ephemeral: true);
            return;
        }

        // Balance is checked again inside the transfer; this is only so the user is not asked to
        // confirm something that is already going to fail.
        var wallet = await _walletService.GetWalletAsync(chosen.Id, Context.User.Id);
        var balance = wallet?.Balance ?? 0;

        if (balance < 0 || balance < amount)
        {
            var error = balance < 0 ? CurrencyErrors.InDebt : CurrencyErrors.InsufficientFunds;
            await FollowupAsync(
                embed: EmbedHelper.Error(
                    "Payment Refused",
                    CurrencyFormatting.DescribeError(error, balance, chosen.Symbol, amount)),
                ephemeral: true);
            return;
        }

        // The idempotency key is minted here and carried through the confirmation, so a double
        // click on the button cannot pay twice.
        var correlationId = _stateService.CreateState(
            Context.User.Id,
            new WalletPayState
            {
                CurrencyId = chosen.Id,
                RecipientId = user.Id,
                Amount = amount,
                Note = note,
                IdempotencyKey = $"pay:{Context.Guild.Id}:{Context.User.Id}:{Guid.NewGuid()}"
            },
            TimeSpan.FromMinutes(5));

        var components = new ComponentBuilder()
            .WithButton(
                "Send",
                ComponentIdBuilder.Build("wallet", "pay-confirm", Context.User.Id, correlationId),
                ButtonStyle.Success)
            .WithButton(
                "Cancel",
                ComponentIdBuilder.Build("wallet", "pay-cancel", Context.User.Id, correlationId),
                ButtonStyle.Secondary)
            .Build();

        await FollowupAsync(
            embed: CurrencyFormatting.PayConfirmationEmbed(user.Id, amount, chosen, note),
            components: components,
            ephemeral: true);
    }

    /// <summary>
    /// Creates units and credits them to a member. Only a mint authority may do this.
    /// </summary>
    [SlashCommand("mint", "Create currency and give it to a member")]
    public async Task MintAsync(
        [Summary("user", "Who receives the units")] IUser user,
        [Summary("amount", "How much to create")] long amount,
        [Summary("currency", "Which currency to mint")]
        [Autocomplete(typeof(CurrencyAutocompleteHandler))] string? currency = null,
        [Summary("reason", "Why these units exist")] string reason = "Manual mint")
    {
        await DeferAsync(ephemeral: true);

        var visible = await _currencyService.GetVisibleInGuildAsync(Context.Guild.Id);
        var resolution = CurrencyFormatting.ResolveCurrency(visible, currency);

        if (!resolution.Success)
        {
            await FollowupAsync(embed: EmbedHelper.Error("Pick a Currency", resolution.Error!), ephemeral: true);
            return;
        }

        var chosen = resolution.Currency!;

        var result = await _mintService.MintAsync(
            chosen.Id,
            user.Id,
            amount,
            reason,
            LedgerSource.Manual,
            $"mint:{chosen.Id}:{user.Id}:{Guid.NewGuid()}",
            Context.User.Id);

        if (!result.Success)
        {
            _logger.LogInformation(
                "Mint refused for user {UserId} on currency {CurrencyId}: {Error}",
                Context.User.Id, chosen.Id, result.Error);

            await FollowupAsync(
                embed: EmbedHelper.Error("Mint Refused", CurrencyFormatting.DescribeError(result.Error, symbol: chosen.Symbol, amount: amount)),
                ephemeral: true);
            return;
        }

        await FollowupAsync(
            embed: CurrencyFormatting.MintReceiptEmbed(
                user.Id, amount, chosen, reason, result.Transaction?.BalanceAfter ?? 0),
            ephemeral: true);
    }

    /// <summary>
    /// Fines a member. Guild currencies only, and the amount is clamped at zero or at the
    /// currency's debt floor.
    /// </summary>
    [SlashCommand("fine", "Fine a member")]
    [RequireModerator]
    public async Task FineAsync(
        [Summary("user", "Who to fine")] IUser user,
        [Summary("amount", "How much to take")] long amount,
        [Summary("reason", "Why they are being fined")] string reason,
        [Summary("currency", "Which currency to fine in")]
        [Autocomplete(typeof(CurrencyAutocompleteHandler))] string? currency = null,
        [Summary("open-case", "Also open a mod case for this fine")] bool openCase = false)
    {
        await DeferAsync(ephemeral: true);

        if (user.Id == Context.User.Id)
        {
            await FollowupAsync(embed: EmbedHelper.Error("Not Allowed", "You can't fine yourself."), ephemeral: true);
            return;
        }

        if (user.IsBot)
        {
            await FollowupAsync(embed: EmbedHelper.Error("Not Allowed", "Bots don't hold wallets."), ephemeral: true);
            return;
        }

        // A moderator cannot fine an administrator; that is the same hierarchy rule the other
        // moderation commands follow.
        if (Context.Guild.GetUser(user.Id) is { GuildPermissions.Administrator: true }
            && Context.User is SocketGuildUser { GuildPermissions.Administrator: false })
        {
            await FollowupAsync(
                embed: EmbedHelper.Error("Not Allowed", "You can't fine an administrator."),
                ephemeral: true);
            return;
        }

        var visible = await _currencyService.GetVisibleInGuildAsync(Context.Guild.Id);

        // Fines are a guild moderation action, so global currencies are not offered here.
        var guildCurrencies = visible.Where(c => c.Scope == CurrencyScope.Guild).ToList();
        var resolution = CurrencyFormatting.ResolveCurrency(guildCurrencies, currency);

        if (!resolution.Success)
        {
            await FollowupAsync(embed: EmbedHelper.Error("Pick a Currency", resolution.Error!), ephemeral: true);
            return;
        }

        var chosen = resolution.Currency!;

        Guid? caseId = null;
        long? caseNumber = null;

        if (openCase)
        {
            try
            {
                var modCase = await _moderationService.CreateCaseAsync(new ModerationCaseCreateDto
                {
                    GuildId = Context.Guild.Id,
                    TargetUserId = user.Id,
                    ModeratorUserId = Context.User.Id,
                    Type = CaseType.Note,
                    Reason = $"Fine: {reason}"
                });

                caseId = modCase.Id;
                caseNumber = modCase.CaseNumber;
            }
            catch (Exception ex)
            {
                // The fine is the point; a failed case link should not swallow it.
                _logger.LogError(ex, "Failed to open a mod case for a fine in guild {GuildId}", Context.Guild.Id);
            }
        }

        var result = await _walletService.FineAsync(
            chosen.Id, user.Id, amount, reason, Context.User.Id, caseId);

        if (!result.Success)
        {
            await FollowupAsync(
                embed: EmbedHelper.Error("Fine Refused", CurrencyFormatting.DescribeError(result.Error, symbol: chosen.Symbol, amount: amount)),
                ephemeral: true);
            return;
        }

        _logger.LogInformation(
            "User {TargetId} fined {Amount} of currency {CurrencyId} by moderator {ModeratorId} in guild {GuildId}",
            user.Id, amount, chosen.Id, Context.User.Id, Context.Guild.Id);

        await FollowupAsync(
            embed: CurrencyFormatting.FineReceiptEmbed(user.Id, amount, result, chosen, reason, caseNumber));
    }

    /// <summary>
    /// Builds the previous/next buttons for a page of history.
    /// </summary>
    internal static MessageComponent BuildHistoryComponents(
        ulong userId,
        string correlationId,
        int page,
        int totalCount,
        int pageSize)
    {
        var totalPages = CurrencyFormatting.TotalPages(totalCount, pageSize);

        return new ComponentBuilder()
            .WithButton(
                "◀ Previous",
                ComponentIdBuilder.Build("wallet", "history-page", userId, correlationId, (page - 1).ToString()),
                ButtonStyle.Secondary,
                disabled: page <= 1)
            .WithButton(
                "Next ▶",
                ComponentIdBuilder.Build("wallet", "history-page", userId, correlationId, (page + 1).ToString()),
                ButtonStyle.Secondary,
                disabled: page >= totalPages)
            .Build();
    }
}
