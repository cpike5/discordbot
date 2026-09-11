using System.Text.Json;
using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models.Llm;
using DiscordBot.Infrastructure.Services.LLM.Tools;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for the five memory tools, the first conversion from <c>IToolProvider</c> to
/// <see cref="IAgentTool"/>.
/// </summary>
/// <remarks>
/// The conversion is meant to change nothing the model sees, so the first test here pins each
/// schema against the literal the deleted <c>MemoryTools</c> class carried. The tool array
/// serializes at position 0 of every request, so a byte that moves there invalidates the whole
/// prompt-cache prefix — which is a real cost with no symptom other than the bill.
/// </remarks>
public class MemoryAgentToolsTests
{
    private const ulong UserId = 4242UL;

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    private static string Compact(string raw) =>
        JsonSerializer.Serialize(JsonDocument.Parse(raw).RootElement);

    private static Mock<IDmAssistantNoteRepository> Notes() => new();

    private static SaveNoteTool SaveNote(Mock<IDmAssistantNoteRepository> notes) =>
        new(notes.Object, Mock.Of<ILogger<SaveNoteTool>>());

    private static DeleteNoteTool DeleteNote(Mock<IDmAssistantNoteRepository> notes) =>
        new(notes.Object, Mock.Of<ILogger<DeleteNoteTool>>());

    private static DmAssistantNote Note(long id, string content, string? tag = null) => new()
    {
        Id = id,
        UserId = UserId,
        Content = content,
        Tag = tag,
        CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
        UpdatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)
    };

    public static TheoryData<string, string> UnchangedSchemas => new()
    {
        {
            "save_note",
            """
            {
                "type": "object",
                "properties": {
                    "content": {
                        "type": "string",
                        "description": "The note content to save. Can be a preference, fact, reminder, or any information the user wants remembered."
                    },
                    "tag": {
                        "type": "string",
                        "description": "Optional category tag for organizing notes (e.g., 'preference', 'fact', 'context', 'todo')."
                    }
                },
                "required": ["content"]
            }
            """
        },
        {
            "search_notes",
            """
            {
                "type": "object",
                "properties": {
                    "query": {
                        "type": "string",
                        "description": "Search term to find in note content or tags."
                    },
                    "limit": {
                        "type": "integer",
                        "description": "Maximum number of results to return. Default is 10.",
                        "default": 10,
                        "minimum": 1,
                        "maximum": 50
                    }
                },
                "required": ["query"]
            }
            """
        },
        {
            "get_note",
            """
            {
                "type": "object",
                "properties": {
                    "note_id": {
                        "type": "integer",
                        "description": "The ID of the note to retrieve."
                    }
                },
                "required": ["note_id"]
            }
            """
        },
        {
            "list_notes",
            """
            {
                "type": "object",
                "properties": {
                    "tag": {
                        "type": "string",
                        "description": "Optional tag to filter notes by category."
                    },
                    "limit": {
                        "type": "integer",
                        "description": "Maximum number of notes to return. Default is 20.",
                        "default": 20,
                        "minimum": 1,
                        "maximum": 50
                    }
                },
                "required": []
            }
            """
        },
        {
            "delete_note",
            """
            {
                "type": "object",
                "properties": {
                    "note_id": {
                        "type": "integer",
                        "description": "The ID of the note to delete."
                    }
                },
                "required": ["note_id"]
            }
            """
        }
    };

    [Theory]
    [MemberData(nameof(UnchangedSchemas))]
    public void TheConversionLeftEverySchemaByteForByteUnchanged(string toolName, string before)
    {
        var notes = Notes();
        var tool = AllTools(notes).Single(t => t.Definition.Name == toolName);

        JsonSerializer.Serialize(tool.Definition.InputSchema).Should().Be(Compact(before));
    }

    [Theory]
    [InlineData("save_note", "Saves a personal note for the user that persists across conversations. Use this when the user asks you to remember something, states a preference, or shares important context they want retained. Notes are private to each user.")]
    [InlineData("search_notes", "Searches the user's saved notes by keyword. Matches against both note content and tags. Use this to recall previously saved information about the user.")]
    [InlineData("get_note", "Retrieves a specific note by its ID. Use this when you need the full content of a particular note.")]
    [InlineData("list_notes", "Lists the user's saved notes, optionally filtered by tag. Returns notes sorted by most recently updated. Use this to browse all saved notes or notes in a specific category.")]
    [InlineData("delete_note", "Deletes a specific note by its ID. Use this when the user wants to remove previously saved information. This action cannot be undone.")]
    public void TheConversionLeftEveryDescriptionUnchanged(string toolName, string description)
    {
        var notes = Notes();

        AllTools(notes).Single(t => t.Definition.Name == toolName)
            .Definition.Description.Should().Be(description);
    }

    [Fact]
    public void EveryMemoryToolIsCatalogued_OnTheDmSurface()
    {
        // The catalogue is what routes a tool to a surface now, so an entry is not a footnote: a
        // tool nobody catalogued is advertised nowhere.
        var notes = Notes();

        foreach (var tool in AllTools(notes))
        {
            var name = tool.Definition.Name;
            ToolCatalog.IsCatalogued(name).Should().BeTrue($"{name} needs a ToolCatalog entry");
            ToolCatalog.Describe(name).Scopes.Should().HaveFlag(ToolScopes.Dm);
        }
    }

    [Fact]
    public void OnlyTheWriteToolsDeclareAMutation()
    {
        var notes = Notes();

        AllTools(notes).Where(t => t.Mutation is not null).Select(t => t.Definition.Name)
            .Should().BeEquivalentTo("save_note", "delete_note");
    }

    [Fact]
    public async Task SaveNote_StoresTheNoteAndReportsItsId()
    {
        var notes = Notes();
        notes.Setup(r => r.AddAsync(It.IsAny<DmAssistantNote>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DmAssistantNote note, CancellationToken _) =>
            {
                note.Id = 11;
                return note;
            });

        var result = await SaveNote(notes).InvokeAsync(
            Json("""{"content":"likes tea","tag":"preference"}"""),
            new ToolContext { UserId = UserId, CanMutate = true });

        result.Success.Should().BeTrue();
        result.Data!.Value.GetProperty("note_id").GetInt64().Should().Be(11);
        notes.Verify(r => r.AddAsync(
            It.Is<DmAssistantNote>(n =>
                n.UserId == UserId && n.Content == "likes tea" && n.Tag == "preference"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveNote_ReportsAMissingContentAsAnExpectedFailure()
    {
        var notes = Notes();

        var result = await SaveNote(notes).InvokeAsync(
            Json("{}"), new ToolContext { UserId = UserId, CanMutate = true });

        // A successful result carrying an explanation, countable as failed_result in the trace.
        result.Success.Should().BeTrue();
        result.Data!.Value.GetProperty("error").GetString().Should().Contain("content");
        ToolOutcomes.Classify(result.Data!.Value).Should().Be(ToolOutcomes.FailedResult);
        notes.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SaveNote_RefusesContentOverTheColumnLength()
    {
        var notes = Notes();

        var result = await SaveNote(notes).InvokeAsync(
            Json($$"""{"content":"{{new string('x', 4097)}}"}"""),
            new ToolContext { UserId = UserId, CanMutate = true });

        result.Data!.Value.GetProperty("error").GetString().Should().Contain("4096");
        notes.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SearchNotes_ReturnsTheMatchesAndTheirCount()
    {
        var notes = Notes();
        notes.Setup(r => r.SearchAsync("tea", UserId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Note(1, "likes tea", "preference") });

        var result = await new SearchNotesTool(notes.Object).InvokeAsync(
            Json("""{"query":"tea"}"""), new ToolContext { UserId = UserId });

        result.Data!.Value.GetProperty("total_results").GetInt32().Should().Be(1);
        result.Data!.Value.GetProperty("results")[0].GetProperty("content").GetString()
            .Should().Be("likes tea");
    }

    [Fact]
    public async Task SearchNotes_ClampsTheLimit()
    {
        var notes = Notes();
        notes.Setup(r => r.SearchAsync("tea", UserId, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DmAssistantNote>());

        await new SearchNotesTool(notes.Object).InvokeAsync(
            Json("""{"query":"tea","limit":9000}"""), new ToolContext { UserId = UserId });

        notes.Verify(r => r.SearchAsync("tea", UserId, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetNote_ReadsAQuotedId()
    {
        // The model quotes numbers sometimes; the old provider's GetInt64() threw on it.
        var notes = Notes();
        notes.Setup(r => r.GetByIdAsync(7L, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Note(7, "remembered"));

        var result = await new GetNoteTool(notes.Object).InvokeAsync(
            Json("""{"note_id":"7"}"""), new ToolContext { UserId = UserId });

        result.Data!.Value.GetProperty("id").GetInt64().Should().Be(7);
    }

    [Fact]
    public async Task GetNote_ReportsAMissingNoteAsAnExpectedFailure()
    {
        var notes = Notes();
        notes.Setup(r => r.GetByIdAsync(41L, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DmAssistantNote?)null);

        var result = await new GetNoteTool(notes.Object).InvokeAsync(
            Json("""{"note_id":41}"""), new ToolContext { UserId = UserId });

        result.Success.Should().BeTrue();
        result.Data!.Value.GetProperty("found").GetBoolean().Should().BeFalse();
        ToolOutcomes.Classify(result.Data!.Value).Should().Be(ToolOutcomes.FailedResult);
    }

    [Fact]
    public async Task ListNotes_OmitsTheFilterWhenThereIsNone()
    {
        var notes = Notes();
        notes.Setup(r => r.ListAsync(UserId, null, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Note(1, "a note") });

        var result = await new ListNotesTool(notes.Object).InvokeAsync(
            Json("{}"), new ToolContext { UserId = UserId });

        var payload = result.Data!.Value;
        payload.GetProperty("total_count").GetInt32().Should().Be(1);
        // Nulls are dropped rather than serialized, so an absent filter costs no tokens.
        payload.TryGetProperty("filter_tag", out _).Should().BeFalse();
        payload.GetProperty("notes")[0].TryGetProperty("tag", out _).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteNote_DeletesAndConfirms()
    {
        var notes = Notes();
        notes.Setup(r => r.DeleteAsync(7L, UserId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await DeleteNote(notes).InvokeAsync(
            Json("""{"note_id":7}"""), new ToolContext { UserId = UserId, CanMutate = true });

        result.Data!.Value.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task DeleteNote_ReportsAMissingNoteAsAnExpectedFailure()
    {
        var notes = Notes();
        notes.Setup(r => r.DeleteAsync(7L, UserId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await DeleteNote(notes).InvokeAsync(
            Json("""{"note_id":7}"""), new ToolContext { UserId = UserId, CanMutate = true });

        ToolOutcomes.Classify(result.Data!.Value).Should().Be(ToolOutcomes.FailedResult);
    }

    private static IReadOnlyList<IAgentTool> AllTools(Mock<IDmAssistantNoteRepository> notes) => new IAgentTool[]
    {
        SaveNote(notes),
        new SearchNotesTool(notes.Object),
        new GetNoteTool(notes.Object),
        new ListNotesTool(notes.Object),
        DeleteNote(notes)
    };
}
