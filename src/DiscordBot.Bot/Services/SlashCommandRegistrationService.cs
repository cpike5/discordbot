using System.Reflection;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Hosted service that owns slash-command registration lifecycle: discovers interaction
/// modules from the assembly, filters them by configuration, and registers commands with
/// Discord (test guild or global) once the gateway reports Ready.
///
/// Split out of <see cref="BotHostedService"/>/<see cref="Handlers.InteractionHandler"/> so
/// command registration is a standalone concern. Must be started before
/// <see cref="BotHostedService"/> so module discovery completes before the client logs in
/// and the Ready event can fire — see the hosted-service ordering block on
/// <c>DiscordServiceExtensions.AddDiscordBot</c> and CLAUDE-REFERENCE.md.
/// </summary>
public class SlashCommandRegistrationService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly BotConfiguration _config;
    private readonly ILogger<SlashCommandRegistrationService> _logger;
    private readonly ICommandModuleConfigurationService _commandModuleConfigService;

    public SlashCommandRegistrationService(
        DiscordSocketClient client,
        InteractionService interactionService,
        IServiceProvider serviceProvider,
        IOptions<BotConfiguration> config,
        ILogger<SlashCommandRegistrationService> logger,
        ICommandModuleConfigurationService commandModuleConfigService)
    {
        _client = client;
        _interactionService = interactionService;
        _serviceProvider = serviceProvider;
        _config = config.Value;
        _logger = logger;
        _commandModuleConfigService = commandModuleConfigService;
    }

    /// <summary>
    /// Discovers modules and wires the Ready event so commands register once connected.
    /// Runs before <see cref="BotHostedService"/> logs in (see ordering doc), so module
    /// discovery is guaranteed to be complete before the gateway can raise Ready.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await DiscoverAndLoadModulesAsync(cancellationToken);
        _client.Ready += OnReadyAsync;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _client.Ready -= OnReadyAsync;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task DiscoverAndLoadModulesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Discovering and loading command modules");

        // Sync module configurations to ensure database has all module definitions
        await _commandModuleConfigService.SyncModulesAsync();

        // Get all module configurations to determine which are enabled
        var moduleConfigurations = await _commandModuleConfigService.GetAllModulesAsync();
        var enabledModuleNames = moduleConfigurations
            .Where(m => m.IsEnabled)
            .Select(m => m.ModuleName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Discover command module types from the executing assembly
        var assembly = Assembly.GetExecutingAssembly();
        var allModuleTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && IsInteractionModule(t))
            .ToList();

        var loadedModules = new List<string>();
        var skippedModules = new List<string>();

        // Build a set of disabled module names for component module parent lookups
        var disabledModuleNames = moduleConfigurations
            .Where(m => !m.IsEnabled)
            .Select(m => m.ModuleName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Register only enabled modules
        foreach (var moduleType in allModuleTypes)
        {
            var moduleName = moduleType.Name;

            // If this is a component module, check if its parent module is disabled
            if (moduleName.EndsWith("ComponentModule", StringComparison.Ordinal))
            {
                var parentModuleName = moduleName.Replace("ComponentModule", "Module");
                if (disabledModuleNames.Contains(parentModuleName))
                {
                    skippedModules.Add(moduleName);
                    _logger.LogInformation("Skipped component module {ModuleName} because parent {ParentModuleName} is disabled",
                        moduleName, parentModuleName);
                    continue;
                }
            }

            // If we have no configuration for this module, default to enabled
            if (!moduleConfigurations.Any(m => m.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase)))
            {
                await _interactionService.AddModuleAsync(moduleType, _serviceProvider);
                loadedModules.Add(moduleName);
                _logger.LogDebug("Loaded unconfigured module {ModuleName} (defaulting to enabled)", moduleName);
                continue;
            }

            if (enabledModuleNames.Contains(moduleName))
            {
                await _interactionService.AddModuleAsync(moduleType, _serviceProvider);
                loadedModules.Add(moduleName);
            }
            else
            {
                skippedModules.Add(moduleName);
            }
        }

        // Log summary of loaded and skipped modules
        _logger.LogInformation("Loaded {EnabledCount} command modules: {Modules}",
            loadedModules.Count,
            string.Join(", ", loadedModules.OrderBy(n => n)));

        if (skippedModules.Count > 0)
        {
            _logger.LogInformation("Skipped {DisabledCount} disabled modules: {Modules}",
                skippedModules.Count,
                string.Join(", ", skippedModules.OrderBy(n => n)));
        }

        _logger.LogDebug("Command registration service initialized with {ModuleCount} modules", _interactionService.Modules.Count());
    }

    /// <summary>
    /// Determines if a type is a Discord.NET interaction module.
    /// Checks if the type inherits from InteractionModuleBase (generic or non-generic).
    /// </summary>
    private static bool IsInteractionModule(Type type)
    {
        var baseType = type.BaseType;
        while (baseType != null)
        {
            if (baseType.IsGenericType &&
                baseType.GetGenericTypeDefinition() == typeof(InteractionModuleBase<>))
            {
                return true;
            }
            baseType = baseType.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Called when the bot is ready and connected to Discord.
    /// Registers slash commands either to a test guild or globally.
    /// </summary>
    private async Task OnReadyAsync()
    {
        _logger.LogInformation("Bot is ready. Connected as {Username}#{Discriminator}", _client.CurrentUser.Username, _client.CurrentUser.Discriminator);
        await RegisterCommandsAsync();
    }

    /// <inheritdoc />
    public async Task RegisterCommandsAsync()
    {
        try
        {
            if (ShouldRegisterToTestGuild(_config.TestGuildId))
            {
                // Register commands to test guild for faster development iteration
                _logger.LogInformation("Registering commands to test guild {GuildId}", _config.TestGuildId!.Value);
                await _interactionService.RegisterCommandsToGuildAsync(_config.TestGuildId.Value);
                _logger.LogInformation("Commands registered to test guild successfully");
            }
            else
            {
                // Register commands globally (takes ~1 hour to propagate)
                _logger.LogInformation("Registering commands globally");
                await _interactionService.RegisterCommandsGloballyAsync();
                _logger.LogInformation("Commands registered globally successfully. Note: Global commands may take up to 1 hour to propagate");
            }
        }
        catch (Discord.Net.HttpException ex) when (ex.DiscordCode == Discord.DiscordErrorCode.MissingPermissions)
        {
            _logger.LogWarning(
                "Missing access to register commands to guild {GuildId}. " +
                "Ensure the bot was invited with the 'applications.commands' scope. " +
                "Re-invite the bot using: https://discord.com/oauth2/authorize?client_id={ClientId}&scope=bot%20applications.commands&permissions=0",
                _config.TestGuildId,
                _client.CurrentUser.Id);

            // Fall back to global registration
            _logger.LogInformation("Falling back to global command registration");
            try
            {
                await _interactionService.RegisterCommandsGloballyAsync();
                _logger.LogInformation("Commands registered globally successfully. Note: Global commands may take up to 1 hour to propagate");
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Failed to register commands globally after guild registration failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register commands");
        }
    }

    /// <summary>
    /// Determines whether commands should register to a test guild (fast iteration) or
    /// globally. Extracted as a pure predicate so the decision is unit-testable without
    /// needing a live DiscordSocketClient/InteractionService.
    /// </summary>
    internal static bool ShouldRegisterToTestGuild(ulong? testGuildId) => testGuildId.HasValue;
}
