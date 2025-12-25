using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for an individual command, including parameters and preconditions.
/// </summary>
public record CommandItemViewModel
{
    /// <summary>
    /// Gets the command name formatted for display (e.g., "/ping").
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the full command name including group prefix (e.g., "/consent grant").
    /// </summary>
    public string FullDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the command description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the formatted parameters string for display.
    /// </summary>
    public string ParametersDisplay { get; init; } = string.Empty;

    /// <summary>
    /// Gets the collection of parameter view models.
    /// </summary>
    public IReadOnlyList<CommandParameterViewModel> Parameters { get; init; } = Array.Empty<CommandParameterViewModel>();

    /// <summary>
    /// Gets whether the command has any parameters.
    /// </summary>
    public bool HasParameters => Parameters.Count > 0;

    /// <summary>
    /// Gets the collection of precondition badges to display.
    /// </summary>
    public IReadOnlyList<BadgeViewModel> PreconditionBadges { get; init; } = Array.Empty<BadgeViewModel>();

    /// <summary>
    /// Gets whether the command has any preconditions.
    /// </summary>
    public bool HasPreconditions => PreconditionBadges.Count > 0;

    /// <summary>
    /// Creates a <see cref="CommandItemViewModel"/> from a <see cref="CommandInfoDto"/>.
    /// </summary>
    /// <param name="dto">The command DTO to map from.</param>
    /// <returns>A new <see cref="CommandItemViewModel"/> instance.</returns>
    public static CommandItemViewModel FromDto(CommandInfoDto dto)
    {
        var parameters = dto.Parameters.Select(CommandParameterViewModel.FromDto).ToList();
        var parametersDisplay = FormatParametersDisplay(parameters);
        var preconditionBadges = dto.Preconditions.Select(MapPreconditionToBadge).ToList();

        return new CommandItemViewModel
        {
            DisplayName = $"/{dto.Name}",
            FullDisplayName = $"/{dto.FullName}",
            Description = dto.Description,
            Parameters = parameters,
            ParametersDisplay = parametersDisplay,
            PreconditionBadges = preconditionBadges
        };
    }

    /// <summary>
    /// Formats the parameters for inline display.
    /// </summary>
    /// <param name="parameters">The parameters to format.</param>
    /// <returns>A formatted string showing parameter names and types.</returns>
    private static string FormatParametersDisplay(IReadOnlyList<CommandParameterViewModel> parameters)
    {
        if (parameters.Count == 0)
        {
            return string.Empty;
        }

        var parts = parameters.Select(p =>
        {
            var name = p.Name;
            if (p.IsRequired)
            {
                return $"<{name}>";
            }
            return $"[{name}]";
        });

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Maps a precondition DTO to a badge view model with appropriate styling.
    /// </summary>
    /// <param name="precondition">The precondition to map.</param>
    /// <returns>A <see cref="BadgeViewModel"/> with appropriate styling.</returns>
    private static BadgeViewModel MapPreconditionToBadge(PreconditionDto precondition)
    {
        var (variant, displayName) = precondition.Type switch
        {
            PreconditionType.Admin => (BadgeVariant.Orange, "Admin"),
            PreconditionType.Owner => (BadgeVariant.Error, "Owner"),
            PreconditionType.RateLimit => (BadgeVariant.Warning, precondition.Configuration ?? "Rate Limited"),
            PreconditionType.BotPermission => (BadgeVariant.Blue, $"Bot: {precondition.Configuration ?? "Permission"}"),
            PreconditionType.UserPermission => (BadgeVariant.Blue, $"User: {precondition.Configuration ?? "Permission"}"),
            PreconditionType.Context => (BadgeVariant.Default, precondition.Configuration ?? "Context"),
            _ => (BadgeVariant.Default, precondition.Name)
        };

        return new BadgeViewModel
        {
            Text = displayName,
            Variant = variant,
            Size = BadgeSize.Small
        };
    }
}

/// <summary>
/// View model for a command parameter.
/// </summary>
public record CommandParameterViewModel
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the parameter description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the friendly type name.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether the parameter is required.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Gets the default value for the parameter, if any.
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// Gets the available choices for the parameter.
    /// </summary>
    public IReadOnlyList<string>? Choices { get; init; }

    /// <summary>
    /// Gets whether the parameter has choices.
    /// </summary>
    public bool HasChoices => Choices != null && Choices.Count > 0;

    /// <summary>
    /// Creates a <see cref="CommandParameterViewModel"/> from a <see cref="CommandParameterDto"/>.
    /// </summary>
    /// <param name="dto">The parameter DTO to map from.</param>
    /// <returns>A new <see cref="CommandParameterViewModel"/> instance.</returns>
    public static CommandParameterViewModel FromDto(CommandParameterDto dto)
    {
        return new CommandParameterViewModel
        {
            Name = dto.Name,
            Description = dto.Description,
            Type = dto.Type,
            IsRequired = dto.IsRequired,
            DefaultValue = dto.DefaultValue,
            Choices = dto.Choices?.ToList()
        };
    }
}
