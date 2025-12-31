# Issue #66 - User Management (Admin Only)

## Implementation Plan

**Document Version:** 1.0
**Date:** 2025-12-09
**Issue Reference:** GitHub Issue #66
**Epic Reference:** Epic 2: Authentication and Authorization (#63)
**Dependencies:** Feature 2.2 - Authorization Policies (#65) - COMPLETED

---

## 1. Requirement Summary

Implement a comprehensive user management system for administrators to manage application users. This feature provides admin-only pages for:

- Listing all users with their roles and Discord link status
- Creating new user accounts
- Editing user details and role assignments
- Enabling/disabling user accounts
- Admin-initiated password resets
- Viewing and unlinking Discord account associations
- Self-protection (preventing admins from deleting or demoting themselves)
- Activity logging for all user management actions

---

## 2. Architectural Considerations

### 2.1 Existing System Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `ApplicationUser` | `src/DiscordBot.Core/Entities/ApplicationUser.cs` | User entity extending IdentityUser with Discord linking support |
| `UserGuildAccess` | `src/DiscordBot.Core/Entities/UserGuildAccess.cs` | Guild-specific permissions for users |
| `SignInManager<ApplicationUser>` | ASP.NET Identity | Authentication operations |
| `UserManager<ApplicationUser>` | ASP.NET Identity | User CRUD operations |
| `RoleManager<IdentityRole>` | ASP.NET Identity | Role management |
| Authorization Policies | `Program.cs` | `RequireAdmin`, `RequireSuperAdmin` policies |
| Pagination Component | `Pages/Shared/Components/_Pagination.cshtml` | Reusable pagination UI |
| Form Components | `Pages/Shared/Components/_FormInput.cshtml`, `_FormSelect.cshtml` | Reusable form inputs |

### 2.2 Integration Requirements

1. **ASP.NET Identity Integration**
   - All user operations MUST use `UserManager<ApplicationUser>` - never modify entities directly
   - Role operations MUST use `UserManager.AddToRoleAsync` / `RemoveFromRoleAsync`
   - Password resets MUST use `UserManager.GeneratePasswordResetTokenAsync`

2. **Authorization Integration**
   - All admin pages require `[Authorize(Policy = "RequireAdmin")]`
   - SuperAdmin-only operations (like editing SuperAdmin users) require additional checks
   - Self-protection checks must occur at the service layer

3. **Existing Component Reuse**
   - Use existing `_Pagination.cshtml` partial with `PaginationViewModel`
   - Use existing `_FormInput.cshtml` and `_FormSelect.cshtml` components
   - Use existing `_Badge.cshtml` for role and status display
   - Use existing `_Alert.cshtml` for success/error messages

### 2.3 Architectural Patterns to Follow

Based on existing codebase patterns:

```
Pattern: Service Layer Interface in Core, Implementation in Bot/Services
Example: IGuildService (Core) -> GuildService (Bot/Services)

Pattern: DTOs for data transfer between layers
Example: GuildDto, PaginatedResponseDto<T>

Pattern: ViewModels for Razor Pages
Example: GuildListViewModel, PaginationViewModel
```

### 2.4 Security Considerations

| Risk | Mitigation |
|------|------------|
| Self-demotion | Service layer prevents users from removing Admin role from themselves |
| Self-deletion | Service layer prevents users from deactivating their own account |
| Privilege escalation | Only SuperAdmins can assign SuperAdmin role |
| Unauthorized access | All pages protected with `[Authorize(Policy = "RequireAdmin")]` |
| Audit trail gaps | All user management actions logged with actor, target, and action details |
| CSRF attacks | All forms use anti-forgery tokens (default in Razor Pages) |

### 2.5 Role Hierarchy

```
SuperAdmin (highest)
    |
  Admin
    |
Moderator
    |
 Viewer (lowest)
```

**Role Assignment Rules:**
- SuperAdmin can assign any role
- Admin can assign Admin, Moderator, Viewer (not SuperAdmin)
- Only SuperAdmin can manage other SuperAdmin users

---

## 3. Data Models

### 3.1 DTOs (New Files)

#### `UserDto.cs`

**Location:** `src/DiscordBot.Core/DTOs/UserDto.cs`

```csharp
namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for user information in listings and details views.
/// </summary>
public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool IsActive { get; set; }
    public bool EmailConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }

    // Discord linking
    public bool IsDiscordLinked { get; set; }
    public ulong? DiscordUserId { get; set; }
    public string? DiscordUsername { get; set; }
    public string? DiscordAvatarUrl { get; set; }

    // Roles
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    // Computed properties
    public string HighestRole => Roles.OrderByDescending(RolePriority).FirstOrDefault() ?? "None";

    private static int RolePriority(string role) => role switch
    {
        "SuperAdmin" => 4,
        "Admin" => 3,
        "Moderator" => 2,
        "Viewer" => 1,
        _ => 0
    };
}
```

#### `UserCreateDto.cs`

**Location:** `src/DiscordBot.Core/DTOs/UserCreateDto.cs`

```csharp
namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for creating a new user.
/// </summary>
public class UserCreateDto
{
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string Role { get; set; } = "Viewer";
    public bool SendWelcomeEmail { get; set; } = true;
}
```

#### `UserUpdateDto.cs`

**Location:** `src/DiscordBot.Core/DTOs/UserUpdateDto.cs`

```csharp
namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for updating an existing user.
/// </summary>
public class UserUpdateDto
{
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public bool? IsActive { get; set; }
    public string? Role { get; set; }
}
```

#### `UserActivityLogDto.cs`

**Location:** `src/DiscordBot.Core/DTOs/UserActivityLogDto.cs`

```csharp
namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for user activity log entries.
/// </summary>
public class UserActivityLogDto
{
    public Guid Id { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorEmail { get; set; } = string.Empty;
    public string? TargetUserId { get; set; }
    public string? TargetEmail { get; set; }
    public UserActivityAction Action { get; set; }
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
}
```

#### `UserSearchQueryDto.cs`

**Location:** `src/DiscordBot.Core/DTOs/UserSearchQueryDto.cs`

```csharp
namespace DiscordBot.Core.DTOs;

/// <summary>
/// Query parameters for searching and filtering users.
/// </summary>
public class UserSearchQueryDto
{
    public string? SearchTerm { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsDiscordLinked { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
}
```

### 3.2 Entities (New Files)

#### `UserActivityLog.cs`

**Location:** `src/DiscordBot.Core/Entities/UserActivityLog.cs`

```csharp
namespace DiscordBot.Core.Entities;

/// <summary>
/// Audit log entry for user management actions.
/// </summary>
public class UserActivityLog
{
    public Guid Id { get; set; }

    /// <summary>
    /// The user who performed the action.
    /// </summary>
    public string ActorUserId { get; set; } = string.Empty;
    public ApplicationUser Actor { get; set; } = null!;

    /// <summary>
    /// The user affected by the action (null for non-user-specific actions).
    /// </summary>
    public string? TargetUserId { get; set; }
    public ApplicationUser? Target { get; set; }

    /// <summary>
    /// The type of action performed.
    /// </summary>
    public UserActivityAction Action { get; set; }

    /// <summary>
    /// Additional details about the action (JSON or descriptive text).
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// When the action occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// IP address of the actor (for security auditing).
    /// </summary>
    public string? IpAddress { get; set; }
}

/// <summary>
/// Types of user management actions that are logged.
/// </summary>
public enum UserActivityAction
{
    UserCreated,
    UserUpdated,
    UserDeleted,
    UserEnabled,
    UserDisabled,
    RoleAssigned,
    RoleRemoved,
    PasswordReset,
    DiscordLinked,
    DiscordUnlinked,
    AccountLocked,
    AccountUnlocked,
    LoginSuccess,
    LoginFailed
}
```

### 3.3 Database Migration Required

Add `UserActivityLog` DbSet to `BotDbContext` and create migration:

```bash
dotnet ef migrations add AddUserActivityLog --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

---

## 4. Service Layer

### 4.1 Interface Definition

**Location:** `src/DiscordBot.Core/Interfaces/IUserManagementService.cs`

```csharp
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
```

### 4.2 Result Type

**Location:** `src/DiscordBot.Core/DTOs/UserManagementResult.cs`

```csharp
namespace DiscordBot.Core.DTOs;

/// <summary>
/// Result of a user management operation.
/// </summary>
public class UserManagementResult
{
    public bool Succeeded { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public UserDto? User { get; private set; }
    public string? GeneratedPassword { get; private set; }

    public static UserManagementResult Success(UserDto? user = null) => new()
    {
        Succeeded = true,
        User = user
    };

    public static UserManagementResult SuccessWithPassword(string password, UserDto user) => new()
    {
        Succeeded = true,
        User = user,
        GeneratedPassword = password
    };

    public static UserManagementResult Failure(string errorCode, string errorMessage) => new()
    {
        Succeeded = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    // Common error codes
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string SelfModificationDenied = "SELF_MODIFICATION_DENIED";
    public const string InsufficientPermissions = "INSUFFICIENT_PERMISSIONS";
    public const string InvalidRole = "INVALID_ROLE";
    public const string EmailAlreadyExists = "EMAIL_ALREADY_EXISTS";
    public const string PasswordValidationFailed = "PASSWORD_VALIDATION_FAILED";
    public const string DiscordNotLinked = "DISCORD_NOT_LINKED";
}
```

### 4.3 Service Implementation

**Location:** `src/DiscordBot.Bot/Services/UserManagementService.cs`

The implementation will:

1. Use `UserManager<ApplicationUser>` for all user operations
2. Use `RoleManager<IdentityRole>` for role queries
3. Inject `BotDbContext` for activity logging
4. Inject `ILogger<UserManagementService>` for logging
5. Implement all self-protection checks
6. Log all operations to `UserActivityLog`

**Key Implementation Details:**

```csharp
// Self-protection check example
private async Task<bool> IsSelfModificationDenied(string actorId, string targetId, string operation)
{
    if (actorId == targetId)
    {
        _logger.LogWarning("User {UserId} attempted to {Operation} their own account", actorId, operation);
        return true;
    }
    return false;
}

// Role hierarchy check example
private bool CanAssignRole(IList<string> actorRoles, string targetRole)
{
    var actorHighest = GetHighestRole(actorRoles);
    return actorHighest switch
    {
        "SuperAdmin" => true, // Can assign any role
        "Admin" => targetRole != "SuperAdmin", // Cannot assign SuperAdmin
        _ => false // Non-admins cannot assign roles
    };
}

// Activity logging example
private async Task LogActivityAsync(
    string actorId,
    string? targetId,
    UserActivityAction action,
    string? details = null,
    string? ipAddress = null)
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
}
```

---

## 5. ViewModels

### 5.1 User List ViewModel

**Location:** `src/DiscordBot.Bot/ViewModels/Pages/UserListViewModel.cs`

```csharp
namespace DiscordBot.Bot.ViewModels.Pages;

public class UserListViewModel
{
    public IReadOnlyList<UserDto> Users { get; set; } = Array.Empty<UserDto>();
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }

    // Search/filter state
    public string? SearchTerm { get; set; }
    public string? RoleFilter { get; set; }
    public bool? ActiveFilter { get; set; }
    public bool? DiscordLinkedFilter { get; set; }

    // Available options for filters
    public IReadOnlyList<string> AvailableRoles { get; set; } = Array.Empty<string>();

    // Current user info for permission checks
    public string CurrentUserId { get; set; } = string.Empty;
    public bool CanCreateUsers { get; set; }
}
```

### 5.2 User Detail ViewModel

**Location:** `src/DiscordBot.Bot/ViewModels/Pages/UserDetailViewModel.cs`

```csharp
namespace DiscordBot.Bot.ViewModels.Pages;

public class UserDetailViewModel
{
    public UserDto User { get; set; } = null!;
    public IReadOnlyList<UserActivityLogDto> RecentActivity { get; set; } = Array.Empty<UserActivityLogDto>();

    // Permission flags for current user
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanResetPassword { get; set; }
    public bool CanChangeRole { get; set; }
    public bool CanUnlinkDiscord { get; set; }
    public bool IsSelf { get; set; }
}
```

### 5.3 User Form ViewModel

**Location:** `src/DiscordBot.Bot/ViewModels/Pages/UserFormViewModel.cs`

```csharp
namespace DiscordBot.Bot.ViewModels.Pages;

public class UserFormViewModel
{
    // For both create and edit
    public string? UserId { get; set; } // Null for create
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Password { get; set; } // Only for create
    public string? ConfirmPassword { get; set; } // Only for create
    public string Role { get; set; } = "Viewer";
    public bool IsActive { get; set; } = true;

    // Options
    public IReadOnlyList<SelectListItem> AvailableRoles { get; set; } = Array.Empty<SelectListItem>();

    // Edit-only display fields
    public bool IsDiscordLinked { get; set; }
    public string? DiscordUsername { get; set; }
    public string? DiscordAvatarUrl { get; set; }

    // Permissions
    public bool CanChangeRole { get; set; }
    public bool CanChangeActiveStatus { get; set; }
    public bool IsSelf { get; set; }
}
```

---

## 6. Razor Pages Structure

### 6.1 Page Overview

| Page | Route | Purpose |
|------|-------|---------|
| Index | `/Admin/Users` | List all users with search/filter/pagination |
| Create | `/Admin/Users/Create` | Create new user form |
| Edit | `/Admin/Users/Edit/{id}` | Edit existing user |
| Details | `/Admin/Users/Details/{id}` | View user details and activity |

### 6.2 Page Specifications

#### 6.2.1 Index Page (User List)

**Files:**
- `src/DiscordBot.Bot/Pages/Admin/Users/Index.cshtml`
- `src/DiscordBot.Bot/Pages/Admin/Users/Index.cshtml.cs`

**Features:**
- Search bar (searches email, display name, Discord username)
- Role filter dropdown
- Active status filter toggle
- Discord linked filter toggle
- Sortable columns (Email, Display Name, Created, Last Login)
- Pagination (using existing `_Pagination.cshtml` component)
- Actions column: View, Edit buttons
- Quick actions: Enable/Disable toggle

**Layout:**
```
+------------------------------------------------------------------+
| Users                                          [+ Create User]    |
+------------------------------------------------------------------+
| Search: [__________]  Role: [All v]  Status: [All v]  Discord: [] |
+------------------------------------------------------------------+
| USER              | ROLE      | STATUS  | DISCORD  | LAST LOGIN  |
|-------------------|-----------|---------|----------|-------------|
| [avatar] email    | [badge]   | Active  | Linked   | 2 hours ago |
|   Display Name    |           |         |          | [View][Edit]|
|-------------------|-----------|---------|----------|-------------|
| [avatar] email2   | [badge]   | Inactive| -        | Never       |
|   Display Name    |           |         |          | [View][Edit]|
+------------------------------------------------------------------+
| Showing 1-20 of 45 users          [< Prev] [1][2][3] [Next >]    |
+------------------------------------------------------------------+
```

#### 6.2.2 Create Page

**Files:**
- `src/DiscordBot.Bot/Pages/Admin/Users/Create.cshtml`
- `src/DiscordBot.Bot/Pages/Admin/Users/Create.cshtml.cs`

**Form Fields:**
- Email (required, email validation)
- Display Name (optional)
- Password (required, complexity validation)
- Confirm Password (required, must match)
- Role (dropdown, filtered by actor's permissions)
- Send Welcome Email (checkbox)

**Validation:**
- Client-side: Required fields, email format, password match
- Server-side: Email uniqueness, password complexity, role permissions

#### 6.2.3 Edit Page

**Files:**
- `src/DiscordBot.Bot/Pages/Admin/Users/Edit.cshtml`
- `src/DiscordBot.Bot/Pages/Admin/Users/Edit.cshtml.cs`

**Form Fields:**
- Email (editable)
- Display Name (editable)
- Role (dropdown, if permitted)
- Active Status (toggle, if not self)

**Read-only Sections:**
- Discord Account Info (if linked)
  - Username
  - Avatar
  - [Unlink Discord] button

**Actions:**
- [Save Changes] - primary
- [Reset Password] - secondary (generates temp password)
- [Cancel] - secondary

**Self-Protection:**
- Cannot change own role
- Cannot deactivate self
- Warning message displayed when editing self

#### 6.2.4 Details Page

**Files:**
- `src/DiscordBot.Bot/Pages/Admin/Users/Details.cshtml`
- `src/DiscordBot.Bot/Pages/Admin/Users/Details.cshtml.cs`

**Sections:**

1. **User Profile Card**
   - Avatar (Discord or placeholder)
   - Display Name / Email
   - Role badge
   - Status indicator (Active/Inactive/Locked)
   - Member since / Last login

2. **Discord Account Section** (if linked)
   - Discord ID
   - Username
   - Avatar URL
   - [Unlink Discord] button

3. **Account Actions**
   - [Edit User]
   - [Reset Password]
   - [Enable/Disable Account]

4. **Activity Log**
   - Recent activity for this user
   - Paginated table of UserActivityLog entries
   - Filterable by action type

---

## 7. Activity Logging Approach

### 7.1 Events to Log

| Action | Trigger | Details Captured |
|--------|---------|------------------|
| `UserCreated` | Create page submit | New user email, assigned role |
| `UserUpdated` | Edit page submit | Changed fields (diff) |
| `UserEnabled` | Enable toggle | Previous state |
| `UserDisabled` | Disable toggle | Previous state |
| `RoleAssigned` | Role dropdown change | Old role -> New role |
| `RoleRemoved` | Role removal | Removed role |
| `PasswordReset` | Reset password button | (no sensitive data) |
| `DiscordUnlinked` | Unlink button | Previous Discord username |

### 7.2 Log Entry Format

```json
{
  "id": "guid",
  "actorUserId": "user-123",
  "actorEmail": "admin@example.com",
  "targetUserId": "user-456",
  "targetEmail": "target@example.com",
  "action": "RoleAssigned",
  "details": "{\"oldRole\":\"Viewer\",\"newRole\":\"Moderator\"}",
  "timestamp": "2025-12-09T10:30:00Z",
  "ipAddress": "192.168.1.1"
}
```

### 7.3 Activity Log Display

The Details page will show recent activity with:
- Human-readable action descriptions
- Relative timestamps ("2 hours ago")
- Actor information
- Expandable details for complex changes

---

## 8. Implementation Sequence

### Phase 1: Foundation (Tasks 2.3.1 - 2.3.2)

1. Create DTOs in `DiscordBot.Core/DTOs/`
   - `UserDto.cs`
   - `UserCreateDto.cs`
   - `UserUpdateDto.cs`
   - `UserActivityLogDto.cs`
   - `UserSearchQueryDto.cs`
   - `UserManagementResult.cs`

2. Create `UserActivityLog` entity and enum

3. Update `BotDbContext`:
   - Add `DbSet<UserActivityLog>`
   - Configure entity relationships

4. Create migration for `UserActivityLog`

5. Create `IUserManagementService` interface

6. Implement `UserManagementService`

### Phase 2: List Page (Tasks 2.3.3, 2.3.7)

7. Create `UserListViewModel`

8. Create `Pages/Admin/Users/Index.cshtml.cs`
   - Inject `IUserManagementService`
   - Implement OnGetAsync with search/filter/pagination

9. Create `Pages/Admin/Users/Index.cshtml`
   - Implement search bar
   - Implement filter dropdowns
   - Implement user table
   - Use existing `_Pagination.cshtml` component

### Phase 3: Create Page (Tasks 2.3.4, 2.3.8)

10. Create `UserFormViewModel`

11. Create `Pages/Admin/Users/Create.cshtml.cs`
    - Implement OnGetAsync (load roles)
    - Implement OnPostAsync (create user)

12. Create `Pages/Admin/Users/Create.cshtml`
    - Use existing form components
    - Add role dropdown

### Phase 4: Edit Page (Tasks 2.3.5, 2.3.8, 2.3.9, 2.3.10, 2.3.11, 2.3.12)

13. Create `Pages/Admin/Users/Edit.cshtml.cs`
    - Implement OnGetAsync (load user)
    - Implement OnPostAsync (update user)
    - Implement OnPostResetPasswordAsync
    - Implement OnPostToggleActiveAsync
    - Implement OnPostUnlinkDiscordAsync
    - Add self-protection checks

14. Create `Pages/Admin/Users/Edit.cshtml`
    - User form with role dropdown
    - Discord account section
    - Action buttons with confirmation modals

### Phase 5: Details Page (Tasks 2.3.6, 2.3.13)

15. Create `UserDetailViewModel`

16. Create `Pages/Admin/Users/Details.cshtml.cs`
    - Implement OnGetAsync (load user + activity)

17. Create `Pages/Admin/Users/Details.cshtml`
    - Profile card
    - Discord section
    - Activity log table

### Phase 6: Confirmation Dialogs (Task 2.3.14)

18. Create confirmation modal component or use JavaScript confirms:
    - Password reset confirmation
    - Disable account confirmation
    - Unlink Discord confirmation

### Phase 7: Documentation (Task 2.3.15)

19. Document user management API
20. Update admin guide with user management section

---

## 9. Subagent Task Plan

### 9.1 design-specialist

Not required for this feature - uses existing design system components.

### 9.2 html-prototyper

Not required - pages will be built directly using existing Razor components.

### 9.3 dotnet-specialist

**Primary implementer for all tasks:**

| Task | Description | Estimated Effort |
|------|-------------|------------------|
| 2.3.1 | Create `IUserManagementService` interface | 1 hour |
| 2.3.2 | Implement `UserManagementService` | 4 hours |
| 2.3.3 | Create `Pages/Admin/Users/Index.cshtml` | 3 hours |
| 2.3.4 | Create `Pages/Admin/Users/Create.cshtml` | 2 hours |
| 2.3.5 | Create `Pages/Admin/Users/Edit.cshtml` | 3 hours |
| 2.3.6 | Create `Pages/Admin/Users/Details.cshtml` | 2 hours |
| 2.3.7 | Implement pagination and search | Included in 2.3.3 |
| 2.3.8 | Add role assignment dropdown | Included in 2.3.4/2.3.5 |
| 2.3.9 | Implement user enable/disable toggle | Included in 2.3.5 |
| 2.3.10 | Implement password reset | 1 hour |
| 2.3.11 | Add Discord account link/unlink | 1 hour |
| 2.3.12 | Prevent self-deletion/demotion | Included in 2.3.2 |
| 2.3.13 | Add activity log for user actions | Included in 2.3.2/2.3.6 |
| 2.3.14 | Add confirmation dialogs | 1 hour |

**Total estimated effort:** 18 hours

### 9.4 docs-writer

| Task | Description | Estimated Effort |
|------|-------------|------------------|
| 2.3.15 | Document user management API | 2 hours |

---

## 10. Timeline / Dependency Map

```
Phase 1: Foundation (Day 1)
├── DTOs (no dependencies)
├── Entity + Migration (no dependencies)
├── IUserManagementService (depends on DTOs)
└── UserManagementService (depends on interface)

Phase 2: List Page (Day 2)
├── UserListViewModel (depends on DTOs)
└── Index Page (depends on service, viewmodel)

Phase 3: Create Page (Day 2)
├── UserFormViewModel (depends on DTOs)
└── Create Page (depends on service, viewmodel)

Phase 4: Edit Page (Day 3)
└── Edit Page (depends on service, viewmodel, create page patterns)

Phase 5: Details Page (Day 3)
├── UserDetailViewModel (depends on DTOs)
└── Details Page (depends on service, viewmodel)

Phase 6: Confirmations (Day 4)
└── Confirmation dialogs (depends on edit page)

Phase 7: Documentation (Day 4)
└── API documentation (can run parallel to Phase 6)
```

**Parallelization Opportunities:**
- DTOs and Entity can be created simultaneously
- Documentation can begin once API is finalized (after Phase 1)
- Create and List pages can be developed in parallel after Phase 1

---

## 11. Acceptance Criteria

### 11.1 User Listing

- [ ] Admin can view list of all users
- [ ] List shows email, display name, role badge, status, Discord link status, last login
- [ ] List is paginated (default 20 per page)
- [ ] Users can be searched by email, display name, or Discord username
- [ ] Users can be filtered by role
- [ ] Users can be filtered by active status
- [ ] Users can be filtered by Discord link status
- [ ] List is sortable by email, display name, created date, last login

### 11.2 User Creation

- [ ] Admin can create new user with email, display name, password, and role
- [ ] Email uniqueness is validated
- [ ] Password complexity is enforced
- [ ] Role dropdown shows only roles the admin can assign
- [ ] Success message shows after creation
- [ ] New user appears in user list

### 11.3 User Editing

- [ ] Admin can edit user email, display name
- [ ] Admin can change user role (within permissions)
- [ ] SuperAdmin cannot be demoted by Admin
- [ ] Self cannot change own role
- [ ] Changes are saved and confirmed

### 11.4 User Enable/Disable

- [ ] Admin can disable user account
- [ ] Admin can re-enable user account
- [ ] Disabled users cannot log in
- [ ] Self cannot disable own account
- [ ] Confirmation required before disabling

### 11.5 Password Reset

- [ ] Admin can reset any user's password
- [ ] New temporary password is generated
- [ ] Password is displayed once to admin
- [ ] Confirmation required before reset
- [ ] User can log in with new password

### 11.6 Discord Account Management

- [ ] User detail shows linked Discord account info
- [ ] Admin can unlink Discord from user account
- [ ] Confirmation required before unlinking
- [ ] User can still log in after unlinking (if they have password)

### 11.7 Self-Protection

- [ ] Admin cannot delete/deactivate own account
- [ ] Admin cannot remove own Admin role
- [ ] Warning displayed when viewing/editing own account
- [ ] Appropriate error messages when self-modification attempted

### 11.8 Activity Logging

- [ ] All user management actions are logged
- [ ] Log entries include actor, target, action, timestamp
- [ ] Log entries include IP address
- [ ] Activity log viewable on user details page
- [ ] Activity log is paginated

---

## 12. Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Self-protection bypass | Low | High | Enforce checks at service layer, not just UI |
| Role escalation | Low | High | Validate role permissions server-side |
| Activity log performance | Medium | Medium | Index on timestamps, paginate results |
| Password exposure in logs | Medium | High | Never log passwords, only hash presence |
| Concurrent edit conflicts | Low | Low | Use optimistic concurrency if needed |
| Large user counts | Low | Medium | Ensure pagination works efficiently |

---

## 13. File Summary

### New Files to Create

**Core Layer (`src/DiscordBot.Core/`):**
- `DTOs/UserDto.cs`
- `DTOs/UserCreateDto.cs`
- `DTOs/UserUpdateDto.cs`
- `DTOs/UserActivityLogDto.cs`
- `DTOs/UserSearchQueryDto.cs`
- `DTOs/UserManagementResult.cs`
- `Entities/UserActivityLog.cs`
- `Interfaces/IUserManagementService.cs`

**Bot Layer (`src/DiscordBot.Bot/`):**
- `Services/UserManagementService.cs`
- `ViewModels/Pages/UserListViewModel.cs`
- `ViewModels/Pages/UserDetailViewModel.cs`
- `ViewModels/Pages/UserFormViewModel.cs`
- `Pages/Admin/Users/Index.cshtml`
- `Pages/Admin/Users/Index.cshtml.cs`
- `Pages/Admin/Users/Create.cshtml`
- `Pages/Admin/Users/Create.cshtml.cs`
- `Pages/Admin/Users/Edit.cshtml`
- `Pages/Admin/Users/Edit.cshtml.cs`
- `Pages/Admin/Users/Details.cshtml`
- `Pages/Admin/Users/Details.cshtml.cs`

**Infrastructure Layer (`src/DiscordBot.Infrastructure/`):**
- Migration file (auto-generated)

### Files to Modify

- `src/DiscordBot.Infrastructure/Data/BotDbContext.cs` - Add UserActivityLog DbSet
- `src/DiscordBot.Bot/Program.cs` - Register IUserManagementService
- `src/DiscordBot.Bot/Pages/Shared/_Sidebar.cshtml` - Add Users navigation link

---

## 14. Test Considerations

### Unit Tests

- `UserManagementService` self-protection checks
- Role hierarchy validation
- DTO mapping correctness

### Integration Tests

- User creation workflow
- Password reset workflow
- Role assignment workflow
- Activity log persistence

### Manual Testing

- Cross-browser form validation
- Pagination with various page sizes
- Search functionality
- Filter combinations

---

*Document prepared by: Systems Architect Agent*
*Review status: Ready for implementation*
