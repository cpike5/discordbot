using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

public interface IDmAssistantNoteRepository : IRepository<DmAssistantNote>
{
    Task<DmAssistantNote?> GetByIdAsync(long id, ulong userId, CancellationToken ct = default);

    Task<IReadOnlyList<DmAssistantNote>> SearchAsync(
        string query, ulong userId, int limit, CancellationToken ct = default);

    Task<IReadOnlyList<DmAssistantNote>> ListAsync(
        ulong userId, string? tag, int limit, CancellationToken ct = default);

    Task<bool> DeleteAsync(long id, ulong userId, CancellationToken ct = default);
}
