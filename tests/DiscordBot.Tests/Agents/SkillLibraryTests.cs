using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Agents;

/// <summary>
/// Unit tests for <see cref="SkillLibrary"/> — reading a directory of markdown files into a
/// surface's skills. These run against a real temp directory and a real <see cref="PromptTemplate"/>
/// over a real <see cref="MemoryCache"/>, because the parts worth testing are exactly the ones a
/// mocked file layer would paper over: path resolution, the <c>.md</c> filter, path ordering, and
/// the fact that one bad file must not take the surface's whole roster down with it.
/// </summary>
public class SkillLibraryTests : IDisposable
{
    private readonly string _directory;

    // PromptTemplate sizes its cache entries by content length, so the cache needs a SizeLimit -
    // same shape as PromptTemplateTests.
    private readonly MemoryCache _cache =
        new(new MemoryCacheOptions { CompactionPercentage = 0.1, SizeLimit = 1024 * 1024 });

    private readonly ISkillLibrary _library;

    public SkillLibraryTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);

        var prompts = new PromptTemplate(Mock.Of<ILogger<PromptTemplate>>(), _cache);
        _library = new SkillLibrary(prompts, _cache, Mock.Of<ILogger<SkillLibrary>>());
    }

    public void Dispose()
    {
        _cache.Dispose();

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Writes a well-formed skill file, unless <paramref name="content"/> overrides it.</summary>
    private void WriteFile(string fileName, string? content = null, string? summary = null)
    {
        content ??= $"""
            ---
            summary: {summary ?? $"Summary for {Path.GetFileNameWithoutExtension(fileName)}."}
            ---
            Instructions for {Path.GetFileNameWithoutExtension(fileName)}.
            """;

        File.WriteAllText(Path.Combine(_directory, fileName), content);
    }

    #region Loading

    [Fact]
    public async Task LoadAsync_LoadsEveryMarkdownFile_OrderedByKey()
    {
        WriteFile("zulu.md");
        WriteFile("alpha.md");
        WriteFile("mike.md");

        var skills = await _library.LoadAsync(_directory);

        // The key falls back to the file name, and the result is sorted by key - not by path - so a
        // renamed file does not quietly reorder the roster in the cached prompt prefix.
        skills.Select(s => s.Key).Should().Equal("alpha", "mike", "zulu");
        skills.Should().OnlyContain(s => s.Instructions.StartsWith("Instructions for"));
    }

    [Fact]
    public async Task LoadAsync_CarriesTheFilePathAsTheSource()
    {
        WriteFile("alpha.md");

        var skill = (await _library.LoadAsync(_directory)).Should().ContainSingle().Subject;

        skill.Source.Should().Be(Path.Combine(_directory, "alpha.md"));
    }

    [Fact]
    public async Task LoadAsync_IgnoresFilesThatAreNotMarkdown()
    {
        WriteFile("alpha.md");
        File.WriteAllText(Path.Combine(_directory, "README.txt"), "not a skill");
        File.WriteAllText(Path.Combine(_directory, "notes.markdown"), "also not a skill");

        var skills = await _library.LoadAsync(_directory);

        skills.Select(s => s.Key).Should().Equal("alpha");
    }

    #endregion

    #region Degraded Directories

    [Fact]
    public async Task LoadAsync_WithAMissingDirectory_ReturnsEmpty()
    {
        // A surface with no skills is the normal state of one nobody has written any for, so this is
        // not an error - the loader tool simply is not advertised.
        var missing = Path.Combine(_directory, "no-such-directory");

        var skills = await _library.LoadAsync(missing);

        skills.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LoadAsync_WithABlankDirectory_ReturnsEmptyWithoutTouchingTheFilesystem(string directory)
    {
        var skills = await _library.LoadAsync(directory);

        skills.Should().BeEmpty();
        // A blank path would otherwise resolve to the application directory and enumerate whatever
        // markdown happens to sit next to the binaries; the early return is what prevents that, and
        // the untouched listing cache is the evidence it was taken.
        _cache.Count.Should().Be(0);
    }

    [Fact]
    public async Task LoadAsync_WithAnEmptyDirectory_ReturnsEmpty()
    {
        (await _library.LoadAsync(_directory)).Should().BeEmpty();
    }

    #endregion

    #region Bad Files

    [Fact]
    public async Task LoadAsync_SkipsAFileThatDoesNotParse_AndKeepsTheRest()
    {
        WriteFile("alpha.md");
        WriteFile("broken.md", content: "No front matter here, just prose.");
        WriteFile("no-summary.md", content: "---\nkey: orphan\n---\nInstructions with no summary.\n");
        WriteFile("zulu.md");

        var skills = await _library.LoadAsync(_directory);

        skills.Select(s => s.Key).Should().Equal("alpha", "zulu");
    }

    [Fact]
    public async Task LoadAsync_WithADuplicateKey_KeepsTheFirstByPathOrder()
    {
        // Files are read in path order, so "which one wins" is deterministic across hosts rather than
        // whatever the filesystem happens to enumerate first.
        WriteFile("a-first.md", content: "---\nkey: shared\nsummary: The first file.\n---\nFirst body.\n");
        WriteFile("b-second.md", content: "---\nkey: shared\nsummary: The second file.\n---\nSecond body.\n");

        var skills = await _library.LoadAsync(_directory);

        skills.Should().ContainSingle()
            .Which.Summary.Should().Be("The first file.");
    }

    [Fact]
    public async Task LoadAsync_WithOnlyBadFiles_ReturnsEmpty()
    {
        WriteFile("broken.md", content: "No front matter here.");

        (await _library.LoadAsync(_directory)).Should().BeEmpty();
    }

    #endregion

    #region Keys

    [Fact]
    public async Task LoadAsync_PrefersTheDeclaredKeyOverTheFileName()
    {
        WriteFile("01-moderation.md", content: "---\nkey: moderation\nsummary: Declared key.\n---\nBody.\n");

        var skill = (await _library.LoadAsync(_directory)).Should().ContainSingle().Subject;

        skill.Key.Should().Be("moderation");
    }

    [Fact]
    public async Task LoadAsync_ReadsToolsFromTheFile()
    {
        WriteFile(
            "moderation.md",
            content: "---\nsummary: With tools.\ntools: get_cases, get_history\n---\nBody.\n");

        var skill = (await _library.LoadAsync(_directory)).Should().ContainSingle().Subject;

        skill.Tools.Should().Equal("get_cases", "get_history");
    }

    #endregion
}
