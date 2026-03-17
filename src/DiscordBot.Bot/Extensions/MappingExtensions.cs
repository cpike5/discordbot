namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for batch async DTO mapping operations.
/// Replaces manual foreach mapping loops in services.
/// </summary>
public static class MappingExtensions
{
    /// <summary>
    /// Maps a collection of entities to DTOs asynchronously using the provided mapper delegate.
    /// </summary>
    /// <typeparam name="TEntity">The source entity type.</typeparam>
    /// <typeparam name="TDto">The target DTO type.</typeparam>
    /// <param name="entities">The entities to map.</param>
    /// <param name="mapper">An async delegate that maps a single entity to a DTO.</param>
    /// <param name="ct">Cancellation token passed to each mapper invocation.</param>
    /// <returns>A list of mapped DTOs in the same order as the input entities.</returns>
    public static async Task<List<TDto>> MapToDtosAsync<TEntity, TDto>(
        this IEnumerable<TEntity> entities,
        Func<TEntity, CancellationToken, Task<TDto>> mapper,
        CancellationToken ct = default)
    {
        var results = new List<TDto>();
        foreach (var entity in entities)
        {
            results.Add(await mapper(entity, ct));
        }
        return results;
    }
}
