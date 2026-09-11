using System.Reflection;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Commands;
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
/// <c>DiscordServiceExtensions.AddDiscordBot</c> and docs/articles/background-services.md.
/// </summary>
public class SlashCommandRegistrationService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly BotConfiguration _config;
    private readonly ILogger<SlashCommandRegistrationService> _logger;
    private readonly ICommandModuleConfigurationService _commandModuleConfigService;
    private readonly NotXOptions _notXOptions;

    /// <summary>
    /// Module names belonging to the not-X feature. Both are top-level modules — a context
    /// menu command cannot be declared inside a <see cref="GroupAttribute"/> module — so
    /// neither is covered by the component-module parent lookup in
    /// <see cref="DiscoverAndLoadModulesAsync"/> and they must be named explicitly.
    /// </summary>
    private static readonly string[] NotXModuleNames =
    [
        nameof(NotXCommandModule),
        nameof(NotXContextMenuModule)
    ];

    /// <summary>
    /// Modules that have no database toggle of their own and follow another module's state.
    /// The <c>*ComponentModule</c> convention below derives its parent by name; these cannot,
    /// so the pairing is explicit. <see cref="NotXContextMenuModule"/> is listed because a
    /// context menu command cannot be declared inside the <c>[Group]</c>-decorated
    /// <see cref="NotXCommandModule"/>, yet the Commands tab presents not-X as one feature.
    /// </summary>
    private static readonly Dictionary<string, string> CompanionModuleParents =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(NotXContextMenuModule)] = nameof(NotXCommandModule)
        };

    public SlashCommandRegistrationService(
        DiscordSocketClient client,
        InteractionService interactionService,
        IServiceProvider serviceProvider,
        IOptions<BotConfiguration> config,
        ILogger<SlashCommandRegistrationService> logger,
        ICommandModuleConfigurationService commandModuleConfigService,
        IOptions<NotXOptions> notXOptions)
    {
        _client = client;
        _interactionService = interactionService;
        _serviceProvider = serviceProvider;
        _config = config.Value;
        _logger = logger;
        _commandModuleConfigService = commandModuleConfigService;
        _notXOptions = notXOptions.Value;
    }

    /// <summary>
    /// Builds the set of module names switched off by configuration, as opposed to by the
    /// database module toggles. Leaving a module out of discovery is what deregisters its
    /// commands: <see cref="RegisterCommandsAsync"/> publishes the loaded command set as a
    /// bulk overwrite, so commands that are no longer registered locally are removed from
    /// Discord on the next startup.
    /// </summary>
    internal static IReadOnlySet<string> GetConfigurationDisabledModules(NotXOptions notXOptions)
    {
        var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!notXOptions.Enabled)
        {
            foreach (var moduleName in NotXModuleNames)
            {
                disabled.Add(moduleName);
            }
        }

        return disabled;
    }

    /// <summary>
    /// Returns the module whose database toggle the given module follows, or null when the
    /// module has a toggle of its own. Component modules follow the <c>*ComponentModule</c>
    /// naming convention; everything else comes from <see cref="CompanionModuleParents"/>.
    /// </summary>
    internal static string? ResolveCompanionParentModule(string moduleName)
    {
        if (moduleName.EndsWith("ComponentModule", StringComparison.Ordinal))
        {
            return moduleName.Replace("ComponentModule", "Module");
        }

        return CompanionModuleParents.GetValueOrDefault(moduleName);
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

        // Discover command module types from the executing assembly.
        // Nested sub-group modules (a [Group] class declared inside another module) are built by
        // Discord.NET as part of their parent, so they must not be passed to AddModuleAsync on
        // their own; doing so throws "Could not build the module ..." at startup.
        var assembly = Assembly.GetExecutingAssembly();
        var allModuleTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && IsInteractionModule(t) && !IsNestedSubModule(t))
            .ToList();

        var loadedModules = new List<string>();
        var skippedModules = new List<string>();

        // Build a set of disabled module names for component module parent lookups
        var disabledModuleNames = moduleConfigurations
            .Where(m => !m.IsEnabled)
            .Select(m => m.ModuleName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Features switched off in configuration outrank the database toggles: their modules
        // are never loaded, so the bulk-overwrite registration drops their commands.
        var configurationDisabledModules = GetConfigurationDisabledModules(_notXOptions);

        // Register only enabled modules
        foreach (var moduleType in allModuleTypes)
        {
            var moduleName = moduleType.Name;

            if (configurationDisabledModules.Contains(moduleName))
            {
                skippedModules.Add(moduleName);
                _logger.LogInformation(
                    "Skipped module {ModuleName} because its feature is disabled in configuration",
                    moduleName);
                continue;
            }

            // If this module follows another module's toggle, skip it when that parent is
            // disabled. Component modules derive their parent by name; modules whose name
            // does not follow that convention are mapped explicitly. Anything not skipped
            // here falls through to the normal per-module lookups below, unchanged.
            var parentModuleName = ResolveCompanionParentModule(moduleName);
            if (parentModuleName is not null && disabledModuleNames.Contains(parentModuleName))
            {
                skippedModules.Add(moduleName);
                _logger.LogInformation("Skipped module {ModuleName} because parent {ParentModuleName} is disabled",
                    moduleName, parentModuleName);
                continue;
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
    /// <summary>
    /// Returns true when the type is a module declared inside another interaction module.
    /// Such types are registered by Discord.NET together with their declaring module.
    /// </summary>
    private static bool IsNestedSubModule(Type type)
    {
        return type.DeclaringType != null && IsInteractionModule(type.DeclaringType);
    }

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
