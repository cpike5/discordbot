using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using System.Reflection;

namespace DiscordBot.Bot.Services.Commands;

/// <summary>
/// Service for extracting command metadata from Discord.NET's InteractionService.
/// </summary>
public class CommandMetadataService : ICommandMetadataService
{
    private readonly InteractionService _interactionService;
    private readonly ILogger<CommandMetadataService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandMetadataService"/> class.
    /// </summary>
    /// <param name="interactionService">The Discord.NET InteractionService.</param>
    /// <param name="logger">The logger.</param>
    public CommandMetadataService(
        InteractionService interactionService,
        ILogger<CommandMetadataService> logger)
    {
        _interactionService = interactionService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<CommandModuleDto>> GetAllModulesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Extracting command metadata from InteractionService");

        var modules = _interactionService.Modules;

        if (modules == null || modules.Count == 0)
        {
            _logger.LogWarning("InteractionService has no registered modules");
            return Task.FromResult<IReadOnlyList<CommandModuleDto>>(Array.Empty<CommandModuleDto>());
        }

        var moduleDtos = new List<CommandModuleDto>();

        foreach (var module in modules)
        {
            var moduleDto = MapModuleToDto(module);

            _logger.LogDebug(
                "Mapped module {ModuleName} with {CommandCount} commands (IsSlashGroup: {IsSlashGroup})",
                moduleDto.Name,
                moduleDto.CommandCount,
                moduleDto.IsSlashGroup);

            // Filter out modules with no slash commands (e.g., component handler modules)
            if (moduleDto.CommandCount == 0)
            {
                _logger.LogDebug(
                    "Excluding module {ModuleName} from results (no slash commands)",
                    moduleDto.Name);
                continue;
            }

            moduleDtos.Add(moduleDto);
        }

        _logger.LogInformation("Extracted metadata for {ModuleCount} modules with {CommandCount} total commands",
            moduleDtos.Count,
            moduleDtos.Sum(m => m.CommandCount));

        return Task.FromResult<IReadOnlyList<CommandModuleDto>>(moduleDtos.AsReadOnly());
    }

    /// <summary>
    /// Maps a Discord.NET ModuleInfo to a CommandModuleDto.
    /// </summary>
    /// <param name="module">The module to map.</param>
    /// <returns>The mapped CommandModuleDto.</returns>
    private CommandModuleDto MapModuleToDto(ModuleInfo module)
    {
        var moduleName = module.Name;
        var displayName = GetDisplayName(moduleName);

        // Get module-level preconditions (will apply to all commands)
        var modulePreconditions = ExtractPreconditions(module.Preconditions);

        var moduleDto = new CommandModuleDto
        {
            Name = moduleName,
            DisplayName = displayName,
            Description = module.Description,
            IsSlashGroup = module.IsSlashGroup,
            GroupName = module.SlashGroupName,
            Commands = new List<CommandInfoDto>()
        };

        // Map all commands in the module
        foreach (var command in module.SlashCommands)
        {
            var commandDto = MapCommandToDto(command, moduleName, modulePreconditions);
            moduleDto.Commands.Add(commandDto);
        }

        return moduleDto;
    }

    /// <summary>
    /// Maps a Discord.NET SlashCommandInfo to a CommandInfoDto.
    /// </summary>
    /// <param name="command">The command to map.</param>
    /// <param name="moduleName">The name of the module containing this command.</param>
    /// <param name="modulePreconditions">Preconditions inherited from the module.</param>
    /// <returns>The mapped CommandInfoDto.</returns>
    private CommandInfoDto MapCommandToDto(
        SlashCommandInfo command,
        string moduleName,
        List<PreconditionDto> modulePreconditions)
    {
        var commandName = command.Name;
        var fullName = string.IsNullOrEmpty(command.Module.SlashGroupName)
            ? commandName
            : $"{command.Module.SlashGroupName} {commandName}";

        // Extract command-level preconditions
        var commandPreconditions = ExtractPreconditions(command.Preconditions);

        // Combine module and command preconditions
        var allPreconditions = new List<PreconditionDto>();
        allPreconditions.AddRange(modulePreconditions);
        allPreconditions.AddRange(commandPreconditions);

        var commandDto = new CommandInfoDto
        {
            Name = commandName,
            FullName = fullName,
            Description = command.Description,
            ModuleName = moduleName,
            Parameters = new List<CommandParameterDto>(),
            Preconditions = allPreconditions
        };

        // Map command parameters
        foreach (var parameter in command.Parameters)
        {
            var parameterDto = MapParameterToDto(parameter);
            commandDto.Parameters.Add(parameterDto);
        }

        return commandDto;
    }

    /// <summary>
    /// Maps a Discord.NET SlashCommandParameterInfo to a CommandParameterDto.
    /// </summary>
    /// <param name="parameter">The parameter to map.</param>
    /// <returns>The mapped CommandParameterDto.</returns>
    private static CommandParameterDto MapParameterToDto(SlashCommandParameterInfo parameter)
    {
        var friendlyTypeName = GetFriendlyTypeName(parameter.ParameterType);
        var choices = ExtractChoices(parameter);

        return new CommandParameterDto
        {
            Name = parameter.Name,
            Description = parameter.Description,
            Type = friendlyTypeName,
            IsRequired = parameter.IsRequired,
            DefaultValue = parameter.DefaultValue?.ToString(),
            Choices = choices
        };
    }

    /// <summary>
    /// Extracts preconditions from a collection of PreconditionAttribute instances.
    /// </summary>
    /// <param name="preconditions">The preconditions to extract.</param>
    /// <returns>A list of PreconditionDto objects.</returns>
    private List<PreconditionDto> ExtractPreconditions(IEnumerable<PreconditionAttribute> preconditions)
    {
        var preconditionDtos = new List<PreconditionDto>();

        foreach (var precondition in preconditions)
        {
            var preconditionDto = MapPreconditionToDto(precondition);
            if (preconditionDto != null)
            {
                preconditionDtos.Add(preconditionDto);
            }
        }

        return preconditionDtos;
    }

    /// <summary>
    /// Maps a PreconditionAttribute to a PreconditionDto.
    /// </summary>
    /// <param name="precondition">The precondition attribute.</param>
    /// <returns>The mapped PreconditionDto, or null if the precondition type is unknown.</returns>
    private PreconditionDto? MapPreconditionToDto(PreconditionAttribute precondition)
    {
        var preconditionType = precondition.GetType();
        var preconditionName = preconditionType.Name.Replace("Attribute", "");

        return precondition switch
        {
            RequireAdminAttribute => new PreconditionDto
            {
                Name = preconditionName,
                Type = PreconditionType.Admin,
                Configuration = null
            },
            Preconditions.RequireOwnerAttribute => new PreconditionDto
            {
                Name = preconditionName,
                Type = PreconditionType.Owner,
                Configuration = null
            },
            RateLimitAttribute rateLimit => new PreconditionDto
            {
                Name = preconditionName,
                Type = PreconditionType.RateLimit,
                Configuration = ExtractRateLimitConfiguration(rateLimit)
            },
            RequireBotPermissionAttribute botPerm => new PreconditionDto
            {
                Name = preconditionName,
                Type = PreconditionType.BotPermission,
                Configuration = botPerm.GuildPermission?.ToString()
            },
            RequireUserPermissionAttribute userPerm => new PreconditionDto
            {
                Name = preconditionName,
                Type = PreconditionType.UserPermission,
                Configuration = userPerm.GuildPermission?.ToString()
            },
            RequireContextAttribute context => new PreconditionDto
            {
                Name = preconditionName,
                Type = PreconditionType.Context,
                Configuration = context.Contexts.ToString()
            },
            _ => new PreconditionDto
            {
                Name = preconditionName,
                Type = PreconditionType.Custom,
                Configuration = preconditionType.FullName
            }
        };
    }

    /// <summary>
    /// Extracts configuration details from a RateLimitAttribute using reflection.
    /// </summary>
    /// <param name="rateLimit">The rate limit attribute.</param>
    /// <returns>A human-readable configuration string.</returns>
    private string ExtractRateLimitConfiguration(RateLimitAttribute rateLimit)
    {
        try
        {
            var type = rateLimit.GetType();

            // Extract private fields using reflection
            var timesField = type.GetField("_times", BindingFlags.NonPublic | BindingFlags.Instance);
            var periodField = type.GetField("_periodSeconds", BindingFlags.NonPublic | BindingFlags.Instance);
            var targetField = type.GetField("_target", BindingFlags.NonPublic | BindingFlags.Instance);

            if (timesField == null || periodField == null || targetField == null)
            {
                _logger.LogWarning("Unable to extract rate limit configuration via reflection");
                return "Rate limit configured";
            }

            var times = timesField.GetValue(rateLimit);
            var periodSeconds = periodField.GetValue(rateLimit);
            var target = targetField.GetValue(rateLimit);

            return $"{times} per {periodSeconds}s ({target})";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract rate limit configuration via reflection");
            return "Rate limit configured";
        }
    }

    /// <summary>
    /// Gets a friendly display name from a module class name.
    /// </summary>
    /// <param name="moduleName">The module class name (e.g., "GeneralModule").</param>
    /// <returns>The formatted display name (e.g., "General").</returns>
    private static string GetDisplayName(string moduleName)
    {
        // Remove "Module" suffix if present
        if (moduleName.EndsWith("Module", StringComparison.OrdinalIgnoreCase))
        {
            return moduleName[..^6];
        }

        return moduleName;
    }

    /// <summary>
    /// Gets a friendly type name for a parameter type.
    /// </summary>
    /// <param name="type">The parameter type.</param>
    /// <returns>A friendly type name (e.g., "String", "Integer", "User").</returns>
    private static string GetFriendlyTypeName(Type type)
    {
        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        // Map Discord types
        if (underlyingType == typeof(IUser) || underlyingType.IsAssignableTo(typeof(IUser)))
            return "User";
        if (underlyingType == typeof(IChannel) || underlyingType.IsAssignableTo(typeof(IChannel)))
            return "Channel";
        if (underlyingType == typeof(IRole) || underlyingType.IsAssignableTo(typeof(IRole)))
            return "Role";
        if (underlyingType == typeof(IMentionable) || underlyingType.IsAssignableTo(typeof(IMentionable)))
            return "Mentionable";
        if (underlyingType == typeof(IAttachment) || underlyingType.IsAssignableTo(typeof(IAttachment)))
            return "Attachment";

        // Map primitive types
        if (underlyingType == typeof(string))
            return "String";
        if (underlyingType == typeof(int) || underlyingType == typeof(long))
            return "Integer";
        if (underlyingType == typeof(double) || underlyingType == typeof(float) || underlyingType == typeof(decimal))
            return "Number";
        if (underlyingType == typeof(bool))
            return "Boolean";

        // For enums, return the enum name
        if (underlyingType.IsEnum)
            return underlyingType.Name;

        // Default to the type name
        return underlyingType.Name;
    }

    /// <summary>
    /// Extracts choices from a parameter (for enum types or explicit choice attributes).
    /// </summary>
    /// <param name="parameter">The parameter to extract choices from.</param>
    /// <returns>A list of choice names, or null if no choices are available.</returns>
    private static List<string>? ExtractChoices(SlashCommandParameterInfo parameter)
    {
        var underlyingType = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;

        // Check if parameter type is an enum
        if (underlyingType.IsEnum)
        {
            var enumNames = Enum.GetNames(underlyingType);
            return enumNames.ToList();
        }

        // Check for explicit choice attributes
        var choices = parameter.Choices;
        if (choices != null && choices.Any())
        {
            return choices.Select(c => c.Name).ToList();
        }

        return null;
    }
}
