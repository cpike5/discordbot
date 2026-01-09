using Discord.WebSocket;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Moderation;
using System.Text.RegularExpressions;

namespace DiscordBot.Bot.Services.Moderation;

/// <summary>
/// Service implementation for managing moderation tags and their application to users.
/// Handles tag creation, deletion, user tag assignment, and template imports.
/// </summary>
public class ModTagService : IModTagService
{
    private readonly IModTagRepository _tagRepository;
    private readonly IUserModTagRepository _userTagRepository;
    private readonly DiscordSocketClient _client;
    private readonly ILogger<ModTagService> _logger;

    // Regex to validate hex color format (#RRGGBB)
    private static readonly Regex HexColorRegex = new(@"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    public ModTagService(
        IModTagRepository tagRepository,
        IUserModTagRepository userTagRepository,
        DiscordSocketClient client,
        ILogger<ModTagService> logger)
    {
        _tagRepository = tagRepository;
        _userTagRepository = userTagRepository;
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ModTagDto> CreateTagAsync(ulong guildId, ModTagCreateDto dto, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "mod_tag",
            "create_tag",
            guildId: guildId);

        try
        {
            _logger.LogInformation("Creating mod tag '{TagName}' in guild {GuildId}", dto.Name, guildId);

            // Validate color format
            if (!HexColorRegex.IsMatch(dto.Color))
            {
                _logger.LogWarning("Invalid color format for tag '{TagName}': {Color}", dto.Name, dto.Color);
                throw new ArgumentException($"Color must be in hex format (#RRGGBB). Got: {dto.Color}", nameof(dto.Color));
            }

            // Check if tag with same name already exists
            var existing = await _tagRepository.GetByNameAsync(guildId, dto.Name, ct);
            if (existing != null)
            {
                _logger.LogWarning("Tag '{TagName}' already exists in guild {GuildId}", dto.Name, guildId);
                throw new InvalidOperationException($"A tag with the name '{dto.Name}' already exists.");
            }

            var tag = new ModTag
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                Name = dto.Name,
                Color = dto.Color.ToUpperInvariant(),
                Category = dto.Category,
                Description = dto.Description,
                IsFromTemplate = false,
                CreatedAt = DateTime.UtcNow
            };

            await _tagRepository.AddAsync(tag, ct);

            _logger.LogInformation("Mod tag '{TagName}' created successfully in guild {GuildId} with ID {TagId}",
                dto.Name, guildId, tag.Id);

            var result = await MapToDtoAsync(tag, ct);

            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteTagAsync(ulong guildId, string tagName, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "mod_tag",
            "delete_tag",
            guildId: guildId);

        try
        {
            _logger.LogInformation("Deleting mod tag '{TagName}' in guild {GuildId}", tagName, guildId);

            var tag = await _tagRepository.GetByNameAsync(guildId, tagName, ct);
            if (tag == null)
            {
                _logger.LogWarning("Mod tag '{TagName}' not found in guild {GuildId}", tagName, guildId);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            // Delete the tag (cascade will remove all user tag assignments)
            await _tagRepository.DeleteAsync(tag, ct);

            _logger.LogInformation("Mod tag '{TagName}' deleted successfully from guild {GuildId}", tagName, guildId);

            BotActivitySource.SetSuccess(activity);
            return true;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ModTagDto>> GetGuildTagsAsync(ulong guildId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "mod_tag",
            "get_guild_tags",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Retrieving all mod tags for guild {GuildId}", guildId);

            var tags = await _tagRepository.GetByGuildAsync(guildId, ct);
            var tagsList = tags.ToList();

            var dtos = new List<ModTagDto>();
            foreach (var tag in tagsList)
            {
                dtos.Add(await MapToDtoAsync(tag, ct));
            }

            _logger.LogDebug("Retrieved {Count} mod tags for guild {GuildId}", dtos.Count, guildId);

            BotActivitySource.SetRecordsReturned(activity, dtos.Count);
            BotActivitySource.SetSuccess(activity);
            return dtos;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ModTagDto?> GetTagByNameAsync(ulong guildId, string name, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "mod_tag",
            "get_tag_by_name",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Retrieving mod tag '{TagName}' in guild {GuildId}", name, guildId);

            var tag = await _tagRepository.GetByNameAsync(guildId, name, ct);
            if (tag == null)
            {
                _logger.LogWarning("Mod tag '{TagName}' not found in guild {GuildId}", name, guildId);
                BotActivitySource.SetSuccess(activity);
                return null;
            }

            var result = await MapToDtoAsync(tag, ct);

            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<UserModTagDto?> ApplyTagAsync(ulong guildId, ulong userId, string tagName, ulong appliedById, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "mod_tag",
            "apply_tag",
            guildId: guildId,
            userId: userId);

        try
        {
            _logger.LogInformation("Applying tag '{TagName}' to user {UserId} in guild {GuildId} by moderator {AppliedById}",
                tagName, userId, guildId, appliedById);

            // Get the tag
            var tag = await _tagRepository.GetByNameAsync(guildId, tagName, ct);
            if (tag == null)
            {
                _logger.LogWarning("Mod tag '{TagName}' not found in guild {GuildId}", tagName, guildId);
                BotActivitySource.SetSuccess(activity);
                return null;
            }

            // Check if tag is already applied
            var existing = await _userTagRepository.ExistsAsync(guildId, userId, tag.Id, ct);
            if (existing)
            {
                _logger.LogWarning("Tag '{TagName}' is already applied to user {UserId} in guild {GuildId}",
                    tagName, userId, guildId);
                throw new InvalidOperationException($"Tag '{tagName}' is already applied to this user.");
            }

            var userTag = new UserModTag
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                UserId = userId,
                TagId = tag.Id,
                AppliedByUserId = appliedById,
                AppliedAt = DateTime.UtcNow
                // Note: Do NOT set Tag navigation property here - it causes EF Core tracking
                // errors when the tag's Guild is already tracked. The foreign key is sufficient.
            };

            await _userTagRepository.AddAsync(userTag, ct);

            _logger.LogInformation("Tag '{TagName}' applied successfully to user {UserId} in guild {GuildId}",
                tagName, userId, guildId);

            var result = await MapUserTagToDtoAsync(userTag, tag, ct);

            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveTagAsync(ulong guildId, ulong userId, string tagName, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "mod_tag",
            "remove_tag",
            guildId: guildId,
            userId: userId);

        try
        {
            _logger.LogInformation("Removing tag '{TagName}' from user {UserId} in guild {GuildId}",
                tagName, userId, guildId);

            // Get the tag
            var tag = await _tagRepository.GetByNameAsync(guildId, tagName, ct);
            if (tag == null)
            {
                _logger.LogWarning("Mod tag '{TagName}' not found in guild {GuildId}", tagName, guildId);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            // Get the user tag assignment
            var userTag = await _userTagRepository.GetAssignmentAsync(guildId, userId, tag.Id, ct);
            if (userTag == null)
            {
                _logger.LogWarning("Tag '{TagName}' is not applied to user {UserId} in guild {GuildId}",
                    tagName, userId, guildId);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            await _userTagRepository.DeleteAsync(userTag, ct);

            _logger.LogInformation("Tag '{TagName}' removed successfully from user {UserId} in guild {GuildId}",
                tagName, userId, guildId);

            BotActivitySource.SetSuccess(activity);
            return true;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<UserModTagDto>> GetUserTagsAsync(ulong guildId, ulong userId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "mod_tag",
            "get_user_tags",
            guildId: guildId,
            userId: userId);

        try
        {
            _logger.LogDebug("Retrieving tags for user {UserId} in guild {GuildId}", userId, guildId);

            var userTags = await _userTagRepository.GetByUserAsync(guildId, userId, ct);
            var userTagsList = userTags.ToList();

            var dtos = new List<UserModTagDto>();
            foreach (var userTag in userTagsList)
            {
                dtos.Add(await MapUserTagToDtoAsync(userTag, null, ct));
            }

            _logger.LogDebug("Retrieved {Count} tags for user {UserId} in guild {GuildId}",
                dtos.Count, userId, guildId);

            BotActivitySource.SetRecordsReturned(activity, dtos.Count);
            BotActivitySource.SetSuccess(activity);
            return dtos;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<int> ImportTemplateTagsAsync(ulong guildId, IEnumerable<string> templateNames, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "mod_tag",
            "import_template_tags",
            guildId: guildId);

        try
        {
            _logger.LogInformation("Importing template tags for guild {GuildId}: {Templates}",
                guildId, string.Join(", ", templateNames));

            var imported = 0;

            foreach (var templateName in templateNames)
            {
                var template = ModTagTemplates.GetByName(templateName);
                if (template == null)
                {
                    _logger.LogWarning("Template tag '{TemplateName}' not found", templateName);
                    continue;
                }

                // Check if tag already exists
                var exists = await _tagRepository.NameExistsAsync(guildId, template.Name, ct);
                if (exists)
                {
                    _logger.LogDebug("Tag '{TagName}' already exists in guild {GuildId}, skipping", template.Name, guildId);
                    continue;
                }

                var tag = new ModTag
                {
                    Id = Guid.NewGuid(),
                    GuildId = guildId,
                    Name = template.Name,
                    Color = template.Color,
                    Category = template.Category,
                    Description = template.Description,
                    IsFromTemplate = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _tagRepository.AddAsync(tag, ct);
                imported++;

                _logger.LogDebug("Imported template tag '{TagName}' to guild {GuildId}", template.Name, guildId);
            }

            _logger.LogInformation("Imported {Count} template tags to guild {GuildId}", imported, guildId);

            BotActivitySource.SetRecordsReturned(activity, imported);
            BotActivitySource.SetSuccess(activity);
            return imported;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Maps a ModTag entity to a DTO.
    /// </summary>
    private async Task<ModTagDto> MapToDtoAsync(ModTag tag, CancellationToken ct = default)
    {
        // Get count of users with this tag
        var userTags = await _userTagRepository.GetByUserAsync(tag.GuildId, 0, ct); // This won't work - need a different method
        // For now, we'll set UserCount to 0 - repository needs a GetByTagAsync method for proper count

        return new ModTagDto
        {
            Id = tag.Id,
            GuildId = tag.GuildId,
            Name = tag.Name,
            Color = tag.Color,
            Category = tag.Category,
            Description = tag.Description,
            IsFromTemplate = tag.IsFromTemplate,
            CreatedAt = tag.CreatedAt,
            UserCount = 0 // TODO: Implement proper count once repository method is available
        };
    }

    /// <summary>
    /// Maps a UserModTag entity to a DTO with resolved usernames.
    /// </summary>
    /// <param name="userTag">The user tag entity to map.</param>
    /// <param name="tag">Optional tag entity to use if navigation property is not loaded.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task<UserModTagDto> MapUserTagToDtoAsync(UserModTag userTag, ModTag? tag = null, CancellationToken ct = default)
    {
        var username = await GetUsernameAsync(userTag.UserId);
        var appliedByUsername = await GetUsernameAsync(userTag.AppliedByUserId);

        // Use provided tag or fall back to navigation property
        var resolvedTag = tag ?? userTag.Tag;

        return new UserModTagDto
        {
            Id = userTag.Id,
            GuildId = userTag.GuildId,
            UserId = userTag.UserId,
            Username = username,
            TagId = userTag.TagId,
            TagName = resolvedTag?.Name ?? string.Empty,
            TagColor = resolvedTag?.Color ?? string.Empty,
            TagCategory = resolvedTag?.Category ?? default,
            AppliedByUserId = userTag.AppliedByUserId,
            AppliedByUsername = appliedByUsername,
            AppliedAt = userTag.AppliedAt
        };
    }

    /// <summary>
    /// Resolves a Discord user ID to username.
    /// </summary>
    private async Task<string> GetUsernameAsync(ulong userId)
    {
        try
        {
            var user = await _client.Rest.GetUserAsync(userId);
            return user?.Username ?? $"Unknown#{userId}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve username for user {UserId}", userId);
            return $"Unknown#{userId}";
        }
    }
}
