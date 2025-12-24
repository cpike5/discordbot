namespace DiscordBot.Bot.Tracing;

/// <summary>
/// Constants for distributed tracing span names, attributes, and baggage keys.
/// </summary>
public static class TracingConstants
{
    /// <summary>
    /// Activity source names.
    /// </summary>
    public static class Sources
    {
        public const string Bot = "DiscordBot.Bot";
        public const string Infrastructure = "DiscordBot.Infrastructure";
    }

    /// <summary>
    /// Span attribute keys following OpenTelemetry semantic conventions.
    /// </summary>
    public static class Attributes
    {
        // Discord-specific attributes
        public const string CommandName = "discord.command.name";
        public const string GuildId = "discord.guild.id";
        public const string UserId = "discord.user.id";
        public const string InteractionId = "discord.interaction.id";
        public const string ComponentType = "discord.component.type";
        public const string ComponentId = "discord.component.id";

        // Database attributes (following OTel semantic conventions)
        public const string DbSystem = "db.system";
        public const string DbOperation = "db.operation";
        public const string DbEntityType = "db.entity.type";
        public const string DbEntityId = "db.entity.id";
        public const string DbDurationMs = "db.duration.ms";

        // Application-specific
        public const string CorrelationId = "correlation.id";
        public const string ErrorMessage = "error.message";
    }

    /// <summary>
    /// Baggage keys for context propagation.
    /// </summary>
    public static class Baggage
    {
        public const string CorrelationId = "correlation-id";
    }

    /// <summary>
    /// Database operation names.
    /// </summary>
    public static class DbOperations
    {
        public const string Select = "SELECT";
        public const string Insert = "INSERT";
        public const string Update = "UPDATE";
        public const string Delete = "DELETE";
        public const string Count = "COUNT";
        public const string Exists = "EXISTS";
    }
}
