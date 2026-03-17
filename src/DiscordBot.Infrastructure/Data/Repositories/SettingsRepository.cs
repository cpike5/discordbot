using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for managing application settings persistence.
/// </summary>
public class SettingsRepository : Repository<ApplicationSetting>, ISettingsRepository
{
    public SettingsRepository(BotDbContext context, ILogger<Repository<ApplicationSetting>> logger)
        : base(context, logger)
    {
    }

    public async Task<ApplicationSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationSetting>> GetByCategoryAsync(
        SettingCategory category,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(s => s.Category == category)
            .OrderBy(s => s.Key)
            .ToListAsync(cancellationToken);
    }

    public override async Task<IReadOnlyList<ApplicationSetting>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertAsync(ApplicationSetting setting, CancellationToken cancellationToken = default)
    {
        var existing = await DbSet
            .FirstOrDefaultAsync(s => s.Key == setting.Key, cancellationToken);

        if (existing != null)
        {
            // Update existing setting - entity is already tracked, just modify properties
            existing.Value = setting.Value ?? string.Empty; // Guard against null
            existing.Category = setting.Category;
            existing.DataType = setting.DataType;
            existing.RequiresRestart = setting.RequiresRestart;
            existing.LastModifiedAt = setting.LastModifiedAt;
            existing.LastModifiedBy = setting.LastModifiedBy;
            // No need to call Update() - EF Core tracks changes automatically
        }
        else
        {
            // Insert new setting
            await DbSet.AddAsync(setting, cancellationToken);
        }

        await Context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var setting = await DbSet
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting != null)
        {
            DbSet.Remove(setting);
            await Context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteByCategoryAsync(SettingCategory category, CancellationToken cancellationToken = default)
    {
        var settings = await DbSet
            .Where(s => s.Category == category)
            .ToListAsync(cancellationToken);

        if (settings.Any())
        {
            DbSet.RemoveRange(settings);
            await Context.SaveChangesAsync(cancellationToken);
        }
    }
}
