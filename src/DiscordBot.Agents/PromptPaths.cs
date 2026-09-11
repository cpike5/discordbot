namespace DiscordBot.Agents;

/// <summary>
/// Resolves a configured prompt or skill path against the two places a deployment might have put it.
/// </summary>
/// <remarks>
/// A path in configuration is normally relative (<c>docs/agents/assistant-agent.md</c>) and has to
/// work both from a published output directory and from a <c>dotnet run</c> in the repository root,
/// which are not the same directory. One rule, in one place, so a prompt file and a skill directory
/// are found by the same search.
/// </remarks>
internal static class PromptPaths
{
    /// <summary>The resolved path of a file, whether or not it exists.</summary>
    /// <param name="path">An absolute path, or one relative to the application or working directory.</param>
    internal static string ResolveFile(string path) => Resolve(path, File.Exists);

    /// <summary>The resolved path of a directory, whether or not it exists.</summary>
    /// <param name="path">An absolute path, or one relative to the application or working directory.</param>
    internal static string ResolveDirectory(string path) => Resolve(path, Directory.Exists);

    /// <summary>
    /// The application directory's candidate, unless only the working directory's exists.
    /// </summary>
    /// <remarks>
    /// Returning the application-directory candidate when neither exists is deliberate: it is the
    /// one a deployment is meant to use, so it is the one worth naming in the error.
    /// </remarks>
    private static string Resolve(string path, Func<string, bool> exists)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        var appPath = Path.Combine(AppContext.BaseDirectory, path);

        if (exists(appPath))
        {
            return appPath;
        }

        var workingPath = Path.Combine(Directory.GetCurrentDirectory(), path);

        return exists(workingPath) ? workingPath : appPath;
    }
}
