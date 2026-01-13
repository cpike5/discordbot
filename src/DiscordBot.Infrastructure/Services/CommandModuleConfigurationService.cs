using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services;

/// <summary>
/// Service implementation for managing command module configuration with business logic.
/// Registered as singleton to maintain the restart pending flag across requests.
/// Uses IServiceScopeFactory to resolve scoped repository instances.
/// </summary>
public class CommandModuleConfigurationService : ICommandModuleConfigurationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CommandModuleConfigurationService> _logger;
    private bool _restartPending;

    /// <inheritdoc />
    public event EventHandler<CommandModuleConfigurationChangedEventArgs>? ConfigurationChanged;

    /// <summary>
    /// Default module configurations to seed on first run.
    /// </summary>
    private static readonly List<DefaultModuleDefinition> DefaultModules = new()
    {
        // Core modules (always enabled, no restart required)
        new("GeneralModule", "General", "Basic bot commands like /ping", "Core", false),
        new("VerifyAccountModule", "Verify Account", "Account verification commands", "Core", false),
        new("ConsentModule", "Consent", "Privacy consent management commands", "Core", false),

        // Administrative modules
        new("AdminModule", "Admin", "Administrative commands for server management", "Admin", true),
        new("WelcomeModule", "Welcome", "Welcome message configuration commands", "Admin", true),
        new("ScheduleModule", "Scheduled Messages", "Commands for scheduling messages", "Admin", true),

        // Moderation modules
        new("ModerationActionModule", "Moderation Actions", "Commands for warn, kick, ban, mute, purge", "Moderation", true),
        new("ModerationHistoryModule", "Moderation History", "View moderation case history", "Moderation", true),
        new("ModStatsModule", "Moderator Stats", "View moderator statistics", "Moderation", true),
        new("ModNoteModule", "Mod Notes", "Add and manage moderator notes on users", "Moderation", true),
        new("ModTagModule", "Mod Tags", "Tag and categorize users for moderation", "Moderation", true),
        new("WatchlistModule", "Watchlist", "Manage user watchlists", "Moderation", true),
        new("InvestigateModule", "Investigate", "Investigate user activity and history", "Moderation", true),

        // Feature modules
        new("RatWatchModule", "Rat Watch", "Rat Watch accountability feature", "Features", true),
        new("ReminderModule", "Reminders", "Personal reminder commands", "Features", true),

        // Audio modules
        new("TtsModule", "Text-to-Speech", "Text-to-speech commands", "Audio", true),
        new("SoundboardModule", "Soundboard", "Soundboard playback commands", "Audio", true),
        new("VoiceModule", "Voice", "Voice channel management commands", "Audio", true),

        // Utility modules
        new("UtilityModule", "Utility", "Utility commands like /userinfo, /serverinfo", "Utility", true)
    };

    public CommandModuleConfigurationService(
        IServiceScopeFactory scopeFactory,
        ILogger<CommandModuleConfigurationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _restartPending = false;
    }

    /// <summary>
    /// Creates a new scope and returns the repository.
    /// </summary>
    private ICommandModuleConfigurationRepository GetRepository(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ICommandModuleConfigurationRepository>();

    public bool IsRestartPending => _restartPending;

    public void ClearRestartPending()
    {
        _logger.LogInformation("Module configuration restart pending flag cleared");
        _restartPending = false;
    }

    public async Task<IReadOnlyList<CommandModuleConfigurationDto>> GetAllModulesAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving all module configurations");

        using var scope = _scopeFactory.CreateScope();
        var repository = GetRepository(scope);

        var configurations = await repository.GetAllAsync(cancellationToken);

        var result = configurations.Select(MapToDto).ToList();
        _logger.LogDebug("Retrieved {Count} module configurations", result.Count);

        return result;
    }

    public async Task<IReadOnlyList<CommandModuleConfigurationDto>> GetModulesByCategoryAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving module configurations for category {Category}", category);

        using var scope = _scopeFactory.CreateScope();
        var repository = GetRepository(scope);

        var configurations = await repository.GetByCategoryAsync(category, cancellationToken);

        var result = configurations.Select(MapToDto).ToList();
        _logger.LogDebug("Retrieved {Count} module configurations for category {Category}", result.Count, category);

        return result;
    }

    public async Task<CommandModuleConfigurationDto?> GetModuleAsync(
        string moduleName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving module configuration for {ModuleName}", moduleName);

        using var scope = _scopeFactory.CreateScope();
        var repository = GetRepository(scope);

        var configuration = await repository.GetByNameAsync(moduleName, cancellationToken);

        if (configuration == null)
        {
            _logger.LogDebug("Module configuration not found for {ModuleName}", moduleName);
            return null;
        }

        return MapToDto(configuration);
    }

    public async Task<bool> IsModuleEnabledAsync(
        string moduleName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Checking if module {ModuleName} is enabled", moduleName);

        using var scope = _scopeFactory.CreateScope();
        var repository = GetRepository(scope);

        var configuration = await repository.GetByNameAsync(moduleName, cancellationToken);

        // Default to enabled if not configured
        var isEnabled = configuration?.IsEnabled ?? true;
        _logger.LogTrace("Module {ModuleName} is {State}", moduleName, isEnabled ? "enabled" : "disabled");

        return isEnabled;
    }

    public async Task<CommandModuleUpdateResultDto> SetModuleEnabledAsync(
        string moduleName,
        bool isEnabled,
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting module {ModuleName} enabled state to {IsEnabled} by user {UserId}",
            moduleName, isEnabled, userId);

        using var scope = _scopeFactory.CreateScope();
        var repository = GetRepository(scope);

        var configuration = await repository.GetByNameAsync(moduleName, cancellationToken);

        if (configuration == null)
        {
            _logger.LogWarning("Module configuration not found for {ModuleName}", moduleName);
            return new CommandModuleUpdateResultDto
            {
                Success = false,
                Errors = new[] { $"Module '{moduleName}' not found" }
            };
        }

        // Check if this is a core module that cannot be disabled
        if (!isEnabled && configuration.Category == "Core")
        {
            _logger.LogWarning("Attempted to disable core module {ModuleName}", moduleName);
            return new CommandModuleUpdateResultDto
            {
                Success = false,
                Errors = new[] { $"Core module '{moduleName}' cannot be disabled" }
            };
        }

        // Check if value is actually changing
        if (configuration.IsEnabled == isEnabled)
        {
            _logger.LogDebug("Module {ModuleName} already has enabled state {IsEnabled}", moduleName, isEnabled);
            return new CommandModuleUpdateResultDto
            {
                Success = true,
                RequiresRestart = false,
                UpdatedModules = Array.Empty<string>()
            };
        }

        // Update the configuration
        configuration.IsEnabled = isEnabled;
        configuration.LastModifiedAt = DateTime.UtcNow;
        configuration.LastModifiedBy = userId;

        await repository.UpsertAsync(configuration, cancellationToken);

        var requiresRestart = configuration.RequiresRestart;
        if (requiresRestart)
        {
            _restartPending = true;
            _logger.LogWarning("Restart pending flag set due to module {ModuleName} state change", moduleName);
        }

        // Raise the ConfigurationChanged event
        OnConfigurationChanged(new CommandModuleConfigurationChangedEventArgs
        {
            UpdatedModules = new[] { moduleName },
            UserId = userId
        });

        _logger.LogInformation("Module {ModuleName} enabled state updated to {IsEnabled}, restart required: {RequiresRestart}",
            moduleName, isEnabled, requiresRestart);

        return new CommandModuleUpdateResultDto
        {
            Success = true,
            RequiresRestart = requiresRestart,
            UpdatedModules = new[] { moduleName }
        };
    }

    public async Task<CommandModuleUpdateResultDto> UpdateModulesAsync(
        CommandModuleConfigurationUpdateDto updates,
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating {Count} module configurations for user {UserId}",
            updates.Modules.Count, userId);

        using var scope = _scopeFactory.CreateScope();
        var repository = GetRepository(scope);

        var errors = new List<string>();
        var updatedModules = new List<string>();
        var requiresRestart = false;

        // Get all existing configurations
        var allConfigurations = await repository.GetAllAsync(cancellationToken);
        var configDict = allConfigurations.ToDictionary(c => c.ModuleName);

        foreach (var (moduleName, isEnabled) in updates.Modules)
        {
            if (!configDict.TryGetValue(moduleName, out var configuration))
            {
                errors.Add($"Module '{moduleName}' not found");
                _logger.LogWarning("Attempted to update unknown module {ModuleName}", moduleName);
                continue;
            }

            // Check if this is a core module that cannot be disabled
            if (!isEnabled && configuration.Category == "Core")
            {
                errors.Add($"Core module '{moduleName}' cannot be disabled");
                _logger.LogWarning("Attempted to disable core module {ModuleName}", moduleName);
                continue;
            }

            // Skip if value is not changing
            if (configuration.IsEnabled == isEnabled)
            {
                _logger.LogDebug("Module {ModuleName} unchanged, skipping", moduleName);
                continue;
            }

            // Update the configuration
            configuration.IsEnabled = isEnabled;
            configuration.LastModifiedAt = DateTime.UtcNow;
            configuration.LastModifiedBy = userId;

            await repository.UpsertAsync(configuration, cancellationToken);
            updatedModules.Add(moduleName);

            if (configuration.RequiresRestart)
            {
                requiresRestart = true;
            }

            _logger.LogInformation("Updated module {ModuleName} enabled state to {IsEnabled}",
                moduleName, isEnabled);
        }

        if (requiresRestart)
        {
            _restartPending = true;
            _logger.LogWarning("Restart pending flag set due to module configuration changes");
        }

        var success = errors.Count == 0;
        _logger.LogInformation("Module update completed: {UpdatedCount} updated, {ErrorCount} errors, restart required: {RequiresRestart}",
            updatedModules.Count, errors.Count, requiresRestart);

        // Raise the ConfigurationChanged event if any modules were updated
        if (updatedModules.Count > 0)
        {
            OnConfigurationChanged(new CommandModuleConfigurationChangedEventArgs
            {
                UpdatedModules = updatedModules,
                UserId = userId
            });
        }

        return new CommandModuleUpdateResultDto
        {
            Success = success,
            RequiresRestart = requiresRestart,
            UpdatedModules = updatedModules,
            Errors = errors
        };
    }

    public async Task<int> SyncModulesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Synchronizing module configurations with default definitions");

        using var scope = _scopeFactory.CreateScope();
        var repository = GetRepository(scope);

        var existingConfigurations = await repository.GetAllAsync(cancellationToken);
        var existingModules = existingConfigurations.ToDictionary(c => c.ModuleName);

        var addedOrUpdated = 0;
        var now = DateTime.UtcNow;

        foreach (var defaultModule in DefaultModules)
        {
            if (!existingModules.ContainsKey(defaultModule.ModuleName))
            {
                // Add new module configuration
                var configuration = new CommandModuleConfiguration
                {
                    ModuleName = defaultModule.ModuleName,
                    DisplayName = defaultModule.DisplayName,
                    Description = defaultModule.Description,
                    Category = defaultModule.Category,
                    IsEnabled = true, // Default to enabled
                    RequiresRestart = defaultModule.RequiresRestart,
                    LastModifiedAt = now,
                    LastModifiedBy = null // System-created
                };

                await repository.UpsertAsync(configuration, cancellationToken);
                addedOrUpdated++;

                _logger.LogInformation("Added default module configuration for {ModuleName}", defaultModule.ModuleName);
            }
        }

        _logger.LogInformation("Module synchronization completed: {Count} modules added", addedOrUpdated);
        return addedOrUpdated;
    }

    /// <summary>
    /// Raises the ConfigurationChanged event.
    /// </summary>
    protected virtual void OnConfigurationChanged(CommandModuleConfigurationChangedEventArgs e)
    {
        ConfigurationChanged?.Invoke(this, e);
    }

    /// <summary>
    /// Maps an entity to a DTO.
    /// </summary>
    private static CommandModuleConfigurationDto MapToDto(CommandModuleConfiguration configuration)
    {
        return new CommandModuleConfigurationDto
        {
            ModuleName = configuration.ModuleName,
            IsEnabled = configuration.IsEnabled,
            DisplayName = configuration.DisplayName,
            Description = configuration.Description,
            Category = configuration.Category,
            RequiresRestart = configuration.RequiresRestart,
            LastModifiedAt = configuration.LastModifiedAt,
            LastModifiedBy = configuration.LastModifiedBy,
            CommandCount = 0, // Will be populated by command metadata service
            Commands = Array.Empty<string>() // Will be populated by command metadata service
        };
    }

    /// <summary>
    /// Internal record for default module definitions.
    /// </summary>
    private record DefaultModuleDefinition(
        string ModuleName,
        string DisplayName,
        string? Description,
        string Category,
        bool RequiresRestart);
}
