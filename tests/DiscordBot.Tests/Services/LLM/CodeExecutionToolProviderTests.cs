using System.Text.Json;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Configuration;
using DiscordBot.Infrastructure.Services.LLM.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// The Python child process must not inherit the bot's environment, where the Docker deployment
/// keeps its secrets. Runs a real interpreter, so it returns early when <c>python3</c> is not on PATH.
/// </summary>
public class CodeExecutionToolProviderTests
{
    [Fact]
    public async Task ExecutePython_DoesNotSeeTheBotsEnvironmentVariables()
    {
        if (!PythonIsAvailable())
        {
            // No Skip support in this project; CI's ubuntu runner and the dev container have python3.
            return;
        }

        // Unique per run: the variable is process-wide and other test classes run in parallel.
        var name = $"DMBOT_LEAK_TEST_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(name, "planted-secret-value");
        try
        {
            var provider = new CodeExecutionToolProvider(
                NullLogger<CodeExecutionToolProvider>.Instance,
                Options.Create(new DmAssistantOptions { EnableCodeExecution = true }));
            var code = $"import os\nprint(os.environ.get('{name}', 'absent'))\nprint('PATH' in os.environ)";
            var input = JsonDocument.Parse(JsonSerializer.Serialize(new { code })).RootElement;

            var result = await provider.ExecuteToolAsync("execute_python", input, new ToolContext());

            result.Success.Should().BeTrue(result.ErrorMessage);
            var stdout = result.Data!.Value.GetProperty("stdout").GetString();
            stdout.Should().NotContain("planted-secret-value");
            stdout.Should().Contain("absent").And.Contain("True", "PATH is passed back so the code can still run tools");
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    private static bool PythonIsAvailable() =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(dir => File.Exists(Path.Combine(dir, "python3")));
}
