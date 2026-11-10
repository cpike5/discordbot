using cpike.Agent;
using cpike.Agent.Prompting;
using DiscordBot.Infrastructure.Services.LLM;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DiscordBot.Tests.TestHelpers;

/// <summary>
/// The assistant's shipped prompt and skill files (<c>docs/agents/prompts/</c>), loaded through the
/// framework's own markdown stores, so a test reads exactly what a deployment reads.
/// </summary>
public static class PromptFiles
{
    /// <summary>The repository root: the directory holding <c>DiscordBot.sln</c>.</summary>
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>The prompt directory, absolute.</summary>
    public static string PromptsDirectory { get; } = Path.Combine(RepositoryRoot, "docs", "agents", "prompts");

    /// <summary>The skill directory, absolute.</summary>
    public static string SkillsDirectory { get; } = Path.Combine(PromptsDirectory, "skills");

    /// <summary>Prompt options pointing at the shipped files, without a file watcher.</summary>
    public static IOptions<PromptOptions> Options() => Microsoft.Extensions.Options.Options.Create(new PromptOptions
    {
        Directory = PromptsDirectory,
        SkillsDirectory = SkillsDirectory,
        DefaultAgent = "bot",
        WatchForChanges = false,
        ContentRoot = RepositoryRoot
    });

    /// <summary>The framework's skill store over the shipped skills.</summary>
    public static MarkdownSkillStore SkillStore() =>
        new(Options(), null, NullLogger<MarkdownSkillStore>.Instance);

    /// <summary>The house prompt store over the shipped prompts, as production composes it.</summary>
    public static CatalogPromptStore CatalogStore(IAgentSkillStore? skills = null, string? baseUrl = "https://bot.example.com") =>
        new(
            new MarkdownAgentPromptStore(Options(), null, NullLogger<MarkdownAgentPromptStore>.Instance),
            skills ?? SkillStore(),
            baseUrl);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DiscordBot.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException("Could not find DiscordBot.sln above the test binaries.");
    }
}
