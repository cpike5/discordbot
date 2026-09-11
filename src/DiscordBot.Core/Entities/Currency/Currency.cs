using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// A named unit of account, scoped either to one guild or to the whole bot. Currencies are never
/// deleted; <see cref="IsActive"/> freezes one instead, so history stays readable.
/// </summary>
/// <remarks>
/// The virtual-currency types live under <c>Entities/Currency/</c> but keep the flat
/// <c>DiscordBot.Core.Entities</c> namespace on purpose: a nested <c>...Entities.Currency</c>
/// namespace would collide with this type's own name and make every unqualified <c>Currency</c>
/// ambiguous (CS0104) in files that import both. The same applies to the DTO, interface,
/// repository, and service folders added for this feature.
/// </remarks>
public class Currency
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Whether this currency belongs to one guild or to every guild.</summary>
    public CurrencyScope Scope { get; set; }

    /// <summary>Owning guild. Required when <see cref="Scope"/> is Guild, null otherwise.</summary>
    public ulong? GuildId { get; set; }

    /// <summary>Display name. Unique within its scope (per guild, or among globals).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Emoji or short text shown next to amounts.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Whether users may send this currency to each other.</summary>
    public bool IsTransferable { get; set; } = true;

    /// <summary>Whether a fine may push a balance below zero.</summary>
    public bool AllowNegative { get; set; }

    /// <summary>
    /// Most negative balance a fine may reach, stored negative (e.g. -100). Required when
    /// <see cref="AllowNegative"/> is set.
    /// </summary>
    public long? DebtFloor { get; set; }

    /// <summary>Automatic income per interval. Groundwork only; nothing reads it yet.</summary>
    public long? IncomeAmount { get; set; }

    /// <summary>How often income pays out. Groundwork only; nothing reads it yet.</summary>
    public IncomeInterval? IncomeInterval { get; set; }

    /// <summary>
    /// Deactivated currencies freeze: no mint, spend, transfer, or fine. History stays readable.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Discord user snowflake ID of the creator.</summary>
    public ulong CreatedById { get; set; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Wallets held in this currency.</summary>
    public ICollection<Wallet> Wallets { get; set; } = new List<Wallet>();

    /// <summary>Principals allowed to mint this currency.</summary>
    public ICollection<MintAuthority> MintAuthorities { get; set; } = new List<MintAuthority>();
}
