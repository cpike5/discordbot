using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services.LLM.Implementations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Infrastructure.Services.LLM.Providers;

/// <summary>
/// DM tool provider for running Claude Code CLI sessions.
/// Spawns a Claude Code process, captures JSON output, and enforces timeout.
/// Disabled by default via <see cref="MogwaiOptions.Enabled"/>.
/// </summary>
public class ClaudeCodeToolProvider : IDmToolProvider
{
    private readonly ILogger<ClaudeCodeToolProvider> _logger;
    private readonly IOptions<MogwaiOptions> _options;
    private readonly IBotOwnerResolver _ownerResolver;

    private static readonly ConcurrentDictionary<ulong, ClaudeCodeSession> _sessions = new();
    private static readonly ConcurrentDictionary<ulong, bool> _activeExecutions = new();

    /// <inheritdoc />
    public string Name => "ClaudeCode";

    /// <inheritdoc />
    public string Description => "Run Claude Code CLI sessions for coding tasks";

    public ClaudeCodeToolProvider(
        ILogger<ClaudeCodeToolProvider> logger,
        IOptions<MogwaiOptions> options,
        IBotOwnerResolver ownerResolver)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ownerResolver = ownerResolver ?? throw new ArgumentNullException(nameof(ownerResolver));
    }

    /// <inheritdoc />
    public IEnumerable<LlmToolDefinition> GetTools()
    {
        if (!_options.Value.Enabled)
            return Enumerable.Empty<LlmToolDefinition>();

        return ClaudeCodeTools.GetAllTools();
    }

    /// <inheritdoc />
    public async Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Value.Enabled)
        {
            return ToolExecutionResult.CreateError("Claude Code integration is disabled.");
        }

        // Defense in depth: this tool spawns a Claude Code process with file-system and shell
        // access, so it must independently verify the caller is the bot owner rather than trust
        // the upstream DM gate. Fail closed if ownership cannot be confirmed.
        if (!await IsOwnerAsync(context.UserId))
        {
            _logger.LogWarning(
                "Rejected Claude Code tool '{Tool}' for non-owner user {UserId}", toolName, context.UserId);
            return ToolExecutionResult.CreateError("Claude Code integration is restricted to the bot owner.");
        }

        return toolName switch
        {
            ClaudeCodeTools.RunClaudeCode => await ExecuteRunClaudeCodeAsync(input, context, cancellationToken),
            ClaudeCodeTools.GetClaudeCodeStatus => ExecuteGetStatus(context),
            _ => throw new NotSupportedException($"Tool '{toolName}' is not supported by this provider")
        };
    }

    private ToolExecutionResult ExecuteGetStatus(ToolContext context)
    {
        var userId = context.UserId;
        var isActive = _activeExecutions.ContainsKey(userId);
        var hasSession = _sessions.TryGetValue(userId, out var session);

        var result = new Dictionary<string, object?>
        {
            ["has_session"] = hasSession,
            ["is_running"] = isActive,
            ["session_id"] = session?.SessionId,
            ["cumulative_cost_usd"] = session?.CumulativeCost,
            ["last_used_utc"] = session?.LastUsed.ToString("O")
        };

        var json = JsonSerializer.Serialize(result);
        var element = JsonDocument.Parse(json).RootElement.Clone();
        return ToolExecutionResult.CreateSuccess(element);
    }

    private async Task<ToolExecutionResult> ExecuteRunClaudeCodeAsync(
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        var userId = context.UserId;

        // Extract parameters
        if (!input.TryGetProperty("prompt", out var promptElement))
        {
            return ToolExecutionResult.CreateError("Missing required parameter: prompt");
        }

        var prompt = promptElement.GetString();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return ToolExecutionResult.CreateError("Parameter prompt cannot be empty");
        }

        var continueSession = true;
        if (input.TryGetProperty("continue_session", out var continueElement))
        {
            continueSession = continueElement.GetBoolean();
        }

        string? workingDirectory = null;
        if (input.TryGetProperty("working_directory", out var wdElement))
        {
            workingDirectory = wdElement.GetString();
        }

        // Budget guard: each Claude Code run can incur real API cost, so refuse once a resumed
        // session's cumulative spend has reached the configured ceiling. The CLI has no reliable
        // way to enforce this for us, so we gate on the cost already accounted for per session.
        // A fresh session (continue_session=false) starts a new budget and is never blocked here.
        var maxBudget = _options.Value.MaxBudgetUsd;
        if (continueSession && _sessions.TryGetValue(userId, out var trackedSession)
            && IsBudgetExhausted(trackedSession.CumulativeCost, maxBudget))
        {
            _logger.LogWarning(
                "Rejected Claude Code run for user {UserId}: cumulative cost ${Cost} reached budget ${Budget}",
                userId, trackedSession.CumulativeCost, maxBudget);
            return ToolExecutionResult.CreateError(
                $"Claude Code budget of ${maxBudget:F2} has been reached for this session " +
                $"(spent ${trackedSession.CumulativeCost:F2}). Start a new session (continue_session=false) to reset the budget.");
        }

        // Concurrency guard
        if (!_activeExecutions.TryAdd(userId, true))
        {
            return ToolExecutionResult.CreateError(
                "A Claude Code session is already running. Wait for it to complete or check status with get_claude_code_status.");
        }

        try
        {
            return await SpawnClaudeProcessAsync(
                prompt, continueSession, workingDirectory, userId, cancellationToken);
        }
        finally
        {
            _activeExecutions.TryRemove(userId, out _);
        }
    }

    private async Task<ToolExecutionResult> SpawnClaudeProcessAsync(
        string prompt,
        bool continueSession,
        string? workingDirectory,
        ulong userId,
        CancellationToken cancellationToken)
    {
        var opts = _options.Value;

        var psi = new ProcessStartInfo
        {
            FileName = opts.ClaudeCliPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            WorkingDirectory = workingDirectory ?? opts.WorkingDirectory
        };

        // Build arguments: --print (read prompt from stdin), --output-format json, --verbose
        psi.ArgumentList.Add("--print");
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("json");
        psi.ArgumentList.Add("--verbose");

        // --allowedTools
        psi.ArgumentList.Add("--allowedTools");
        psi.ArgumentList.Add(opts.AllowedTools);

        // --max-turns
        psi.ArgumentList.Add("--max-turns");
        psi.ArgumentList.Add(opts.MaxTurns.ToString());

        // --bare
        if (opts.UseBareMode)
        {
            psi.ArgumentList.Add("--bare");
        }

        // --dangerously-skip-permissions
        if (opts.SkipPermissions)
        {
            psi.ArgumentList.Add("--dangerously-skip-permissions");
        }

        // --append-system-prompt
        if (!string.IsNullOrEmpty(opts.AppendSystemPrompt))
        {
            psi.ArgumentList.Add("--append-system-prompt");
            psi.ArgumentList.Add(opts.AppendSystemPrompt);
        }

        // --resume with session ID
        if (continueSession && _sessions.TryGetValue(userId, out var existingSession))
        {
            psi.ArgumentList.Add("--resume");
            psi.ArgumentList.Add(existingSession.SessionId);
        }

        using var process = new Process { StartInfo = psi };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) stderr.AppendLine(e.Data);
        };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            _logger.LogWarning(ex, "Claude CLI not found at {Path}", opts.ClaudeCliPath);
            return ToolExecutionResult.CreateError(
                $"Claude CLI not found at '{opts.ClaudeCliPath}'. Ensure it is installed and in PATH.");
        }

        // Pipe prompt via stdin
        await process.StandardInput.WriteAsync(prompt);
        process.StandardInput.Close();

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Timeout handling
        var timeoutMs = opts.TimeoutSeconds * 1000;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeoutMs);

        bool timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            KillProcessTree(process);
        }

        var stdoutStr = stdout.ToString();
        var stderrStr = stderr.ToString();

        _logger.LogDebug(
            "Claude Code execution completed. ExitCode={ExitCode}, Stdout={StdoutLen}chars, Stderr={StderrLen}chars, TimedOut={TimedOut}",
            timedOut ? -1 : process.ExitCode,
            stdoutStr.Length,
            stderrStr.Length,
            timedOut);

        if (timedOut)
        {
            return ToolExecutionResult.CreateError(
                $"Claude Code timed out after {opts.TimeoutSeconds} seconds. The process was killed.");
        }

        // Non-zero exit code
        if (process.ExitCode != 0)
        {
            var truncatedStderr = TruncateOutput(stderrStr, opts.MaxOutputLength);
            _logger.LogWarning(
                "Claude Code exited with non-zero code {ExitCode}. Stderr: {Stderr}",
                process.ExitCode,
                truncatedStderr);

            var errorResult = new Dictionary<string, object?>
            {
                ["error"] = true,
                ["exit_code"] = process.ExitCode,
                ["stderr"] = truncatedStderr
            };
            var errorJson = JsonSerializer.Serialize(errorResult);
            var errorElement = JsonDocument.Parse(errorJson).RootElement.Clone();
            return ToolExecutionResult.CreateError(errorJson);
        }

        // Parse JSON output
        return ParseClaudeOutput(stdoutStr, userId, opts.MaxOutputLength, continueSession);
    }

    private ToolExecutionResult ParseClaudeOutput(string rawOutput, ulong userId, int maxOutputLength, bool continueSession)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawOutput);
            var root = doc.RootElement;

            string? resultText = null;
            string? sessionId = null;
            decimal totalCost = 0;
            int totalDurationMs = 0;

            // The --output-format json --verbose output is a JSON array of conversation messages
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var message in root.EnumerateArray())
                {
                    // Sum cost across all messages
                    if (message.TryGetProperty("cost_usd", out var costProp))
                    {
                        totalCost += costProp.GetDecimal();
                    }

                    // Sum duration
                    if (message.TryGetProperty("duration_ms", out var durationProp))
                    {
                        totalDurationMs += durationProp.GetInt32();
                    }

                    // Get session_id if present
                    if (message.TryGetProperty("session_id", out var sidProp))
                    {
                        sessionId = sidProp.GetString();
                    }

                    // Extract text from the last assistant message
                    if (message.TryGetProperty("type", out var typeProp) &&
                        typeProp.GetString() == "assistant" &&
                        message.TryGetProperty("message", out var msgProp) &&
                        msgProp.TryGetProperty("content", out var contentProp) &&
                        contentProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var block in contentProp.EnumerateArray())
                        {
                            if (block.TryGetProperty("type", out var blockType) &&
                                blockType.GetString() == "text" &&
                                block.TryGetProperty("text", out var textProp))
                            {
                                resultText = textProp.GetString();
                            }
                        }
                    }
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                // Fallback: single object response
                if (root.TryGetProperty("result", out var resultProp))
                {
                    resultText = resultProp.GetString();
                }

                if (root.TryGetProperty("session_id", out var sidProp))
                {
                    sessionId = sidProp.GetString();
                }

                if (root.TryGetProperty("total_cost_usd", out var costProp))
                {
                    totalCost = costProp.GetDecimal();
                }

                if (root.TryGetProperty("is_error", out var errorProp) && errorProp.GetBoolean())
                {
                    return ToolExecutionResult.CreateError(resultText ?? "Claude Code returned an error.");
                }
            }

            // Update session tracking. Resuming accumulates cost against the running budget;
            // a fresh session (continue_session=false) resets the cumulative total so its budget
            // starts clean.
            if (!string.IsNullOrEmpty(sessionId))
            {
                _sessions.AddOrUpdate(
                    userId,
                    _ => new ClaudeCodeSession(sessionId, totalCost, DateTime.UtcNow),
                    (_, existing) => new ClaudeCodeSession(
                        sessionId,
                        continueSession ? existing.CumulativeCost + totalCost : totalCost,
                        DateTime.UtcNow));
            }

            // Truncate result if needed
            resultText = TruncateOutput(resultText ?? string.Empty, maxOutputLength);

            var result = new Dictionary<string, object?>
            {
                ["result"] = resultText,
                ["session_id"] = sessionId,
                ["cost_usd"] = totalCost,
                ["duration_ms"] = totalDurationMs
            };

            var json = JsonSerializer.Serialize(result);
            var element = JsonDocument.Parse(json).RootElement.Clone();
            return ToolExecutionResult.CreateSuccess(element);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Claude Code JSON output, returning raw output");

            // Fallback: return raw output as-is
            var truncated = TruncateOutput(rawOutput, maxOutputLength);
            var fallback = new Dictionary<string, object?>
            {
                ["result"] = truncated,
                ["parse_error"] = true
            };

            var json = JsonSerializer.Serialize(fallback);
            var element = JsonDocument.Parse(json).RootElement.Clone();
            return ToolExecutionResult.CreateSuccess(element);
        }
    }

    private async Task<bool> IsOwnerAsync(ulong userId)
    {
        try
        {
            var ownerId = await _ownerResolver.GetOwnerIdAsync();
            return ownerId != 0 && userId == ownerId;
        }
        catch (Exception ex)
        {
            // Fail closed: if ownership cannot be confirmed, deny access to the process-spawning tool.
            _logger.LogError(ex, "Failed to resolve bot owner while authorizing Claude Code access");
            return false;
        }
    }

    /// <summary>
    /// Determines whether a session's cumulative spend has reached the configured budget ceiling.
    /// A non-positive ceiling disables the limit (treated as unlimited).
    /// </summary>
    internal static bool IsBudgetExhausted(decimal cumulativeCost, decimal maxBudgetUsd)
    {
        return maxBudgetUsd > 0m && cumulativeCost >= maxBudgetUsd;
    }

    private static string TruncateOutput(string output, int maxLength)
    {
        if (output.Length <= maxLength)
            return output;

        return output[..maxLength] + "... (output truncated)";
    }

    private void KillProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to kill process tree for PID {Pid}", process.Id);
        }
    }

    /// <summary>
    /// In-memory session state for a Claude Code session.
    /// </summary>
    private sealed record ClaudeCodeSession(
        string SessionId,
        decimal CumulativeCost,
        DateTime LastUsed);
}
