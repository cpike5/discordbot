using DiscordBot.Agents;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Configuration.Assistant;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Models.Llm;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// The house rules for a skill file, asserted over the files that actually ship.
/// </summary>
/// <remarks>
/// <para>
/// A skill is the one part of the tooling that is authored in markdown and validated at runtime, so
/// a mistake in one is discovered by a model quietly not being offered something. The narrowing that
/// causes that is deliberate — a name the surface does not advertise is dropped with a <c>Debug</c>
/// line, because on the guild surface an allow-list excluding a tool is routine — which means a typo
/// and a correct exclusion look identical in production. Here they do not: a name that no tool
/// implements, or that belongs to the other surface, is a red build.
/// </para>
/// <para>
/// The files are read from the repository rather than from a fixture. They ship in the image
/// (<c>docs/agents/</c> is copied by the Dockerfile), so they are production content and this is a
/// test of production content.
/// </para>
/// </remarks>
public class SkillContractTests
{
    /// <summary>The two surfaces, as the options classes default them.</summary>
    public static TheoryData<string, ToolScopes> Surfaces() => new()
    {
        { new DmAssistantOptions().SkillsPath, ToolScopes.Dm },
        { new AssistantToolOptions().SkillsPath, ToolScopes.Guild }
    };

    [Fact]
    public void TheSkillFilesAreFound()
    {
        // A guard on the guards. Every theory below passes vacuously on an empty directory, which is
        // the right answer for `guild/` and would silently hide a broken repository-root walk.
        SkillFiles(new DmAssistantOptions().SkillsPath).Should().NotBeEmpty(
            "the DM surface ships skill files, so finding none means the files were not located");
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void EverySkillFileParses(string directory, ToolScopes scope)
    {
        _ = scope;

        foreach (var file in SkillFiles(directory))
        {
            SkillFile.TryParse(File.ReadAllText(file), Path.GetFileNameWithoutExtension(file), file, out _, out var error)
                .Should().BeTrue($"{Path.GetFileName(file)} must be a usable skill, but {error}");
        }
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void EverySkillKeyIsUniqueWithinItsSurface(string directory, ToolScopes scope)
    {
        _ = scope;

        // Keys are what the model passes to load_skill and the loader's lookup is first-match, so two
        // files claiming one key means one of them is unreachable.
        var keys = Skills(directory).Select(s => s.Key).ToList();

        keys.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void EveryToolASkillNamesIsImplemented(string directory, ToolScopes scope)
    {
        _ = scope;

        var implemented = RegisteredAgentTools.All
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var skill in Skills(directory))
        {
            foreach (var tool in skill.Tools)
            {
                implemented.Should().Contain(tool,
                    $"skill '{skill.Key}' names {tool}, which no tool implements - the name is "
                    + "dropped silently at runtime, so the skill would advertise less than it says");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void EveryToolASkillNamesResolvesOnItsOwnSurface(string directory, ToolScopes scope)
    {
        // The one that matters. A skill in dm/ naming a guild-scoped tool parses, loads, and unlocks
        // nothing: the catalogue routes that tool to the other assistant, so this surface's registry
        // never held it. Nothing at runtime says so.
        foreach (var skill in Skills(directory))
        {
            foreach (var tool in skill.Tools)
            {
                var entry = ToolCatalog.Describe(tool);

                entry.Scopes.HasFlag(scope).Should().BeTrue(
                    $"skill '{skill.Key}' is in {directory} but names {tool}, which the catalogue "
                    + $"advertises on {entry.Scopes} - it can never be unlocked here");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void NoSkillNamesTheLoaderItself(string directory, ToolScopes scope)
    {
        _ = scope;

        // Hiding load_skill behind a skill would mean nothing could ever load one.
        foreach (var skill in Skills(directory))
        {
            skill.Tools.Should().NotContain(
                t => string.Equals(t, SkillSession.DefaultLoaderToolName, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static IEnumerable<AgentSkill> Skills(string directory)
    {
        foreach (var file in SkillFiles(directory))
        {
            if (SkillFile.TryParse(
                    File.ReadAllText(file), Path.GetFileNameWithoutExtension(file), file, out var skill, out _))
            {
                yield return skill!;
            }
        }
    }

    /// <summary>
    /// The skill files of one surface. An empty directory is legitimate — <c>docs/agents/skills/guild/</c>
    /// ships empty on purpose — so these theories pass vacuously there, which is the correct answer.
    /// </summary>
    private static IEnumerable<string> SkillFiles(string directory)
    {
        var path = Path.Combine(RepositoryRoot(), directory);

        return Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*.md").OrderBy(f => f, StringComparer.Ordinal)
            : Enumerable.Empty<string>();
    }

    /// <summary>
    /// The repository root, found by walking up from the test binaries until the solution file
    /// appears. Tests run from <c>bin/</c>, and the skill files are content rather than output.
    /// </summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DiscordBot.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests must be able to find the repository they are testing");

        return directory!.FullName;
    }
}
