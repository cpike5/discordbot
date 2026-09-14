using System.ComponentModel.DataAnnotations;
using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models.Llm;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Blazor.Pages.Guilds;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Guilds/AssistantSettings.cshtml</c> +
/// <c>AssistantSettingsModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b).
/// Reproduces the channel allow-list, the Tool Access checklist (grouped by
/// <see cref="ToolCatalog"/> category, with the "using the default set" banner and
/// <see cref="ToolCatalog.NormalizeSelection"/> on save), and the rate-limit override.
/// </summary>
public partial class AssistantSettings : GuildPageBase
{
    [Inject]
    private IAssistantGuildSettingsService SettingsService { get; set; } = default!;

    [Inject]
    private IServiceScopeFactory ScopeFactory { get; set; } = default!;

    [Inject]
    private IDiscordChannelResolver ChannelResolver { get; set; } = default!;

    [Inject]
    private IOptions<AssistantOptions> AssistantOptionsAccessor { get; set; } = default!;

    [Inject]
    private ISettingsService GlobalSettingsService { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private ILogger<AssistantSettings> Logger { get; set; } = default!;

    protected InputModel Input { get; set; } = new();
    protected List<ChannelSelectItem> AvailableChannels { get; set; } = [];
    protected List<ToolCategoryGroup> ToolCategories { get; set; } = [];
    protected bool UsingDefaultToolSet { get; private set; }
    protected int DefaultRateLimit { get; private set; }
    protected int RateLimitWindowMinutes { get; private set; }
    protected bool GloballyEnabled { get; private set; }

    protected override async Task OnGuildContextReadyAsync()
    {
        if (Guild is null)
        {
            return;
        }

        await LoadAsync(SettingsService);
    }

    /// <summary>
    /// Loads the page's data through <paramref name="settingsService"/> - the circuit-scoped
    /// <see cref="SettingsService"/> for the initial load, a fresh scope's instance via
    /// <see cref="ScopeFactory"/> for the reload that follows <see cref="HandleValidSubmit"/>'s
    /// save (docs/architecture/patterns.md "Blazor Components" § Per-operation scopes).
    /// </summary>
    private async Task LoadAsync(IAssistantGuildSettingsService settingsService)
    {
        var guildId = (ulong)GuildId;
        var settings = await settingsService.GetOrCreateSettingsAsync(guildId);
        var allowedChannels = settings.GetAllowedChannelIdsList();

        AvailableChannels = ChannelResolver.GetTextChannels(guildId)
            .Where(c => c.Type is ChannelDisplayType.Text or ChannelDisplayType.Announcement)
            .Select(ChannelSelectItem.FromChannelInfo)
            .ToList();

        var enabledTools = settings.GetEnabledToolsList();
        UsingDefaultToolSet = enabledTools.Count == 0;
        ToolCategories = BuildToolCategories(enabledTools);

        DefaultRateLimit = AssistantOptionsAccessor.Value.RateLimits.DefaultRateLimit;
        RateLimitWindowMinutes = AssistantOptionsAccessor.Value.RateLimits.RateLimitWindowMinutes;
        GloballyEnabled = await GlobalSettingsService.GetSettingValueAsync<bool>("Assistant:GloballyEnabled");

        Input = new InputModel
        {
            IsEnabled = settings.IsEnabled,
            AllowedChannelIds = allowedChannels.Select(id => id.ToString()).ToList(),
            RateLimitOverride = settings.RateLimitOverride,
            EnabledTools = enabledTools
        };
    }

    protected async Task HandleValidSubmit()
    {
        if (Guild is null)
        {
            return;
        }

        var guildId = (ulong)GuildId;

        // Fetch, mutate and save within one scope (one DbContext) so the entity GetOrCreateSettingsAsync
        // returns is the same tracked instance UpdateSettingsAsync saves - see docs/architecture/patterns.md
        // "Blazor Components" § Per-operation scopes.
        await ScopeFactory.RunAsync<IAssistantGuildSettingsService>(async s =>
        {
            var settings = await s.GetOrCreateSettingsAsync(guildId);

            settings.IsEnabled = Input.IsEnabled;
            settings.RateLimitOverride = Input.RateLimitOverride;

            var channelIds = Input.AllowedChannelIds
                .Select(v => ulong.TryParse(v, out var id) ? (ulong?)id : null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToList();
            settings.SetAllowedChannelIdsList(channelIds);
            settings.SetEnabledToolsList(ToolCatalog.NormalizeSelection(Input.EnabledTools, ToolScopes.Guild));

            await s.UpdateSettingsAsync(settings);
        });

        Logger.LogInformation("Successfully updated assistant settings for guild {GuildId}", guildId);
        Toast.Success("Assistant settings saved successfully.");

        await ScopeFactory.RunAsync<IAssistantGuildSettingsService>(LoadAsync);
    }

    protected void ToggleChannel(string channelId, bool selected)
    {
        if (selected)
        {
            if (!Input.AllowedChannelIds.Contains(channelId))
            {
                Input.AllowedChannelIds.Add(channelId);
            }
        }
        else
        {
            Input.AllowedChannelIds.Remove(channelId);
        }
    }

    protected void ToggleTool(string toolName, bool selected)
    {
        if (selected)
        {
            if (!Input.EnabledTools.Contains(toolName))
            {
                Input.EnabledTools.Add(toolName);
            }
        }
        else
        {
            Input.EnabledTools.Remove(toolName);
        }
    }

    /// <summary>
    /// Builds the grouped checklist from the tool catalogue, ticking whatever the guild has saved
    /// (or the house default set, when the guild has saved nothing) - mirrors
    /// <c>AssistantSettingsModel.BuildToolCategories</c>.
    /// </summary>
    private static List<ToolCategoryGroup> BuildToolCategories(List<string> enabledTools)
    {
        var selected = enabledTools.Count > 0
            ? enabledTools.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : ToolCatalog.DefaultsForScope(ToolScopes.Guild).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return ToolCatalog.ForScope(ToolScopes.Guild)
            .GroupBy(t => t.Category, StringComparer.Ordinal)
            .Select(g => new ToolCategoryGroup
            {
                Category = g.Key,
                Tools = g.Select(t => new ToolSelectItem
                {
                    Name = t.Name,
                    DisplayName = t.DisplayName,
                    Description = t.Description,
                    IsSelected = selected.Contains(t.Name)
                }).ToList()
            })
            .ToList();
    }

    /// <summary>Mirrors <c>AssistantSettingsModel.InputModel</c> (minus <c>GuildId</c>).</summary>
    public sealed class InputModel
    {
        [Display(Name = "Enable AI Assistant")]
        public bool IsEnabled { get; set; }

        [Display(Name = "Allowed Channels")]
        public List<string> AllowedChannelIds { get; set; } = [];

        [Display(Name = "Rate Limit Override")]
        [Range(1, 100, ErrorMessage = "Rate limit must be between 1 and 100")]
        public int? RateLimitOverride { get; set; }

        [Display(Name = "Enabled Tools")]
        public List<string> EnabledTools { get; set; } = [];
    }

    /// <summary>One catalogue category and the tools filed under it.</summary>
    public sealed class ToolCategoryGroup
    {
        public string Category { get; set; } = string.Empty;
        public List<ToolSelectItem> Tools { get; set; } = [];
    }

    /// <summary>One tool row in the checklist.</summary>
    public sealed class ToolSelectItem
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }
}
