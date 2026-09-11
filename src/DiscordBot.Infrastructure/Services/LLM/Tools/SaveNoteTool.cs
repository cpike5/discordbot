using System.Text.Json;
using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>save_note</c> — writes a note to the DM assistant's memory.
/// </summary>
public sealed class SaveNoteTool : IAgentTool
{
    /// <summary>Longest note the store accepts, matching the entity's column length.</summary>
    private const int MaxContentLength = 4096;

    private static readonly LlmToolDefinition ToolDefinition = new()
    {
        Name = "save_note",
        Description = "Saves a personal note for the user that persists across conversations. Use this when the user asks you to remember something, states a preference, or shares important context they want retained. Notes are private to each user.",
        InputSchema = ToolInput.ObjectSchema(
            new
            {
                content = ToolInput.Schema(
                    "string",
                    "The note content to save. Can be a preference, fact, reminder, or any information the user wants remembered."),
                tag = ToolInput.Schema(
                    "string",
                    "Optional category tag for organizing notes (e.g., 'preference', 'fact', 'context', 'todo').")
            },
            "content")
    };

    private readonly IDmAssistantNoteRepository _notes;
    private readonly ILogger<SaveNoteTool> _logger;

    /// <summary>Creates the tool.</summary>
    public SaveNoteTool(IDmAssistantNoteRepository notes, ILogger<SaveNoteTool> logger)
    {
        _notes = notes ?? throw new ArgumentNullException(nameof(notes));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public LlmToolDefinition Definition => ToolDefinition;

    /// <inheritdoc />
    public string? Mutation => "save notes";

    /// <inheritdoc />
    public async Task<ToolExecutionResult> InvokeAsync(
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        var content = ToolInput.GetString(input, "content");
        if (content is null)
        {
            return ToolResults.Error("Missing required parameter: content");
        }

        if (content.Length > MaxContentLength)
        {
            return ToolResults.Error(
                $"Note content exceeds maximum length of {MaxContentLength} characters.");
        }

        var now = DateTime.UtcNow;
        var note = new DmAssistantNote
        {
            UserId = context.UserId,
            Content = content,
            Tag = ToolInput.GetString(input, "tag"),
            CreatedAt = now,
            UpdatedAt = now
        };

        await _notes.AddAsync(note, cancellationToken);

        _logger.LogDebug("Saved note {NoteId} for user {UserId}", note.Id, context.UserId);

        return ToolResults.Json(new
        {
            success = true,
            note_id = note.Id,
            message = "Note saved successfully."
        });
    }
}
