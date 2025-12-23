using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for displaying command log details.
/// </summary>
public record CommandLogDetailViewModel
{
    /// <summary>
    /// Gets the unique identifier for the command log entry.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the guild ID where the command was executed.
    /// </summary>
    public ulong? GuildId { get; init; }

    /// <summary>
    /// Gets the guild name where the command was executed.
    /// </summary>
    public string GuildName { get; init; } = "Direct Message";

    /// <summary>
    /// Gets the user ID who executed the command.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    /// Gets the username of the user who executed the command.
    /// </summary>
    public string Username { get; init; } = "Unknown";

    /// <summary>
    /// Gets the name of the command that was executed.
    /// </summary>
    public string CommandName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the command parameters as JSON string.
    /// </summary>
    public string? Parameters { get; init; }

    /// <summary>
    /// Gets the formatted parameters for display (pretty-printed JSON).
    /// </summary>
    public string FormattedParameters { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether the command has parameters.
    /// </summary>
    public bool HasParameters => !string.IsNullOrWhiteSpace(Parameters);

    /// <summary>
    /// Gets the timestamp when the command was executed.
    /// </summary>
    public DateTime ExecutedAt { get; init; }

    /// <summary>
    /// Gets the response time in milliseconds.
    /// </summary>
    public int ResponseTimeMs { get; init; }

    /// <summary>
    /// Gets whether the command executed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the error message if the command failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets whether the command has an error message.
    /// </summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>
    /// Gets the status text for display.
    /// </summary>
    public string StatusText => Success ? "Success" : "Failed";

    /// <summary>
    /// Gets whether guild details link should be shown.
    /// </summary>
    public bool HasGuildLink => GuildId.HasValue;

    /// <summary>
    /// Creates a <see cref="CommandLogDetailViewModel"/> from a <see cref="CommandLogDto"/>.
    /// </summary>
    public static CommandLogDetailViewModel FromDto(CommandLogDto dto)
    {
        string formattedParams = string.Empty;
        if (!string.IsNullOrWhiteSpace(dto.Parameters))
        {
            try
            {
                // Pretty-print JSON
                var jsonElement = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(dto.Parameters);
                formattedParams = System.Text.Json.JsonSerializer.Serialize(jsonElement, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                // If not valid JSON, use as-is
                formattedParams = dto.Parameters;
            }
        }

        return new CommandLogDetailViewModel
        {
            Id = dto.Id,
            GuildId = dto.GuildId,
            GuildName = dto.GuildName ?? "Direct Message",
            UserId = dto.UserId,
            Username = dto.Username ?? "Unknown",
            CommandName = dto.CommandName,
            Parameters = dto.Parameters,
            FormattedParameters = formattedParams,
            ExecutedAt = dto.ExecutedAt,
            ResponseTimeMs = dto.ResponseTimeMs,
            Success = dto.Success,
            ErrorMessage = dto.ErrorMessage
        };
    }
}
