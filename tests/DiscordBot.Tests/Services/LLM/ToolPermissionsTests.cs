using System.Text.Json;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Infrastructure.Services.LLM.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for the caller-access seam: <see cref="ToolPermissions.MutationForbidden"/> and the
/// <c>ToolContext.CanMutate</c> check inside the tools that write. This is the replacement for the
/// deleted <c>ToolContext.UserRoles</c>, and it has to work before the first write tool ships.
/// </summary>
public class ToolPermissionsTests
{
    private const ulong UserId = 4242UL;

    [Fact]
    public void MutationForbidden_IsASuccessfulResultCarryingADirective()
    {
        // Not an error: flagging it would inflate tool-failure metrics and prepend "Error: " on the
        // wire, which reads to the model as a malfunction to retry around rather than a decision.
        var result = ToolPermissions.MutationForbidden("save notes");

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();

        var payload = result.Data!.Value;
        payload.GetProperty("forbidden").GetBoolean().Should().BeTrue();
        payload.GetProperty("message").GetString().Should().Contain("save notes");
    }

    private static MemoryToolProvider BuildMemoryProvider(Mock<IDmAssistantNoteRepository> repository) =>
        new(Mock.Of<ILogger<MemoryToolProvider>>(), repository.Object);

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    [Fact]
    public async Task SaveNote_RefusesAndWritesNothing_WhenTheCallerMayNotMutate()
    {
        var repository = new Mock<IDmAssistantNoteRepository>();
        var provider = BuildMemoryProvider(repository);

        var result = await provider.ExecuteToolAsync(
            "save_note",
            Json("""{"content":"remember this"}"""),
            new ToolContext { UserId = UserId, CanMutate = false });

        result.Data!.Value.GetProperty("forbidden").GetBoolean().Should().BeTrue();
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteNote_RefusesAndWritesNothing_WhenTheCallerMayNotMutate()
    {
        var repository = new Mock<IDmAssistantNoteRepository>();
        var provider = BuildMemoryProvider(repository);

        var result = await provider.ExecuteToolAsync(
            "delete_note",
            Json("""{"note_id":7}"""),
            new ToolContext { UserId = UserId, CanMutate = false });

        result.Data!.Value.GetProperty("forbidden").GetBoolean().Should().BeTrue();
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ListNotes_StillWorks_WhenTheCallerMayNotMutate()
    {
        // The flag gates writes only; a read-only caller must keep every read tool.
        var repository = new Mock<IDmAssistantNoteRepository>();
        repository
            .Setup(r => r.ListAsync(UserId, It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DmAssistantNote>());

        var provider = BuildMemoryProvider(repository);

        var result = await provider.ExecuteToolAsync(
            "list_notes", Json("{}"), new ToolContext { UserId = UserId, CanMutate = false });

        result.Success.Should().BeTrue();
        result.Data!.Value.TryGetProperty("forbidden", out _).Should().BeFalse();
    }

    [Fact]
    public async Task SaveNote_WritesThrough_WhenTheCallerMayMutate()
    {
        var repository = new Mock<IDmAssistantNoteRepository>();
        repository
            .Setup(r => r.AddAsync(It.IsAny<DmAssistantNote>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DmAssistantNote note, CancellationToken _) => note);

        var provider = BuildMemoryProvider(repository);

        var result = await provider.ExecuteToolAsync(
            "save_note",
            Json("""{"content":"remember this"}"""),
            new ToolContext { UserId = UserId, CanMutate = true });

        result.Success.Should().BeTrue();
        result.Data!.Value.TryGetProperty("forbidden", out _).Should().BeFalse();
        repository.Verify(
            r => r.AddAsync(It.IsAny<DmAssistantNote>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ToolContext_DefaultsToReadOnly()
    {
        // The default has to be the safe one: a context nobody assessed must not be able to write.
        new ToolContext().CanMutate.Should().BeFalse();
    }
}
