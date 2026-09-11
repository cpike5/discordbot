using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace DiscordBot.E2E;

/// <summary>
/// Collection fixture that boots the real <c>DiscordBot.Bot.dll</c> as a child process in
/// web-only mode (<c>Discord:Enabled=false</c>, see CLAUDE.md "Running it locally") against a
/// throwaway SQLite database in a temp directory, waits for <c>/health</c> to report success,
/// and tears the process and temp directory down afterwards.
///
/// Assumes the solution is already built - this fixture does not build anything. It resolves
/// <c>DiscordBot.Bot.dll</c> relative to the test assembly's own build configuration (Debug or
/// Release), so `dotnet build DiscordBot.sln` (Debug) and the CI `-c Release` build both work
/// without extra flags. Set <c>E2E_BOT_DLL</c> to override the resolved path entirely.
///
/// Does nothing when <c>E2E_ENABLED</c> is not "1" - <see cref="E2EFactAttribute"/> skips every
/// test in that case, but the fixture itself must stay inert too since xUnit constructs
/// collection fixtures even when every test in the collection is skipped.
/// </summary>
public sealed class BotHostFixture : IAsyncLifetime
{
    private const string SeededAdminEmailValue = "e2e-admin@example.test";
    private const string SeededAdminPasswordValue = "E2e-Test-Passw0rd!";

    private Process? _process;
    private StreamWriter? _logWriter;
    private string? _tempDir;

    /// <summary>Base URL of the running host, e.g. http://127.0.0.1:53214. Null until <see cref="InitializeAsync"/> has started the process.</summary>
    public string BaseUrl { get; private set; } = string.Empty;

    /// <summary>Email of the admin account seeded via Identity:DefaultAdmin.</summary>
    public string SeededAdminEmail => SeededAdminEmailValue;

    /// <summary>Password of the admin account seeded via Identity:DefaultAdmin.</summary>
    public string SeededAdminPassword => SeededAdminPasswordValue;

    /// <summary>Path to the file the child process's stdout/stderr were captured to.</summary>
    public string LogFilePath { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("E2E_ENABLED") != "1")
        {
            // Every test is Skip-marked by E2EFactAttribute in this mode; stay inert so a plain
            // `dotnet test DiscordBot.sln` never launches Chromium or a bot process.
            return;
        }

        _tempDir = Path.Combine(Path.GetTempPath(), $"discordbot-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        LogFilePath = Path.Combine(_tempDir, "host.log");

        var dllPath = ResolveBotDllPath();
        var port = GetFreeTcpPort();
        BaseUrl = $"http://127.0.0.1:{port}";

        var dbPath = Path.Combine(_tempDir, "e2e.db");
        var dataProtectionPath = Path.Combine(_tempDir, "dp-keys");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { dllPath },
            WorkingDirectory = Path.GetDirectoryName(dllPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["Discord__Enabled"] = "false";
        startInfo.Environment["ASPNETCORE_URLS"] = BaseUrl;
        startInfo.Environment["ConnectionStrings__DefaultConnection"] = $"Data Source={dbPath}";
        startInfo.Environment["Identity__DefaultAdmin__Email"] = SeededAdminEmailValue;
        startInfo.Environment["Identity__DefaultAdmin__Password"] = SeededAdminPasswordValue;
        startInfo.Environment["DataProtection__KeyPath"] = dataProtectionPath;
        // No token/keys are configured for Discord OAuth, OpenRouter or Azure Speech: those
        // features are optional and the host starts fine without them (see
        // docs/articles/configuration-guide.md "Optional Secrets").

        _logWriter = new StreamWriter(LogFilePath, append: false) { AutoFlush = true };

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) => WriteLogLine(e.Data);
        _process.ErrorDataReceived += (_, e) => WriteLogLine(e.Data);

        try
        {
            _process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to start '{dllPath}'. Build the solution first (dotnet build DiscordBot.sln " +
                $"-p:SkipTailwind=true) or set E2E_BOT_DLL to an existing DiscordBot.Bot.dll. See {LogFilePath}.",
                ex);
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        await WaitForHealthyAsync(TimeSpan.FromSeconds(60)).ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (_process is { HasExited: false })
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().ConfigureAwait(false);
            }
            catch
            {
                // Best-effort: the process may have already exited between the check and Kill.
            }
        }

        _process?.Dispose();
        _logWriter?.Dispose();

        if (_tempDir is not null && Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup: a file briefly locked by SQLite/Data Protection on
                // shutdown shouldn't fail the test run.
            }
        }
    }

    private void WriteLogLine(string? line)
    {
        if (line is null)
        {
            return;
        }

        try
        {
            _logWriter?.WriteLine(line);
        }
        catch
        {
            // Ignore: the writer may already be disposed if output arrives during teardown.
        }
    }

    private async Task WaitForHealthyAsync(TimeSpan timeout)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow + timeout;
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            if (_process is { HasExited: true })
            {
                throw new InvalidOperationException(
                    $"DiscordBot.Bot exited early (code {_process.ExitCode}) before /health became reachable. " +
                    $"See {LogFilePath}.");
            }

            try
            {
                using var response = await http.GetAsync($"{BaseUrl}/health").ConfigureAwait(false);
                // /health returns 200 for Healthy and Degraded (Discord disconnected is expected
                // and reported as Degraded in web-only mode; only Unhealthy is 503 - see
                // docs/articles/configuration-guide.md "Discord:Enabled (web-only mode)").
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"DiscordBot.Bot did not become healthy at {BaseUrl}/health within {timeout}. See {LogFilePath}.",
            lastError);
    }

    private static string ResolveBotDllPath()
    {
        var overridePath = Environment.GetEnvironmentVariable("E2E_BOT_DLL");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            if (!File.Exists(overridePath))
            {
                throw new FileNotFoundException(
                    $"E2E_BOT_DLL was set but no file exists at '{overridePath}'.", overridePath);
            }

            return overridePath;
        }

        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var configuration = FindBuildConfiguration(AppContext.BaseDirectory);

        var dllPath = Path.Combine(
            repoRoot, "src", "DiscordBot.Bot", "bin", configuration, "net10.0", "DiscordBot.Bot.dll");

        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException(
                $"DiscordBot.Bot.dll not found at '{dllPath}'. Build the solution first " +
                "(dotnet build DiscordBot.sln -p:SkipTailwind=true), or set E2E_BOT_DLL to an " +
                "existing build output.", dllPath);
        }

        return dllPath;
    }

    /// <summary>
    /// The test assembly's own output path is "...\bin\&lt;Configuration&gt;\net10.0\...", so the
    /// bot is built with the same configuration whenever the caller didn't pass -c explicitly to
    /// only one of the two `dotnet build`/`dotnet test` invocations.
    /// </summary>
    private static string FindBuildConfiguration(string testAssemblyDirectory)
    {
        var segments = testAssemblyDirectory
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(s => s.Length > 0)
            .ToArray();

        var binIndex = Array.LastIndexOf(segments, "bin");
        if (binIndex >= 0 && binIndex + 1 < segments.Length)
        {
            return segments[binIndex + 1];
        }

        // Fall back to Debug, dotnet's own default, if the layout is unrecognized.
        return "Debug";
    }

    private static string FindRepoRoot(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DiscordBot.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not find DiscordBot.sln above '{startDirectory}'. " +
            "Set E2E_BOT_DLL to point at DiscordBot.Bot.dll directly if the test assembly moved.");
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
