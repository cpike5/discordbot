using Discord.WebSocket;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Constants;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Guilds.Currency;

/// <summary>
/// Feature prices for one guild. Today the priceable features are the guild's soundboard sounds,
/// each listed with the price it carries and the roles that pay nothing.
/// <para>
/// Every row is keyed by <see cref="CurrencyFeatureKeys.Soundboard(Guid)"/>. That is the whole
/// contract with the charge seam: <c>SoundboardOrchestrationService</c> asks for a hold on exactly
/// that string, and the portal's price badge looks the sound up by it, so a key built any other
/// way would save a price nothing ever charges.
/// </para>
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class PricesModel : GuildPageModelBase
{
    private readonly IGuildService _guildService;
    private readonly ISoundService _soundService;
    private readonly DiscordSocketClient _discordClient;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<PricesModel> _logger;
    private readonly ICurrencyService? _currencyService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PricesModel"/> class.
    /// </summary>
    /// <param name="guildService">The guild service.</param>
    /// <param name="soundService">The sound service, for the guild's priceable sounds.</param>
    /// <param name="discordClient">The Discord client, for the exempt-role picker.</param>
    /// <param name="settingsService">The settings service, for the runtime feature switch.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="currencyService">The currency service, null when the feature is off.</param>
    public PricesModel(
        IGuildService guildService,
        ISoundService soundService,
        DiscordSocketClient discordClient,
        ISettingsService settingsService,
        ILogger<PricesModel> logger,
        ICurrencyService? currencyService = null)
    {
        _guildService = guildService;
        _soundService = soundService;
        _discordClient = discordClient;
        _settingsService = settingsService;
        _logger = logger;
        _currencyService = currencyService;
    }

    /// <summary>The sounds, the prices they carry, and the pickers for changing them.</summary>
    public CurrencyPricesViewModel ViewModel { get; set; } = new();

    /// <summary>Whether currency features are switched off at runtime, so nothing is charged.</summary>
    public bool IsCurrencyRuntimeDisabled { get; set; }

    /// <summary>
    /// Loads the guild's sounds alongside the prices already set for them.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from the route.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        if (_currencyService == null)
        {
            return NotFound();
        }

        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            return NotFound();
        }

        IsCurrencyRuntimeDisabled =
            !(await _settingsService.GetSettingValueAsync<bool?>("Features:CurrencyEnabled", cancellationToken) ?? true);

        var sounds = await _soundService.GetAllByGuildAsync(guildId, cancellationToken);
        var prices = await _currencyService.GetPricesForGuildAsync(guildId, cancellationToken);
        var currencies = await _currencyService.GetVisibleInGuildAsync(guildId, includeInactive: false, cancellationToken);

        // One lookup keyed exactly the way the charge seam builds its key.
        var pricesByKey = prices
            .GroupBy(p => p.FeatureKey, StringComparer.Ordinal)
            // A guild entry wins over the entry that applies everywhere, the same order the price
            // lookup resolves them in.
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(p => p.GuildId.HasValue).First(),
                StringComparer.Ordinal);

        var rows = sounds
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(sound =>
            {
                var featureKey = CurrencyFeatureKeys.Soundboard(sound.Id);
                pricesByKey.TryGetValue(featureKey, out var price);

                return new CurrencyPriceRowViewModel
                {
                    SoundId = sound.Id,
                    SoundName = sound.Name,
                    FeatureKey = featureKey,
                    DurationSeconds = sound.DurationSeconds,
                    PlayCount = sound.PlayCount,
                    Price = price is { IsActive: true } ? price : null
                };
            })
            .ToList();

        var soundKeys = rows.Select(r => r.FeatureKey).ToHashSet(StringComparer.Ordinal);

        ViewModel = new CurrencyPricesViewModel
        {
            GuildId = guildId,
            GuildName = guild.Name,
            Sounds = rows,
            Currencies = currencies.OrderBy(c => c.Name).ToList(),
            Roles = BuildRoleOptions(guildId),
            // Anything priced here that is not one of this guild's sounds: a global entry, or a
            // feature area a later project added. Read-only, so nothing that costs money is hidden.
            OtherPrices = prices
                .Where(p => p.IsActive && !soundKeys.Contains(p.FeatureKey))
                .OrderBy(p => p.FeatureKey, StringComparer.Ordinal)
                .ToList()
        };

        _logger.LogDebug(
            "Loaded prices page for guild {GuildId}: {SoundCount} sounds, {PricedCount} priced",
            guildId, rows.Count, ViewModel.PricedCount);

        PopulateGuildLayout(
            guildId,
            guild.Name,
            guild.IconUrl,
            "currency",
            "Prices",
            "What using a feature costs. Anything without a price stays free.");

        return Page();
    }

    /// <summary>
    /// The roles offered in the exempt picker, highest first. Managed and @@everyone roles are left
    /// out: the first are bot-owned and the second would make the price meaningless.
    /// </summary>
    private List<CurrencyRoleOptionViewModel> BuildRoleOptions(ulong guildId)
    {
        var guild = _discordClient.GetGuild(guildId);

        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} is not in the Discord cache; exempt roles are unavailable", guildId);
            return new List<CurrencyRoleOptionViewModel>();
        }

        return guild.Roles
            .Where(r => !r.IsEveryone && !r.IsManaged)
            .OrderByDescending(r => r.Position)
            .Select(r => new CurrencyRoleOptionViewModel
            {
                Id = r.Id,
                Name = r.Name,
                Color = r.Color.ToString()
            })
            .ToList();
    }
}
