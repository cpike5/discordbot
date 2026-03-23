using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for managing theme persistence.
/// </summary>
public class ThemeRepository : Repository<Theme>, IThemeRepository
{
    public ThemeRepository(BotDbContext context, ILogger<Repository<Theme>> logger)
        : base(context, logger)
    {
    }

    public async Task<Theme?> GetByKeyAsync(string themeKey, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ThemeKey == themeKey, cancellationToken);
    }

    public async Task<Theme?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Theme>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public override async Task UpdateAsync(Theme theme, CancellationToken cancellationToken = default)
    {
        var existing = await DbSet
            .FirstOrDefaultAsync(t => t.Id == theme.Id, cancellationToken);

        if (existing != null)
        {
            existing.ThemeKey = theme.ThemeKey;
            existing.DisplayName = theme.DisplayName;
            existing.Description = theme.Description;
            existing.ColorDefinition = theme.ColorDefinition;
            existing.IsActive = theme.IsActive;

            await Context.SaveChangesAsync(cancellationToken);
        }
    }
}
