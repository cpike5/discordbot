using System.Linq.Expressions;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Generic repository interface providing basic CRUD operations.
/// </summary>
/// <typeparam name="T">Entity type.</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Gets an entity by its primary key.
    /// </summary>
    Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all entities.
    /// </summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds entities matching the specified predicate.
    /// </summary>
    Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity.
    /// </summary>
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity.
    /// </summary>
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity.
    /// </summary>
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any entity matches the predicate.
    /// </summary>
    Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of entities matching the predicate.
    /// </summary>
    Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default);
}
