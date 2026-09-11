using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// The <c>/currency</c> command group: creating a guild currency and listing the ones visible
/// here. Global currencies and bot credit are portal-only, by design.
/// </summary>
[Group("currency", "Virtual currency administration")]
[RequireGuildActive]
[RequireCurrencyEnabled]
public class CurrencyModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ICurrencyService _currencyService;
    private readonly IAuditLogService _auditLog;
    private readonly CurrencyOptions _options;
    private readonly ILogger<CurrencyModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyModule"/> class.
    /// </summary>
    public CurrencyModule(
        ICurrencyService currencyService,
        IAuditLogService auditLog,
        IOptions<CurrencyOptions> options,
        ILogger<CurrencyModule> logger)
    {
        _currencyService = currencyService;
        _auditLog = auditLog;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Creates a currency for this guild. The creator becomes its first mint authority.
    /// </summary>
    [SlashCommand("create", "Create a currency for this server")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task CreateAsync(
        [Summary("name", "What the currency is called")] string name,
        [Summary("symbol", "Emoji or short text shown next to amounts")] string symbol,
        [Summary("transferable", "Whether members can pay each other (default yes)")] bool transferable = true,
        [Summary("allow-negative", "Whether fines may push a balance below zero")] bool allowNegative = false,
        [Summary("debt-floor", "Most negative balance a fine may reach, e.g. -100")] long? debtFloor = null)
    {
        await DeferAsync(ephemeral: true);

        // The configured default is what the portal pre-fills too, so a command-created currency
        // and a portal-created one land on the same floor.
        long? floor = allowNegative ? debtFloor ?? _options.DefaultDebtFloor : null;

        var result = await _currencyService.CreateAsync(
            new CurrencyCreateDto
            {
                Scope = CurrencyScope.Guild,
                GuildId = Context.Guild.Id,
                Name = name,
                Symbol = symbol,
                IsTransferable = transferable,
                AllowNegative = allowNegative,
                DebtFloor = floor
            },
            Context.User.Id);

        if (!result.Success)
        {
            await FollowupAsync(
                embed: EmbedHelper.Error("Currency Not Created", CurrencyFormatting.DescribeError(result.Error)),
                ephemeral: true);
            return;
        }

        var currency = result.Currency!;

        _logger.LogInformation(
            "Currency {CurrencyId} ({Name}) created in guild {GuildId} by {UserId}",
            currency.Id, currency.Name, Context.Guild.Id, Context.User.Id);

        await _auditLog.CreateBuilder()
            .ForCategory(AuditLogCategory.Configuration)
            .WithAction(AuditLogAction.CurrencyCreated)
            .ByUser(Context.User.Id.ToString())
            .InGuild(Context.Guild.Id)
            .OnTarget("Currency", currency.Id.ToString())
            .WithDetails(new
            {
                currency.Name,
                currency.Symbol,
                currency.IsTransferable,
                currency.AllowNegative,
                currency.DebtFloor,
                source = "discord"
            })
            .LogAsync();

        var description =
            $"**{currency.Symbol} {currency.Name}** is live. You can mint it with `/wallet mint`.";

        await FollowupAsync(
            embed: EmbedHelper.Success("Currency Created", description),
            ephemeral: true);
    }

    /// <summary>
    /// Lists the currencies usable in this guild: the guild's own plus the active globals.
    /// </summary>
    [SlashCommand("list", "List the currencies available here")]
    public async Task ListAsync()
    {
        await DeferAsync(ephemeral: true);

        var currencies = await _currencyService.GetVisibleInGuildAsync(Context.Guild.Id);

        await FollowupAsync(
            embed: CurrencyFormatting.CurrencyListEmbed(Context.Guild.Name, currencies),
            ephemeral: true);
    }
}
