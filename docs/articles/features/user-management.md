# User Management Guide

This guide provides comprehensive documentation for the User Management feature, which allows administrators to create, manage, and monitor application users through the admin web interface.

**Feature Status:** Implemented (Issue #66)

**Access Level:** Admin and SuperAdmin roles only

---

## Table of Contents

- [Overview](#overview)
- [Role Hierarchy](#role-hierarchy)
- [Accessing User Management](#accessing-user-management)
- [User List](#user-list)
- [Creating Users](#creating-users)
- [Editing Users](#editing-users)
- [User Details](#user-details)
- [Managing User Status](#managing-user-status)
- [Role Management](#role-management)
- [Password Reset](#password-reset)
- [Discord Account Management](#discord-account-management)
- [Activity Logging](#activity-logging)
- [Self-Protection Rules](#self-protection-rules)
- [Security Considerations](#security-considerations)
- [Troubleshooting](#troubleshooting)

---

## Overview

The User Management system provides administrators with comprehensive tools to manage application users. This feature is accessible only to users with Admin or SuperAdmin roles through the admin web interface.

**Key Capabilities:**

- View all users with search and filtering
- Create new user accounts with initial roles
- Edit user information (email, display name, active status)
- Assign and modify user roles
- Enable/disable user accounts
- Reset user passwords (admin-initiated)
- View and unlink Discord account associations
- Monitor user activity through audit logs
- Self-protection mechanisms to prevent accidental account lockout

**Architecture:**

- **Service Layer:** `IUserManagementService` (Core) / `UserManagementService` (Bot)
- **Web UI:** Razor Pages in `/Admin/Users/` directory
- **Authorization:** ASP.NET Identity with custom authorization policies
- **Audit Logging:** All actions logged to `UserActivityLog` table with IP addresses

---

## Role Hierarchy

The system uses a four-tier role hierarchy with strict permission boundaries:

```
SuperAdmin (Highest)
    |
  Admin
    |
Moderator
    |
 Viewer (Lowest)
```

### Role Capabilities

| Role | Create Users | Edit Users | Assign Roles | Reset Passwords | View Activity |
|------|--------------|------------|--------------|-----------------|---------------|
| **SuperAdmin** | Yes | All users | All roles | Yes | Yes |
| **Admin** | Yes | Below SuperAdmin | Admin/Moderator/Viewer | Yes (below SuperAdmin) | Yes |
| **Moderator** | No | No | No | No | No |
| **Viewer** | No | No | No | No | No |

### Role Assignment Rules

1. **SuperAdmin** can assign any role including SuperAdmin
2. **Admin** can assign Admin, Moderator, or Viewer (cannot assign SuperAdmin)
3. Only **SuperAdmin** can manage other SuperAdmin users
4. Users cannot assign themselves a higher role than they currently have
5. Each user has exactly one role (roles are mutually exclusive)

---

## Accessing User Management

### Navigation

1. Log in to the admin interface
2. Navigate to the sidebar menu
3. Click **Users** under the Admin section

**Direct URL:** `https://yourdomain.com/Admin/Users`

**Required Permission:** `RequireAdmin` policy (Admin or SuperAdmin role)

### Authorization Check

All user management pages enforce the `RequireAdmin` authorization policy. Users without Admin or SuperAdmin roles will receive a 403 Forbidden error.

---

## User List

The user list provides a comprehensive view of all application users with search, filter, and pagination capabilities.

**Route:** `/Admin/Users`

**Page Model:** `IndexModel` (`Pages/Admin/Users/Index.cshtml.cs`)

### Features

#### Search Bar

Search across multiple fields:
- Email address (partial match, case-insensitive)
- Display name (partial match, case-insensitive)
- Discord username (partial match, case-insensitive)

**Example:** Searching for "john" will match:
- `john.doe@example.com`
- `John Smith` (display name)
- `JohnnyGamer` (Discord username)

#### Filters

| Filter | Options | Description |
|--------|---------|-------------|
| **Role** | All, SuperAdmin, Admin, Moderator, Viewer | Filter by assigned role |
| **Status** | All, Active, Inactive | Filter by active/disabled status |
| **Discord** | All, Linked, Not Linked | Filter by Discord account link status |

Filters can be combined for refined searches.

#### User Table Columns

| Column | Description | Sortable |
|--------|-------------|----------|
| **User** | Avatar (if Discord linked), email, display name | Yes (by email) |
| **Role** | Highest assigned role (badge) | No |
| **Status** | Active/Inactive indicator | No |
| **Discord** | Discord link status | No |
| **Last Login** | Relative time since last login | Yes |
| **Actions** | View, Edit buttons | No |

#### Pagination

- Default page size: 20 users
- Configurable via query string: `?pageSize=50`
- Navigation controls: Previous, Next, page numbers
- Shows total user count and current range

#### Quick Actions

**Enable/Disable Toggle:**
- Inline toggle for changing active status
- Confirmation dialog before disabling accounts
- Blocked for self-modification (your own account)

---

## Creating Users

Create new user accounts with initial configuration.

**Route:** `/Admin/Users/Create`

**Page Model:** `CreateModel` (`Pages/Admin/Users/Create.cshtml.cs`)

### Form Fields

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| **Email** | Text | Yes | Valid email format, unique |
| **Display Name** | Text | No | Max 100 characters |
| **Password** | Password | Yes | Complexity requirements (see below) |
| **Confirm Password** | Password | Yes | Must match Password |
| **Role** | Dropdown | Yes | Limited by your permissions |
| **Send Welcome Email** | Checkbox | No | Default: checked |

### Password Requirements

ASP.NET Identity password complexity rules:
- Minimum 6 characters (configurable in `Program.cs`)
- At least one uppercase letter
- At least one lowercase letter
- At least one digit
- At least one non-alphanumeric character

**Example Valid Password:** `SecureP@ss123`

### Role Assignment

The role dropdown shows only roles you have permission to assign:

- **SuperAdmin sees:** SuperAdmin, Admin, Moderator, Viewer
- **Admin sees:** Admin, Moderator, Viewer

### Workflow

1. Click **Create User** button in user list header
2. Fill in required fields (email, password, confirm password)
3. Optionally set display name
4. Select role from dropdown
5. Choose whether to send welcome email
6. Click **Create User** button
7. On success, redirected to user list with success message
8. On error, form displays validation errors

### Activity Logged

- **Action:** `UserCreated`
- **Details:** Email address and assigned role
- **Actor:** Current admin user
- **IP Address:** Captured from request

### Common Errors

| Error | Cause | Resolution |
|-------|-------|------------|
| Email already exists | Email is already registered | Use different email or edit existing user |
| Password too weak | Doesn't meet complexity rules | Add uppercase, lowercase, digit, special char |
| Insufficient permissions | Trying to assign unauthorized role | Select a role within your permissions |

---

## Editing Users

Modify existing user information, roles, and status.

**Route:** `/Admin/Users/Edit/{id}`

**Page Model:** `EditModel` (`Pages/Admin/Users/Edit.cshtml.cs`)

### Editable Fields

| Field | Editable | Notes |
|-------|----------|-------|
| **Email** | Yes | Must remain unique |
| **Display Name** | Yes | Optional field |
| **Role** | Yes | Subject to permissions and self-protection |
| **Active Status** | Yes | Cannot disable own account |

### Read-Only Information

When editing, the following are displayed but not editable:
- User ID
- Account creation date
- Last login timestamp
- Email confirmation status
- Discord account information (managed separately)

### Discord Account Section

If the user has a linked Discord account, this section displays:
- Discord username
- Discord user ID
- Avatar image
- **Unlink Discord** button (see [Discord Account Management](#discord-account-management))

### Form Actions

| Button | Action | Confirmation |
|--------|--------|--------------|
| **Save Changes** | Submit form | No |
| **Reset Password** | Generate temporary password | Yes |
| **Unlink Discord** | Remove Discord association | Yes |
| **Enable/Disable** | Toggle active status | Yes (for disable) |
| **Cancel** | Return to list | No |

### Self-Editing Warning

When editing your own account, a warning message is displayed:

> **Warning:** You are editing your own account. Some actions are restricted to prevent accidental lockout.

**Restrictions when editing self:**
- Cannot change your own role
- Cannot disable your own account
- Role dropdown is disabled
- Active status toggle is disabled

### Workflow

1. Click **Edit** button from user list or details page
2. Modify desired fields
3. Click **Save Changes**
4. On success, redirected to user details with success message
5. On error, form displays validation errors

### Activity Logged

- **Action:** `UserUpdated`
- **Details:** JSON array of changed fields with old/new values
- **Example:** `["DisplayName: 'John' -> 'John Smith'", "Email: 'old@example.com' -> 'new@example.com'"]`

---

## User Details

View comprehensive user information and activity history.

**Route:** `/Admin/Users/Details/{id}`

**Page Model:** `DetailsModel` (`Pages/Admin/Users/Details.cshtml.cs`)

### Sections

#### 1. User Profile Card

Displays:
- User avatar (Discord avatar if linked, otherwise placeholder)
- Display name or email
- Email address
- Role badge
- Status indicator (Active, Inactive, or Locked Out)
- Member since date
- Last login timestamp

#### 2. Account Information

Shows detailed account data:
- User ID (GUID)
- Email confirmation status
- Account creation date
- Last login date and time
- Lockout status and end time (if locked)

#### 3. Discord Account

**If linked:**
- Discord username (e.g., `JohnDoe#1234`)
- Discord user ID
- Avatar URL
- **Unlink Discord** button

**If not linked:**
- "No Discord account linked" message
- Instructions for user to link via profile settings

#### 4. Recent Activity

Displays the 10 most recent activity log entries for this user:
- Action type (created, updated, role assigned, etc.)
- Actor who performed the action
- Timestamp (relative, e.g., "2 hours ago")
- Action details (expandable for complex changes)

Link to full activity history at bottom of section.

#### 5. Quick Actions

Action buttons based on permissions:
- **Edit User** - Opens edit page
- **Reset Password** - Generates temporary password
- **Enable/Disable Account** - Toggles active status
- **View Full Activity Log** - Shows all activity

### Permission-Based Display

Elements shown/hidden based on your role:
- SuperAdmin sees all actions for all users
- Admin sees all actions for users below SuperAdmin
- Self-viewing shows limited actions (cannot manage self)

---

## Managing User Status

### Active Status

Users can be in one of three states:

| Status | Login Allowed | Description |
|--------|---------------|-------------|
| **Active** | Yes | Normal account status |
| **Inactive** | No | Admin-disabled account |
| **Locked Out** | No | Auto-locked after failed login attempts |

### Disabling Accounts

**Purpose:** Temporarily prevent a user from logging in without deleting their account.

**Methods:**

1. **From User List:**
   - Click toggle switch in Status column
   - Confirm in dialog
   - User immediately logged out (if currently logged in)

2. **From Edit Page:**
   - Uncheck "Active" checkbox
   - Click Save Changes
   - Confirmation required

**Workflow:**
1. Locate user in list or details page
2. Click disable action
3. Confirm in dialog: "Are you sure you want to disable this account?"
4. Success message displayed
5. User's status changes to Inactive

**Effects of Disabling:**
- User cannot log in
- Active sessions are invalidated
- User receives "Account disabled" message on login attempt
- All user data and permissions are preserved
- Can be re-enabled at any time

### Enabling Accounts

**Purpose:** Restore access to a previously disabled account.

**Workflow:**
1. Locate disabled user in list
2. Filter by "Status: Inactive" to find disabled accounts
3. Click enable toggle or edit user
4. Confirm action
5. User can immediately log in

### Account Lockout

Accounts are automatically locked after multiple failed login attempts (configurable in `Program.cs`).

**Default Settings:**
- Max failed attempts: 5
- Lockout duration: 15 minutes

**Unlocking:**
- Automatic after lockout duration expires
- Manual unlock by admin (future feature)

### Self-Protection

**Critical Rule:** You cannot disable your own account.

**Reason:** Prevents accidental administrator lockout.

**Enforcement:**
- UI: Disable toggle is hidden when viewing own account
- Service layer: `SetUserActiveStatusAsync` returns error for self-modification
- Error message: "You cannot disable your own account"

**Activity Logged:**
- **Action:** `UserEnabled` or `UserDisabled`
- **Details:** Previous state
- **Actor:** Current admin user

---

## Role Management

### Viewing User Roles

User roles are displayed:
- As badges in user list
- In user profile card on details page
- In edit form as dropdown

### Changing User Roles

**Route:** Edit page (`/Admin/Users/Edit/{id}`)

**Workflow:**
1. Navigate to user edit page
2. Locate "Role" dropdown
3. Select new role from available options
4. Click **Save Changes**
5. All previous roles are removed (users have one role)
6. New role immediately takes effect

**Role Dropdown Options:**

Displayed roles depend on your own role:

```csharp
// SuperAdmin sees:
- SuperAdmin
- Admin
- Moderator
- Viewer

// Admin sees:
- Admin
- Moderator
- Viewer
// (SuperAdmin option hidden)
```

### Role Change Restrictions

1. **Cannot change own role**
   - Prevents privilege escalation or accidental demotion
   - Dropdown disabled when editing self

2. **Cannot assign higher role than yours**
   - Admin cannot assign SuperAdmin
   - Enforced at service layer

3. **Cannot manage SuperAdmin users as Admin**
   - Admins cannot edit SuperAdmin users
   - Edit page returns 403 Forbidden

### Role Assignment Process

Internally, role assignment:
1. Validates actor has permission to assign target role
2. Removes all existing roles from user
3. Assigns new single role
4. Logs role change with old and new values

**Code Example:**

```csharp
// Service method signature
Task<UserManagementResult> AssignRoleAsync(
    string userId,
    string role,
    string actorUserId,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);
```

### Activity Logged

- **Action:** `RoleAssigned`
- **Details:** `{"OldRole": "Viewer", "NewRole": "Admin"}`
- **Actor:** Current admin user

### Permission Effects

Role changes take effect immediately:
- User's next request reflects new permissions
- Active sessions are **not** invalidated (by design)
- User sees new role-specific UI on next page load

---

## Password Reset

Admin-initiated password reset generates a secure temporary password.

**Access:** Edit page or details page

### Workflow

1. Navigate to user edit or details page
2. Click **Reset Password** button
3. Confirm in dialog: "Generate a new temporary password for this user?"
4. System generates 16-character secure password
5. Password displayed **once** in success message
6. Copy password and provide to user securely (email, direct message, etc.)
7. User can log in with temporary password
8. User should change password immediately via profile settings

### Temporary Password Characteristics

Generated passwords are:
- 16 characters long
- Include uppercase letters (A-Z)
- Include lowercase letters (a-z)
- Include digits (0-9)
- Include special characters (!@#$%^&*)
- Randomly shuffled for security

**Example:** `aB3!xY7@zK2$mN9%`

### Security Considerations

1. **One-Time Display**
   - Password shown only once in success alert
   - Not stored in logs or database (hashed immediately)
   - Not sent via email automatically

2. **Secure Delivery**
   - Admins responsible for secure password delivery
   - Recommended: Use secure channel (encrypted email, direct message)
   - Avoid insecure channels (SMS, unencrypted email)

3. **User Action Required**
   - User should change password on first login
   - Consider forcing password change (future enhancement)

### Password Reset Process

**Service Layer:**

```csharp
// 1. Generate secure password
var tempPassword = GenerateTemporaryPassword();

// 2. Get reset token from Identity
var token = await _userManager.GeneratePasswordResetTokenAsync(user);

// 3. Reset password using token
var resetResult = await _userManager.ResetPasswordAsync(user, token, tempPassword);

// 4. Return password in result
return UserManagementResult.SuccessWithPassword(tempPassword, userDto);
```

### Activity Logged

- **Action:** `PasswordReset`
- **Details:** `null` (passwords never logged)
- **Actor:** Current admin user
- **IP Address:** Captured

### Common Scenarios

| Scenario | Solution |
|----------|----------|
| User forgot password | Admin resets, provides temp password, user changes it |
| Compromised account | Admin resets password, notifies user, user changes it |
| New user setup | Admin creates user with initial password, user changes on first login |

---

## Discord Account Management

### Discord Linking Overview

Users can link their Discord accounts for:
- Single sign-on (Discord OAuth)
- Discord-specific features
- Avatar display in admin UI
- Guild access management

### Viewing Discord Account Info

**User Details Page:**
- Discord username (e.g., `JohnDoe#1234`)
- Discord user ID (numeric snowflake)
- Avatar image
- Link status indicator

**User List Page:**
- "Linked" or empty indicator in Discord column
- Filter by Discord link status

### Unlinking Discord Accounts

**Purpose:** Remove Discord association without deleting the user account.

**Use Cases:**
- User changed Discord account
- Security concern with Discord account
- User prefers email/password login only
- Troubleshooting authentication issues

**Workflow:**

1. Navigate to user edit or details page
2. Locate Discord Account section
3. Click **Unlink Discord** button
4. Confirm in dialog: "Remove Discord account link for this user?"
5. Success message displayed
6. Discord fields cleared (username, ID, avatar)

**Effects of Unlinking:**
- Discord username, ID, and avatar removed from database
- User can no longer log in with "Sign in with Discord"
- User must use email/password to log in
- User can re-link Discord via profile settings
- User data and permissions are preserved

### Self-Unlinking

Users can unlink their own Discord accounts via:
- Profile settings page (future feature)
- Contact admin to unlink

### Re-Linking Discord

After unlinking, users can re-link by:
1. Logging in with email/password
2. Going to profile settings
3. Clicking "Link Discord Account"
4. Completing Discord OAuth flow

### Activity Logged

- **Action:** `DiscordUnlinked`
- **Details:** `{"PreviousUsername": "JohnDoe#1234"}`
- **Actor:** Current admin user

### Troubleshooting Discord Link Issues

| Issue | Cause | Resolution |
|-------|-------|------------|
| Cannot log in with Discord | Account unlinked | Unlink and re-link Discord |
| Wrong Discord account linked | User authenticated with wrong account | Unlink and have user re-link with correct account |
| Discord username not updating | Cached data | Unlink and re-link to refresh |

---

## Activity Logging

All user management actions are logged to the `UserActivityLog` table for audit and compliance purposes.

### What is Logged

Every user management operation logs:
- **Actor:** User who performed the action (admin)
- **Target:** User affected by the action
- **Action Type:** Enum value (e.g., `UserCreated`, `RoleAssigned`)
- **Details:** JSON string with action-specific details
- **Timestamp:** UTC datetime of action
- **IP Address:** Actor's IP address

### Activity Log Schema

```csharp
public class UserActivityLog
{
    public Guid Id { get; set; }
    public string ActorUserId { get; set; }        // Admin who acted
    public string? TargetUserId { get; set; }      // User affected
    public UserActivityAction Action { get; set; } // Action type
    public string? Details { get; set; }           // JSON details
    public DateTime Timestamp { get; set; }        // When it happened
    public string? IpAddress { get; set; }         // Actor's IP
}
```

### Logged Actions

| Action | Triggered By | Details Captured |
|--------|--------------|------------------|
| `UserCreated` | Create user page | Email, assigned role |
| `UserUpdated` | Edit user page | Changed fields (diff) |
| `UserEnabled` | Enable toggle | Previous state |
| `UserDisabled` | Disable toggle | Previous state |
| `RoleAssigned` | Role dropdown change | Old role, new role |
| `RoleRemoved` | Remove role | Removed role name |
| `PasswordReset` | Reset password button | (no password data) |
| `DiscordUnlinked` | Unlink Discord button | Previous Discord username |
| `LoginSuccess` | User login | Login method (Discord/email) |
| `LoginFailed` | Failed login attempt | Reason (invalid password, etc.) |

### Viewing Activity Logs

**User-Specific Activity:**

Navigate to user details page - shows last 10 actions for that user.

**Full Activity Log:**

Service method `GetActivityLogAsync` supports:
- Filter by user ID (optional)
- Pagination (default 50 per page)
- Ordered by timestamp (newest first)

**Example Usage:**

```csharp
// Get activity for specific user
var userActivity = await _userManagementService.GetActivityLogAsync(
    userId: "abc123",
    page: 1,
    pageSize: 50
);

// Get all activity (system-wide)
var allActivity = await _userManagementService.GetActivityLogAsync(
    userId: null,
    page: 1,
    pageSize: 100
);
```

### Activity Log Details Format

Details are stored as JSON strings for complex data:

```json
// RoleAssigned
{
  "OldRole": "Viewer",
  "NewRole": "Admin"
}

// UserUpdated
[
  "DisplayName: 'John' -> 'John Smith'",
  "Email: 'old@example.com' -> 'new@example.com'"
]

// DiscordUnlinked
{
  "PreviousUsername": "JohnDoe#1234"
}
```

### Retention and Compliance

**Current Implementation:**
- Activity logs stored indefinitely
- No automatic cleanup

**Future Considerations:**
- Configurable retention period (e.g., 90 days, 1 year)
- Archival to cold storage
- GDPR compliance (user data deletion requests)

---

## Self-Protection Rules

Self-protection mechanisms prevent administrators from accidentally locking themselves out of the system.

### Rule 1: Cannot Disable Own Account

**Restriction:** Users cannot disable their own account.

**Enforcement:**
- UI: Disable toggle hidden when viewing own account
- Edit page: Active status checkbox disabled for self
- Service: `SetUserActiveStatusAsync` returns error

**Error Message:** `"You cannot disable your own account"`

**Reason:** Prevents accidental administrator lockout.

**Code Check:**

```csharp
if (actorUserId == userId)
{
    return UserManagementResult.Failure(
        UserManagementResult.SelfModificationDenied,
        "You cannot disable your own account");
}
```

### Rule 2: Cannot Change Own Role

**Restriction:** Users cannot change their own role.

**Enforcement:**
- Edit page: Role dropdown disabled when editing self
- Service: `AssignRoleAsync` returns error for self-modification

**Error Message:** `"You cannot change your own role"`

**Reason:** Prevents privilege escalation or accidental demotion.

**Code Check:**

```csharp
if (actorUserId == userId)
{
    return UserManagementResult.Failure(
        UserManagementResult.SelfModificationDenied,
        "You cannot change your own role");
}
```

### Rule 3: Cannot Delete Own Account

**Restriction:** Users cannot delete their own account (future feature).

**Reason:** Same as disable - prevents lockout.

### Self-Editing UI Indicators

When editing your own account, the UI displays:

**Warning Banner:**
> You are editing your own account. Role changes and account deactivation are disabled to prevent accidental lockout.

**Disabled Controls:**
- Role dropdown (greyed out)
- Active status toggle (greyed out)
- Delete button (hidden)

**Allowed Actions on Self:**
- Change email address
- Change display name
- Change password (via profile settings)
- View activity log

### Bypassing Self-Protection

**Q: What if I need to change my own role?**

**A:** Another admin with sufficient privileges must change your role.

**Q: What if I'm the only admin?**

**A:** Contact a SuperAdmin or use database direct access to modify roles (emergency only).

**Emergency Role Assignment (Database):**

```sql
-- WARNING: Use only in emergency, bypasses audit logging
INSERT INTO AspNetUserRoles (UserId, RoleId)
SELECT 'user-id-guid', Id FROM AspNetRoles WHERE Name = 'SuperAdmin';
```

---

## Security Considerations

### Authentication and Authorization

1. **Page-Level Authorization**
   - All user management pages require `[Authorize(Policy = "RequireAdmin")]`
   - ASP.NET Identity verifies user has Admin or SuperAdmin role
   - Unauthorized access returns 403 Forbidden

2. **Service-Level Authorization**
   - Every service method validates actor permissions
   - Role hierarchy enforced (Admin cannot manage SuperAdmin)
   - Self-protection rules enforced

### Audit Logging

1. **Comprehensive Logging**
   - All actions logged with actor, target, timestamp
   - IP addresses captured for forensic analysis
   - JSON details for complex operations

2. **Log Integrity**
   - Logs are append-only (no deletion via UI)
   - Logs include unique GUID for tamper detection
   - Database constraints prevent orphaned logs

### Password Security

1. **Password Storage**
   - Passwords hashed using ASP.NET Identity (PBKDF2)
   - Never stored in plain text
   - Never logged (even in activity log)

2. **Password Reset**
   - Temporary passwords generated cryptographically
   - Displayed once, not stored
   - Admins responsible for secure delivery

3. **Password Complexity**
   - Enforced by ASP.NET Identity
   - Configurable in `Program.cs`
   - Default: 6+ chars, uppercase, lowercase, digit, special char

### Session Management

1. **Active Sessions**
   - Disabling user account does not invalidate active sessions (by design)
   - Role changes take effect on next request
   - Consider manual session invalidation for security incidents

2. **Concurrent Access**
   - Multiple admins can manage users concurrently
   - Optimistic concurrency not implemented (last write wins)
   - Activity log shows who made which changes

### Data Protection

1. **HTTPS Required**
   - All admin pages require HTTPS
   - Configured in `Program.cs` with HTTPS redirection

2. **Anti-Forgery Tokens**
   - All forms include anti-CSRF tokens
   - Default Razor Pages protection

3. **Input Validation**
   - Client-side validation for UX
   - Server-side validation for security
   - Email uniqueness enforced at database level

### Privilege Escalation Prevention

1. **Role Hierarchy Enforcement**
   - Admin cannot assign SuperAdmin role
   - Admin cannot manage SuperAdmin users
   - Enforced at both UI and service layers

2. **Self-Protection Rules**
   - Cannot elevate own role
   - Cannot disable own account
   - Prevents accidental or malicious lockout

---

## Troubleshooting

### Common Issues

#### Cannot Create User: Email Already Exists

**Symptoms:** Error message "A user with this email already exists" when creating user.

**Cause:** Email address is already registered.

**Resolution:**
1. Search for existing user by email
2. Edit existing user instead of creating new one
3. Or use different email address

---

#### Cannot Edit SuperAdmin Users as Admin

**Symptoms:** 403 Forbidden error when trying to edit SuperAdmin user.

**Cause:** Only SuperAdmins can manage other SuperAdmins.

**Resolution:**
- Log in as SuperAdmin to edit SuperAdmin users
- Admins can only manage Admin/Moderator/Viewer users

---

#### Password Reset Not Working

**Symptoms:** User reports they cannot log in with reset password.

**Causes and Resolutions:**

| Cause | Resolution |
|-------|------------|
| Typo in temporary password | Regenerate password, copy carefully |
| User entered wrong email | Verify email address in user details |
| Browser autocomplete | Have user clear autocomplete, enter manually |
| Account disabled | Check user status, enable account |

---

#### Cannot Disable Own Account

**Symptoms:** Disable toggle doesn't work when viewing own account.

**Cause:** Self-protection rule prevents accidental lockout.

**Resolution:**
- This is expected behavior
- Have another admin disable your account if needed
- Or use database direct access (emergency only)

---

#### User Not Appearing in List

**Symptoms:** User exists but doesn't appear in user list.

**Possible Causes:**
1. **Active Filter:** Clear "Status" filter or set to "All"
2. **Role Filter:** Clear "Role" filter or select user's role
3. **Search Filter:** Clear search term
4. **Pagination:** Check other pages

**Resolution:**
1. Click "Clear Filters" or reset all filters to "All"
2. Increase page size to view more users
3. Search by user's email directly

---

#### Discord Account Won't Unlink

**Symptoms:** Error when clicking "Unlink Discord" button.

**Causes:**

| Cause | Error Message | Resolution |
|-------|---------------|------------|
| User has no Discord linked | "User does not have a linked Discord account" | Verify user details show Discord info |
| Database error | "Failed to unlink Discord account" | Check application logs, retry |

---

#### Activity Log Shows Unexpected Actions

**Symptoms:** Activity log shows actions you didn't perform.

**Causes:**
1. Another admin performed the action (check ActorEmail)
2. Automated system action (e.g., auto-lockout)
3. Time zone confusion (logs are in UTC)

**Resolution:**
1. Check ActorEmail field to see who performed action
2. Convert timestamp from UTC to local time
3. Review IP address for actor verification

---

### Debugging Tips

#### Enable Debug Logging

In `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "DiscordBot.Bot.Services.UserManagementService": "Debug"
    }
  }
}
```

#### Check Application Logs

UserManagementService logs all operations:

```
[INF] Creating new user with email: john@example.com by actor: abc123
[INF] Successfully created user def456 with email john@example.com
[WRN] User abc123 attempted to change their own active status
```

#### Verify User Claims

Check user claims to troubleshoot authorization:

```csharp
var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value);
```

---

## Related Documentation

- [API Endpoints Reference](api-endpoints.md) - User Management Service interface documentation
- [Authorization Policies](authorization-policies.md) - Role-based authorization implementation
- [Identity Configuration](identity-configuration.md) - ASP.NET Identity setup and configuration
- [Database Schema](database-schema.md) - UserActivityLog and ApplicationUser entities

---

**Last Updated:** December 9, 2024

**Feature Version:** 1.0 (Issue #66)
