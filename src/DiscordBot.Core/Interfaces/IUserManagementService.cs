using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for user management operations.
/// All methods enforce authorization rules and log activities.
/// </summary>
public interface IUserManagementService
{
    // Query operations
    Task<PaginatedResponseDto<UserDto>> GetUsersAsync(
        UserSearchQueryDto query,
        CancellationToken cancellationToken = default);

    Task<UserDto?> GetUserByIdAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAvailableRolesAsync(
        string currentUserId,
        CancellationToken cancellationToken = default);

    // Create operations
    Task<UserManagementResult> CreateUserAsync(
        UserCreateDto request,
        string actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    // Update operations
    Task<UserManagementResult> UpdateUserAsync(
        string userId,
        UserUpdateDto request,
        string actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task<UserManagementResult> SetUserActiveStatusAsync(
        string userId,
        bool isActive,
        string actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task<UserManagementResult> AssignRoleAsync(
        string userId,
        string role,
        string actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task<UserManagementResult> RemoveRoleAsync(
        string userId,
        string role,
        string actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    // Password operations
    Task<UserManagementResult> ResetPasswordAsync(
        string userId,
        string actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    // Discord linking operations
    Task<UserManagementResult> UnlinkDiscordAccountAsync(
        string userId,
        string actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    // Activity log
    Task<PaginatedResponseDto<UserActivityLogDto>> GetActivityLogAsync(
        string? userId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    // Validation
    Task<bool> CanManageUserAsync(
        string actorUserId,
        string targetUserId,
        CancellationToken cancellationToken = default);
}
