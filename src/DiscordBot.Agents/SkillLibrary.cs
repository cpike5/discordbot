using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Agents;

/// <summary>
/// Loads skills from a directory of markdown files, through <see cref="IPromptTemplate"/> so a skill
/// is cached and hot-reloaded exactly like the agent prompts beside it.
/// </summary>
public sealed class SkillLibrary : ISkillLibrary
{
    /// <summary>
    /// How long a directory's file list is cached. Matches <see cref="PromptTemplate"/>'s own file
    /// cache, so adding a skill file and editing one take effect on the same terms.
    /// </summary>
    private static readonly TimeSpan ListingCacheDuration = TimeSpan.FromMinutes(5);

    private readonly IPromptTemplate _prompts;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SkillLibrary> _logger;

    /// <summary>Creates the library.</summary>
    /// <param name="prompts">Used to read each file, for its cache and its path resolution.</param>
    /// <param name="cache">Caches the directory listing.</param>
    /// <param name="logger">Reports a file that is not a usable skill.</param>
    public SkillLibrary(IPromptTemplate prompts, IMemoryCache cache, ILogger<SkillLibrary> logger)
    {
        _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentSkill>> LoadAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return Array.Empty<AgentSkill>();
        }

        var files = ListFiles(directory);

        if (files.Count == 0)
        {
            return Array.Empty<AgentSkill>();
        }

        var skills = new List<AgentSkill>(files.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string content;
            try
            {
                content = await _prompts.LoadAsync(file, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One unreadable file must not take a surface's whole roster down with it.
                _logger.LogError(ex, "Could not read skill file {SkillFile}", file);
                continue;
            }

            var defaultKey = Path.GetFileNameWithoutExtension(file);

            if (!SkillFile.TryParse(content, defaultKey, file, out var skill, out var error))
            {
                _logger.LogWarning(
                    "Skill file {SkillFile} was ignored: {Reason}", file, error);
                continue;
            }

            if (!seen.Add(skill!.Key))
            {
                _logger.LogWarning(
                    "Skill key {SkillKey} is declared more than once in {Directory}; {SkillFile} was ignored",
                    skill.Key, directory, file);
                continue;
            }

            skills.Add(skill);
        }

        var ordered = skills.OrderBy(s => s.Key, StringComparer.Ordinal).ToList();

        _logger.LogDebug(
            "Loaded {SkillCount} skills from {Directory}: {SkillKeys}",
            ordered.Count, directory, string.Join(", ", ordered.Select(s => s.Key)));

        return ordered;
    }

    /// <summary>
    /// The <c>.md</c> files in <paramref name="directory"/>, sorted by path and cached briefly.
    /// </summary>
    /// <remarks>
    /// The listing is cached rather than the parsed skills: the parse is cheap once the file content
    /// is cached by <see cref="IPromptTemplate"/>, and caching only the listing means an edit to a
    /// skill's body is picked up on that cache's terms rather than on a second one layered over it.
    /// </remarks>
    private IReadOnlyList<string> ListFiles(string directory)
    {
        var cacheKey = $"skill_library:{directory}";

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<string>? cached) && cached is not null)
        {
            return cached;
        }

        var resolved = PromptPaths.ResolveDirectory(directory);

        IReadOnlyList<string> files;

        if (!Directory.Exists(resolved))
        {
            // Not an error: a surface with no skills is the normal state of one nobody has written
            // any for, and it costs nothing - the loader tool is not advertised either.
            _logger.LogDebug(
                "No skill directory at {Directory} (resolved to {ResolvedDirectory})", directory, resolved);
            files = Array.Empty<string>();
        }
        else
        {
            files = Directory
                .EnumerateFiles(resolved, "*.md", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
        }

        _cache.Set(cacheKey, files, new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(ListingCacheDuration)
            .SetSize(Math.Max(1, files.Count)));

        return files;
    }
}
