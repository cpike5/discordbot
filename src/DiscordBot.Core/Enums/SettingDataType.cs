namespace DiscordBot.Core.Enums;

/// <summary>
/// Defines the data type of a setting value for validation and UI control selection.
/// </summary>
public enum SettingDataType
{
    /// <summary>
    /// String/text value (rendered as text input).
    /// </summary>
    String,

    /// <summary>
    /// Integer number value (rendered as number input).
    /// </summary>
    Integer,

    /// <summary>
    /// Boolean true/false value (rendered as toggle switch).
    /// </summary>
    Boolean,

    /// <summary>
    /// Decimal number value (rendered as number input with decimals).
    /// </summary>
    Decimal,

    /// <summary>
    /// Complex JSON value (rendered as textarea).
    /// </summary>
    Json
}
