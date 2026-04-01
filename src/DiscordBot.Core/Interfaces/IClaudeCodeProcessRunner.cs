namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Wraps execution of the <c>claude</c> CLI binary for doc generation tasks.
/// </summary>
public interface IClaudeCodeProcessRunner
{
    Task<ProcessRunResult> RunAsync(string promptPath, string workingDirectory, CancellationToken ct);
}

/// <summary>
/// Captures the outcome of a <c>claude</c> CLI subprocess invocation.
/// </summary>
public record ProcessRunResult(int ExitCode, string Output, string Error);
