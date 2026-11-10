using cpike.Agent.Prompting;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Models.Llm;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// The house rules for a skill file, asserted over the files that actually ship.
/// </summary>
/// <remarks>
/// <para>
/// A skill is the one part of the tooling that is authored in markdown and validated at runtime, so
/// a mistake in one is discovered by a model quietly not being offered something: the framework drops
/// a skill none of whose tools are registered, and drops a tool name nothing registers, without
/// failing the run. Here a typo, or a tool that belongs to the other surface, is a red build.
/// </para>
/// <para>
/// The files are read from <c>docs/agents/prompts/skills/</c> in the repository, parsed with the
/// framework's own <see cref="PromptFileParser"/>. They are copied next to the binary at build time,
/// so they are production content and this is a test of production content. A skill's surface is the
/// subdirectory it lives in (<c>dm/</c>, <c>guild/</c>), and it reaches a mode by the mode's
/// <c>skills:</c> line.
/// </para>
/// </remarks>
public class SkillContractTests
{
    /// <summary>The surface subdirectories and the catalogue scope each one's tools must belong to.</summary>
    private static readonly IReadOnlyDictionary<string, ToolScopes> SurfaceDirectories =
        new Dictionary<string, ToolScopes>(StringComparer.OrdinalIgnoreCase)
        {
            ["dm"] = ToolScopes.Dm,
            ["guild"] = ToolScopes.Guild,
        };

    public static TheoryData<string> SkillFileNames()
    {
        var data = new TheoryData<string>();
        foreach (var file in SkillFiles())
        {
            data.Add(Path.GetRelativePath(PromptFiles.SkillsDirectory, file));
        }

        return data;
    }

    [Fact]
    public void TheSkillFilesAreFound()
    {
        // A guard on the guards. Every theory below passes vacuously on an empty directory.
        SkillFiles().Should().NotBeEmpty(
            "the DM surface ships skill files, so finding none means the files were not located");
    }

    [Theory]
    [MemberData(nameof(SkillFileNames))]
    public void EverySkillFileParses(string relativePath)
    {
        var file = Path.Combine(PromptFiles.SkillsDirectory, relativePath);

        var act = () => PromptFileParser.ParseSkill(File.ReadAllText(file), Path.GetFileNameWithoutExtension(file));

        act.Should().NotThrow($"{relativePath} must be a usable skill: a key, a summary, tools and a body");
    }

    [Theory]
    [MemberData(nameof(SkillFileNames))]
    public void EverySkillLivesInAKnownSurfaceDirectory(string relativePath)
    {
        SurfaceDirectories.Should().ContainKey(SurfaceOf(relativePath),
            $"{relativePath} must sit under a surface directory so its tools can be checked against that surface");
    }

    [Fact]
    public void EverySkillKeyIsUnique()
    {
        // One skill store reads every surface's directory, and keys are what the model passes to
        // load_skill: two files claiming one key means one of them is unreachable.
        Skills().Select(s => s.Skill.Key).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void EveryToolASkillNamesIsImplemented()
    {
        var implemented = RegisteredAgentTools.All
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, skill) in Skills())
        {
            foreach (var tool in skill.Tools)
            {
                implemented.Should().Contain(tool,
                    $"skill '{skill.Key}' names {tool}, which no tool implements - the name is "
                    + "dropped silently at runtime, so the skill would advertise less than it says");
            }
        }
    }

    [Fact]
    public void EveryToolASkillNamesIsCataloguedForItsSurface()
    {
        // The one that matters. A skill in dm/ naming a guild-scoped tool parses, loads, and unlocks
        // nothing on the DM surface. Nothing at runtime says so.
        foreach (var (surface, skill) in Skills())
        {
            var scope = SurfaceDirectories[surface];
            foreach (var tool in skill.Tools)
            {
                var entry = ToolCatalog.Describe(tool);

                entry.Scopes.HasFlag(scope).Should().BeTrue(
                    $"skill '{skill.Key}' is in {surface}/ but names {tool}, which the catalogue "
                    + $"advertises on {entry.Scopes} - it can never be unlocked there");
            }
        }
    }

    [Fact]
    public void NoSkillNamesTheLoaderItself()
    {
        // Hiding load_skill behind a skill would mean nothing could ever load one.
        foreach (var (_, skill) in Skills())
        {
            skill.Tools.Should().NotContain(t => string.Equals(t, "load_skill", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void EverySkillAModeListsExists_AndBelongsToThatModesSurface()
    {
        var skills = Skills().ToDictionary(s => s.Skill.Key, s => s.Surface, StringComparer.OrdinalIgnoreCase);
        var store = PromptFiles.CatalogStore();

        foreach (var (mode, _) in CatalogPromptStore.SurfaceModes)
        {
            foreach (var key in store.Get(mode).Skills)
            {
                skills.Should().ContainKey(key, $"mode '{mode}' lists skill '{key}', which no skill file defines");
                skills[key].Should().Be(mode, $"mode '{mode}' lists '{key}', which lives under another surface's directory");
            }
        }
    }

    [Fact]
    public void EverySkillFileIsOnSomeModesRoster()
    {
        // A skill no mode lists is never offered: dead content that looks live.
        var store = PromptFiles.CatalogStore();
        var listed = CatalogPromptStore.SurfaceModes.Keys
            .SelectMany(mode => store.Get(mode).Skills)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Skills().Select(s => s.Skill.Key).Where(k => !listed.Contains(k)).Should().BeEmpty();
    }

    private static string SurfaceOf(string relativePath) =>
        relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];

    private static IEnumerable<(string Surface, SkillDefinition Skill)> Skills() =>
        SkillFiles().Select(file => (
            SurfaceOf(Path.GetRelativePath(PromptFiles.SkillsDirectory, file)),
            PromptFileParser.ParseSkill(File.ReadAllText(file), Path.GetFileNameWithoutExtension(file))));

    private static IEnumerable<string> SkillFiles() =>
        Directory.Exists(PromptFiles.SkillsDirectory)
            ? Directory.EnumerateFiles(PromptFiles.SkillsDirectory, "*.md", SearchOption.AllDirectories)
                .Where(f => !string.Equals(Path.GetFileName(f), "README.md", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.Ordinal)
            : Enumerable.Empty<string>();
}
