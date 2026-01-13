using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for managing command module configuration persistence.
/// </summary>
public class CommandModuleConfigurationRepository : ICommandModuleConfigurationRepository
{
    private readonly BotDbContext _context;

    public CommandModuleConfigurationRepository(BotDbContext context)
    {
        _context = context;
    }

    public async Task<CommandModuleConfiguration?> GetByNameAsync(
        string moduleName,
        CancellationToken cancellationToken = default)
    {
        return await _context.CommandModuleConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ModuleName == moduleName, cancellationToken);
    }

    public async Task<IReadOnlyList<CommandModuleConfiguration>> GetByCategoryAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        return await _context.CommandModuleConfigurations
            .AsNoTracking()
            .Where(c => c.Category == category)
            .OrderBy(c => c.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommandModuleConfiguration>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.CommandModuleConfigurations
            .AsNoTracking()
            .OrderBy(c => c.Category)
            .ThenBy(c => c.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommandModuleConfiguration>> GetEnabledAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.CommandModuleConfigurations
            .AsNoTracking()
            .Where(c => c.IsEnabled)
            .OrderBy(c => c.Category)
            .ThenBy(c => c.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertAsync(
        CommandModuleConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.CommandModuleConfigurations
            .FirstOrDefaultAsync(c => c.ModuleName == configuration.ModuleName, cancellationToken);

        if (existing != null)
        {
            // Update existing configuration - entity is already tracked
            existing.IsEnabled = configuration.IsEnabled;
            existing.DisplayName = configuration.DisplayName;
            existing.Description = configuration.Description;
            existing.Category = configuration.Category;
            existing.RequiresRestart = configuration.RequiresRestart;
            existing.LastModifiedAt = configuration.LastModifiedAt;
            existing.LastModifiedBy = configuration.LastModifiedBy;
        }
        else
        {
            // Insert new configuration
            await _context.CommandModuleConfigurations.AddAsync(configuration, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertRangeAsync(
        IEnumerable<CommandModuleConfiguration> configurations,
        CancellationToken cancellationToken = default)
    {
        foreach (var configuration in configurations)
        {
            var existing = await _context.CommandModuleConfigurations
                .FirstOrDefaultAsync(c => c.ModuleName == configuration.ModuleName, cancellationToken);

            if (existing != null)
            {
                // Update existing configuration - entity is already tracked
                existing.IsEnabled = configuration.IsEnabled;
                existing.DisplayName = configuration.DisplayName;
                existing.Description = configuration.Description;
                existing.Category = configuration.Category;
                existing.RequiresRestart = configuration.RequiresRestart;
                existing.LastModifiedAt = configuration.LastModifiedAt;
                existing.LastModifiedBy = configuration.LastModifiedBy;
            }
            else
            {
                // Insert new configuration
                await _context.CommandModuleConfigurations.AddAsync(configuration, cancellationToken);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        var configuration = await _context.CommandModuleConfigurations
            .FirstOrDefaultAsync(c => c.ModuleName == moduleName, cancellationToken);

        if (configuration != null)
        {
            _context.CommandModuleConfigurations.Remove(configuration);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
