using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Autocomplete;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Slash commands for managing moderator tags and applying them to users.
/// Tags are color-coded labels used to mark users (e.g., Trusted, Spammer, VIP).
/// </summary>
[RequireGuildActive]
[RequireModerationEnabled]
[RequireModerator]
[Group("modtag", "Moderator tag commands")]
public class ModTagModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IModTagService _tagService;
    private readonly ILogger<ModTagModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModTagModule"/> class.
    /// </summary>
    public ModTagModule(
        IModTagService tagService,
        ILogger<ModTagModule> logger)
    {
        _tagService = tagService;
        _logger = logger;
    }

    /// <summary>
    /// Applies a tag to a user.
    /// </summary>
    [SlashCommand("add", "Apply a tag to a user")]
    public async Task AddTagToUserAsync(
        [Summary("user", "The user to tag")] IUser user,
        [Summary("tag", "The tag name")]
        [Autocomplete(typeof(ModTagAutocompleteHandler))] string tag)
    {
        _logger.LogInformation(
            "Mod tag add command executed by {ModeratorUsername} (ID: {ModeratorId}) applying tag '{TagName}' to user {TargetUsername} (ID: {TargetId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            tag,
            user.Username,
            user.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        try
        {
            // Prevent tagging bots
            if (user.IsBot)
            {
                var botEmbed = new EmbedBuilder()
                    .WithTitle("❌ Cannot Tag Bots")
                    .WithDescription("Bots cannot be tagged. Please select a human user.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: botEmbed, ephemeral: true);
                _logger.LogDebug("Tag application failed: target is a bot");
                return;
            }

            // Apply tag via service
            var userTag = await _tagService.ApplyTagAsync(
                Context.Guild.Id,
                user.Id,
                tag,
                Context.User.Id);

            if (userTag == null)
            {
                var notFoundEmbed = new EmbedBuilder()
                    .WithTitle("❌ Tag Not Found")
                    .WithDescription($"No tag named `{tag}` exists for this server. Use `/modtag create` to create it.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: notFoundEmbed, ephemeral: true);
                _logger.LogDebug("Tag application failed: tag '{TagName}' not found", tag);
                return;
            }

            _logger.LogInformation(
                "Tag '{TagName}' applied to user {TargetId} by {ModeratorId}",
                tag,
                user.Id,
                Context.User.Id);

            // Build confirmation embed with tag color
            var tagColor = TryParseHexColor(userTag.TagColor, out var color) ? color : Color.Blue;

            var embed = new EmbedBuilder()
                .WithTitle("✅ Tag Applied")
                .WithDescription($"Applied tag **{userTag.TagName}** to <@{user.Id}>")
                .AddField("Tag Category", userTag.TagCategory.ToString(), inline: true)
                .WithColor(tagColor)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed, ephemeral: true);

            _logger.LogDebug("Mod tag add command response sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply tag '{TagName}' to user {TargetId}", tag, user.Id);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to apply tag: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Removes a tag from a user.
    /// </summary>
    [SlashCommand("remove", "Remove a tag from a user")]
    public async Task RemoveTagFromUserAsync(
        [Summary("user", "The user to remove the tag from")] IUser user,
        [Summary("tag", "The tag name")]
        [Autocomplete(typeof(UserModTagAutocompleteHandler))] string tag)
    {
        _logger.LogInformation(
            "Mod tag remove command executed by {ModeratorUsername} (ID: {ModeratorId}) removing tag '{TagName}' from user {TargetUsername} (ID: {TargetId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            tag,
            user.Username,
            user.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        try
        {
            // Remove tag via service
            var removed = await _tagService.RemoveTagAsync(Context.Guild.Id, user.Id, tag);

            if (!removed)
            {
                var notFoundEmbed = new EmbedBuilder()
                    .WithTitle("❌ Tag Not Found")
                    .WithDescription($"User <@{user.Id}> does not have the tag `{tag}`.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: notFoundEmbed, ephemeral: true);
                _logger.LogDebug("Tag removal failed: user {TargetId} does not have tag '{TagName}'", user.Id, tag);
                return;
            }

            _logger.LogInformation(
                "Tag '{TagName}' removed from user {TargetId} by {ModeratorId}",
                tag,
                user.Id,
                Context.User.Id);

            // Build confirmation embed
            var embed = new EmbedBuilder()
                .WithTitle("✅ Tag Removed")
                .WithDescription($"Removed tag **{tag}** from <@{user.Id}>")
                .WithColor(Color.Green)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed, ephemeral: true);

            _logger.LogDebug("Mod tag remove command response sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove tag '{TagName}' from user {TargetId}", tag, user.Id);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to remove tag: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Lists tags for a specific user, or all tags available in the guild.
    /// </summary>
    [SlashCommand("list", "List tags")]
    public async Task ListAsync(
        [Summary("user", "User to view tags for (omit for all guild tags)")] IUser? user = null)
    {
        _logger.LogInformation(
            "Mod tag list command executed by {ModeratorUsername} (ID: {ModeratorId}) for {Target} in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            user != null ? $"user {user.Username} (ID: {user.Id})" : "all guild tags",
            Context.Guild.Name,
            Context.Guild.Id);

        try
        {
            if (user != null)
            {
                // Get user tags
                var userTags = (await _tagService.GetUserTagsAsync(Context.Guild.Id, user.Id)).ToList();

                _logger.LogDebug("Retrieved {TagCount} tags for user {TargetId}", userTags.Count, user.Id);

                var embed = new EmbedBuilder()
                    .WithTitle($"🏷️ Tags: {user.Username}")
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp();

                if (userTags.Count == 0)
                {
                    embed.WithDescription("This user has no tags.");
                }
                else
                {
                    // Group by category
                    var positive = userTags.Where(t => t.TagCategory == TagCategory.Positive).ToList();
                    var negative = userTags.Where(t => t.TagCategory == TagCategory.Negative).ToList();
                    var neutral = userTags.Where(t => t.TagCategory == TagCategory.Neutral).ToList();

                    if (positive.Count > 0)
                    {
                        embed.AddField(
                            "✅ Positive Tags",
                            string.Join(", ", positive.Select(t => $"`{t.TagName}`")),
                            inline: false);
                    }

                    if (negative.Count > 0)
                    {
                        embed.AddField(
                            "⚠️ Negative Tags",
                            string.Join(", ", negative.Select(t => $"`{t.TagName}`")),
                            inline: false);
                    }

                    if (neutral.Count > 0)
                    {
                        embed.AddField(
                            "ℹ️ Neutral Tags",
                            string.Join(", ", neutral.Select(t => $"`{t.TagName}`")),
                            inline: false);
                    }
                }

                await RespondAsync(embed: embed.Build(), ephemeral: true);
            }
            else
            {
                // Get all guild tags
                var guildTags = (await _tagService.GetGuildTagsAsync(Context.Guild.Id)).ToList();

                _logger.LogDebug("Retrieved {TagCount} guild tags", guildTags.Count);

                var embed = new EmbedBuilder()
                    .WithTitle("🏷️ Available Tags")
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp();

                if (guildTags.Count == 0)
                {
                    embed.WithDescription("No tags have been created for this server. Use `/modtag create` to create one.");
                }
                else
                {
                    // Group by category
                    var positive = guildTags.Where(t => t.Category == TagCategory.Positive).ToList();
                    var negative = guildTags.Where(t => t.Category == TagCategory.Negative).ToList();
                    var neutral = guildTags.Where(t => t.Category == TagCategory.Neutral).ToList();

                    if (positive.Count > 0)
                    {
                        var tagList = string.Join("\n", positive.Select(t =>
                            $"• `{t.Name}` — {(string.IsNullOrEmpty(t.Description) ? "No description" : t.Description)}"));
                        embed.AddField("✅ Positive Tags", tagList, inline: false);
                    }

                    if (negative.Count > 0)
                    {
                        var tagList = string.Join("\n", negative.Select(t =>
                            $"• `{t.Name}` — {(string.IsNullOrEmpty(t.Description) ? "No description" : t.Description)}"));
                        embed.AddField("⚠️ Negative Tags", tagList, inline: false);
                    }

                    if (neutral.Count > 0)
                    {
                        var tagList = string.Join("\n", neutral.Select(t =>
                            $"• `{t.Name}` — {(string.IsNullOrEmpty(t.Description) ? "No description" : t.Description)}"));
                        embed.AddField("ℹ️ Neutral Tags", tagList, inline: false);
                    }

                    embed.WithFooter($"Total: {guildTags.Count} tags");
                }

                await RespondAsync(embed: embed.Build(), ephemeral: true);
            }

            _logger.LogDebug("Mod tag list command response sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list tags");

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to retrieve tags: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Creates a new moderator tag for the server.
    /// Requires admin permissions.
    /// </summary>
    [SlashCommand("create", "Create a new tag for this server")]
    [RequireAdmin]
    public async Task CreateAsync(
        [Summary("name", "Tag name")] string name,
        [Summary("color", "Hex color (e.g., #FF5733)")] string color,
        [Summary("category", "Tag category")]
        [Choice("Positive", "Positive")]
        [Choice("Negative", "Negative")]
        [Choice("Neutral", "Neutral")] string category = "Neutral",
        [Summary("description", "Tag description")] string? description = null)
    {
        _logger.LogInformation(
            "Mod tag create command executed by {ModeratorUsername} (ID: {ModeratorId}) creating tag '{TagName}' in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            name,
            Context.Guild.Name,
            Context.Guild.Id);

        try
        {
            // Validate tag name
            if (string.IsNullOrWhiteSpace(name))
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Invalid Tag Name")
                    .WithDescription("Tag name cannot be empty.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: errorEmbed, ephemeral: true);
                _logger.LogDebug("Tag creation failed: empty name");
                return;
            }

            // Validate color format (must be hex like #FF5733)
            if (!System.Text.RegularExpressions.Regex.IsMatch(color, @"^#[0-9A-Fa-f]{6}$"))
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Invalid Color Format")
                    .WithDescription("Color must be in hex format (e.g., `#FF5733`).")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: errorEmbed, ephemeral: true);
                _logger.LogDebug("Tag creation failed: invalid color format '{Color}'", color);
                return;
            }

            // Parse category
            if (!Enum.TryParse<TagCategory>(category, out var tagCategory))
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Invalid Category")
                    .WithDescription("Category must be Positive, Negative, or Neutral.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: errorEmbed, ephemeral: true);
                _logger.LogDebug("Tag creation failed: invalid category '{Category}'", category);
                return;
            }

            // Create tag via service
            var createDto = new ModTagCreateDto
            {
                GuildId = Context.Guild.Id,
                Name = name,
                Color = color.ToUpper(), // Normalize to uppercase
                Category = tagCategory,
                Description = description
            };

            var createdTag = await _tagService.CreateTagAsync(Context.Guild.Id, createDto);

            _logger.LogInformation(
                "Tag '{TagName}' created by {ModeratorId} with ID {TagId}",
                name,
                Context.User.Id,
                createdTag.Id);

            // Build confirmation embed with tag preview
            var tagColor = TryParseHexColor(createdTag.Color, out var embedColor) ? embedColor : Color.Blue;

            var embed = new EmbedBuilder()
                .WithTitle("✅ Tag Created")
                .WithDescription($"Created tag **{createdTag.Name}**")
                .AddField("Category", createdTag.Category.ToString(), inline: true)
                .AddField("Color", createdTag.Color, inline: true)
                .WithColor(tagColor)
                .WithCurrentTimestamp()
                .Build();

            if (!string.IsNullOrEmpty(createdTag.Description))
            {
                embed = embed.ToEmbedBuilder()
                    .AddField("Description", createdTag.Description, inline: false)
                    .Build();
            }

            await RespondAsync(embed: embed, ephemeral: true);

            _logger.LogDebug("Mod tag create command response sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create tag '{TagName}'", name);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to create tag: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Deletes a tag from the server.
    /// All user assignments of this tag will be removed.
    /// Requires admin permissions.
    /// </summary>
    [SlashCommand("delete", "Delete a tag from this server")]
    [RequireAdmin]
    public async Task DeleteAsync(
        [Summary("tag", "The tag name to delete")]
        [Autocomplete(typeof(ModTagAutocompleteHandler))] string tag)
    {
        _logger.LogInformation(
            "Mod tag delete command executed by {ModeratorUsername} (ID: {ModeratorId}) deleting tag '{TagName}' in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            tag,
            Context.Guild.Name,
            Context.Guild.Id);

        try
        {
            // Get tag info before deletion to show user count
            var tagInfo = await _tagService.GetTagByNameAsync(Context.Guild.Id, tag);

            if (tagInfo == null)
            {
                var notFoundEmbed = new EmbedBuilder()
                    .WithTitle("❌ Tag Not Found")
                    .WithDescription($"No tag named `{tag}` exists for this server.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: notFoundEmbed, ephemeral: true);
                _logger.LogDebug("Tag deletion failed: tag '{TagName}' not found", tag);
                return;
            }

            // Delete tag via service
            var deleted = await _tagService.DeleteTagAsync(Context.Guild.Id, tag);

            if (!deleted)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Deletion Failed")
                    .WithDescription("Failed to delete the tag. It may have already been deleted.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: errorEmbed, ephemeral: true);
                _logger.LogWarning("Tag deletion failed unexpectedly for tag '{TagName}'", tag);
                return;
            }

            _logger.LogInformation(
                "Tag '{TagName}' deleted by {ModeratorId} (removed from {UserCount} users)",
                tag,
                Context.User.Id,
                tagInfo.UserCount);

            // Build confirmation with warning about user assignments
            var embed = new EmbedBuilder()
                .WithTitle("✅ Tag Deleted")
                .WithDescription($"Successfully deleted tag **{tag}**")
                .WithColor(Color.Green)
                .WithCurrentTimestamp();

            if (tagInfo.UserCount > 0)
            {
                embed.WithFooter($"Removed from {tagInfo.UserCount} user{(tagInfo.UserCount != 1 ? "s" : "")}");
            }

            await RespondAsync(embed: embed.Build(), ephemeral: true);

            _logger.LogDebug("Mod tag delete command response sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete tag '{TagName}'", tag);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to delete tag: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Attempts to parse a hex color string into a Discord Color.
    /// </summary>
    private static bool TryParseHexColor(string hexColor, out Color color)
    {
        color = Color.Default;

        if (string.IsNullOrWhiteSpace(hexColor) || !hexColor.StartsWith('#') || hexColor.Length != 7)
        {
            return false;
        }

        try
        {
            var r = Convert.ToByte(hexColor.Substring(1, 2), 16);
            var g = Convert.ToByte(hexColor.Substring(3, 2), 16);
            var b = Convert.ToByte(hexColor.Substring(5, 2), 16);
            color = new Color(r, g, b);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
