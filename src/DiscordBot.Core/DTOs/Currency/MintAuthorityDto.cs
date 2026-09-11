using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// One grant letting a principal mint a currency.
/// </summary>
public record MintAuthorityDto
{
    /// <summary>Grant id.</summary>
    public Guid Id { get; init; }

    /// <summary>The currency the grant applies to.</summary>
    public Guid CurrencyId { get; init; }

    /// <summary>The kind of principal named by <see cref="PrincipalId"/>.</summary>
    public MintPrincipalType PrincipalType { get; init; }

    /// <summary>Discord user or role snowflake ID. Null for the system principal.</summary>
    public ulong? PrincipalId { get; init; }

    /// <summary>Discord user snowflake ID of whoever granted this.</summary>
    public ulong GrantedById { get; init; }

    /// <summary>UTC grant timestamp.</summary>
    public DateTime GrantedAt { get; init; }
}
