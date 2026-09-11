using DiscordBot.Core.Entities;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// The one shape a saved note takes in a tool result, so the three tools that return notes return
/// the same thing.
/// </summary>
internal static class NotePayloads
{
    /// <summary>
    /// <paramref name="note"/> as the model sees it. <c>tag</c> is omitted rather than null when the
    /// note has none — <see cref="Agents.ToolJson.Compact"/> drops nulls.
    /// </summary>
    internal static object Describe(DmAssistantNote note) => new
    {
        id = note.Id,
        content = note.Content,
        tag = note.Tag,
        created_at = note.CreatedAt.ToString("o"),
        updated_at = note.UpdatedAt.ToString("o")
    };
}
