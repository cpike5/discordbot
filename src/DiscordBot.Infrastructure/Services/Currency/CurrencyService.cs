using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services;

/// <summary>
/// Currency administration: creating and editing currencies, managing mint authorities, and
/// pricing features.
/// <para>
/// This service enforces the rules that belong to the data. Who may call it is decided above:
/// Discord preconditions on the command side, authorization handlers on the portal side.
/// </para>
/// </summary>
public class CurrencyService : ICurrencyService
{
    private const int MaxNameLength = 64;
    private const int MaxSymbolLength = 16;
    private const int MaxFeatureKeyLength = 128;

    private readonly ICurrencyRepository _currencies;
    private readonly IMintAuthorityRepository _authorities;
    private readonly IPriceRepository _prices;
    private readonly IWalletRepository _wallets;
    private readonly ILedgerRepository _ledger;
    private readonly ILogger<CurrencyService> _logger;

    public CurrencyService(
        ICurrencyRepository currencies,
        IMintAuthorityRepository authorities,
        IPriceRepository prices,
        IWalletRepository wallets,
        ILedgerRepository ledger,
        ILogger<CurrencyService> logger)
    {
        _currencies = currencies;
        _authorities = authorities;
        _prices = prices;
        _wallets = wallets;
        _ledger = ledger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CurrencyDto?> GetAsync(Guid currencyId, CancellationToken cancellationToken = default)
    {
        var currency = await _currencies.GetAsync(currencyId, cancellationToken);
        return currency?.ToDto();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CurrencyDto>> GetVisibleInGuildAsync(
        ulong guildId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var currencies = await _currencies.GetVisibleInGuildAsync(guildId, includeInactive, cancellationToken);
        return currencies.Select(c => c.ToDto()).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CurrencyDto>> GetGlobalAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var currencies = await _currencies.GetGlobalAsync(includeInactive, cancellationToken);
        return currencies.Select(c => c.ToDto()).ToList();
    }

    /// <inheritdoc />
    public async Task<CurrencyResult> CreateAsync(CurrencyCreateDto request, ulong createdById, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = request.Name?.Trim() ?? string.Empty;
        var symbol = request.Symbol?.Trim() ?? string.Empty;

        var invalid = ValidateRules(request.Scope, request.GuildId, name, symbol, request.AllowNegative, request.DebtFloor, request.IncomeAmount);
        if (invalid != null)
        {
            return CurrencyResult.Failed(invalid);
        }

        if (await _currencies.NameExistsAsync(request.Scope, request.GuildId, name, null, cancellationToken))
        {
            return CurrencyResult.Failed(CurrencyErrors.DuplicateName);
        }

        var currency = new Currency
        {
            Id = Guid.NewGuid(),
            Scope = request.Scope,
            GuildId = request.GuildId,
            Name = name,
            Symbol = symbol,
            // Guild currencies circulate by default; a global one such as bot credit does not.
            IsTransferable = request.IsTransferable ?? request.Scope == CurrencyScope.Guild,
            AllowNegative = request.AllowNegative,
            DebtFloor = request.AllowNegative ? request.DebtFloor : null,
            IncomeAmount = request.IncomeAmount,
            IncomeInterval = request.IncomeInterval,
            IsActive = true,
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow
        };

        await _currencies.AddAsync(currency, cancellationToken);

        // The creator can mint from the start, otherwise a new currency has no way to exist.
        await _authorities.AddAsync(
            new MintAuthority
            {
                Id = Guid.NewGuid(),
                CurrencyId = currency.Id,
                PrincipalType = MintPrincipalType.User,
                PrincipalId = createdById,
                GrantedById = createdById,
                GrantedAt = DateTime.UtcNow
            },
            cancellationToken);

        _logger.LogInformation(
            "Created {Scope} currency {Name} ({CurrencyId}) for guild {GuildId} by {CreatedById}",
            currency.Scope, currency.Name, currency.Id, currency.GuildId, createdById);

        return new CurrencyResult { Success = true, Currency = currency.ToDto() };
    }

    /// <inheritdoc />
    public async Task<CurrencyResult> UpdateAsync(Guid currencyId, CurrencyUpdateDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currency = await _currencies.GetAsync(currencyId, cancellationToken);
        if (currency == null)
        {
            return CurrencyResult.Failed(CurrencyErrors.CurrencyNotFound);
        }

        var name = request.Name?.Trim() ?? currency.Name;
        var symbol = request.Symbol?.Trim() ?? currency.Symbol;
        var allowNegative = request.AllowNegative ?? currency.AllowNegative;
        var debtFloor = request.DebtFloor ?? currency.DebtFloor;
        var incomeAmount = request.IncomeAmount ?? currency.IncomeAmount;

        var invalid = ValidateRules(currency.Scope, currency.GuildId, name, symbol, allowNegative, debtFloor, incomeAmount);
        if (invalid != null)
        {
            return CurrencyResult.Failed(invalid);
        }

        if (!string.Equals(name, currency.Name, StringComparison.OrdinalIgnoreCase) &&
            await _currencies.NameExistsAsync(currency.Scope, currency.GuildId, name, currencyId, cancellationToken))
        {
            return CurrencyResult.Failed(CurrencyErrors.DuplicateName);
        }

        currency.Name = name;
        currency.Symbol = symbol;
        currency.IsTransferable = request.IsTransferable ?? currency.IsTransferable;
        currency.AllowNegative = allowNegative;
        currency.DebtFloor = allowNegative ? debtFloor : null;
        currency.IncomeAmount = incomeAmount;
        currency.IncomeInterval = request.IncomeInterval ?? currency.IncomeInterval;

        await _currencies.UpdateAsync(currency, cancellationToken);

        _logger.LogInformation("Updated currency {Name} ({CurrencyId})", currency.Name, currency.Id);

        return new CurrencyResult { Success = true, Currency = currency.ToDto() };
    }

    /// <inheritdoc />
    public async Task<CurrencyResult> DeactivateAsync(Guid currencyId, CancellationToken cancellationToken = default)
    {
        var currency = await _currencies.GetAsync(currencyId, cancellationToken);
        if (currency == null)
        {
            return CurrencyResult.Failed(CurrencyErrors.CurrencyNotFound);
        }

        if (!currency.IsActive)
        {
            return new CurrencyResult { Success = true, Currency = currency.ToDto() };
        }

        // Currencies are never deleted: the wallets and the ledger stay readable.
        currency.IsActive = false;
        await _currencies.UpdateAsync(currency, cancellationToken);

        _logger.LogInformation("Deactivated currency {Name} ({CurrencyId})", currency.Name, currency.Id);

        return new CurrencyResult { Success = true, Currency = currency.ToDto() };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MintAuthorityDto>> GetMintAuthoritiesAsync(Guid currencyId, CancellationToken cancellationToken = default)
    {
        var authorities = await _authorities.GetForCurrencyAsync(currencyId, cancellationToken);
        return authorities.Select(a => a.ToDto()).ToList();
    }

    /// <inheritdoc />
    public async Task<MintAuthorityResult> GrantMintAuthorityAsync(
        Guid currencyId,
        MintPrincipalType principalType,
        ulong? principalId,
        ulong grantedById,
        CancellationToken cancellationToken = default)
    {
        var currency = await _currencies.GetAsync(currencyId, cancellationToken);
        if (currency == null)
        {
            return MintAuthorityResult.Failed(CurrencyErrors.CurrencyNotFound);
        }

        if (!currency.IsActive)
        {
            return MintAuthorityResult.Failed(CurrencyErrors.CurrencyInactive);
        }

        // System grants name no principal; user and role grants must name one.
        var expectsPrincipal = principalType != MintPrincipalType.System;
        if (expectsPrincipal != principalId.HasValue)
        {
            return MintAuthorityResult.Failed(CurrencyErrors.InvalidCurrencyRules);
        }

        var existing = await _authorities.ExistsAsync(currencyId, principalType, principalId, cancellationToken);
        if (existing)
        {
            var current = (await _authorities.GetForCurrencyAsync(currencyId, cancellationToken))
                .First(a => a.PrincipalType == principalType && a.PrincipalId == principalId);

            return new MintAuthorityResult { Success = true, Authority = current.ToDto() };
        }

        var authority = new MintAuthority
        {
            Id = Guid.NewGuid(),
            CurrencyId = currencyId,
            PrincipalType = principalType,
            PrincipalId = principalId,
            GrantedById = grantedById,
            GrantedAt = DateTime.UtcNow
        };

        await _authorities.AddAsync(authority, cancellationToken);

        _logger.LogInformation(
            "Granted {PrincipalType} mint authority on currency {CurrencyId} to {PrincipalId} by {GrantedById}",
            principalType, currencyId, principalId, grantedById);

        return new MintAuthorityResult { Success = true, Authority = authority.ToDto() };
    }

    /// <inheritdoc />
    public async Task<bool> RevokeMintAuthorityAsync(Guid authorityId, CancellationToken cancellationToken = default)
    {
        var authority = await _authorities.GetAsync(authorityId, cancellationToken);
        if (authority == null)
        {
            return false;
        }

        await _authorities.DeleteAsync(authority, cancellationToken);

        _logger.LogInformation("Revoked mint authority {AuthorityId} on currency {CurrencyId}", authorityId, authority.CurrencyId);
        return true;
    }

    /// <inheritdoc />
    public async Task<PriceEntryResult> SetPriceAsync(PriceEntrySaveDto request, ulong updatedById, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var featureKey = request.FeatureKey?.Trim() ?? string.Empty;
        if (!IsValidFeatureKey(featureKey))
        {
            return PriceEntryResult.Failed(CurrencyErrors.InvalidFeatureKey);
        }

        if (request.Amount <= 0)
        {
            return PriceEntryResult.Failed(CurrencyErrors.InvalidAmount);
        }

        var currency = await _currencies.GetAsync(request.CurrencyId, cancellationToken);
        if (currency == null)
        {
            return PriceEntryResult.Failed(CurrencyErrors.CurrencyNotFound);
        }

        if (!currency.IsActive)
        {
            return PriceEntryResult.Failed(CurrencyErrors.CurrencyInactive);
        }

        // A guild currency may only price features in its own guild. A global currency may price
        // anywhere, including everywhere at once.
        if (currency.Scope == CurrencyScope.Guild && request.GuildId != currency.GuildId)
        {
            return PriceEntryResult.Failed(CurrencyErrors.PriceScopeMismatch);
        }

        var entry = await _prices.GetByFeatureAsync(featureKey, request.GuildId, cancellationToken);

        if (entry == null)
        {
            entry = new PriceEntry
            {
                Id = Guid.NewGuid(),
                FeatureKey = featureKey,
                GuildId = request.GuildId,
                CurrencyId = request.CurrencyId,
                Amount = request.Amount,
                ExemptRoleIds = request.ExemptRoleIds.Distinct().ToList(),
                IsActive = request.IsActive,
                UpdatedById = updatedById,
                UpdatedAt = DateTime.UtcNow
            };

            await _prices.AddAsync(entry, cancellationToken);
        }
        else
        {
            entry.CurrencyId = request.CurrencyId;
            entry.Amount = request.Amount;
            entry.ExemptRoleIds = request.ExemptRoleIds.Distinct().ToList();
            entry.IsActive = request.IsActive;
            entry.UpdatedById = updatedById;
            entry.UpdatedAt = DateTime.UtcNow;

            await _prices.UpdateAsync(entry, cancellationToken);
        }

        _logger.LogInformation(
            "Set price {Amount} on {FeatureKey} in guild {GuildId} using currency {CurrencyId}",
            request.Amount, featureKey, request.GuildId, request.CurrencyId);

        return new PriceEntryResult { Success = true, Price = entry.ToDto(currency) };
    }

    /// <inheritdoc />
    public async Task<bool> RemovePriceAsync(string featureKey, ulong? guildId, CancellationToken cancellationToken = default)
    {
        var entry = await _prices.GetByFeatureAsync(featureKey?.Trim() ?? string.Empty, guildId, cancellationToken);
        if (entry == null || !entry.IsActive)
        {
            return false;
        }

        // Deactivate rather than delete, so the exempt roles survive a price being turned off and on.
        entry.IsActive = false;
        entry.UpdatedAt = DateTime.UtcNow;
        await _prices.UpdateAsync(entry, cancellationToken);

        _logger.LogInformation("Removed price on {FeatureKey} in guild {GuildId}", featureKey, guildId);
        return true;
    }

    /// <inheritdoc />
    public async Task<PriceEntryDto?> GetActivePriceAsync(string featureKey, ulong? guildId, CancellationToken cancellationToken = default)
    {
        var entry = await _prices.GetActiveAsync(featureKey?.Trim() ?? string.Empty, guildId, cancellationToken);
        return entry?.ToDto();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PriceEntryDto>> GetPricesForGuildAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        var entries = await _prices.GetForGuildAsync(guildId, cancellationToken);
        return entries.Select(e => e.ToDto()).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WalletReconciliationDto>> ReconcileAsync(Guid currencyId, CancellationToken cancellationToken = default)
    {
        var wallets = await _wallets.GetForCurrencyAsync(currencyId, false, cancellationToken);
        var sums = await _ledger.SumByWalletForCurrencyAsync(currencyId, cancellationToken);

        var drifted = new List<WalletReconciliationDto>();

        foreach (var wallet in wallets)
        {
            var ledgerSum = sums.TryGetValue(wallet.Id, out var sum) ? sum : 0L;
            if (ledgerSum == wallet.CachedBalance)
            {
                continue;
            }

            drifted.Add(new WalletReconciliationDto
            {
                WalletId = wallet.Id,
                UserId = wallet.UserId,
                CachedBalance = wallet.CachedBalance,
                LedgerSum = ledgerSum
            });
        }

        if (drifted.Count > 0)
        {
            _logger.LogWarning(
                "Currency {CurrencyId} has {Count} wallet(s) whose cached balance differs from the ledger",
                currencyId, drifted.Count);
        }

        return drifted;
    }

    /// <summary>
    /// Checks the rules that hold for both create and edit, returning a reason code or null.
    /// </summary>
    private static string? ValidateRules(
        CurrencyScope scope,
        ulong? guildId,
        string name,
        string symbol,
        bool allowNegative,
        long? debtFloor,
        long? incomeAmount)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            return CurrencyErrors.InvalidCurrencyRules;
        }

        if (string.IsNullOrWhiteSpace(symbol) || symbol.Length > MaxSymbolLength)
        {
            return CurrencyErrors.InvalidCurrencyRules;
        }

        // A guild currency belongs to exactly one guild; a global one belongs to none.
        if (scope == CurrencyScope.Guild != guildId.HasValue)
        {
            return CurrencyErrors.InvalidCurrencyRules;
        }

        // Debt is a floor, not an open credit line: allowing it without one is not a currency.
        if (allowNegative && (!debtFloor.HasValue || debtFloor.Value >= 0))
        {
            return CurrencyErrors.InvalidCurrencyRules;
        }

        if (incomeAmount is <= 0)
        {
            return CurrencyErrors.InvalidCurrencyRules;
        }

        return null;
    }

    /// <summary>
    /// Feature keys are <c>{area}:{identifier}</c>, e.g. <c>soundboard:{soundId}</c>.
    /// </summary>
    private static bool IsValidFeatureKey(string featureKey)
    {
        if (string.IsNullOrWhiteSpace(featureKey) || featureKey.Length > MaxFeatureKeyLength)
        {
            return false;
        }

        var separator = featureKey.IndexOf(':');
        return separator > 0 && separator < featureKey.Length - 1;
    }
}
