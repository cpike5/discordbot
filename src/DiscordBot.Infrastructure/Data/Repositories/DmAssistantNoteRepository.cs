using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

public class DmAssistantNoteRepository : Repository<DmAssistantNote>, IDmAssistantNoteRepository
{
    private readonly ILogger<DmAssistantNoteRepository> _logger;

    public DmAssistantNoteRepository(
        BotDbContext context,
        ILogger<DmAssistantNoteRepository> logger,
        ILogger<Repository<DmAssistantNote>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<DmAssistantNote?> GetByIdAsync(long id, ulong userId, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);
    }

    public async Task<IReadOnlyList<DmAssistantNote>> SearchAsync(
        string query, ulong userId, int limit, CancellationToken ct = default)
    {
        // Escape LIKE wildcards to prevent injection of % and _ characters
        var escaped = query
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
        var pattern = $"%{escaped}%";

        return await DbSet
            .AsNoTracking()
            .Where(n => n.UserId == userId &&
                (EF.Functions.Like(n.Content, pattern, "\\") ||
                 (n.Tag != null && EF.Functions.Like(n.Tag, pattern, "\\"))))
            .OrderByDescending(n => n.UpdatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DmAssistantNote>> ListAsync(
        ulong userId, string? tag, int limit, CancellationToken ct = default)
    {
        var query = DbSet
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (!string.IsNullOrEmpty(tag))
        {
            query = query.Where(n => n.Tag == tag);
        }

        return await query
            .OrderByDescending(n => n.UpdatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<bool> DeleteAsync(long id, ulong userId, CancellationToken ct = default)
    {
        var deleted = await DbSet
            .Where(n => n.Id == id && n.UserId == userId)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            _logger.LogDebug("Deleted note {NoteId} for user {UserId}", id, userId);
        }

        return deleted > 0;
    }
}
