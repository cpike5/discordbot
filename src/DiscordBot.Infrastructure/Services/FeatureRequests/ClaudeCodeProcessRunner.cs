using System.Diagnostics;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Infrastructure.Services.FeatureRequests;

/// <summary>
/// Runs the <c>claude</c> CLI binary in a subprocess and captures its output.
/// The binary path defaults to <c>"claude"</c> (expected on PATH) and can be overridden
/// via <c>FeatureRequests:DocGen:ClaudeCodeBinaryPath</c> in configuration.
/// </summary>
public class ClaudeCodeProcessRunner : IClaudeCodeProcessRunner
{
    private readonly string _binaryPath;
    private readonly ILogger<ClaudeCodeProcessRunner> _logger;

    public ClaudeCodeProcessRunner(IOptions<FeatureRequestsOptions> options, ILogger<ClaudeCodeProcessRunner> logger)
    {
        _logger = logger;
        _binaryPath = options.Value.DocGen.ClaudeCodeBinaryPath;
    }

    /// <inheritdoc/>
    public async Task<ProcessRunResult> RunAsync(string promptPath, string workingDirectory, CancellationToken ct)
    {
        _logger.LogDebug(
            "Starting claude process. Binary={Binary}, PromptPath={PromptPath}, WorkingDirectory={WorkingDirectory}",
            _binaryPath, promptPath, workingDirectory);

        var psi = new ProcessStartInfo
        {
            FileName = _binaryPath,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        // Use ArgumentList (not Arguments string) so paths are passed as discrete tokens,
        // preventing any argument-splitting or injection via special characters in the path.
        psi.ArgumentList.Add("--print");
        psi.ArgumentList.Add(promptPath);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start claude process at '{_binaryPath}'");

        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var output = await outputTask;
        var error = await errorTask;

        _logger.LogDebug(
            "claude process exited with code {ExitCode}. OutputLength={OutputLength}, ErrorLength={ErrorLength}",
            process.ExitCode, output.Length, error.Length);

        return new ProcessRunResult(process.ExitCode, output, error);
    }
}
