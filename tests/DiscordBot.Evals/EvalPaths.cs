namespace DiscordBot.Evals;

/// <summary>
/// Where the repository's content lives, from inside a test binary.
/// </summary>
/// <remarks>
/// The prompt and skill files are repository content rather than build output, and the prompt-path resolution
/// the application uses (application directory, then working directory) does not find them from
/// <c>bin/</c>. Walking up to the solution file is the honest way to say "the files that ship".
/// </remarks>
public static class EvalPaths
{
    /// <summary>The assistant's prompt directory (<c>docs/agents/prompts</c>), absolute.</summary>
    public static string PromptsDirectory { get; } =
        Path.Combine(RepositoryRoot(), "docs", "agents", "prompts");

    /// <summary>The assistant's skill directory, absolute.</summary>
    public static string SkillsDirectory { get; } = Path.Combine(PromptsDirectory, "skills");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DiscordBot.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException(
                   "Could not find DiscordBot.sln above the test binaries, so the skill files that "
                   + "ship cannot be located.");
    }
}
