using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for managing application users with comprehensive authorization and audit logging.
/// </summary>
public class UserManagementService : IUserManagementService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly BotDbContext _dbContext;
    private readonly ILogger<UserManagementService> _logger;

    private static readonly string[] AllRoles = { "SuperAdmin", "Admin", "Moderator", "Viewer" };

    public UserManagementService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        BotDbContext dbContext,
        ILogger<UserManagementService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PaginatedResponseDto<UserDto>> GetUsersAsync(
        UserSearchQueryDto query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching users with search term: {SearchTerm}, role: {Role}, page: {Page}",
            query.SearchTerm, query.Role, query.Page);

        var usersQuery = _userManager.Users.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchLower = query.SearchTerm.ToLower();
            usersQuery = usersQuery.Where(u =>
                u.Email!.ToLower().Contains(searchLower) ||
                (u.DisplayName != null && u.DisplayName.ToLower().Contains(searchLower)) ||
                (u.DiscordUsername != null && u.DiscordUsername.ToLower().Contains(searchLower)));
        }

        // Apply active status filter
        if (query.IsActive.HasValue)
        {
            usersQuery = usersQuery.Where(u => u.IsActive == query.IsActive.Value);
        }

        // Apply Discord linked filter
        if (query.IsDiscordLinked.HasValue)
        {
            usersQuery = query.IsDiscordLinked.Value
                ? usersQuery.Where(u => u.DiscordUserId != null)
                : usersQuery.Where(u => u.DiscordUserId == null);
        }

        // Apply sorting
        usersQuery = query.SortBy.ToLower() switch
        {
            "email" => query.SortDescending
                ? usersQuery.OrderByDescending(u => u.Email)
                : usersQuery.OrderBy(u => u.Email),
            "displayname" => query.SortDescending
                ? usersQuery.OrderByDescending(u => u.DisplayName)
                : usersQuery.OrderBy(u => u.DisplayName),
            "lastlogin" => query.SortDescending
                ? usersQuery.OrderByDescending(u => u.LastLoginAt)
                : usersQuery.OrderBy(u => u.LastLoginAt),
            _ => query.SortDescending
                ? usersQuery.OrderByDescending(u => u.CreatedAt)
                : usersQuery.OrderBy(u => u.CreatedAt)
        };

        // Get total count
        var totalCount = await usersQuery.CountAsync(cancellationToken);

        // Apply pagination
        var users = await usersQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        // Map to DTOs with roles
        var userDtos = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userDtos.Add(MapToDto(user, roles.ToList()));
        }

        // Apply role filter after mapping (since roles are in separate table)
        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            userDtos = userDtos.Where(u => u.Roles.Contains(query.Role)).ToList();
            totalCount = userDtos.Count;
        }

        _logger.LogInformation("Retrieved {Count} users (page {Page} of {TotalPages})",
            userDtos.Count, query.Page, (int)Math.Ceiling((double)totalCount / query.PageSize));

        return new PaginatedResponseDto<UserDto>
        {
            Items = userDtos,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetUserByIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching user by ID: {UserId}", userId);

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User not found: {UserId}", userId);
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        return MapToDto(user, roles.ToList());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAvailableRolesAsync(
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        var currentUser = await _userManager.FindByIdAsync(currentUserId);
        if (currentUser == null)
        {
            return Array.Empty<string>();
        }

        var currentUserRoles = await _userManager.GetRolesAsync(currentUser);
        var highestRole = GetHighestRole(currentUserRoles);

        // SuperAdmin can assign any role, Admin cannot assign SuperAdmin
        return highestRole switch
        {
            "SuperAdmin" => AllRoles,
            "Admin" => new[] { "Admin", "Moderator", "Viewer" },
            _ => Array.Empty<string>()
        };
    }

    /// <inheritdoc />
    public async Task<UserManagementResult> CreateUserAsync(
        UserCreateDto request,
        string actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating new user with email: {Email} by actor: {ActorId}",
            request.Email, actorUserId);

        // Validate password match
        if (request.Password != request.ConfirmPassword)
        {
            return UserManagementResult.Failure(
                UserManagementResult.PasswordValidationFailed,
                "Passwords do not match");
        }

        // Check if email already exists
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            _logger.LogWarning("Attempt to create user with existing email: {Email}", request.Email);
            return UserManagementResult.Failure(
                UserManagementResult.EmailAlreadyExists,
                "A user with this email already exists");
        }

        // Validate role permissions
        var availableRoles = await GetAvailableRolesAsync(actorUserId, cancellationToken);
        if (!availableRoles.Contains(request.Role))
        {
            _logger.LogWarning("Actor {ActorId} attempted to assign unauthorized role: {Role}",
                actorUserId, request.Role);
            return UserManagementResult.Failure(
                UserManagementResult.InsufficientPermissions,
                $"You do not have permission to assign the role: {request.Role}");
        }

        // Create user
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            EmailConfirmed = true, // Auto-confirm admin-created accounts
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            _logger.LogError("Failed to create user {Email}: {Errors}", request.Email, errors);
            return UserManagementResult.Failure(
                UserManagementResult.PasswordValidationFailed,
                errors);
        }

        // Assign role
        var roleResult = await _userManager.AddToRoleAsync(user, request.Role);
        if (!roleResult.Succeeded)
        {
            _logger.LogError("Failed to assign role {Role} to user {UserId}", request.Role, user.Id);
            // User was created but role assignment failed - still return success but log the issue
        }

        // Log activity
        await LogActivityAsync(
            actorUserId,
            user.Id,
            UserActivityAction.UserCreated,
            JsonSerializer.Serialize(new { Email = user.Email, Role = request.Role }),
            ipAddress);

        _logger.LogInformation("Successfully created user {UserId} with email {Email}",
            user.Id, user.Email);

        var roles = await _userManager.GetRolesAsync(user);
        return UserManagementResult.Success(MapToDto(user, roles.ToList()));
    }

    /// <inheritdoc />
    public async Task<UserManagementResult> UpdateUserAsync(
        string userId,
        UserUpdateDto request,
        string actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating user {UserId} by actor {ActorId}", userId, actorUserId);

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return UserManagementResult.Failure(
                UserManagementResult.UserNotFound,
                "User not found");
        }

        var changes = new List<string>();

        // Update display name
        if (request.DisplayName != null && user.DisplayName != request.DisplayName)
        {
            user.DisplayName = request.DisplayName;
            changes.Add($"DisplayName: '{user.DisplayName}' -> '{request.DisplayName}'");
        }

        // Update email
        if (!string.IsNullOrWhiteSpace(request.Email) && user.Email != request.Email)
        {
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null && existingUser.Id != userId)
            {
                return UserManagementResult.Failure(
                    UserManagementResult.EmailAlreadyExists,
                    "A user with this email already exists");
            }

            var oldEmail = user.Email;
            user.Email = request.Email;
            user.UserName = request.Email;
            changes.Add($"Email: '{oldEmail}' -> '{request.Email}'");
        }

        // Update active status
        if (request.IsActive.HasValue && user.IsActive != request.IsActive.Value)
        {
            // Check self-modification
            if (actorUserId == userId)
            {
                _logger.LogWarning("User {UserId} attempted to change their own active status", userId);
                return UserManagementResult.Failure(
                    UserManagementResult.SelfModificationDenied,
                    "You cannot change your own active status");
            }

            user.IsActive = request.IsActive.Value;
            changes.Add($"IsActive: {!request.IsActive.Value} -> {request.IsActive.Value}");
        }

        // Save changes
        if (changes.Any())
        {
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to update user {UserId}: {Errors}", userId, errors);
                return UserManagementResult.Failure("UPDATE_FAILED", errors);
            }

            await LogActivityAsync(
                actorUserId,
                userId,
                UserActivityAction.UserUpdated,
                JsonSerializer.Serialize(changes),
                ipAddress);
        }

        // Handle role change
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var roleResult = await AssignRoleAsync(userId, request.Role, actorUserId, ipAddress, cancellationToken);
            if (!roleResult.Succeeded)
            {
                return roleResult;
            }
        }

        _logger.LogInformation("Successfully updated user {UserId}", userId);

        var roles = await _userManager.GetRolesAsync(user);
        return UserManagementResult.Success(MapToDto(user, roles.ToList()));
    }

    /// <inheritdoc />
    public async Task<UserManagementResult> SetUserActiveStatusAsync(
        string userId,
        bool isActive,
        string actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting user {UserId} active status to {IsActive} by actor {ActorId}",
            userId, isActive, actorUserId);

        // Check self-modification
        if (actorUserId == userId)
        {
            _logger.LogWarning("User {UserId} attempted to change their own active status", userId);
            return UserManagementResult.Failure(
                UserManagementResult.SelfModificationDenied,
                "You cannot disable your own account");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return UserManagementResult.Failure(
                UserManagementResult.UserNotFound,
                "User not found");
        }

        if (user.IsActive == isActive)
        {
            // No change needed
            var roles = await _userManager.GetRolesAsync(user);
            return UserManagementResult.Success(MapToDto(user, roles.ToList()));
        }

        user.IsActive = isActive;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
            _logger.LogError("Failed to update active status for user {UserId}: {Errors}", userId, errors);
            return UserManagementResult.Failure("UPDATE_FAILED", errors);
        }

        await LogActivityAsync(
            actorUserId,
            userId,
            isActive ? UserActivityAction.UserEnabled : UserActivityAction.UserDisabled,
            null,
            ipAddress);

        _logger.LogInformation("Successfully set user {UserId} active status to {IsActive}", userId, isActive);

        var userRoles = await _userManager.GetRolesAsync(user);
        return UserManagementResult.Success(MapToDto(user, userRoles.ToList()));
    }

    /// <inheritdoc />
    public async Task<UserManagementResult> AssignRoleAsync(
        string userId,
        string role,
        string actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Assigning role {Role} to user {UserId} by actor {ActorId}",
            role, userId, actorUserId);

        // Check self-modification for role changes
        if (actorUserId == userId)
        {
            _logger.LogWarning("User {UserId} attempted to change their own role", userId);
            return UserManagementResult.Failure(
                UserManagementResult.SelfModificationDenied,
                "You cannot change your own role");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return UserManagementResult.Failure(
                UserManagementResult.UserNotFound,
                "User not found");
        }

        // Validate role exists
        if (!await _roleManager.RoleExistsAsync(role))
        {
            return UserManagementResult.Failure(
                UserManagementResult.InvalidRole,
                $"Role does not exist: {role}");
        }

        // Validate actor has permission to assign this role
        var availableRoles = await GetAvailableRolesAsync(actorUserId, cancellationToken);
        if (!availableRoles.Contains(role))
        {
            _logger.LogWarning("Actor {ActorId} attempted to assign unauthorized role {Role} to user {UserId}",
                actorUserId, role, userId);
            return UserManagementResult.Failure(
                UserManagementResult.InsufficientPermissions,
                $"You do not have permission to assign the role: {role}");
        }

        // Get current roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        var oldRole = currentRoles.FirstOrDefault();

        // If user already has this role, return success
        if (currentRoles.Contains(role))
        {
            return UserManagementResult.Success(MapToDto(user, currentRoles.ToList()));
        }

        // Remove all existing roles
        if (currentRoles.Any())
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                var errors = string.Join(", ", removeResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to remove existing roles from user {UserId}: {Errors}", userId, errors);
                return UserManagementResult.Failure("ROLE_UPDATE_FAILED", errors);
            }
        }

        // Add new role
        var addResult = await _userManager.AddToRoleAsync(user, role);
        if (!addResult.Succeeded)
        {
            var errors = string.Join(", ", addResult.Errors.Select(e => e.Description));
            _logger.LogError("Failed to assign role {Role} to user {UserId}: {Errors}", role, userId, errors);
            return UserManagementResult.Failure("ROLE_UPDATE_FAILED", errors);
        }

        await LogActivityAsync(
            actorUserId,
            userId,
            UserActivityAction.RoleAssigned,
            JsonSerializer.Serialize(new { OldRole = oldRole, NewRole = role }),
            ipAddress);

        _logger.LogInformation("Successfully assigned role {Role} to user {UserId}", role, userId);

        var newRoles = await _userManager.GetRolesAsync(user);
        return UserManagementResult.Success(MapToDto(user, newRoles.ToList()));
    }

    /// <inheritdoc />
    public async Task<UserManagementResult> RemoveRoleAsync(
        string userId,
        string role,
        string actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Removing role {Role} from user {UserId} by actor {ActorId}",
            role, userId, actorUserId);

        // Check self-modification
        if (actorUserId == userId)
        {
            _logger.LogWarning("User {UserId} attempted to remove their own role", userId);
            return UserManagementResult.Failure(
                UserManagementResult.SelfModificationDenied,
                "You cannot remove your own role");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return UserManagementResult.Failure(
                UserManagementResult.UserNotFound,
                "User not found");
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(role))
        {
            // Role not assigned, return success
            return UserManagementResult.Success(MapToDto(user, currentRoles.ToList()));
        }

        var removeResult = await _userManager.RemoveFromRoleAsync(user, role);
        if (!removeResult.Succeeded)
        {
            var errors = string.Join(", ", removeResult.Errors.Select(e => e.Description));
            _logger.LogError("Failed to remove role {Role} from user {UserId}: {Errors}", role, userId, errors);
            return UserManagementResult.Failure("ROLE_UPDATE_FAILED", errors);
        }

        await LogActivityAsync(
            actorUserId,
            userId,
            UserActivityAction.RoleRemoved,
            JsonSerializer.Serialize(new { RemovedRole = role }),
            ipAddress);

        _logger.LogInformation("Successfully removed role {Role} from user {UserId}", role, userId);

        var newRoles = await _userManager.GetRolesAsync(user);
        return UserManagementResult.Success(MapToDto(user, newRoles.ToList()));
    }

    /// <inheritdoc />
    public async Task<UserManagementResult> ResetPasswordAsync(
        string userId,
        string actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resetting password for user {UserId} by actor {ActorId}",
            userId, actorUserId);

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return UserManagementResult.Failure(
                UserManagementResult.UserNotFound,
                "User not found");
        }

        // Generate a secure temporary password
        var tempPassword = GenerateTemporaryPassword();

        // Remove existing password and set new one
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, token, tempPassword);

        if (!resetResult.Succeeded)
        {
            var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
            _logger.LogError("Failed to reset password for user {UserId}: {Errors}", userId, errors);
            return UserManagementResult.Failure("PASSWORD_RESET_FAILED", errors);
        }

        await LogActivityAsync(
            actorUserId,
            userId,
            UserActivityAction.PasswordReset,
            null, // Never log passwords
            ipAddress);

        _logger.LogInformation("Successfully reset password for user {UserId}", userId);

        var roles = await _userManager.GetRolesAsync(user);
        return UserManagementResult.SuccessWithPassword(tempPassword, MapToDto(user, roles.ToList()));
    }

    /// <inheritdoc />
    public async Task<UserManagementResult> UnlinkDiscordAccountAsync(
        string userId,
        string actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Unlinking Discord account for user {UserId} by actor {ActorId}",
            userId, actorUserId);

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return UserManagementResult.Failure(
                UserManagementResult.UserNotFound,
                "User not found");
        }

        if (user.DiscordUserId == null)
        {
            return UserManagementResult.Failure(
                UserManagementResult.DiscordNotLinked,
                "User does not have a linked Discord account");
        }

        var oldDiscordUsername = user.DiscordUsername;
        user.DiscordUserId = null;
        user.DiscordUsername = null;
        user.DiscordAvatarUrl = null;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
            _logger.LogError("Failed to unlink Discord account for user {UserId}: {Errors}", userId, errors);
            return UserManagementResult.Failure("UPDATE_FAILED", errors);
        }

        await LogActivityAsync(
            actorUserId,
            userId,
            UserActivityAction.DiscordUnlinked,
            JsonSerializer.Serialize(new { PreviousUsername = oldDiscordUsername }),
            ipAddress);

        _logger.LogInformation("Successfully unlinked Discord account for user {UserId}", userId);

        var roles = await _userManager.GetRolesAsync(user);
        return UserManagementResult.Success(MapToDto(user, roles.ToList()));
    }

    /// <inheritdoc />
    public async Task<PaginatedResponseDto<UserActivityLogDto>> GetActivityLogAsync(
        string? userId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching activity log for user: {UserId}, page: {Page}", userId ?? "all", page);

        var query = _dbContext.UserActivityLogs
            .Include(l => l.Actor)
            .Include(l => l.Target)
            .AsQueryable();

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(l => l.TargetUserId == userId || l.ActorUserId == userId);
        }

        query = query.OrderByDescending(l => l.Timestamp);

        var totalCount = await query.CountAsync(cancellationToken);

        var logs = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var logDtos = logs.Select(l => new UserActivityLogDto
        {
            Id = l.Id,
            ActorUserId = l.ActorUserId,
            ActorEmail = l.Actor.Email ?? string.Empty,
            TargetUserId = l.TargetUserId,
            TargetEmail = l.Target?.Email,
            Action = l.Action,
            Details = l.Details,
            Timestamp = l.Timestamp,
            IpAddress = l.IpAddress
        }).ToList();

        return new PaginatedResponseDto<UserActivityLogDto>
        {
            Items = logDtos,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    /// <inheritdoc />
    public async Task<bool> CanManageUserAsync(
        string actorUserId,
        string targetUserId,
        CancellationToken cancellationToken = default)
    {
        // Self-management is restricted
        if (actorUserId == targetUserId)
        {
            return false;
        }

        var actor = await _userManager.FindByIdAsync(actorUserId);
        var target = await _userManager.FindByIdAsync(targetUserId);

        if (actor == null || target == null)
        {
            return false;
        }

        var actorRoles = await _userManager.GetRolesAsync(actor);
        var targetRoles = await _userManager.GetRolesAsync(target);

        var actorHighest = GetHighestRole(actorRoles);
        var targetHighest = GetHighestRole(targetRoles);

        // Only SuperAdmin can manage other SuperAdmins
        if (targetHighest == "SuperAdmin" && actorHighest != "SuperAdmin")
        {
            return false;
        }

        return actorHighest is "SuperAdmin" or "Admin";
    }

    #region Private Helper Methods

    private UserDto MapToDto(ApplicationUser user, List<string> roles)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            IsActive = user.IsActive,
            EmailConfirmed = user.EmailConfirmed,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
            LockoutEnd = user.LockoutEnd,
            IsDiscordLinked = user.DiscordUserId.HasValue,
            DiscordUserId = user.DiscordUserId,
            DiscordUsername = user.DiscordUsername,
            DiscordAvatarUrl = user.DiscordAvatarUrl,
            Roles = roles
        };
    }

    private async Task LogActivityAsync(
        string actorId,
        string? targetId,
        UserActivityAction action,
        string? details = null,
        string? ipAddress = null)
    {
        try
        {
            var log = new UserActivityLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = actorId,
                TargetUserId = targetId,
                Action = action,
                Details = details,
                Timestamp = DateTime.UtcNow,
                IpAddress = ipAddress
            };

            _dbContext.UserActivityLogs.Add(log);
            await _dbContext.SaveChangesAsync();

            _logger.LogDebug("Logged activity: {Action} by {ActorId} on {TargetId}",
                action, actorId, targetId ?? "N/A");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log activity: {Action} by {ActorId}",
                action, actorId);
            // Don't throw - logging failure should not fail the operation
        }
    }

    private static string GetHighestRole(IList<string> roles)
    {
        if (roles.Contains("SuperAdmin")) return "SuperAdmin";
        if (roles.Contains("Admin")) return "Admin";
        if (roles.Contains("Moderator")) return "Moderator";
        if (roles.Contains("Viewer")) return "Viewer";
        return "None";
    }

    private static string GenerateTemporaryPassword()
    {
        // Generate a secure 16-character password
        const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*";
        var random = new Random();
        var password = new char[16];

        // Ensure at least one of each required type
        password[0] = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[random.Next(26)]; // Uppercase
        password[1] = "abcdefghijklmnopqrstuvwxyz"[random.Next(26)]; // Lowercase
        password[2] = "1234567890"[random.Next(10)]; // Digit
        password[3] = "!@#$%^&*"[random.Next(8)]; // Special char

        // Fill the rest randomly
        for (int i = 4; i < 16; i++)
        {
            password[i] = validChars[random.Next(validChars.Length)];
        }

        // Shuffle the array
        for (int i = password.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }

        return new string(password);
    }

    #endregion
}
