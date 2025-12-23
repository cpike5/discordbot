using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for managing application settings persistence.
/// </summary>
public class SettingsRepository : ISettingsRepository
{
    private readonly BotDbContext _context;

    public SettingsRepository(BotDbContext context)
    {
        _context = context;
    }

    public async Task<ApplicationSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _context.ApplicationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationSetting>> GetByCategoryAsync(
        SettingCategory category,
        CancellationToken cancellationToken = default)
    {
        return await _context.ApplicationSettings
            .AsNoTracking()
            .Where(s => s.Category == category)
            .OrderBy(s => s.Key)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationSetting>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ApplicationSettings
            .AsNoTracking()
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertAsync(ApplicationSetting setting, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ApplicationSettings
            .FirstOrDefaultAsync(s => s.Key == setting.Key, cancellationToken);

        if (existing != null)
        {
            // Update existing setting
            existing.Value = setting.Value;
            existing.Category = setting.Category;
            existing.DataType = setting.DataType;
            existing.RequiresRestart = setting.RequiresRestart;
            existing.LastModifiedAt = setting.LastModifiedAt;
            existing.LastModifiedBy = setting.LastModifiedBy;

            _context.ApplicationSettings.Update(existing);
        }
        else
        {
            // Insert new setting
            await _context.ApplicationSettings.AddAsync(setting, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var setting = await _context.ApplicationSettings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting != null)
        {
            _context.ApplicationSettings.Remove(setting);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteByCategoryAsync(SettingCategory category, CancellationToken cancellationToken = default)
    {
        var settings = await _context.ApplicationSettings
            .Where(s => s.Category == category)
            .ToListAsync(cancellationToken);

        if (settings.Any())
        {
            _context.ApplicationSettings.RemoveRange(settings);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
