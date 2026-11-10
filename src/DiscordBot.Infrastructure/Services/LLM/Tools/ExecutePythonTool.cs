using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using cpike.Agent;
using DiscordBot.Core.Configuration;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>execute_python</c> — spawns a Python process, captures output, and enforces a timeout.
/// Disabled by default via <see cref="DmAssistantOptions.EnableCodeExecution"/>.
/// </summary>
[cpike.Agent.Tools.NotDelegable]
public sealed class ExecutePythonTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "execute_python",
        "Executes Python code and returns stdout/stderr output. Use for calculations, data processing, text manipulation, or any task that benefits from scripting. The code runs in an isolated process with a timeout. Print results to stdout.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "code": {
                        "type": "string",
                        "description": "The Python code to execute. Has access to the standard library. Print output to stdout for results."
                    }
                },
                "required": ["code"]
            }
            """).RootElement.Clone());

    private readonly ILogger<ExecutePythonTool> _logger;
    private readonly IOptions<DmAssistantOptions> _options;

    /// <summary>Creates the tool.</summary>
    public ExecutePythonTool(
        ILogger<ExecutePythonTool> logger,
        IOptions<DmAssistantOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public AgentToolDefinition Definition => ToolDefinition;

    /// <inheritdoc />
    public async Task<string> InvokeAsync(
        IReadOnlyDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken)
    {
        var input = ToolInput.ToElement(arguments);

        if (!_options.Value.EnableCodeExecution)
        {
            return ToolResults.Error("Code execution is disabled.");
        }

        if (!input.TryGetProperty("code", out var codeElement))
        {
            return ToolResults.Error("Missing required parameter: code");
        }

        var code = codeElement.GetString();
        if (string.IsNullOrWhiteSpace(code))
        {
            return ToolResults.Error("Parameter code cannot be empty");
        }

        return await ExecutePythonAsync(code, cancellationToken);
    }

    private async Task<string> ExecutePythonAsync(string code, CancellationToken cancellationToken)
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
                return ToolResults.Error(
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

            return JsonSerializer.Serialize(result);
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
