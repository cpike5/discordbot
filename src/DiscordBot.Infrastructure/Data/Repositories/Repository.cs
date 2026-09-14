using System.Diagnostics;
using System.Linq.Expressions;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Tracing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Generic repository implementation providing basic CRUD operations.
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly BotDbContext Context;
    protected readonly DbSet<T> DbSet;
    protected readonly ILogger<Repository<T>> Logger;
    private readonly string _entityTypeName;
    private const int SlowOperationThresholdMs = 100;

    public Repository(BotDbContext context, ILogger<Repository<T>> logger)
    {
        Context = context;
        DbSet = context.Set<T>();
        Logger = logger;
        _entityTypeName = typeof(T).Name;
    }

    public virtual async Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        // Start tracing activity
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetByIdAsync",
            entityType: _entityTypeName,
            dbOperation: "SELECT",
            entityId: id?.ToString());

        var stopwatch = Stopwatch.StartNew();
        Logger.LogDebug("Repository<{EntityType}>.GetByIdAsync starting. Id={Id}", _entityTypeName, id);

        try
        {
            var result = await DbSet.FindAsync(new[] { id }, cancellationToken);
            stopwatch.Stop();

            Logger.LogDebug(
                "Repository<{EntityType}>.GetByIdAsync completed in {ElapsedMs}ms. Found={Found}",
                _entityTypeName, stopwatch.ElapsedMilliseconds, result != null);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                Logger.LogWarning(
                    "Repository<{EntityType}>.GetByIdAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, Id={Id}",
                    _entityTypeName, stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, id);
            }

            // Complete tracing activity with success
            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record exception on tracing activity
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            Logger.LogError(ex,
                "Repository<{EntityType}>.GetByIdAsync failed. Id={Id}, ElapsedMs={ElapsedMs}, Error={Error}",
                _entityTypeName, id, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Start tracing activity
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetAllAsync",
            entityType: _entityTypeName,
            dbOperation: "SELECT");

        var stopwatch = Stopwatch.StartNew();
        Logger.LogDebug("Repository<{EntityType}>.GetAllAsync starting", _entityTypeName);

        try
        {
            var result = await DbSet.ToListAsync(cancellationToken);
            stopwatch.Stop();

            Logger.LogDebug(
                "Repository<{EntityType}>.GetAllAsync completed in {ElapsedMs}ms. Count={Count}",
                _entityTypeName, stopwatch.ElapsedMilliseconds, result.Count);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                Logger.LogWarning(
                    "Repository<{EntityType}>.GetAllAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, Count={Count}",
                    _entityTypeName, stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, result.Count);
            }

            // Complete tracing activity with success
            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record exception on tracing activity
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            Logger.LogError(ex,
                "Repository<{EntityType}>.GetAllAsync failed. ElapsedMs={ElapsedMs}, Error={Error}",
                _entityTypeName, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public virtual async Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        // Start tracing activity
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "FindAsync",
            entityType: _entityTypeName,
            dbOperation: "SELECT");

        var stopwatch = Stopwatch.StartNew();
        Logger.LogDebug("Repository<{EntityType}>.FindAsync starting. Predicate={Predicate}", _entityTypeName, predicate);

        try
        {
            var result = await DbSet.Where(predicate).ToListAsync(cancellationToken);
            stopwatch.Stop();

            Logger.LogDebug(
                "Repository<{EntityType}>.FindAsync completed in {ElapsedMs}ms. Count={Count}",
                _entityTypeName, stopwatch.ElapsedMilliseconds, result.Count);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                Logger.LogWarning(
                    "Repository<{EntityType}>.FindAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, Count={Count}",
                    _entityTypeName, stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, result.Count);
            }

            // Complete tracing activity with success
            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record exception on tracing activity
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            Logger.LogError(ex,
                "Repository<{EntityType}>.FindAsync failed. ElapsedMs={ElapsedMs}, Error={Error}",
                _entityTypeName, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        var entityId = GetEntityId(entity);

        // Start tracing activity
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "AddAsync",
            entityType: _entityTypeName,
            dbOperation: "INSERT",
            entityId: entityId);

        var stopwatch = Stopwatch.StartNew();
        Logger.LogDebug("Repository<{EntityType}>.AddAsync starting. EntityId={EntityId}", _entityTypeName, entityId);

        try
        {
            await DbSet.AddAsync(entity, cancellationToken);
            await Context.SaveChangesAsync(cancellationToken);
            stopwatch.Stop();

            Logger.LogInformation(
                "Repository<{EntityType}>.AddAsync: Entity added successfully. EntityId={EntityId}, ElapsedMs={ElapsedMs}",
                _entityTypeName, entityId, stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                Logger.LogWarning(
                    "Repository<{EntityType}>.AddAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, EntityId={EntityId}",
                    _entityTypeName, stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, entityId);
            }

            // Complete tracing activity with success
            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return entity;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record exception on tracing activity
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            Logger.LogError(ex,
                "Repository<{EntityType}>.AddAsync failed. EntityId={EntityId}, ElapsedMs={ElapsedMs}, Error={Error}",
                _entityTypeName, entityId, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        var entityId = GetEntityId(entity);

        // Start tracing activity
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "UpdateAsync",
            entityType: _entityTypeName,
            dbOperation: "UPDATE",
            entityId: entityId);

        var stopwatch = Stopwatch.StartNew();
        Logger.LogDebug("Repository<{EntityType}>.UpdateAsync starting. EntityId={EntityId}", _entityTypeName, entityId);

        try
        {
            DbSet.Update(entity);
            await Context.SaveChangesAsync(cancellationToken);
            stopwatch.Stop();

            Logger.LogInformation(
                "Repository<{EntityType}>.UpdateAsync: Entity updated successfully. EntityId={EntityId}, ElapsedMs={ElapsedMs}",
                _entityTypeName, entityId, stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                Logger.LogWarning(
                    "Repository<{EntityType}>.UpdateAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, EntityId={EntityId}",
                    _entityTypeName, stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, entityId);
            }

            // Complete tracing activity with success
            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record exception on tracing activity
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            Logger.LogError(ex,
                "Repository<{EntityType}>.UpdateAsync failed. EntityId={EntityId}, ElapsedMs={ElapsedMs}, Error={Error}",
                _entityTypeName, entityId, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public virtual async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        var entityId = GetEntityId(entity);

        // Start tracing activity
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "DeleteAsync",
            entityType: _entityTypeName,
            dbOperation: "DELETE",
            entityId: entityId);

        var stopwatch = Stopwatch.StartNew();
        Logger.LogDebug("Repository<{EntityType}>.DeleteAsync starting. EntityId={EntityId}", _entityTypeName, entityId);

        try
        {
            DbSet.Remove(entity);
            await Context.SaveChangesAsync(cancellationToken);
            stopwatch.Stop();

            Logger.LogInformation(
                "Repository<{EntityType}>.DeleteAsync: Entity deleted successfully. EntityId={EntityId}, ElapsedMs={ElapsedMs}",
                _entityTypeName, entityId, stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                Logger.LogWarning(
                    "Repository<{EntityType}>.DeleteAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, EntityId={EntityId}",
                    _entityTypeName, stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, entityId);
            }

            // Complete tracing activity with success
            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record exception on tracing activity
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            Logger.LogError(ex,
                "Repository<{EntityType}>.DeleteAsync failed. EntityId={EntityId}, ElapsedMs={ElapsedMs}, Error={Error}",
                _entityTypeName, entityId, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public virtual async Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        // Start tracing activity
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "ExistsAsync",
            entityType: _entityTypeName,
            dbOperation: "EXISTS");

        var stopwatch = Stopwatch.StartNew();
        Logger.LogDebug("Repository<{EntityType}>.ExistsAsync starting. Predicate={Predicate}", _entityTypeName, predicate);

        try
        {
            var result = await DbSet.AnyAsync(predicate, cancellationToken);
            stopwatch.Stop();

            Logger.LogDebug(
                "Repository<{EntityType}>.ExistsAsync completed in {ElapsedMs}ms. Exists={Exists}",
                _entityTypeName, stopwatch.ElapsedMilliseconds, result);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                Logger.LogWarning(
                    "Repository<{EntityType}>.ExistsAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms",
                    _entityTypeName, stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs);
            }

            // Complete tracing activity with success
            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record exception on tracing activity
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            Logger.LogError(ex,
                "Repository<{EntityType}>.ExistsAsync failed. ElapsedMs={ElapsedMs}, Error={Error}",
                _entityTypeName, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public virtual async Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        // Start tracing activity
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "CountAsync",
            entityType: _entityTypeName,
            dbOperation: "COUNT");

        var stopwatch = Stopwatch.StartNew();
        Logger.LogDebug("Repository<{EntityType}>.CountAsync starting. HasPredicate={HasPredicate}",
            _entityTypeName, predicate != null);

        try
        {
            var result = predicate == null
                ? await DbSet.CountAsync(cancellationToken)
                : await DbSet.CountAsync(predicate, cancellationToken);
            stopwatch.Stop();

            Logger.LogDebug(
                "Repository<{EntityType}>.CountAsync completed in {ElapsedMs}ms. Count={Count}",
                _entityTypeName, stopwatch.ElapsedMilliseconds, result);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                Logger.LogWarning(
                    "Repository<{EntityType}>.CountAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, Count={Count}",
                    _entityTypeName, stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, result);
            }

            // Complete tracing activity with success
            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record exception on tracing activity
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            Logger.LogError(ex,
                "Repository<{EntityType}>.CountAsync failed. ElapsedMs={ElapsedMs}, Error={Error}",
                _entityTypeName, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Retrieves an entity by its primary key with eager-loaded navigation properties.
    /// Replaces repetitive GetByIdAsync overrides that only differ in their Include calls.
    /// </summary>
    /// <typeparam name="TKey">The type of the primary key.</typeparam>
    /// <param name="id">The primary key value.</param>
    /// <param name="includeBuilder">A function that applies Include/ThenInclude calls to the query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The entity with includes applied, or null if not found.</returns>
    protected async Task<T?> GetByIdWithIncludesAsync<TKey>(
        TKey id,
        Func<IQueryable<T>, IQueryable<T>> includeBuilder,
        CancellationToken ct = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetByIdWithIncludesAsync",
            entityType: _entityTypeName,
            dbOperation: "SELECT",
            entityId: id?.ToString());

        var stopwatch = Stopwatch.StartNew();
        Logger.LogDebug("Repository<{EntityType}>.GetByIdWithIncludesAsync starting. Id={Id}", _entityTypeName, id);

        try
        {
            var query = includeBuilder(DbSet.AsNoTracking());
            // Build a predicate that matches the primary key using EF Core's FindAsync-style lookup
            var entityType = Context.Model.FindEntityType(typeof(T));
            var primaryKey = entityType?.FindPrimaryKey();
            if (primaryKey == null || primaryKey.Properties.Count != 1)
            {
                Logger.LogWarning(
                    "Repository<{EntityType}>.GetByIdWithIncludesAsync: Cannot determine single primary key",
                    _entityTypeName);
                return null;
            }

            var keyProperty = primaryKey.Properties[0];
            var parameter = Expression.Parameter(typeof(T), "e");
            var keyAccess = Expression.Property(parameter, keyProperty.PropertyInfo!);
            var keyValue = Expression.Constant(id, typeof(TKey));
            var equals = Expression.Equal(keyAccess, keyValue);
            var predicate = Expression.Lambda<Func<T, bool>>(equals, parameter);

            var result = await query.FirstOrDefaultAsync(predicate, ct);
            stopwatch.Stop();

            Logger.LogDebug(
                "Repository<{EntityType}>.GetByIdWithIncludesAsync completed in {ElapsedMs}ms. Found={Found}",
                _entityTypeName, stopwatch.ElapsedMilliseconds, result != null);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                Logger.LogWarning(
                    "Repository<{EntityType}>.GetByIdWithIncludesAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, Id={Id}",
                    _entityTypeName, stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, id);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);
            Logger.LogError(ex,
                "Repository<{EntityType}>.GetByIdWithIncludesAsync failed. Id={Id}, ElapsedMs={ElapsedMs}, Error={Error}",
                _entityTypeName, id, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Executes a paginated query, returning the items for the requested page and the total count.
    /// The caller is responsible for applying filters, includes, and ordering to the query before calling this method.
    /// </summary>
    /// <param name="query">A pre-built query with filters and ordering already applied.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple containing the page of items and the total count across all pages.</returns>
    protected async Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(
        IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetPagedAsync",
            entityType: _entityTypeName,
            dbOperation: "SELECT");

        var stopwatch = Stopwatch.StartNew();
        Logger.LogDebug(
            "Repository<{EntityType}>.GetPagedAsync starting. Page={Page}, PageSize={PageSize}",
            _entityTypeName, page, pageSize);

        try
        {
            var totalCount = await query.CountAsync(ct);

            var skip = (page - 1) * pageSize;
            var items = await query
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            stopwatch.Stop();

            Logger.LogDebug(
                "Repository<{EntityType}>.GetPagedAsync completed in {ElapsedMs}ms. ItemCount={ItemCount}, TotalCount={TotalCount}",
                _entityTypeName, stopwatch.ElapsedMilliseconds, items.Count, totalCount);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                Logger.LogWarning(
                    "Repository<{EntityType}>.GetPagedAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, Page={Page}, TotalCount={TotalCount}",
                    _entityTypeName, stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, page, totalCount);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);
            return (items, totalCount);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);
            Logger.LogError(ex,
                "Repository<{EntityType}>.GetPagedAsync failed. Page={Page}, PageSize={PageSize}, ElapsedMs={ElapsedMs}, Error={Error}",
                _entityTypeName, page, pageSize, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Returns an existing entity matching the predicate, or creates and persists a new one using the factory.
    /// </summary>
    /// <param name="predicate">The condition to find an existing entity.</param>
    /// <param name="factory">A factory function that creates a new entity when none is found.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The existing or newly created entity.</returns>
    protected async Task<T> GetOrCreateAsync(
        Expression<Func<T, bool>> predicate,
        Func<T> factory,
        CancellationToken ct = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetOrCreateAsync",
            entityType: _entityTypeName,
            dbOperation: "UPSERT");

        var stopwatch = Stopwatch.StartNew();
        Logger.LogDebug("Repository<{EntityType}>.GetOrCreateAsync starting", _entityTypeName);

        try
        {
            var existing = await DbSet.FirstOrDefaultAsync(predicate, ct);
            if (existing != null)
            {
                stopwatch.Stop();
                Logger.LogDebug(
                    "Repository<{EntityType}>.GetOrCreateAsync found existing entity in {ElapsedMs}ms",
                    _entityTypeName, stopwatch.ElapsedMilliseconds);
                InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);
                return existing;
            }

            var newEntity = factory();
            await DbSet.AddAsync(newEntity, ct);
            await Context.SaveChangesAsync(ct);
            stopwatch.Stop();

            Logger.LogInformation(
                "Repository<{EntityType}>.GetOrCreateAsync created new entity in {ElapsedMs}ms",
                _entityTypeName, stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                Logger.LogWarning(
                    "Repository<{EntityType}>.GetOrCreateAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms",
                    _entityTypeName, stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);
            return newEntity;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);
            Logger.LogError(ex,
                "Repository<{EntityType}>.GetOrCreateAsync failed. ElapsedMs={ElapsedMs}, Error={Error}",
                _entityTypeName, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Attempts to extract the entity ID using reflection.
    /// Looks for common ID property names (Id, {EntityType}Id).
    /// Returns "Unknown" if no ID property is found.
    /// </summary>
    private string GetEntityId(T entity)
    {
        if (entity == null)
            return "null";

        var entityType = typeof(T);

        // Try common ID property names
        var idProperty = entityType.GetProperty("Id")
            ?? entityType.GetProperty($"{entityType.Name}Id");

        if (idProperty != null)
        {
            var idValue = idProperty.GetValue(entity);
            return idValue?.ToString() ?? "null";
        }

        return "Unknown";
    }
}
