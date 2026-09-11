using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Components;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.Models;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Component handlers for the wallet commands: the <c>/wallet pay</c> confirmation and the
/// <c>/wallet history</c> pagination. IDs are built by <see cref="ComponentIdBuilder"/> and the
/// data behind them lives in <see cref="IInteractionStateService"/>.
/// </summary>
[RequireGuildActive]
[RequireCurrencyEnabled]
public class WalletComponentModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ICurrencyService _currencyService;
    private readonly IWalletService _walletService;
    private readonly IInteractionStateService _stateService;
    private readonly CurrencyOptions _options;
    private readonly ILogger<WalletComponentModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WalletComponentModule"/> class.
    /// </summary>
    public WalletComponentModule(
        ICurrencyService currencyService,
        IWalletService walletService,
        IInteractionStateService stateService,
        IOptions<CurrencyOptions> options,
        ILogger<WalletComponentModule> logger)
    {
        _currencyService = currencyService;
        _walletService = walletService;
        _stateService = stateService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Sends the payment the user confirmed.
    /// </summary>
    [ComponentInteraction("wallet:pay-confirm:*:*:")]
    public async Task HandlePayConfirmAsync()
    {
        var component = (SocketMessageComponent)Context.Interaction;

        var parts = await TryAuthorizeAsync(component.Data.CustomId);
        if (parts == null)
        {
            return;
        }

        if (!_stateService.TryGetState<WalletPayState>(parts.CorrelationId, out var state) || state == null)
        {
            await RespondAsync("This confirmation has expired. Run `/wallet pay` again.", ephemeral: true);
            return;
        }

        // Removing the state first makes a second click a no-op even before the idempotency key
        // gets its say.
        _stateService.TryRemoveState(parts.CorrelationId);

        await DeferAsync();

        var currency = await _currencyService.GetAsync(state.CurrencyId);
        if (currency == null)
        {
            await UpdateWithEmbedAsync(component, EmbedHelper.Error("Payment Refused", CurrencyFormatting.DescribeError(CurrencyErrors.CurrencyNotFound)));
            return;
        }

        var result = await _walletService.TransferAsync(
            state.CurrencyId,
            Context.User.Id,
            state.RecipientId,
            state.Amount,
            state.Note,
            state.IdempotencyKey);

        if (!result.Success)
        {
            _logger.LogInformation(
                "Transfer refused for user {UserId} on currency {CurrencyId}: {Error}",
                Context.User.Id, state.CurrencyId, result.Error);

            await UpdateWithEmbedAsync(
                component,
                EmbedHelper.Error(
                    "Payment Refused",
                    CurrencyFormatting.DescribeError(result.Error, result.SenderBalance, currency.Symbol, state.Amount)));
            return;
        }

        _logger.LogInformation(
            "User {UserId} sent {Amount} of currency {CurrencyId} to {RecipientId}",
            Context.User.Id, state.Amount, state.CurrencyId, state.RecipientId);

        await UpdateWithEmbedAsync(
            component,
            CurrencyFormatting.PayReceiptEmbed(
                Context.User.Id, state.RecipientId, state.Amount, currency, result.SenderBalance));
    }

    /// <summary>
    /// Drops a pending payment without writing anything.
    /// </summary>
    [ComponentInteraction("wallet:pay-cancel:*:*:")]
    public async Task HandlePayCancelAsync()
    {
        var component = (SocketMessageComponent)Context.Interaction;

        var parts = await TryAuthorizeAsync(component.Data.CustomId);
        if (parts == null)
        {
            return;
        }

        _stateService.TryRemoveState(parts.CorrelationId);

        await DeferAsync();
        await UpdateWithEmbedAsync(component, EmbedHelper.Info("Payment Cancelled", "Nothing was sent."));
    }

    /// <summary>
    /// Moves the history embed to another page.
    /// </summary>
    [ComponentInteraction("wallet:history-page:*:*:*")]
    public async Task HandleHistoryPageAsync()
    {
        var component = (SocketMessageComponent)Context.Interaction;

        var parts = await TryAuthorizeAsync(component.Data.CustomId);
        if (parts == null)
        {
            return;
        }

        if (!int.TryParse(parts.Data, out var page) || page < 1)
        {
            await RespondAsync("That page doesn't exist.", ephemeral: true);
            return;
        }

        if (!_stateService.TryGetState<WalletHistoryState>(parts.CorrelationId, out var state) || state == null)
        {
            await RespondAsync("This history view has expired. Run `/wallet history` again.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var currency = await _currencyService.GetAsync(state.CurrencyId);
        if (currency == null)
        {
            await UpdateWithEmbedAsync(component, EmbedHelper.Error("Currency Not Found", CurrencyFormatting.DescribeError(CurrencyErrors.CurrencyNotFound)));
            return;
        }

        var results = await _walletService.GetHistoryAsync(state.WalletId, page, _options.HistoryPageSize);

        await component.ModifyOriginalResponseAsync(props =>
        {
            props.Embed = CurrencyFormatting.HistoryEmbed(Context.User.Username, currency, results);
            props.Components = WalletModule.BuildHistoryComponents(
                Context.User.Id, parts.CorrelationId, page, results.TotalCount, _options.HistoryPageSize);
        });
    }

    /// <summary>
    /// Parses the custom ID and checks that the clicker is the user the component was built for.
    /// </summary>
    private async Task<ComponentIdParts?> TryAuthorizeAsync(string customId)
    {
        if (!ComponentIdBuilder.TryParse(customId, out var parts))
        {
            _logger.LogWarning("Invalid wallet component ID: {CustomId}", customId);
            await RespondAsync("This button is invalid or expired.", ephemeral: true);
            return null;
        }

        if (parts.UserId != Context.User.Id)
        {
            await RespondAsync("Only the person who ran the command can use these buttons.", ephemeral: true);
            return null;
        }

        return parts;
    }

    /// <summary>
    /// Replaces the original ephemeral response with a final embed and clears its buttons.
    /// </summary>
    private static Task UpdateWithEmbedAsync(SocketMessageComponent component, Embed embed) =>
        component.ModifyOriginalResponseAsync(props =>
        {
            props.Embed = embed;
            props.Components = new ComponentBuilder().Build();
        });
}
