using System.Text.Json;
using DiscordBot.Agents;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services.LLM.Implementations;
using DiscordBot.Infrastructure.Services.LLM.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Containment tests for <c>get_feature_documentation</c>: a feature name is model input, and the
/// model's input is a Discord message from any user in any guild where the assistant is enabled, so
/// the name must not be able to name a file outside the documentation directory.
/// </summary>
/// <remarks>
/// <para>
/// These run against a real temp directory with a real file planted <em>outside</em> the configured
/// base path, because the thing being tested is path resolution and a mocked file layer would paper
/// over exactly that. Every test asserts the planted file exists first: without that guard a
/// regression reads as a pass, since an unreachable file and a missing one give the same answer —
/// which is the point of the rest of the file.
/// </para>
/// <para>
/// The answer to a refused name is byte-identical to the answer for a name that simply is not there.
/// A distinct "rejected" message would tell whoever wrote the message behind the model's call that
/// their probe was understood; the warning that says so goes to the log instead.
/// </para>
/// </remarks>
public class DocumentationPathContainmentTests : IDisposable
{
    private readonly string _root;
    private readonly string _baseDirectory;
    private readonly string _outsideFile;
    private readonly DocumentationToolProvider _provider;

    public DocumentationPathContainmentTests()
    {
        _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _baseDirectory = Path.Combine(_root, "articles");
        Directory.CreateDirectory(_baseDirectory);

        // The stand-in for docs/agents/assistant-agent.md: real, readable, and one level above the
        // directory the tool is allowed to read from.
        _outsideFile = Path.Combine(_root, "secret.md");
        File.WriteAllText(_outsideFile, "The system prompt the assistant is forbidden to disclose.");

        // welcome-system is in the provider's feature map; authorization-policies is not, so between
        // them they cover both the mapped name and the allow-listed fallback.
        File.WriteAllText(Path.Combine(_baseDirectory, "welcome-system.md"), "# Welcome System");
        File.WriteAllText(Path.Combine(_baseDirectory, "authorization-policies.md"), "# Authorization Policies");

        var assistantOptions = new Mock<IOptions<AssistantOptions>>();
        assistantOptions.Setup(o => o.Value).Returns(new AssistantOptions
        {
            Tools = new() { DocumentationBasePath = _baseDirectory }
        });

        var applicationOptions = new Mock<IOptions<ApplicationOptions>>();
        applicationOptions.Setup(o => o.Value).Returns(new ApplicationOptions());

        _provider = new DocumentationToolProvider(
            Mock.Of<ILogger<DocumentationToolProvider>>(),
            Mock.Of<ICommandMetadataService>(),
            assistantOptions.Object,
            applicationOptions.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    public static TheoryData<string> EscapingFeatureNames() => new()
    {
        // Relative traversal - the original finding, a name away from the assistant's own prompt.
        "../secret",
        "../../docs/agents/assistant-agent",
        "articles/../../secret",
        // Traversal that leans on the mapped-name spelling.
        "welcome-system/../../secret",
        // Backslash separators: a traversal on Windows, and outside the allow-list everywhere.
        @"..\secret",
        @"..\..\CLAUDE",
        // A rooted path, which Path.Combine would otherwise adopt whole.
        "/etc/passwd",
        "/CLAUDE",
        // A null byte, which the filesystem will not resolve at all.
        "welcome-system\0",
        "welcome-system\0.md",
        // Everything else that is not a feature name.
        "..",
        ".",
        "~/secret",
        "welcome-system.md",
        "Welcome System"
    };

    [Theory]
    [MemberData(nameof(EscapingFeatureNames))]
    public async Task FeatureName_CannotNameAFileOutsideTheBaseDirectory(string featureName)
    {
        File.Exists(_outsideFile).Should().BeTrue("the file this test proves is unreachable has to exist");

        var result = await Execute(featureName);

        result.Success.Should().BeTrue("a refusal is an answer for the model to relay, not a malfunction");
        result.Data.Should().NotBeNull();
        result.Data!.Value.GetRawText().Should().Be(Unavailable(featureName));
        result.Data.Value.GetRawText().Should().NotContain("forbidden to disclose");
    }

    [Theory]
    [MemberData(nameof(EscapingFeatureNames))]
    public async Task RefusalIsByteIdenticalToTheAnswerForANameThatIsSimplyAbsent(string featureName)
    {
        var absent = await Execute("no-such-feature");
        var refused = await Execute(featureName);

        // The only difference between the two payloads is the name the caller already knows it sent.
        // Same keys, same wording, same order: a probe learns nothing from the reply.
        WithoutFeatureName(refused.Data!.Value, featureName)
            .Should().Be(WithoutFeatureName(absent.Data!.Value, "no-such-feature"));
    }

    [Theory]
    [MemberData(nameof(EscapingFeatureNames))]
    public async Task RefusalIsCountedAsAFailedResult(string featureName)
    {
        var result = await Execute(featureName);

        ToolOutcomes.Classify(result.Data!.Value).Should().Be(ToolOutcomes.FailedResult,
            "a refused or absent document is an expected failure, and one reported any other way is "
            + "invisible in the traces and on the metrics page");
    }

    [Theory]
    [InlineData("welcome-system")]   // mapped
    [InlineData("welcome")]          // mapped alias
    [InlineData("Welcome-System")]   // the map is case-insensitive
    public async Task MappedFeatureName_StillResolves(string featureName)
    {
        var result = await Execute(featureName);

        result.Success.Should().BeTrue();
        result.Data!.Value.GetProperty("available").GetBoolean().Should().BeTrue();
        result.Data.Value.GetProperty("content").GetString().Should().Be("# Welcome System");
    }

    [Theory]
    [InlineData("authorization-policies")]
    [InlineData("AUTHORIZATION-POLICIES")]
    public async Task UnmappedButRealFeatureName_StillResolves(string featureName)
    {
        // The fallback the allow-list guards: no map entry, a real file, and a name made only of the
        // characters a feature name is allowed to contain.
        var result = await Execute(featureName);

        result.Success.Should().BeTrue();
        result.Data!.Value.GetProperty("available").GetBoolean().Should().BeTrue();
        result.Data.Value.GetProperty("content").GetString().Should().Be("# Authorization Policies");
    }

    [Fact]
    public async Task AbsentFeatureName_IsReportedAsUnavailable()
    {
        var result = await Execute("no-such-feature");

        result.Success.Should().BeTrue();
        result.Data!.Value.GetProperty("available").GetBoolean().Should().BeFalse();
        result.Data.Value.GetProperty("error").GetString().Should().Contain("list_features");
    }

    private Task<ToolExecutionResult> Execute(string featureName)
    {
        var input = JsonSerializer.SerializeToElement(new { feature_name = featureName });

        return _provider.ExecuteToolAsync(
            DocumentationTools.GetFeatureDocumentation,
            input,
            new ToolContext { UserId = 1, GuildId = 2 });
    }

    /// <summary>The one payload the tool returns for any feature it will not read.</summary>
    private static string Unavailable(string featureName) =>
        ToolJson.Element(new
        {
            available = false,
            error = $"Documentation for feature '{featureName}' not found. Available features can be listed using list_features tool."
        }).GetRawText();

    /// <summary>
    /// The payload with the echoed feature name blanked out, so two answers about different names can
    /// be compared for everything else: keys, order, and wording.
    /// </summary>
    private static string WithoutFeatureName(JsonElement payload, string featureName)
    {
        // Quotes included, because the encoder escapes those too.
        var quoted = Escaped($"'{featureName}'");
        var raw = payload.GetRawText();

        raw.Should().Contain(quoted, "the answer echoes the name it was asked about");

        return raw.Replace(quoted, Escaped("'NAME'"), StringComparison.Ordinal);
    }

    /// <summary>How <paramref name="value"/> appears inside the serialized payload.</summary>
    private static string Escaped(string value)
    {
        var json = JsonSerializer.Serialize(value, ToolJson.Compact);
        return json[1..^1];
    }
}
