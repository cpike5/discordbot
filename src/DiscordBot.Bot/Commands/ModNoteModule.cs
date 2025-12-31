using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Slash commands for managing moderator notes on users.
/// Moderator notes are private annotations visible only to moderators for tracking concerns,
/// observations, or context about specific users.
/// </summary>
[RequireGuildActive]
[RequireModerationEnabled]
[RequireModerator]
[Group("modnote", "Moderator note commands")]
public class ModNoteModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IModNoteService _noteService;
    private readonly ILogger<ModNoteModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModNoteModule"/> class.
    /// </summary>
    public ModNoteModule(
        IModNoteService noteService,
        ILogger<ModNoteModule> logger)
    {
        _noteService = noteService;
        _logger = logger;
    }

    /// <summary>
    /// Adds a new moderator note about a user.
    /// Notes are private to moderators and used for tracking observations and context.
    /// </summary>
    [SlashCommand("add", "Add a private note about a user")]
    public async Task AddAsync(
        [Summary("user", "The user to add a note about")] IUser user,
        [Summary("note", "The note content")] string note)
    {
        _logger.LogInformation(
            "Mod note add command executed by {ModeratorUsername} (ID: {ModeratorId}) for user {TargetUsername} (ID: {TargetId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            user.Username,
            user.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        try
        {
            // Validate note length
            if (string.IsNullOrWhiteSpace(note))
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Invalid Note")
                    .WithDescription("Note content cannot be empty.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: errorEmbed, ephemeral: true);
                _logger.LogDebug("Note creation failed: empty content");
                return;
            }

            if (note.Length > 2000)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Note Too Long")
                    .WithDescription("Note content must be 2000 characters or less.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: errorEmbed, ephemeral: true);
                _logger.LogDebug("Note creation failed: content too long ({Length} chars)", note.Length);
                return;
            }

            // Add note via service
            var createdNote = await _noteService.AddNoteAsync(
                Context.Guild.Id,
                user.Id,
                note,
                Context.User.Id);

            _logger.LogInformation(
                "Mod note {NoteId} created by {ModeratorId} for user {TargetId}",
                createdNote.Id,
                Context.User.Id,
                user.Id);

            // Build confirmation embed with note preview
            var notePreview = note.Length > 200 ? note[..197] + "..." : note;

            var embed = new EmbedBuilder()
                .WithTitle("✅ Note Added")
                .WithDescription($"Added note for <@{user.Id}>")
                .AddField("Note", $"> {notePreview}", inline: false)
                .AddField("Note ID", createdNote.Id.ToString(), inline: true)
                .WithColor(Color.Green)
                .WithCurrentTimestamp()
                .WithFooter("Visible to moderators only")
                .Build();

            await RespondAsync(embed: embed, ephemeral: true);

            _logger.LogDebug("Mod note add command response sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add mod note for user {TargetId}", user.Id);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to add note: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Lists all moderator notes for a specific user.
    /// </summary>
    [SlashCommand("list", "View all notes for a user")]
    public async Task ListAsync(
        [Summary("user", "The user to view notes for")] IUser user)
    {
        _logger.LogInformation(
            "Mod note list command executed by {ModeratorUsername} (ID: {ModeratorId}) for user {TargetUsername} (ID: {TargetId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            user.Username,
            user.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        try
        {
            // Get notes from service
            var notes = (await _noteService.GetNotesAsync(Context.Guild.Id, user.Id)).ToList();

            _logger.LogDebug("Retrieved {NoteCount} notes for user {TargetId}", notes.Count, user.Id);

            // Build embed
            var embed = new EmbedBuilder()
                .WithTitle($"📝 Moderator Notes: {user.Username}")
                .WithColor(Color.Blue)
                .WithCurrentTimestamp()
                .WithFooter("Visible to moderators only");

            if (notes.Count == 0)
            {
                embed.WithDescription("No notes found for this user.");
            }
            else
            {
                // Add notes as fields (limit to 10 most recent)
                var displayNotes = notes.OrderByDescending(n => n.CreatedAt).Take(10).ToList();

                foreach (var note in displayNotes)
                {
                    var timestamp = new DateTimeOffset(note.CreatedAt).ToUnixTimeSeconds();
                    var notePreview = note.Content.Length > 100 ? note.Content[..97] + "..." : note.Content;

                    embed.AddField(
                        $"Note {note.Id.ToString()[..8]}... — <t:{timestamp}:R>",
                        $"By: {note.AuthorUsername}\n> {notePreview}",
                        inline: false);
                }

                if (notes.Count > 10)
                {
                    embed.WithDescription($"Showing 10 of {notes.Count} notes (most recent first).");
                }
            }

            await RespondAsync(embed: embed.Build(), ephemeral: true);

            _logger.LogDebug("Mod note list command response sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list mod notes for user {TargetId}", user.Id);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to retrieve notes: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Deletes a specific moderator note by ID.
    /// </summary>
    [SlashCommand("remove", "Delete a note")]
    public async Task RemoveAsync(
        [Summary("note_id", "The note ID to delete")] string noteId)
    {
        _logger.LogInformation(
            "Mod note remove command executed by {ModeratorUsername} (ID: {ModeratorId}) for note {NoteId} in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            noteId,
            Context.Guild.Name,
            Context.Guild.Id);

        try
        {
            // Parse GUID from noteId
            if (!Guid.TryParse(noteId, out var parsedNoteId))
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Invalid Note ID")
                    .WithDescription("The note ID must be a valid GUID. Use `/modnote list` to see note IDs.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: errorEmbed, ephemeral: true);
                _logger.LogDebug("Note removal failed: invalid GUID format {NoteId}", noteId);
                return;
            }

            // Delete note via service
            var deleted = await _noteService.DeleteNoteAsync(parsedNoteId, Context.User.Id);

            if (!deleted)
            {
                var notFoundEmbed = new EmbedBuilder()
                    .WithTitle("❌ Note Not Found")
                    .WithDescription("No note with that ID was found.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: notFoundEmbed, ephemeral: true);
                _logger.LogDebug("Note removal failed: note {NoteId} not found", parsedNoteId);
                return;
            }

            _logger.LogInformation(
                "Mod note {NoteId} deleted by {ModeratorId}",
                parsedNoteId,
                Context.User.Id);

            // Send confirmation
            var embed = new EmbedBuilder()
                .WithTitle("✅ Note Deleted")
                .WithDescription($"Successfully deleted note `{parsedNoteId}`")
                .WithColor(Color.Green)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed, ephemeral: true);

            _logger.LogDebug("Mod note remove command response sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete mod note {NoteId}", noteId);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to delete note: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }
}
