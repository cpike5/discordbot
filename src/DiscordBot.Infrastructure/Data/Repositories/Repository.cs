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
