using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// A grant letting one principal create units of a currency. The creator is added as a
/// <see cref="MintPrincipalType.User"/> authority when the currency is created.
/// </summary>
public class MintAuthority
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The currency this grant applies to.</summary>
    public Guid CurrencyId { get; set; }

    /// <summary>The kind of principal named by <see cref="PrincipalId"/>.</summary>
    public MintPrincipalType PrincipalType { get; set; }

    /// <summary>
    /// Discord user or role snowflake ID. Null for <see cref="MintPrincipalType.System"/>.
    /// </summary>
    public ulong? PrincipalId { get; set; }

    /// <summary>Discord user snowflake ID of whoever granted this.</summary>
    public ulong GrantedById { get; set; }

    /// <summary>UTC grant timestamp.</summary>
    public DateTime GrantedAt { get; set; }

    /// <summary>Navigation to the owning currency.</summary>
    public Currency? Currency { get; set; }
}
