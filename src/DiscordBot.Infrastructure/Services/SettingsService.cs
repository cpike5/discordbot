using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

namespace DiscordBot.Infrastructure.Services;

/// <summary>
/// Service implementation for managing application settings with validation and configuration merging.
/// Registered as singleton to maintain the restart pending flag across requests.
/// Uses IServiceScopeFactory to resolve scoped repository instances.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SettingsService> _logger;
    private bool _restartPending;

    /// <inheritdoc />
    public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

    public SettingsService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<SettingsService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
        _restartPending = false;
    }

    /// <summary>
    /// Creates a new scope and returns the settings repository.
    /// </summary>
    private ISettingsRepository GetRepository(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ISettingsRepository>();

    public bool IsRestartPending => _restartPending;

    public void ClearRestartPending()
    {
        _logger.LogInformation("Restart pending flag cleared");
        _restartPending = false;
    }

    public async Task<IReadOnlyList<SettingDto>> GetSettingsByCategoryAsync(
        SettingCategory category,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving settings for category {Category}", category);

        using var scope = _scopeFactory.CreateScope();
        var repository = GetRepository(scope);

        var definitions = SettingDefinitions.GetByCategory(category);
        var dbSettings = await repository.GetByCategoryAsync(category, cancellationToken);
        var dbSettingsDict = dbSettings.ToDictionary(s => s.Key, s => s.Value);

        var result = definitions.Select(def => MapToDto(def, dbSettingsDict)).ToList();

        _logger.LogDebug("Retrieved {Count} settings for category {Category}", result.Count, category);
        return result;
    }

    public async Task<IReadOnlyList<SettingDto>> GetAllSettingsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving all settings");

        using var scope = _scopeFactory.CreateScope();
        var repository = GetRepository(scope);

        var dbSettings = await repository.GetAllAsync(cancellationToken);
        var dbSettingsDict = dbSettings.ToDictionary(s => s.Key, s => s.Value);

        var result = SettingDefinitions.All.Select(def => MapToDto(def, dbSettingsDict)).ToList();

        _logger.LogDebug("Retrieved {Count} total settings", result.Count);
        return result;
    }

    public async Task<T?> GetSettingValueAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Getting setting value for key {Key}", key);

        using var scope = _scopeFactory.CreateScope();
        var repository = GetRepository(scope);

        // Check database first
        var dbSetting = await repository.GetByKeyAsync(key, cancellationToken);
        string? valueStr = dbSetting?.Value;

        // Fall back to configuration if not in database
        if (valueStr == null)
        {
            valueStr = _configuration[key];
            _logger.LogTrace("Setting {Key} not in database, using config value: {Value}", key, valueStr);
        }

        if (valueStr == null)
        {
            _logger.LogTrace("Setting {Key} not found, returning default", key);
            return default;
        }

        try
        {
            return ConvertValue<T>(valueStr);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert setting {Key} value '{Value}' to type {Type}, returning default",
                key, valueStr, typeof(T).Name);
            return default;
        }
    }

    public async Task<SettingsUpdateResultDto> UpdateSettingsAsync(
        SettingsUpdateDto updates,
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating {Count} settings for user {UserId}", updates.Settings.Count, userId);

        using var scope = _scopeFactory.CreateScope();
        var repository = GetRepository(scope);

        var errors = new List<string>();
        var updatedKeys = new List<string>();
        var requiresRestart = false;

        foreach (var (key, value) in updates.Settings)
        {
            var definition = SettingDefinitions.GetByKey(key);
            if (definition == null)
            {
                errors.Add($"Unknown setting key: {key}");
                _logger.LogWarning("Attempt to update unknown setting key {Key}", key);
                continue;
            }

            // Validate the value
            var validationError = ValidateValue(definition, value);
            if (validationError != null)
            {
                errors.Add($"{definition.DisplayName}: {validationError}");
                _logger.LogWarning("Validation failed for setting {Key}: {Error}", key, validationError);
                continue;
            }

            try
            {
                var setting = new ApplicationSetting
                {
                    Key = key,
                    Value = value ?? string.Empty, // Ensure we never pass null to satisfy NOT NULL constraint
                    Category = definition.Category,
                    DataType = definition.DataType,
                    RequiresRestart = definition.RequiresRestart,
                    LastModifiedAt = DateTime.UtcNow,
                    LastModifiedBy = userId
                };

                await repository.UpsertAsync(setting, cancellationToken);
                updatedKeys.Add(key);

                if (definition.RequiresRestart)
                {
                    requiresRestart = true;
                }

                _logger.LogInformation("Updated setting {Key} to '{Value}' (RequiresRestart: {RequiresRestart})",
                    key, value, definition.RequiresRestart);
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                errors.Add($"{definition.DisplayName}: Failed to save - {innerMessage}");
                _logger.LogError(ex, "Failed to save setting {Key}. Inner exception: {InnerException}",
                    key, ex.InnerException?.Message ?? "None");
            }
        }

        if (requiresRestart)
        {
            _restartPending = true;
            _logger.LogWarning("Restart pending flag set due to settings changes");
        }

        var success = errors.Count == 0;
        _logger.LogInformation("Settings update completed: {UpdatedCount} updated, {ErrorCount} errors, restart required: {RestartRequired}",
            updatedKeys.Count, errors.Count, requiresRestart);

        // Raise the SettingsChanged event if any settings were updated
        if (updatedKeys.Count > 0)
        {
            OnSettingsChanged(new SettingsChangedEventArgs
            {
                UpdatedKeys = updatedKeys,
                UserId = userId
            });
        }

        return new SettingsUpdateResultDto
        {
            Success = success,
            Errors = errors,
            RestartRequired = requiresRestart,
            UpdatedKeys = updatedKeys
        };
    }

    /// <summary>
    /// Raises the SettingsChanged event.
    /// </summary>
    protected virtual void OnSettingsChanged(SettingsChangedEventArgs e)
    {
        SettingsChanged?.Invoke(this, e);
    }

    public async Task<SettingsUpdateResultDto> ResetCategoryAsync(
        SettingCategory category,
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resetting category {Category} to defaults for user {UserId}", category, userId);

        using var scope = _scopeFactory.CreateScope();
        var repository = GetRepository(scope);

        try
        {
            await repository.DeleteByCategoryAsync(category, cancellationToken);

            var definitions = SettingDefinitions.GetByCategory(category);
            var requiresRestart = definitions.Any(d => d.RequiresRestart);

            if (requiresRestart)
            {
                _restartPending = true;
                _logger.LogWarning("Restart pending flag set due to category reset");
            }

            _logger.LogInformation("Successfully reset category {Category} to defaults", category);

            return new SettingsUpdateResultDto
            {
                Success = true,
                RestartRequired = requiresRestart,
                UpdatedKeys = definitions.Select(d => d.Key).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset category {Category}", category);
            return new SettingsUpdateResultDto
            {
                Success = false,
                Errors = new List<string> { $"Failed to reset category: {ex.Message}" }
            };
        }
    }

    public async Task<SettingsUpdateResultDto> ResetAllAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resetting all settings to defaults for user {UserId}", userId);

        using var scope = _scopeFactory.CreateScope();
        var repository = GetRepository(scope);

        try
        {
            // Delete all settings from all categories
            foreach (SettingCategory category in Enum.GetValues(typeof(SettingCategory)))
            {
                await repository.DeleteByCategoryAsync(category, cancellationToken);
            }

            var requiresRestart = SettingDefinitions.All.Any(d => d.RequiresRestart);

            if (requiresRestart)
            {
                _restartPending = true;
                _logger.LogWarning("Restart pending flag set due to reset all");
            }

            _logger.LogInformation("Successfully reset all settings to defaults");

            return new SettingsUpdateResultDto
            {
                Success = true,
                RestartRequired = requiresRestart,
                UpdatedKeys = SettingDefinitions.All.Select(d => d.Key).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset all settings");
            return new SettingsUpdateResultDto
            {
                Success = false,
                Errors = new List<string> { $"Failed to reset all settings: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Maps a setting definition to a DTO, merging database value with default.
    /// </summary>
    private SettingDto MapToDto(SettingDefinition definition, Dictionary<string, string> dbValues)
    {
        // Priority: database value > config value > definition default
        var value = dbValues.GetValueOrDefault(definition.Key)
            ?? _configuration[definition.Key]
            ?? definition.DefaultValue;

        return new SettingDto
        {
            Key = definition.Key,
            Value = value,
            Category = definition.Category,
            DataType = definition.DataType,
            RequiresRestart = definition.RequiresRestart,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            ValidationRules = definition.ValidationRules,
            AllowedValues = definition.AllowedValues,
            DefaultValue = definition.DefaultValue
        };
    }

    /// <summary>
    /// Validates a setting value against its definition.
    /// </summary>
    private string? ValidateValue(SettingDefinition definition, string value)
    {
        // Check allowed values first
        if (definition.AllowedValues != null && !definition.AllowedValues.Contains(value))
        {
            return $"Value must be one of: {string.Join(", ", definition.AllowedValues)}";
        }

        // Type-specific validation
        switch (definition.DataType)
        {
            case SettingDataType.Integer:
                if (!int.TryParse(value, out var intValue))
                {
                    return "Value must be a valid integer";
                }
                return ValidateNumericRange(intValue, definition.ValidationRules);

            case SettingDataType.Decimal:
                if (!decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue))
                {
                    return "Value must be a valid decimal number";
                }
                return ValidateNumericRange((double)decimalValue, definition.ValidationRules);

            case SettingDataType.Boolean:
                if (!bool.TryParse(value, out _))
                {
                    return "Value must be 'true' or 'false'";
                }
                break;

            case SettingDataType.Json:
                try
                {
                    JsonDocument.Parse(value);
                }
                catch
                {
                    return "Value must be valid JSON";
                }
                break;
        }

        return null;
    }

    /// <summary>
    /// Validates numeric values against min/max constraints.
    /// </summary>
    private string? ValidateNumericRange(double value, string? validationRulesJson)
    {
        if (validationRulesJson == null)
        {
            return null;
        }

        try
        {
            var rules = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(validationRulesJson);
            if (rules == null)
            {
                return null;
            }

            if (rules.TryGetValue("min", out var minElement))
            {
                var min = minElement.GetDouble();
                if (value < min)
                {
                    return $"Value must be at least {min}";
                }
            }

            if (rules.TryGetValue("max", out var maxElement))
            {
                var max = maxElement.GetDouble();
                if (value > max)
                {
                    return $"Value must be at most {max}";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse validation rules: {Rules}", validationRulesJson);
        }

        return null;
    }

    /// <summary>
    /// Converts a string value to the specified type.
    /// </summary>
    private T? ConvertValue<T>(string value)
    {
        var targetType = typeof(T);

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType == typeof(string))
        {
            return (T)(object)value;
        }

        if (underlyingType == typeof(int))
        {
            return (T)(object)int.Parse(value);
        }

        if (underlyingType == typeof(bool))
        {
            return (T)(object)bool.Parse(value);
        }

        if (underlyingType == typeof(decimal))
        {
            return (T)(object)decimal.Parse(value, CultureInfo.InvariantCulture);
        }

        if (underlyingType == typeof(double))
        {
            return (T)(object)double.Parse(value, CultureInfo.InvariantCulture);
        }

        // Fallback to Convert.ChangeType for other types
        return (T?)Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
    }
}
