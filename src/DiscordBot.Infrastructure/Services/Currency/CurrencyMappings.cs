using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;

namespace DiscordBot.Infrastructure.Services;

/// <summary>
/// Entity to DTO projections for the virtual currency system. Kept in one place so a ledger row
/// renders the same whichever service returned it, including from the charge and mint services in
/// the Bot project.
/// </summary>
public static class CurrencyMappings
{
    public static CurrencyDto ToDto(this Currency currency) => new()
    {
        Id = currency.Id,
        Scope = currency.Scope,
        GuildId = currency.GuildId,
        Name = currency.Name,
        Symbol = currency.Symbol,
        IsTransferable = currency.IsTransferable,
        AllowNegative = currency.AllowNegative,
        DebtFloor = currency.DebtFloor,
        IncomeAmount = currency.IncomeAmount,
        IncomeInterval = currency.IncomeInterval,
        IsActive = currency.IsActive,
        CreatedById = currency.CreatedById,
        CreatedAt = currency.CreatedAt
    };

    public static WalletDto ToDto(this Wallet wallet, Currency? currency = null)
    {
        var source = currency ?? wallet.Currency;

        return new WalletDto
        {
            Id = wallet.Id,
            CurrencyId = wallet.CurrencyId,
            CurrencyName = source?.Name ?? string.Empty,
            CurrencySymbol = source?.Symbol ?? string.Empty,
            GuildId = source?.GuildId,
            UserId = wallet.UserId,
            Balance = wallet.CachedBalance,
            CreatedAt = wallet.CreatedAt
        };
    }

    public static LedgerTransactionDto ToDto(this LedgerTransaction transaction) => new()
    {
        Id = transaction.Id,
        WalletId = transaction.WalletId,
        Type = transaction.Type,
        Source = transaction.Source,
        Amount = transaction.Amount,
        BalanceAfter = transaction.BalanceAfter,
        Reason = transaction.Reason,
        FeatureKey = transaction.FeatureKey,
        IdempotencyKey = transaction.IdempotencyKey,
        ReferenceTransactionId = transaction.ReferenceTransactionId,
        ModerationCaseId = transaction.ModerationCaseId,
        ActorId = transaction.ActorId,
        CorrelationId = transaction.CorrelationId,
        CreatedAt = transaction.CreatedAt
    };

    public static MintAuthorityDto ToDto(this MintAuthority authority) => new()
    {
        Id = authority.Id,
        CurrencyId = authority.CurrencyId,
        PrincipalType = authority.PrincipalType,
        PrincipalId = authority.PrincipalId,
        GrantedById = authority.GrantedById,
        GrantedAt = authority.GrantedAt
    };

    public static PriceEntryDto ToDto(this PriceEntry price, Currency? currency = null)
    {
        var source = currency ?? price.Currency;

        return new PriceEntryDto
        {
            Id = price.Id,
            FeatureKey = price.FeatureKey,
            GuildId = price.GuildId,
            CurrencyId = price.CurrencyId,
            CurrencyName = source?.Name ?? string.Empty,
            CurrencySymbol = source?.Symbol ?? string.Empty,
            Amount = price.Amount,
            ExemptRoleIds = price.ExemptRoleIds.ToList(),
            IsActive = price.IsActive,
            UpdatedById = price.UpdatedById,
            UpdatedAt = price.UpdatedAt
        };
    }
}
