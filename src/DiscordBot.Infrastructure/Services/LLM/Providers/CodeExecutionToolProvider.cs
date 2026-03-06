using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services.LLM.Implementations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Infrastructure.Services.LLM.Providers;

/// <summary>
/// DM tool provider for executing Python code.
/// Spawns a Python process, captures output, and enforces timeout.
/// Disabled by default via <see cref="DmAssistantOptions.EnableCodeExecution"/>.
/// </summary>
public class CodeExecutionToolProvider : IDmToolProvider
{
    private readonly ILogger<CodeExecutionToolProvider> _logger;
    private readonly IOptions<DmAssistantOptions> _options;

    /// <inheritdoc />
    public string Name => "CodeExecution";

    /// <inheritdoc />
    public string Description => "Execute Python code for calculations and scripting";

    public CodeExecutionToolProvider(
        ILogger<CodeExecutionToolProvider> logger,
        IOptions<DmAssistantOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public IEnumerable<LlmToolDefinition> GetTools()
    {
        if (!_options.Value.EnableCodeExecution)
            return Enumerable.Empty<LlmToolDefinition>();

        return CodeExecutionTools.GetAllTools();
    }

    /// <inheritdoc />
    public async Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Value.EnableCodeExecution)
        {
            return ToolExecutionResult.CreateError("Code execution is disabled.");
        }

        if (!string.Equals(toolName, CodeExecutionTools.ExecutePython, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Tool '{toolName}' is not supported by this provider");
        }

        if (!input.TryGetProperty("code", out var codeElement))
        {
            return ToolExecutionResult.CreateError("Missing required parameter: code");
        }

        var code = codeElement.GetString();
        if (string.IsNullOrWhiteSpace(code))
        {
            return ToolExecutionResult.CreateError("Parameter code cannot be empty");
        }

        return await ExecutePythonAsync(code, cancellationToken);
    }

    private async Task<ToolExecutionResult> ExecutePythonAsync(string code, CancellationToken cancellationToken)
    {
        var opts = _options.Value;
        var tempFile = Path.Combine(Path.GetTempPath(), $"dmbot_{Guid.NewGuid():N}.py");

        try
        {
            await File.WriteAllTextAsync(tempFile, code, cancellationToken);

            var psi = new ProcessStartInfo
            {
                FileName = opts.PythonPath,
                Arguments = tempFile,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

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
                _logger.LogWarning(ex, "Python interpreter not found at {Path}", opts.PythonPath);
                return ToolExecutionResult.CreateError(
                    $"Python interpreter not found at '{opts.PythonPath}'. Ensure Python is installed.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var timeoutMs = opts.CodeExecutionTimeoutSeconds * 1000;
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
            var maxLen = opts.CodeExecutionMaxOutputLength;

            bool truncated = false;
            if (stdoutStr.Length > maxLen)
            {
                stdoutStr = stdoutStr[..maxLen];
                truncated = true;
            }
            if (stderrStr.Length > maxLen)
            {
                stderrStr = stderrStr[..maxLen];
                truncated = true;
            }

            _logger.LogDebug(
                "Python execution completed. ExitCode={ExitCode}, Stdout={StdoutLen}chars, Stderr={StderrLen}chars, TimedOut={TimedOut}",
                timedOut ? -1 : process.ExitCode,
                stdoutStr.Length,
                stderrStr.Length,
                timedOut);

            var result = new Dictionary<string, object?>
            {
                ["stdout"] = stdoutStr,
                ["stderr"] = stderrStr,
                ["exit_code"] = timedOut ? -1 : process.ExitCode,
                ["timed_out"] = timedOut,
                ["truncated"] = truncated
            };

            var json = JsonSerializer.Serialize(result);
            var element = JsonDocument.Parse(json).RootElement.Clone();
            return ToolExecutionResult.CreateSuccess(element);
        }
        finally
        {
            try { File.Delete(tempFile); }
            catch { /* best-effort cleanup */ }
        }
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
}
