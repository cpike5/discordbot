using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for managing themes with user preference hierarchy.
/// Theme resolution priority: user preference > admin default > system default.
/// </summary>
public class ThemeService : IThemeService
{
    private const string DefaultThemeKey = "discord-dark";
    private const string DefaultThemeSettingKey = "Appearance:DefaultThemeId";

    private readonly IThemeRepository _themeRepository;
    private readonly ISettingsService _settingsService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly BotDbContext _dbContext;
    private readonly ILogger<ThemeService> _logger;

    public ThemeService(
        IThemeRepository themeRepository,
        ISettingsService settingsService,
        UserManager<ApplicationUser> userManager,
        BotDbContext dbContext,
        ILogger<ThemeService> logger)
    {
        _themeRepository = themeRepository;
        _settingsService = settingsService;
        _userManager = userManager;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ThemeDto>> GetActiveThemesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching all active themes");

        var themes = await _themeRepository.GetAllActiveAsync(cancellationToken);
        return themes.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<ThemeDto?> GetThemeByKeyAsync(string themeKey, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching theme with key {ThemeKey}", themeKey);

        var theme = await _themeRepository.GetByKeyAsync(themeKey, cancellationToken);
        return theme != null ? MapToDto(theme) : null;
    }

    /// <inheritdoc />
    public async Task<CurrentThemeDto> GetUserThemeAsync(string userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting effective theme for user {UserId}", userId);

        // First, try to get user's explicit preference
        var user = await _dbContext.Set<ApplicationUser>()
            .Include(u => u.PreferredTheme)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user?.PreferredTheme != null && user.PreferredTheme.IsActive)
        {
            _logger.LogDebug("User {UserId} has explicit theme preference: {ThemeKey}",
                userId, user.PreferredTheme.ThemeKey);

            return new CurrentThemeDto
            {
                Theme = MapToDto(user.PreferredTheme),
                Source = ThemeSource.User
            };
        }

        // Fall back to system default
        var defaultTheme = await GetDefaultThemeAsync(cancellationToken);
        _logger.LogDebug("Using system default theme for user {UserId}: {ThemeKey}",
            userId, defaultTheme.ThemeKey);

        return new CurrentThemeDto
        {
            Theme = defaultTheme,
            Source = ThemeSource.System
        };
    }

    /// <inheritdoc />
    public async Task<ThemeDto> GetDefaultThemeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching system default theme");

        // Try to get configured default theme ID from settings
        var defaultThemeId = await _settingsService.GetSettingValueAsync<int?>(
            DefaultThemeSettingKey, cancellationToken);

        if (defaultThemeId.HasValue)
        {
            var configuredTheme = await _themeRepository.GetByIdAsync(defaultThemeId.Value, cancellationToken);
            if (configuredTheme != null && configuredTheme.IsActive)
            {
                _logger.LogDebug("Using configured default theme: {ThemeKey}", configuredTheme.ThemeKey);
                return MapToDto(configuredTheme);
            }

            _logger.LogWarning("Configured default theme ID {ThemeId} not found or inactive, falling back",
                defaultThemeId.Value);
        }

        // Fall back to discord-dark theme
        var discordDark = await _themeRepository.GetByKeyAsync(DefaultThemeKey, cancellationToken);
        if (discordDark != null)
        {
            return MapToDto(discordDark);
        }

        // Absolute fallback - return first active theme
        var activeThemes = await _themeRepository.GetAllActiveAsync(cancellationToken);
        if (activeThemes.Any())
        {
            _logger.LogWarning("Discord Dark theme not found, using first active theme");
            return MapToDto(activeThemes.First());
        }

        // This should never happen if database is seeded correctly
        _logger.LogError("No active themes found in database");
        throw new InvalidOperationException("No active themes available in the system");
    }

    /// <inheritdoc />
    public async Task<bool> SetUserThemeAsync(string userId, int? themeId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting theme preference for user {UserId} to theme ID {ThemeId}",
            userId, themeId?.ToString() ?? "null (clear)");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found", userId);
            return false;
        }

        // If clearing preference, just set to null
        if (!themeId.HasValue)
        {
            user.PreferredThemeId = null;
            var clearResult = await _userManager.UpdateAsync(user);
            if (clearResult.Succeeded)
            {
                _logger.LogInformation("Cleared theme preference for user {UserId}", userId);
                return true;
            }

            _logger.LogError("Failed to clear theme preference for user {UserId}: {Errors}",
                userId, string.Join(", ", clearResult.Errors.Select(e => e.Description)));
            return false;
        }

        // Validate theme exists and is active
        var theme = await _themeRepository.GetByIdAsync(themeId.Value, cancellationToken);
        if (theme == null)
        {
            _logger.LogWarning("Theme ID {ThemeId} not found", themeId.Value);
            return false;
        }

        if (!theme.IsActive)
        {
            _logger.LogWarning("Theme ID {ThemeId} is not active", themeId.Value);
            return false;
        }

        // Set user's preferred theme
        user.PreferredThemeId = themeId.Value;
        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            _logger.LogInformation("Updated theme preference for user {UserId} to {ThemeKey}",
                userId, theme.ThemeKey);
            return true;
        }

        _logger.LogError("Failed to update theme preference for user {UserId}: {Errors}",
            userId, string.Join(", ", result.Errors.Select(e => e.Description)));
        return false;
    }

    /// <inheritdoc />
    public async Task<bool> SetDefaultThemeAsync(int themeId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting system default theme to ID {ThemeId}", themeId);

        // Validate theme exists and is active
        var theme = await _themeRepository.GetByIdAsync(themeId, cancellationToken);
        if (theme == null)
        {
            _logger.LogWarning("Theme ID {ThemeId} not found", themeId);
            return false;
        }

        if (!theme.IsActive)
        {
            _logger.LogWarning("Theme ID {ThemeId} is not active", themeId);
            return false;
        }

        // Store in settings
        var updateResult = await _settingsService.UpdateSettingsAsync(
            new SettingsUpdateDto
            {
                Settings = new Dictionary<string, string>
                {
                    [DefaultThemeSettingKey] = themeId.ToString()
                }
            },
            "system",
            cancellationToken);

        if (updateResult.Success)
        {
            _logger.LogInformation("System default theme set to {ThemeKey}", theme.ThemeKey);
            return true;
        }

        _logger.LogError("Failed to update default theme setting: {Errors}",
            string.Join(", ", updateResult.Errors));
        return false;
    }

    private static ThemeDto MapToDto(Theme theme)
    {
        return new ThemeDto
        {
            Id = theme.Id,
            ThemeKey = theme.ThemeKey,
            DisplayName = theme.DisplayName,
            Description = theme.Description,
            ColorDefinition = theme.ColorDefinition,
            IsActive = theme.IsActive
        };
    }
}
