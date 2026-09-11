namespace DiscordBot.Core.Enums;

/// <summary>
/// Represents the specific action performed in an audit log entry.
/// </summary>
public enum AuditLogAction
{
    /// <summary>
    /// A new entity was created.
    /// </summary>
    Created = 1,

    /// <summary>
    /// An existing entity was updated.
    /// </summary>
    Updated = 2,

    /// <summary>
    /// An entity was deleted.
    /// </summary>
    Deleted = 3,

    /// <summary>
    /// A user logged in to the system.
    /// </summary>
    Login = 4,

    /// <summary>
    /// A user logged out of the system.
    /// </summary>
    Logout = 5,

    /// <summary>
    /// Permissions were changed for a user or role.
    /// </summary>
    PermissionChanged = 6,

    /// <summary>
    /// A configuration setting was changed.
    /// </summary>
    SettingChanged = 7,

    /// <summary>
    /// A command was executed.
    /// </summary>
    CommandExecuted = 8,

    /// <summary>
    /// A message was deleted.
    /// </summary>
    MessageDeleted = 9,

    /// <summary>
    /// A message was edited.
    /// </summary>
    MessageEdited = 10,

    /// <summary>
    /// A user was banned from a guild.
    /// </summary>
    UserBanned = 11,

    /// <summary>
    /// A user was unbanned from a guild.
    /// </summary>
    UserUnbanned = 12,

    /// <summary>
    /// A user was kicked from a guild.
    /// </summary>
    UserKicked = 13,

    /// <summary>
    /// A role was assigned to a user.
    /// </summary>
    RoleAssigned = 14,

    /// <summary>
    /// A role was removed from a user.
    /// </summary>
    RoleRemoved = 15,

    /// <summary>
    /// The Discord bot has started.
    /// </summary>
    BotStarted = 16,

    /// <summary>
    /// The Discord bot has stopped.
    /// </summary>
    BotStopped = 17,

    /// <summary>
    /// The Discord bot connected to the Discord gateway.
    /// </summary>
    BotConnected = 18,

    /// <summary>
    /// The Discord bot disconnected from the Discord gateway.
    /// </summary>
    BotDisconnected = 19,

    /// <summary>
    /// User data was purged from the system (GDPR right to be forgotten).
    /// </summary>
    UserDataPurged = 20,

    /// <summary>
    /// Bulk data purge operation was executed.
    /// </summary>
    BulkDataPurged = 21,

    /// <summary>
    /// User data was exported (GDPR right of access).
    /// </summary>
    UserDataExported = 22,

    /// <summary>
    /// The LLM model catalog was refreshed from OpenRouter.
    /// </summary>
    LlmCatalogRefreshed = 23,

    /// <summary>
    /// An LLM model was added to the admin allowlist.
    /// </summary>
    LlmModelEnabled = 24,

    /// <summary>
    /// An LLM model was removed from the admin allowlist.
    /// </summary>
    LlmModelDisabled = 25,

    /// <summary>
    /// A virtual currency was created.
    /// </summary>
    CurrencyCreated = 26,

    /// <summary>
    /// A virtual currency's rules were changed.
    /// </summary>
    CurrencyUpdated = 27,

    /// <summary>
    /// A virtual currency was deactivated, freezing every balance operation on it.
    /// </summary>
    CurrencyDeactivated = 28,

    /// <summary>
    /// A principal was granted authority to mint a currency.
    /// </summary>
    MintAuthorityGranted = 29,

    /// <summary>
    /// A principal's authority to mint a currency was revoked.
    /// </summary>
    MintAuthorityRevoked = 30,

    /// <summary>
    /// Units of a currency were created and credited to a wallet.
    /// </summary>
    CurrencyMinted = 31,

    /// <summary>
    /// A moderator fined a user in a guild currency.
    /// </summary>
    CurrencyFined = 32,

    /// <summary>
    /// A ledger row was corrected by an adjustment.
    /// </summary>
    CurrencyAdjusted = 33,

    /// <summary>
    /// A price was set on a feature.
    /// </summary>
    PriceSet = 34,

    /// <summary>
    /// A feature's price was removed, making it free again.
    /// </summary>
    PriceRemoved = 35
}
