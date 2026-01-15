using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for managing theme persistence.
/// </summary>
public class ThemeRepository : IThemeRepository
{
    private readonly BotDbContext _context;

    public ThemeRepository(BotDbContext context)
    {
        _context = context;
    }

    public async Task<Theme?> GetByKeyAsync(string themeKey, CancellationToken cancellationToken = default)
    {
        return await _context.Themes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ThemeKey == themeKey, cancellationToken);
    }

    public async Task<Theme?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Themes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Theme>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Themes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(Theme theme, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Themes
            .FirstOrDefaultAsync(t => t.Id == theme.Id, cancellationToken);

        if (existing != null)
        {
            existing.ThemeKey = theme.ThemeKey;
            existing.DisplayName = theme.DisplayName;
            existing.Description = theme.Description;
            existing.ColorDefinition = theme.ColorDefinition;
            existing.IsActive = theme.IsActive;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
