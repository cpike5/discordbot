using System.Data.Common;
using System.Diagnostics;
using System.Text;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Infrastructure.Data.Interceptors;

/// <summary>
/// EF Core interceptor that tracks query execution performance and logs slow queries.
/// </summary>
/// <remarks>
/// This interceptor monitors all database queries and logs:
/// <list type="bullet">
/// <item>Debug-level logs for all queries with execution time and sanitized parameters</item>
/// <item>Warning-level logs for slow queries exceeding the configured threshold</item>
/// <item>Error-level logs for failed queries with exception details</item>
/// <item>Metrics collection via IDatabaseMetricsCollector (optional)</item>
/// </list>
/// The interceptor is designed to never throw exceptions that could break query execution.
/// </remarks>
public class QueryPerformanceInterceptor : DbCommandInterceptor
{
    private readonly ILogger<QueryPerformanceInterceptor> _logger;
    private readonly DatabaseSettings _settings;
    private readonly IDatabaseMetricsCollector? _metricsCollector;
    private const int MaxParameterValueLength = 50;
    private const int MaxCommandTextLength = 1000;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPerformanceInterceptor"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="settings">The database settings configuration.</param>
    /// <param name="metricsCollector">Optional metrics collector for performance tracking.</param>
    public QueryPerformanceInterceptor(
        ILogger<QueryPerformanceInterceptor> logger,
        IOptions<DatabaseSettings> settings,
        IDatabaseMetricsCollector? metricsCollector = null)
    {
        _logger = logger;
        _settings = settings.Value;
        _metricsCollector = metricsCollector;
    }

    #region Async Query Execution

    /// <summary>
    /// Intercepts async reader query execution (SELECT queries).
    /// </summary>
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        LogQueryExecution(command, eventData, "ReaderExecutedAsync");
        return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    /// Intercepts async scalar query execution (queries returning single values).
    /// </summary>
    public override async ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        LogQueryExecution(command, eventData, "ScalarExecutedAsync");
        return await base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    /// Intercepts async non-query execution (INSERT, UPDATE, DELETE queries).
    /// </summary>
    public override async ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        LogQueryExecution(command, eventData, "NonQueryExecutedAsync");
        return await base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    #endregion

    #region Sync Query Execution

    /// <summary>
    /// Intercepts sync reader query execution (SELECT queries).
    /// </summary>
    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        LogQueryExecution(command, eventData, "ReaderExecuted");
        return base.ReaderExecuted(command, eventData, result);
    }

    /// <summary>
    /// Intercepts sync scalar query execution (queries returning single values).
    /// </summary>
    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        LogQueryExecution(command, eventData, "ScalarExecuted");
        return base.ScalarExecuted(command, eventData, result);
    }

    /// <summary>
    /// Intercepts sync non-query execution (INSERT, UPDATE, DELETE queries).
    /// </summary>
    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        LogQueryExecution(command, eventData, "NonQueryExecuted");
        return base.NonQueryExecuted(command, eventData, result);
    }

    #endregion

    #region Failed Query Execution

    /// <summary>
    /// Intercepts async command failures and logs error details.
    /// </summary>
    public override async Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        LogQueryFailure(command, eventData);
        await base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    /// <summary>
    /// Intercepts sync command failures and logs error details.
    /// </summary>
    public override void CommandFailed(
        DbCommand command,
        CommandErrorEventData eventData)
    {
        LogQueryFailure(command, eventData);
        base.CommandFailed(command, eventData);
    }

    #endregion

    #region Logging Methods

    /// <summary>
    /// Logs query execution details including performance metrics and sanitized parameters.
    /// </summary>
    private void LogQueryExecution(
        DbCommand command,
        CommandExecutedEventData eventData,
        string methodName)
    {
        try
        {
            var elapsedMs = eventData.Duration.TotalMilliseconds;
            var commandText = TruncateCommandText(command.CommandText);

            // Record metrics
            _metricsCollector?.RecordQuery(elapsedMs, command.CommandType.ToString());

            // Always log at Debug level with execution time
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var parameters = _settings.LogQueryParameters
                    ? SanitizeParameters(command)
                    : "(parameter logging disabled)";

                _logger.LogDebug(
                    "EF Query executed via {Method}. ElapsedMs={ElapsedMs:F2}, CommandType={CommandType}, SQL={CommandText}, Parameters={Parameters}",
                    methodName,
                    elapsedMs,
                    command.CommandType,
                    commandText,
                    parameters);
            }

            // Log warning for slow queries
            if (elapsedMs > _settings.SlowQueryThresholdMs)
            {
                var parameters = _settings.LogQueryParameters
                    ? SanitizeParameters(command)
                    : "(parameter logging disabled)";

                _logger.LogWarning(
                    "EF Slow query detected. ElapsedMs={ElapsedMs:F2}, Threshold={ThresholdMs}ms, CommandType={CommandType}, SQL={CommandText}, Parameters={Parameters}",
                    elapsedMs,
                    _settings.SlowQueryThresholdMs,
                    command.CommandType,
                    commandText,
                    parameters);

                // Record slow query details
                _metricsCollector?.RecordSlowQuery(
                    command.CommandText,
                    elapsedMs,
                    _settings.LogQueryParameters ? SanitizeParameters(command) : null);
            }
        }
        catch (Exception ex)
        {
            // Never allow logging to break query execution
            _logger.LogError(ex,
                "QueryPerformanceInterceptor.LogQueryExecution failed. Error={Error}",
                ex.Message);
        }
    }

    /// <summary>
    /// Logs query failure details with exception information.
    /// </summary>
    private void LogQueryFailure(
        DbCommand command,
        CommandErrorEventData eventData)
    {
        try
        {
            var elapsedMs = eventData.Duration.TotalMilliseconds;
            var commandText = TruncateCommandText(command.CommandText);
            var parameters = _settings.LogQueryParameters
                ? SanitizeParameters(command)
                : "(parameter logging disabled)";

            // Record query error
            _metricsCollector?.RecordQueryError(elapsedMs, eventData.Exception.Message);

            _logger.LogError(eventData.Exception,
                "EF Query failed. ElapsedMs={ElapsedMs:F2}, CommandType={CommandType}, SQL={CommandText}, Parameters={Parameters}, Error={Error}",
                elapsedMs,
                command.CommandType,
                commandText,
                parameters,
                eventData.Exception.Message);
        }
        catch (Exception ex)
        {
            // Never allow logging to break query execution
            _logger.LogError(ex,
                "QueryPerformanceInterceptor.LogQueryFailure failed. Error={Error}",
                ex.Message);
        }
    }

    /// <summary>
    /// Sanitizes command parameters by showing parameter names and types while masking sensitive values.
    /// </summary>
    /// <param name="command">The database command containing parameters.</param>
    /// <returns>A sanitized string representation of parameters.</returns>
    private static string SanitizeParameters(DbCommand command)
    {
        if (command.Parameters.Count == 0)
        {
            return "(none)";
        }

        var sb = new StringBuilder();
        sb.Append('[');

        for (int i = 0; i < command.Parameters.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            var param = command.Parameters[i];
            sb.Append(param.ParameterName);
            sb.Append('=');

            // Show type and masked value
            if (param.Value == null || param.Value == DBNull.Value)
            {
                sb.Append("NULL");
            }
            else
            {
                var value = param.Value.ToString() ?? string.Empty;
                var typeName = param.Value.GetType().Name;

                // Truncate long values and mask sensitive-looking data
                if (value.Length > MaxParameterValueLength)
                {
                    sb.Append($"{typeName}:'{value[..MaxParameterValueLength]}...' (truncated, length={value.Length})");
                }
                else if (IsSensitiveParameter(param.ParameterName))
                {
                    sb.Append($"{typeName}:'***REDACTED***'");
                }
                else
                {
                    sb.Append($"{typeName}:'{value}'");
                }
            }
        }

        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>
    /// Determines if a parameter name suggests it contains sensitive data.
    /// </summary>
    /// <param name="parameterName">The parameter name to check.</param>
    /// <returns>True if the parameter appears to contain sensitive data.</returns>
    private static bool IsSensitiveParameter(string parameterName)
    {
        var lowerName = parameterName.ToLowerInvariant();
        return lowerName.Contains("password") ||
               lowerName.Contains("token") ||
               lowerName.Contains("secret") ||
               lowerName.Contains("key") ||
               lowerName.Contains("credential");
    }

    /// <summary>
    /// Truncates long command text for logging purposes.
    /// </summary>
    /// <param name="commandText">The SQL command text.</param>
    /// <returns>Truncated command text if it exceeds the maximum length.</returns>
    private static string TruncateCommandText(string commandText)
    {
        if (string.IsNullOrEmpty(commandText))
        {
            return "(empty)";
        }

        // Normalize whitespace for cleaner logs
        var normalized = string.Join(" ", commandText.Split(new[] { ' ', '\r', '\n', '\t' },
            StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length > MaxCommandTextLength)
        {
            return $"{normalized[..MaxCommandTextLength]}... (truncated, length={normalized.Length})";
        }

        return normalized;
    }

    #endregion
}
