using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using System.Text.Json;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for displaying audit log entry details.
/// </summary>
public record AuditLogDetailViewModel
{
    /// <summary>
    /// Gets the unique identifier for the audit log entry.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// Gets the timestamp when the action occurred (UTC).
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the UTC timestamp in ISO 8601 format for client-side timezone conversion.
    /// Use with data-utc attribute in HTML elements.
    /// </summary>
    public string TimestampUtcIso { get; init; } = string.Empty;

    /// <summary>
    /// Gets the category name for display.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets the action name for display.
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// Gets the CSS class for the action badge.
    /// </summary>
    public string ActionBadgeClass { get; init; } = string.Empty;

    /// <summary>
    /// Gets the identifier of the actor who performed the action.
    /// </summary>
    public string ActorId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display name of the actor who performed the action.
    /// </summary>
    public string ActorName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the type name of the actor.
    /// </summary>
    public string ActorTypeName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the initials of the actor for avatar display.
    /// </summary>
    public string ActorInitials { get; init; } = string.Empty;

    /// <summary>
    /// Gets the CSS class for the actor avatar background color.
    /// </summary>
    public string ActorAvatarClass { get; init; } = string.Empty;

    /// <summary>
    /// Gets the type name of the entity that was affected.
    /// </summary>
    public string TargetType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the identifier of the entity that was affected.
    /// </summary>
    public string TargetId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Discord guild ID associated with this action.
    /// </summary>
    public ulong? GuildId { get; init; }

    /// <summary>
    /// Gets the guild name for display.
    /// </summary>
    public string? GuildName { get; init; }

    /// <summary>
    /// Gets whether the entry has guild context.
    /// </summary>
    public bool HasGuild => GuildId.HasValue;

    /// <summary>
    /// Gets the additional contextual information as a JSON string.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Gets the formatted details for display (pretty-printed JSON).
    /// </summary>
    public string? FormattedDetails { get; init; }

    /// <summary>
    /// Gets whether the entry has details.
    /// </summary>
    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);

    /// <summary>
    /// Gets the parsed details as a dictionary for field-by-field display.
    /// </summary>
    public Dictionary<string, object>? ParsedDetails { get; init; }

    /// <summary>
    /// Gets the IP address from which the action was performed.
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    /// Gets the correlation ID to group related audit log entries.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets the related audit log entries (same correlation ID).
    /// </summary>
    public IReadOnlyList<AuditLogListItem> RelatedEntries { get; init; } = Array.Empty<AuditLogListItem>();

    /// <summary>
    /// Gets whether there are related entries.
    /// </summary>
    public bool HasRelatedEntries => RelatedEntries.Any();

    /// <summary>
    /// Creates an <see cref="AuditLogDetailViewModel"/> from an <see cref="AuditLogDto"/>.
    /// </summary>
    /// <param name="dto">The audit log DTO to map from.</param>
    /// <param name="relatedEntries">Optional related audit log entries with same correlation ID.</param>
    /// <returns>A new <see cref="AuditLogDetailViewModel"/> instance.</returns>
    public static AuditLogDetailViewModel FromDto(
        AuditLogDto dto,
        IReadOnlyList<AuditLogDto>? relatedEntries = null)
    {
        var actorName = dto.ActorDisplayName ?? dto.ActorId ?? "Unknown";
        var actorId = dto.ActorId ?? "Unknown";
        var actorInitials = GetInitials(actorName);
        var actorAvatarClass = GetActorAvatarClass(dto.ActorType);
        var actionBadgeClass = GetActionBadgeClass(dto.Action);

        string? formattedDetails = null;
        Dictionary<string, object>? parsedDetails = null;

        if (!string.IsNullOrWhiteSpace(dto.Details))
        {
            try
            {
                // Pretty-print JSON
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(dto.Details);
                formattedDetails = JsonSerializer.Serialize(jsonElement, new JsonSerializerOptions { WriteIndented = true });

                // Parse as dictionary for field-by-field display
                parsedDetails = JsonSerializer.Deserialize<Dictionary<string, object>>(dto.Details);
            }
            catch
            {
                // If not valid JSON, use as-is
                formattedDetails = dto.Details;
            }
        }

        var relatedItems = relatedEntries?
            .Where(e => e.Id != dto.Id) // Exclude self
            .Select(AuditLogListItem.FromDto)
            .ToList() ?? new List<AuditLogListItem>();

        return new AuditLogDetailViewModel
        {
            Id = dto.Id,
            Timestamp = dto.Timestamp,
            TimestampUtcIso = dto.Timestamp.ToString("o"),
            Category = dto.CategoryName,
            Action = dto.ActionName,
            ActionBadgeClass = actionBadgeClass,
            ActorId = actorId,
            ActorName = actorName,
            ActorTypeName = dto.ActorTypeName,
            ActorInitials = actorInitials,
            ActorAvatarClass = actorAvatarClass,
            TargetType = dto.TargetType ?? string.Empty,
            TargetId = dto.TargetId ?? string.Empty,
            GuildId = dto.GuildId,
            GuildName = dto.GuildName,
            Details = dto.Details,
            FormattedDetails = formattedDetails,
            ParsedDetails = parsedDetails,
            IpAddress = dto.IpAddress,
            CorrelationId = dto.CorrelationId,
            RelatedEntries = relatedItems
        };
    }

    /// <summary>
    /// Generates initials from a name for avatar display.
    /// </summary>
    private static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}".ToUpper();

        return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
    }

    /// <summary>
    /// Gets the CSS class for actor avatar based on actor type.
    /// </summary>
    private static string GetActorAvatarClass(AuditLogActorType actorType)
    {
        return actorType switch
        {
            AuditLogActorType.System => "bg-bg-tertiary text-text-secondary",
            AuditLogActorType.Bot => "bg-bg-tertiary text-text-secondary",
            AuditLogActorType.User => "bg-accent-blue text-white",
            _ => "bg-bg-tertiary text-text-secondary"
        };
    }

    /// <summary>
    /// Gets the CSS class for action badge based on action.
    /// </summary>
    private static string GetActionBadgeClass(AuditLogAction action)
    {
        return action switch
        {
            AuditLogAction.Created => "bg-success text-white",
            AuditLogAction.Updated => "bg-accent-blue text-white",
            AuditLogAction.Deleted => "bg-error text-white",
            AuditLogAction.Login => "bg-success text-white",
            AuditLogAction.Logout => "bg-bg-tertiary text-text-secondary",
            AuditLogAction.PermissionChanged => "bg-accent-orange text-white",
            AuditLogAction.SettingChanged => "bg-accent-orange text-white",
            AuditLogAction.CommandExecuted => "bg-accent-blue text-white",
            AuditLogAction.MessageDeleted => "bg-error text-white",
            AuditLogAction.MessageEdited => "bg-accent-blue text-white",
            AuditLogAction.UserBanned => "bg-error text-white",
            AuditLogAction.UserUnbanned => "bg-success text-white",
            AuditLogAction.UserKicked => "bg-accent-orange text-white",
            AuditLogAction.RoleAssigned => "bg-success text-white",
            AuditLogAction.RoleRemoved => "bg-accent-orange text-white",
            _ => "bg-bg-tertiary text-text-secondary"
        };
    }
}
