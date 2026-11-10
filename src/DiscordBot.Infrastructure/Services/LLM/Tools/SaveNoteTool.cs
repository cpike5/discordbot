using System.Text.Json;
using cpike.Agent;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>save_note</c> — writes a note to the DM assistant's memory.
/// </summary>
public sealed class SaveNoteTool : IAgentTool
{
    /// <summary>Longest note the store accepts, matching the entity's column length.</summary>
    private const int MaxContentLength = 4096;

    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "save_note",
        "Saves a personal note for the user that persists across conversations. Use this when the user asks you to remember something, states a preference, or shares important context they want retained. Notes are private to each user.",
        ToolInput.ObjectSchema(
            new
            {
                content = ToolInput.Schema(
                    "string",
                    "The note content to save. Can be a preference, fact, reminder, or any information the user wants remembered."),
                tag = ToolInput.Schema(
                    "string",
                    "Optional category tag for organizing notes (e.g., 'preference', 'fact', 'context', 'todo').")
            },
            "content"),
        requiresEdit: true);

    private readonly IDmAssistantNoteRepository _notes;
    private readonly DiscordRunContext _run;
    private readonly ILogger<SaveNoteTool> _logger;

    /// <summary>Creates the tool.</summary>
    public SaveNoteTool(IDmAssistantNoteRepository notes, DiscordRunContext run, ILogger<SaveNoteTool> logger)
    {
        _notes = notes ?? throw new ArgumentNullException(nameof(notes));
        _run = run ?? throw new ArgumentNullException(nameof(run));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public AgentToolDefinition Definition => ToolDefinition;

    /// <inheritdoc />
    public async Task<string> InvokeAsync(
        IReadOnlyDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken)
    {
        var input = ToolInput.ToElement(arguments);
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
            UserId = _run.UserId,
            Content = content,
            Tag = ToolInput.GetString(input, "tag"),
            CreatedAt = now,
            UpdatedAt = now
        };

        await _notes.AddAsync(note, cancellationToken);

        _logger.LogDebug("Saved note {NoteId} for user {UserId}", note.Id, _run.UserId);

        return ToolResults.Json(new
        {
            success = true,
            note_id = note.Id,
            message = "Note saved successfully."
        });
    }
}
