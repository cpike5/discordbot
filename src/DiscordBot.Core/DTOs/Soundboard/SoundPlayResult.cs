using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs.Soundboard;

/// <summary>
/// Result of a sound playback operation.
/// </summary>
public class SoundPlayResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the playback was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if playback failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the sound entity that was played.
    /// Null if playback failed before fetching metadata.
    /// </summary>
    public Sound? Sound { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the sound was queued (vs. playing immediately).
    /// </summary>
    public bool WasQueued { get; set; }

    /// <summary>
    /// Gets or sets the queue position if the sound was queued.
    /// Null if sound is playing immediately.
    /// </summary>
    public int? QueuePosition { get; set; }

    /// <summary>
    /// Gets or sets what the charge seam decided when the sound was priced.
    /// <para>
    /// Null when the currency feature is switched off entirely.
    /// <see cref="ChargeHoldStatus.Free"/> when no price applies or the user is exempt,
    /// <see cref="ChargeHoldStatus.Held"/> when the price was reserved and then charged, and any
    /// other value on a refusal, which the caller renders from <see cref="ErrorMessage"/>.
    /// </para>
    /// </summary>
    public ChargeHoldStatus? ChargeStatus { get; set; }

    /// <summary>
    /// Gets or sets the price of this sound in the guild, in whole units. Null when nothing was
    /// priced; zero when the user pays nothing.
    /// </summary>
    public long? Price { get; set; }

    /// <summary>
    /// Gets or sets the user's balance in the priced currency: what they had when a refusal was
    /// decided, or what they have left after a charge.
    /// </summary>
    public long? Balance { get; set; }

    /// <summary>
    /// Gets or sets the symbol of the priced currency, for rendering amounts next to it.
    /// </summary>
    public string? CurrencySymbol { get; set; }
}
