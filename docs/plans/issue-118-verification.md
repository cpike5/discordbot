# Implementation Plan: Issue #118 - Discord Bot Account Verification

**Document Version:** 1.0
**Date:** 2025-12-09
**Issue Reference:** GitHub Issue #118
**Prerequisites:** Issue #117 (Discord OAuth Tokens) - COMPLETE

---

## 1. Requirement Summary

Implement a Discord bot-based account verification system that allows users who created accounts via email/password to link their Discord account without using OAuth. The flow is:

1. User clicks "Link via Discord Bot" button in the web profile page
2. System creates a pending verification record associated with the ApplicationUser
3. User runs `/verify-account` slash command in Discord
4. Bot generates a unique verification code (e.g., "ABC-123") and returns it via ephemeral message
5. User enters the code in the web UI verification form
6. System validates the code and links the Discord ID to the ApplicationUser

### Key Constraints

- **Code Format:** 6-character alphanumeric using charset `ABCDEFGHJKLMNPQRSTUVWXYZ23456789` (excludes ambiguous characters: 0, O, 1, I, L)
- **Code Expiry:** 15 minutes from generation
- **Rate Limiting:** Maximum 3 verification codes per Discord user per hour
- **Security:** Ephemeral bot responses only; codes are single-use
- **Cleanup:** Background job removes expired verification codes

---

## 2. Architectural Considerations

### 2.1 Existing Infrastructure

| Component | Location | Relevance |
|-----------|----------|-----------|
| `ApplicationUser` entity | `src/DiscordBot.Core/Entities/ApplicationUser.cs` | Target entity for Discord linking |
| `LinkDiscord.cshtml` page | `src/DiscordBot.Bot/Pages/Account/LinkDiscord.cshtml` | UI to extend with bot verification option |
| `InteractionStateService` | `src/DiscordBot.Bot/Services/InteractionStateService.cs` | Pattern for temporary state management |
| `InteractionStateCleanupService` | `src/DiscordBot.Bot/Services/InteractionStateCleanupService.cs` | Pattern for background cleanup |
| `RateLimitAttribute` | `src/DiscordBot.Bot/Preconditions/RateLimitAttribute.cs` | Existing rate limit implementation |
| `IDiscordTokenService` | `src/DiscordBot.Core/Interfaces/IDiscordTokenService.cs` | Pattern for service interface design |
| `BotDbContext` | `src/DiscordBot.Infrastructure/Data/BotDbContext.cs` | Database context to extend |
| `AdminModule` | `src/DiscordBot.Bot/Commands/AdminModule.cs` | Pattern for slash command modules |

### 2.2 Data Model Design

The verification system requires a new entity to track pending and completed verifications:

```
VerificationCode
├── Id (Guid) - Primary key
├── ApplicationUserId (string, FK) - User initiating verification from web UI
├── Code (string, 6 chars) - The verification code
├── DiscordUserId (ulong) - Discord user who ran /verify-account
├── Status (enum) - Pending, Completed, Expired, Cancelled
├── CreatedAt (DateTime)
├── ExpiresAt (DateTime)
├── CompletedAt (DateTime?)
└── IpAddress (string?) - IP that initiated the verification
```

**Status Flow:**
```
[Web UI] User clicks "Link via Bot"
    -> Status = Pending (no DiscordUserId yet)

[Discord] User runs /verify-account
    -> Status = Pending (DiscordUserId set, Code generated)

[Web UI] User enters code
    -> Status = Completed (accounts linked)

[Background] Code expires
    -> Status = Expired
```

### 2.3 Security Considerations

1. **Code Entropy:** 6 characters from 32-character set = 32^6 = ~1 billion combinations
2. **Brute Force Protection:** Rate limit code validation attempts per IP/user
3. **Code Expiry:** 15-minute window limits attack surface
4. **Single Use:** Codes cannot be reused after successful validation
5. **User Binding:** Code is bound to specific ApplicationUser and DiscordUser pair
6. **Ephemeral Messages:** Bot responses only visible to the user who ran the command

### 2.4 Integration Points

| Integration | Approach |
|-------------|----------|
| Web UI to Bot | Verification code serves as the bridge |
| Database | EF Core entity with proper indexing |
| Rate Limiting | Per-Discord-user limit on code generation |
| Background Cleanup | HostedService pattern like InteractionStateCleanupService |
| User Management | Activity logging for audit trail |

---

## 3. Subagent Task Plan

### 3.1 dotnet-specialist Tasks

#### Task 118.1: Create VerificationCode Entity and Enums

**Description:** Create the core domain entity for verification codes.

**File to Create:** `src/DiscordBot.Core/Entities/VerificationCode.cs`

```csharp
namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a verification code used to link a Discord account via the bot.
/// </summary>
public class VerificationCode
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to the ApplicationUser initiating verification.
    /// </summary>
    public string ApplicationUserId { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property to the ApplicationUser.
    /// </summary>
    public ApplicationUser ApplicationUser { get; set; } = null!;

    /// <summary>
    /// The 6-character verification code (e.g., "ABC-123" stored as "ABC123").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Discord user ID who ran /verify-account. Null until command is executed.
    /// </summary>
    public ulong? DiscordUserId { get; set; }

    /// <summary>
    /// Current status of the verification.
    /// </summary>
    public VerificationStatus Status { get; set; } = VerificationStatus.Pending;

    /// <summary>
    /// When the verification was initiated.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the code expires (CreatedAt + 15 minutes).
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When the verification was completed (if successful).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// IP address that initiated the verification request.
    /// </summary>
    public string? IpAddress { get; set; }
}

/// <summary>
/// Status of a verification code.
/// </summary>
public enum VerificationStatus
{
    /// <summary>Verification initiated, awaiting code entry.</summary>
    Pending = 0,

    /// <summary>Code validated, accounts linked successfully.</summary>
    Completed = 1,

    /// <summary>Code expired without being used.</summary>
    Expired = 2,

    /// <summary>Verification cancelled by user.</summary>
    Cancelled = 3
}
```

**Acceptance Criteria:**
- [ ] Entity created with all required properties
- [ ] VerificationStatus enum defined with all states
- [ ] XML documentation on all public members
- [ ] Solution builds without errors

---

#### Task 118.2: Create EF Core Configuration and Update DbContext

**Description:** Configure EF Core mapping for the VerificationCode entity.

**File to Create:** `src/DiscordBot.Infrastructure/Data/Configurations/VerificationCodeConfiguration.cs`

```csharp
using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the VerificationCode entity.
/// </summary>
public class VerificationCodeConfiguration : IEntityTypeConfiguration<VerificationCode>
{
    public void Configure(EntityTypeBuilder<VerificationCode> builder)
    {
        builder.ToTable("VerificationCodes");

        // Primary key
        builder.HasKey(v => v.Id);

        // Foreign key to ApplicationUser
        builder.HasOne(v => v.ApplicationUser)
            .WithMany()
            .HasForeignKey(v => v.ApplicationUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // String property configurations
        builder.Property(v => v.ApplicationUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(v => v.Code)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(v => v.IpAddress)
            .HasMaxLength(45);

        // Configure ulong to long conversion for SQLite compatibility
        builder.Property(v => v.DiscordUserId)
            .HasConversion(
                v => v.HasValue ? (long)v.Value : (long?)null,
                v => v.HasValue ? (ulong)v.Value : (ulong?)null);

        // Configure enum as int
        builder.Property(v => v.Status)
            .HasConversion<int>()
            .IsRequired();

        // Configure DateTime properties
        builder.Property(v => v.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(v => v.ExpiresAt)
            .IsRequired();

        // Indexes for efficient queries
        builder.HasIndex(v => v.Code);
        builder.HasIndex(v => v.ApplicationUserId);
        builder.HasIndex(v => v.DiscordUserId);
        builder.HasIndex(v => v.Status);
        builder.HasIndex(v => v.ExpiresAt);

        // Composite index for cleanup queries
        builder.HasIndex(v => new { v.Status, v.ExpiresAt });
    }
}
```

**File to Modify:** `src/DiscordBot.Infrastructure/Data/BotDbContext.cs`

Add DbSet:
```csharp
public DbSet<VerificationCode> VerificationCodes => Set<VerificationCode>();
```

**Acceptance Criteria:**
- [ ] Configuration class created with proper table mapping
- [ ] All string properties have max length constraints
- [ ] ulong conversion configured for SQLite compatibility
- [ ] Indexes created for common query patterns
- [ ] DbSet added to BotDbContext

---

#### Task 118.3: Create Database Migration

**Description:** Generate and apply the EF Core migration.

**Command:**
```bash
dotnet ef migrations add AddVerificationCodes --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

**Acceptance Criteria:**
- [ ] Migration created successfully
- [ ] Migration includes VerificationCodes table with all columns
- [ ] Migration includes all indexes
- [ ] `dotnet ef database update` succeeds

---

#### Task 118.4: Create IVerificationService Interface

**Description:** Define the service interface for verification operations.

**File to Create:** `src/DiscordBot.Core/Interfaces/IVerificationService.cs`

```csharp
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for managing Discord bot account verification codes.
/// </summary>
public interface IVerificationService
{
    /// <summary>
    /// Initiates a verification request for a user.
    /// Creates a pending verification record.
    /// </summary>
    /// <param name="applicationUserId">The user initiating verification.</param>
    /// <param name="ipAddress">Optional IP address for audit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with verification ID if successful.</returns>
    Task<VerificationInitiationResult> InitiateVerificationAsync(
        string applicationUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a verification code for a Discord user.
    /// Called when user runs /verify-account command.
    /// </summary>
    /// <param name="discordUserId">Discord user ID from the command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with generated code if successful.</returns>
    Task<CodeGenerationResult> GenerateCodeForDiscordUserAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a verification code and links accounts.
    /// </summary>
    /// <param name="applicationUserId">User submitting the code.</param>
    /// <param name="code">The verification code entered.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure with reason.</returns>
    Task<CodeValidationResult> ValidateCodeAsync(
        string applicationUserId,
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels any pending verification for a user.
    /// </summary>
    /// <param name="applicationUserId">User cancelling verification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CancelPendingVerificationAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a Discord user has exceeded the rate limit for code generation.
    /// </summary>
    /// <param name="discordUserId">Discord user ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if rate limit exceeded, false otherwise.</returns>
    Task<bool> IsRateLimitedAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current pending verification for a user, if any.
    /// </summary>
    /// <param name="applicationUserId">User to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pending verification or null.</returns>
    Task<VerificationCode?> GetPendingVerificationAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up expired verification codes.
    /// Called by background service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of codes cleaned up.</returns>
    Task<int> CleanupExpiredCodesAsync(CancellationToken cancellationToken = default);
}
```

**File to Create:** `src/DiscordBot.Core/DTOs/VerificationDtos.cs`

```csharp
namespace DiscordBot.Core.DTOs;

/// <summary>
/// Result of initiating a verification request.
/// </summary>
public class VerificationInitiationResult
{
    public bool Succeeded { get; private set; }
    public Guid? VerificationId { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static VerificationInitiationResult Success(Guid verificationId) => new()
    {
        Succeeded = true,
        VerificationId = verificationId
    };

    public static VerificationInitiationResult Failure(string errorCode, string errorMessage) => new()
    {
        Succeeded = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    // Error codes
    public const string AlreadyLinked = "ALREADY_LINKED";
    public const string PendingVerificationExists = "PENDING_EXISTS";
    public const string UserNotFound = "USER_NOT_FOUND";
}

/// <summary>
/// Result of generating a verification code.
/// </summary>
public class CodeGenerationResult
{
    public bool Succeeded { get; private set; }
    public string? Code { get; private set; }
    public string? FormattedCode { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static CodeGenerationResult Success(string code, DateTime expiresAt) => new()
    {
        Succeeded = true,
        Code = code,
        FormattedCode = FormatCode(code),
        ExpiresAt = expiresAt
    };

    public static CodeGenerationResult Failure(string errorCode, string errorMessage) => new()
    {
        Succeeded = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    private static string FormatCode(string code)
    {
        // Format as "ABC-123" for display
        if (code.Length == 6)
            return $"{code.Substring(0, 3)}-{code.Substring(3, 3)}";
        return code;
    }

    // Error codes
    public const string RateLimited = "RATE_LIMITED";
    public const string AlreadyLinked = "ALREADY_LINKED";
    public const string NoPendingVerification = "NO_PENDING_VERIFICATION";
}

/// <summary>
/// Result of validating a verification code.
/// </summary>
public class CodeValidationResult
{
    public bool Succeeded { get; private set; }
    public ulong? LinkedDiscordUserId { get; private set; }
    public string? LinkedDiscordUsername { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static CodeValidationResult Success(ulong discordUserId, string? discordUsername = null) => new()
    {
        Succeeded = true,
        LinkedDiscordUserId = discordUserId,
        LinkedDiscordUsername = discordUsername
    };

    public static CodeValidationResult Failure(string errorCode, string errorMessage) => new()
    {
        Succeeded = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    // Error codes
    public const string InvalidCode = "INVALID_CODE";
    public const string CodeExpired = "CODE_EXPIRED";
    public const string CodeAlreadyUsed = "CODE_ALREADY_USED";
    public const string UserMismatch = "USER_MISMATCH";
    public const string AlreadyLinked = "ALREADY_LINKED";
    public const string DiscordAlreadyLinked = "DISCORD_ALREADY_LINKED";
}
```

**Acceptance Criteria:**
- [ ] Interface defines all required verification operations
- [ ] DTOs provide clear success/failure patterns
- [ ] Error codes are comprehensive and documented
- [ ] Solution builds without errors

---

#### Task 118.5: Implement VerificationService

**Description:** Implement the verification service with code generation and validation logic.

**File to Create:** `src/DiscordBot.Bot/Services/VerificationService.cs`

```csharp
using System.Security.Cryptography;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for managing Discord bot account verification codes.
/// </summary>
public class VerificationService : IVerificationService
{
    private readonly BotDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<VerificationService> _logger;

    // Character set excluding ambiguous characters (0, O, 1, I, L)
    private const string CodeCharset = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 6;
    private static readonly TimeSpan CodeExpiry = TimeSpan.FromMinutes(15);
    private const int MaxCodesPerHour = 3;

    public VerificationService(
        BotDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<VerificationService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<VerificationInitiationResult> InitiateVerificationAsync(
        string applicationUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Initiating verification for user {UserId}", applicationUserId);

        // Check if user exists and doesn't already have Discord linked
        var user = await _userManager.FindByIdAsync(applicationUserId);
        if (user == null)
        {
            return VerificationInitiationResult.Failure(
                VerificationInitiationResult.UserNotFound,
                "User not found.");
        }

        if (user.DiscordUserId.HasValue)
        {
            return VerificationInitiationResult.Failure(
                VerificationInitiationResult.AlreadyLinked,
                "Discord account is already linked.");
        }

        // Cancel any existing pending verifications
        await CancelPendingVerificationAsync(applicationUserId, cancellationToken);

        // Create new pending verification
        var verification = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = applicationUserId,
            Code = string.Empty, // Code generated when Discord user runs command
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(CodeExpiry),
            IpAddress = ipAddress
        };

        _context.VerificationCodes.Add(verification);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Verification initiated for user {UserId}, verification ID: {VerificationId}",
            applicationUserId, verification.Id);

        return VerificationInitiationResult.Success(verification.Id);
    }

    public async Task<CodeGenerationResult> GenerateCodeForDiscordUserAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Generating verification code for Discord user {DiscordUserId}", discordUserId);

        // Check if Discord user is already linked to an account
        var existingUser = await _userManager.Users
            .FirstOrDefaultAsync(u => u.DiscordUserId == discordUserId, cancellationToken);

        if (existingUser != null)
        {
            return CodeGenerationResult.Failure(
                CodeGenerationResult.AlreadyLinked,
                "This Discord account is already linked to a user account.");
        }

        // Check rate limit
        if (await IsRateLimitedAsync(discordUserId, cancellationToken))
        {
            return CodeGenerationResult.Failure(
                CodeGenerationResult.RateLimited,
                "Rate limit exceeded. You can request a maximum of 3 codes per hour.");
        }

        // Find any pending verification that hasn't been claimed yet
        // (no DiscordUserId assigned means waiting for Discord command)
        var pendingVerification = await _context.VerificationCodes
            .Where(v => v.Status == VerificationStatus.Pending)
            .Where(v => v.DiscordUserId == null)
            .Where(v => v.ExpiresAt > DateTime.UtcNow)
            .OrderBy(v => v.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (pendingVerification == null)
        {
            return CodeGenerationResult.Failure(
                CodeGenerationResult.NoPendingVerification,
                "No pending verification found. Please initiate verification from the web interface first.");
        }

        // Generate unique code
        var code = GenerateUniqueCode();

        // Update the verification with Discord user and code
        pendingVerification.DiscordUserId = discordUserId;
        pendingVerification.Code = code;
        pendingVerification.ExpiresAt = DateTime.UtcNow.Add(CodeExpiry);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Verification code generated for Discord user {DiscordUserId}, verification {VerificationId}",
            discordUserId, pendingVerification.Id);

        return CodeGenerationResult.Success(code, pendingVerification.ExpiresAt);
    }

    public async Task<CodeValidationResult> ValidateCodeAsync(
        string applicationUserId,
        string code,
        CancellationToken cancellationToken = default)
    {
        // Normalize code (remove dashes, uppercase)
        code = code.Replace("-", "").Replace(" ", "").ToUpperInvariant();

        _logger.LogDebug("Validating code for user {UserId}", applicationUserId);

        // Check if user already has Discord linked
        var user = await _userManager.FindByIdAsync(applicationUserId);
        if (user == null)
        {
            return CodeValidationResult.Failure(
                CodeValidationResult.InvalidCode,
                "User not found.");
        }

        if (user.DiscordUserId.HasValue)
        {
            return CodeValidationResult.Failure(
                CodeValidationResult.AlreadyLinked,
                "Discord account is already linked.");
        }

        // Find the verification by code
        var verification = await _context.VerificationCodes
            .Where(v => v.Code == code)
            .Where(v => v.ApplicationUserId == applicationUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (verification == null)
        {
            _logger.LogWarning("Invalid verification code attempt for user {UserId}", applicationUserId);
            return CodeValidationResult.Failure(
                CodeValidationResult.InvalidCode,
                "Invalid verification code.");
        }

        if (verification.Status == VerificationStatus.Completed)
        {
            return CodeValidationResult.Failure(
                CodeValidationResult.CodeAlreadyUsed,
                "This code has already been used.");
        }

        if (verification.Status == VerificationStatus.Expired || verification.ExpiresAt <= DateTime.UtcNow)
        {
            verification.Status = VerificationStatus.Expired;
            await _context.SaveChangesAsync(cancellationToken);

            return CodeValidationResult.Failure(
                CodeValidationResult.CodeExpired,
                "Verification code has expired. Please request a new code.");
        }

        if (!verification.DiscordUserId.HasValue)
        {
            return CodeValidationResult.Failure(
                CodeValidationResult.InvalidCode,
                "Verification code not yet activated. Please run /verify-account in Discord first.");
        }

        // Check if the Discord user is already linked to another account
        var existingDiscordUser = await _userManager.Users
            .FirstOrDefaultAsync(u => u.DiscordUserId == verification.DiscordUserId, cancellationToken);

        if (existingDiscordUser != null)
        {
            return CodeValidationResult.Failure(
                CodeValidationResult.DiscordAlreadyLinked,
                "This Discord account is already linked to another user.");
        }

        // Success! Link the accounts
        user.DiscordUserId = verification.DiscordUserId;
        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            _logger.LogError(
                "Failed to update user {UserId} with Discord ID: {Errors}",
                applicationUserId,
                string.Join(", ", updateResult.Errors.Select(e => e.Description)));

            return CodeValidationResult.Failure(
                CodeValidationResult.InvalidCode,
                "Failed to link accounts. Please try again.");
        }

        // Mark verification as completed
        verification.Status = VerificationStatus.Completed;
        verification.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Successfully linked Discord user {DiscordUserId} to user {UserId}",
            verification.DiscordUserId, applicationUserId);

        return CodeValidationResult.Success(verification.DiscordUserId.Value);
    }

    public async Task CancelPendingVerificationAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default)
    {
        var pendingVerifications = await _context.VerificationCodes
            .Where(v => v.ApplicationUserId == applicationUserId)
            .Where(v => v.Status == VerificationStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var verification in pendingVerifications)
        {
            verification.Status = VerificationStatus.Cancelled;
        }

        if (pendingVerifications.Any())
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Cancelled {Count} pending verifications for user {UserId}",
                pendingVerifications.Count, applicationUserId);
        }
    }

    public async Task<bool> IsRateLimitedAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default)
    {
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);

        var recentCodeCount = await _context.VerificationCodes
            .Where(v => v.DiscordUserId == discordUserId)
            .Where(v => v.CreatedAt >= oneHourAgo)
            .CountAsync(cancellationToken);

        return recentCodeCount >= MaxCodesPerHour;
    }

    public async Task<VerificationCode?> GetPendingVerificationAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default)
    {
        return await _context.VerificationCodes
            .Where(v => v.ApplicationUserId == applicationUserId)
            .Where(v => v.Status == VerificationStatus.Pending)
            .Where(v => v.ExpiresAt > DateTime.UtcNow)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> CleanupExpiredCodesAsync(CancellationToken cancellationToken = default)
    {
        var expiredCodes = await _context.VerificationCodes
            .Where(v => v.Status == VerificationStatus.Pending)
            .Where(v => v.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var code in expiredCodes)
        {
            code.Status = VerificationStatus.Expired;
        }

        if (expiredCodes.Any())
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Marked {Count} verification codes as expired", expiredCodes.Count);
        }

        // Delete old completed/expired/cancelled codes (older than 24 hours)
        var oldCutoff = DateTime.UtcNow.AddHours(-24);
        var oldCodes = await _context.VerificationCodes
            .Where(v => v.Status != VerificationStatus.Pending)
            .Where(v => v.CreatedAt <= oldCutoff)
            .ToListAsync(cancellationToken);

        if (oldCodes.Any())
        {
            _context.VerificationCodes.RemoveRange(oldCodes);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Deleted {Count} old verification codes", oldCodes.Count);
        }

        return expiredCodes.Count + oldCodes.Count;
    }

    private string GenerateUniqueCode()
    {
        var bytes = new byte[CodeLength];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        var chars = new char[CodeLength];
        for (int i = 0; i < CodeLength; i++)
        {
            chars[i] = CodeCharset[bytes[i] % CodeCharset.Length];
        }

        return new string(chars);
    }
}
```

**File to Modify:** `src/DiscordBot.Bot/Program.cs`

Add service registration:
```csharp
builder.Services.AddScoped<IVerificationService, VerificationService>();
```

**Acceptance Criteria:**
- [ ] Service implements all interface methods
- [ ] Code generation uses cryptographically secure random
- [ ] Rate limiting enforced (3 codes per hour per Discord user)
- [ ] Code expiry enforced (15 minutes)
- [ ] Proper logging throughout
- [ ] Service registered in DI container

---

#### Task 118.6: Create Verification Cleanup Background Service

**Description:** Create a background service to clean up expired verification codes.

**File to Create:** `src/DiscordBot.Bot/Services/VerificationCleanupService.cs`

```csharp
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically cleans up expired verification codes.
/// Runs every 5 minutes.
/// </summary>
public class VerificationCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VerificationCleanupService> _logger;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    public VerificationCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<VerificationCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Verification cleanup service started, cleanup interval: {Interval}",
            CleanupInterval);

        using var timer = new PeriodicTimer(CleanupInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var verificationService = scope.ServiceProvider
                        .GetRequiredService<IVerificationService>();

                    var cleanedCount = await verificationService
                        .CleanupExpiredCodesAsync(stoppingToken);

                    if (cleanedCount > 0)
                    {
                        _logger.LogDebug(
                            "Verification cleanup completed: {CleanedCount} codes processed",
                            cleanedCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during verification code cleanup");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Verification cleanup service is stopping");
        }
    }
}
```

**File to Modify:** `src/DiscordBot.Bot/Program.cs`

Add hosted service:
```csharp
builder.Services.AddHostedService<VerificationCleanupService>();
```

**Acceptance Criteria:**
- [ ] Background service runs every 5 minutes
- [ ] Properly creates scope for scoped services
- [ ] Handles cancellation gracefully
- [ ] Logs cleanup operations
- [ ] Registered as hosted service

---

#### Task 118.7: Create /verify-account Slash Command Module

**Description:** Create the Discord slash command for account verification.

**File to Create:** `src/DiscordBot.Bot/Commands/VerifyAccountModule.cs`

```csharp
using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Discord slash command module for account verification.
/// </summary>
public class VerifyAccountModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IVerificationService _verificationService;
    private readonly ILogger<VerifyAccountModule> _logger;

    public VerifyAccountModule(
        IVerificationService verificationService,
        ILogger<VerifyAccountModule> logger)
    {
        _verificationService = verificationService;
        _logger = logger;
    }

    /// <summary>
    /// Generates a verification code to link this Discord account to a web account.
    /// </summary>
    [SlashCommand("verify-account", "Generate a verification code to link your Discord account to the admin panel")]
    [RateLimit(3, 3600, Core.Enums.RateLimitTarget.User)] // 3 per hour
    public async Task VerifyAccountAsync()
    {
        _logger.LogInformation(
            "Verify-account command executed by Discord user {Username} (ID: {UserId})",
            Context.User.Username,
            Context.User.Id);

        var result = await _verificationService.GenerateCodeForDiscordUserAsync(Context.User.Id);

        if (!result.Succeeded)
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("Verification Failed")
                .WithDescription(result.ErrorMessage)
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);

            _logger.LogWarning(
                "Verification failed for Discord user {UserId}: {ErrorCode} - {ErrorMessage}",
                Context.User.Id, result.ErrorCode, result.ErrorMessage);
            return;
        }

        var successEmbed = new EmbedBuilder()
            .WithTitle("Verification Code Generated")
            .WithDescription("Use this code to link your Discord account to your web account.")
            .WithColor(Color.Green)
            .AddField("Your Code", $"```\n{result.FormattedCode}\n```", inline: false)
            .AddField("Expires", $"<t:{new DateTimeOffset(result.ExpiresAt!.Value).ToUnixTimeSeconds()}:R>", inline: true)
            .AddField("Instructions", "1. Go to your profile page in the admin panel\n2. Enter this code in the verification form\n3. Click 'Verify' to link your account", inline: false)
            .WithFooter("This code is only visible to you and expires in 15 minutes.")
            .WithCurrentTimestamp()
            .Build();

        await RespondAsync(embed: successEmbed, ephemeral: true);

        _logger.LogInformation(
            "Verification code generated for Discord user {UserId}, expires at {ExpiresAt}",
            Context.User.Id, result.ExpiresAt);
    }
}
```

**Acceptance Criteria:**
- [ ] Command registered as `/verify-account`
- [ ] Response is always ephemeral (only visible to user)
- [ ] Rate limit applied (3 per hour)
- [ ] Success response shows formatted code and expiry
- [ ] Error responses are informative
- [ ] Proper logging

---

#### Task 118.8: Update LinkDiscord Page with Bot Verification Option

**Description:** Add "Link via Discord Bot" option to the LinkDiscord page.

**File to Modify:** `src/DiscordBot.Bot/Pages/Account/LinkDiscord.cshtml.cs`

Add the following properties and handlers:

```csharp
// Add to existing properties
/// <summary>
/// Indicates whether there is a pending verification.
/// </summary>
public bool HasPendingVerification { get; set; }

/// <summary>
/// The pending verification details.
/// </summary>
public VerificationCode? PendingVerification { get; set; }

/// <summary>
/// The verification code entered by the user.
/// </summary>
[BindProperty]
public string? VerificationCode { get; set; }

// Add IVerificationService to constructor
private readonly IVerificationService _verificationService;

// Update OnGetAsync to check for pending verification
public async Task<IActionResult> OnGetAsync()
{
    // ... existing code ...

    // Check for pending verification (only if not already linked)
    if (!IsDiscordLinked)
    {
        PendingVerification = await _verificationService.GetPendingVerificationAsync(user.Id);
        HasPendingVerification = PendingVerification != null;
    }

    return Page();
}

/// <summary>
/// Handles POST requests to initiate bot verification.
/// </summary>
public async Task<IActionResult> OnPostInitiateBotVerificationAsync()
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null)
    {
        return NotFound("User not found.");
    }

    var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
    var result = await _verificationService.InitiateVerificationAsync(user.Id, ipAddress);

    if (!result.Succeeded)
    {
        StatusMessage = result.ErrorMessage ?? "Failed to initiate verification.";
        IsSuccess = false;
    }
    else
    {
        StatusMessage = "Verification initiated. Run /verify-account in Discord to get your code.";
        IsSuccess = true;
    }

    return RedirectToPage();
}

/// <summary>
/// Handles POST requests to validate verification code.
/// </summary>
public async Task<IActionResult> OnPostVerifyCodeAsync()
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null)
    {
        return NotFound("User not found.");
    }

    if (string.IsNullOrWhiteSpace(VerificationCode))
    {
        StatusMessage = "Please enter a verification code.";
        IsSuccess = false;
        return RedirectToPage();
    }

    var result = await _verificationService.ValidateCodeAsync(user.Id, VerificationCode);

    if (!result.Succeeded)
    {
        StatusMessage = result.ErrorMessage ?? "Invalid verification code.";
        IsSuccess = false;
    }
    else
    {
        StatusMessage = "Discord account linked successfully!";
        IsSuccess = true;
    }

    return RedirectToPage();
}

/// <summary>
/// Handles POST requests to cancel pending verification.
/// </summary>
public async Task<IActionResult> OnPostCancelVerificationAsync()
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null)
    {
        return NotFound("User not found.");
    }

    await _verificationService.CancelPendingVerificationAsync(user.Id);
    StatusMessage = "Verification cancelled.";
    IsSuccess = true;

    return RedirectToPage();
}
```

**File to Modify:** `src/DiscordBot.Bot/Pages/Account/LinkDiscord.cshtml`

Add the bot verification section (in the "Discord Not Linked" section):

```html
<!-- Add after existing OAuth link button, inside the "Discord Not Linked" section -->

@if (!Model.HasPendingVerification)
{
    <!-- Divider -->
    <div class="flex items-center my-6 max-w-md mx-auto">
        <div class="flex-1 border-t border-border-secondary"></div>
        <span class="px-4 text-sm text-text-tertiary">or</span>
        <div class="flex-1 border-t border-border-secondary"></div>
    </div>

    <!-- Link via Bot Option -->
    <div class="text-center">
        <h3 class="text-lg font-semibold text-text-primary mb-2">Link via Discord Bot</h3>
        <p class="text-text-secondary text-sm mb-4 max-w-md mx-auto">
            If you prefer not to use OAuth, you can link your account using a verification code from our Discord bot.
        </p>
        <form method="post" asp-page-handler="InitiateBotVerification">
            <button
                type="submit"
                class="btn btn-secondary inline-flex items-center gap-2">
                <!-- Bot Icon -->
                <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                Link via Discord Bot
            </button>
        </form>
    </div>
}
else
{
    <!-- Pending Verification State -->
    <div class="max-w-md mx-auto mt-6 p-4 bg-info-bg border border-info-border rounded-lg">
        <h3 class="text-lg font-semibold text-text-primary mb-2">Verification in Progress</h3>
        <p class="text-sm text-text-secondary mb-4">
            Run the <code class="bg-bg-tertiary px-1 py-0.5 rounded text-text-primary">/verify-account</code> command in Discord to get your verification code.
        </p>

        @if (Model.PendingVerification?.ExpiresAt > DateTime.UtcNow)
        {
            <p class="text-xs text-text-tertiary mb-4">
                Expires: <time datetime="@Model.PendingVerification.ExpiresAt.ToString("O")">
                    @Model.PendingVerification.ExpiresAt.ToString("HH:mm:ss UTC")
                </time>
            </p>
        }

        <!-- Code Entry Form -->
        <form method="post" asp-page-handler="VerifyCode" class="space-y-4">
            <div>
                <label for="verificationCode" class="block text-sm font-medium text-text-primary mb-1">
                    Verification Code
                </label>
                <input
                    type="text"
                    id="verificationCode"
                    name="VerificationCode"
                    placeholder="ABC-123"
                    maxlength="7"
                    class="w-full px-3 py-2 bg-bg-primary border border-border-primary rounded-md text-text-primary placeholder-text-tertiary focus:outline-none focus:ring-2 focus:ring-accent-blue focus:border-accent-blue font-mono text-lg tracking-widest text-center uppercase"
                    autocomplete="off"
                    spellcheck="false" />
            </div>
            <div class="flex gap-3">
                <button
                    type="submit"
                    class="btn btn-primary flex-1">
                    Verify
                </button>
                <form method="post" asp-page-handler="CancelVerification" class="inline">
                    <button
                        type="submit"
                        class="btn btn-ghost">
                        Cancel
                    </button>
                </form>
            </div>
        </form>
    </div>
}
```

**Acceptance Criteria:**
- [ ] "Link via Discord Bot" button shown when no OAuth configured or as alternative
- [ ] Initiation creates pending verification
- [ ] Code entry form shown when verification is pending
- [ ] Successful verification links accounts and shows success message
- [ ] Cancel option available
- [ ] Form validation for code format
- [ ] Styling matches design system

---

#### Task 118.9: Add Verification Status Display to User Profile

**Description:** Show Discord linking status and verification state in user profile areas.

This is covered by Task 118.8 updates to LinkDiscord page. The page already shows:
- Whether Discord is linked (existing functionality)
- Pending verification state (new)
- Verification form (new)

**Acceptance Criteria:**
- [ ] LinkDiscord page shows all verification states clearly
- [ ] User can see verification progress
- [ ] Clear feedback on success/failure

---

### 3.2 docs-writer Tasks

#### Task 118.10: Document Verification Process

**Description:** Create user-facing and developer documentation for the verification feature.

**File to Create:** `docs/articles/bot-verification.md`

```markdown
# Discord Bot Account Verification

This document describes how to link a Discord account to a web account using the Discord bot verification system.

## Overview

Users who create accounts via email/password can link their Discord account without using OAuth by using a verification code from the Discord bot.

## User Flow

### Step 1: Initiate Verification

1. Log into the admin panel
2. Navigate to **Account** > **Link Discord**
3. Click **Link via Discord Bot**

### Step 2: Get Verification Code

1. Open Discord
2. Go to any server where the bot is present
3. Run the slash command: `/verify-account`
4. The bot will reply with a 6-character code (e.g., `ABC-123`)
5. This code is only visible to you (ephemeral message)

### Step 3: Enter Code

1. Return to the admin panel
2. Enter the verification code in the form
3. Click **Verify**
4. Your Discord account is now linked!

## Security Features

- **Code Expiry:** Codes expire after 15 minutes
- **Rate Limiting:** Maximum 3 codes per hour per Discord user
- **Single Use:** Codes cannot be reused after successful verification
- **Ephemeral Messages:** Bot responses are only visible to the user

## Troubleshooting

### "No pending verification found"

You must initiate verification from the web interface before running the Discord command.

### "Rate limit exceeded"

Wait up to one hour before requesting another code.

### "Code expired"

Request a new code by running `/verify-account` again.

### "Discord account already linked"

Your Discord account is already linked to another user account. Contact an administrator if this is unexpected.

## Technical Details

For developers implementing or maintaining this feature:

- Verification codes use charset: `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`
- Code length: 6 characters
- Expiry: 15 minutes
- Rate limit: 3 codes per Discord user per hour
- Background cleanup runs every 5 minutes
```

**Acceptance Criteria:**
- [ ] User-facing documentation is clear and complete
- [ ] All flow steps documented
- [ ] Troubleshooting section covers common issues
- [ ] Technical details included for developers

---

### 3.3 Testing Tasks

#### Task 118.11: Write Unit Tests for VerificationService

**Description:** Create comprehensive unit tests for the verification service.

**File to Create:** `tests/DiscordBot.Tests/Services/VerificationServiceTests.cs`

Test cases to cover:
- `InitiateVerificationAsync_CreatesVerification`
- `InitiateVerificationAsync_FailsWhenAlreadyLinked`
- `InitiateVerificationAsync_CancelsPreviousPending`
- `GenerateCodeForDiscordUserAsync_GeneratesUniqueCode`
- `GenerateCodeForDiscordUserAsync_FailsWhenRateLimited`
- `GenerateCodeForDiscordUserAsync_FailsWhenDiscordAlreadyLinked`
- `GenerateCodeForDiscordUserAsync_FailsWhenNoPendingVerification`
- `ValidateCodeAsync_LinksAccountsOnSuccess`
- `ValidateCodeAsync_FailsWithInvalidCode`
- `ValidateCodeAsync_FailsWithExpiredCode`
- `ValidateCodeAsync_FailsWhenCodeAlreadyUsed`
- `IsRateLimitedAsync_ReturnsTrueAfterThreeCodes`
- `CleanupExpiredCodesAsync_MarksExpiredCodes`
- `CleanupExpiredCodesAsync_DeletesOldCodes`

**Acceptance Criteria:**
- [ ] All service methods have test coverage
- [ ] Tests use in-memory database
- [ ] Tests mock external dependencies
- [ ] All tests pass

---

#### Task 118.12: Write Integration Tests for Verification Flow

**Description:** Create integration tests for the full verification flow.

**File to Create:** `tests/DiscordBot.Tests/Integration/VerificationFlowTests.cs`

Test cases:
- Full flow: Initiate -> Generate Code -> Validate -> Accounts Linked
- Expiry flow: Initiate -> Wait -> Code Expired
- Rate limit flow: Generate 3 codes -> 4th fails
- Cancel flow: Initiate -> Cancel -> No pending verification

**Acceptance Criteria:**
- [ ] Integration tests cover full flows
- [ ] Tests verify database state changes
- [ ] Tests pass

---

## 4. Timeline / Dependency Map

```
Phase 1: Foundation (Day 1)
├── Task 118.1: Create VerificationCode Entity
├── Task 118.2: Create EF Core Configuration
└── Task 118.3: Create Database Migration

Phase 2: Service Layer (Day 1-2)
├── Task 118.4: Create IVerificationService Interface
├── Task 118.5: Implement VerificationService (depends on 118.1-118.3)
└── Task 118.6: Create Cleanup Background Service (depends on 118.4-118.5)

Phase 3: Discord Integration (Day 2)
└── Task 118.7: Create /verify-account Command (depends on 118.5)

Phase 4: Web UI (Day 2-3)
├── Task 118.8: Update LinkDiscord Page (depends on 118.5)
└── Task 118.9: Verification Status Display (depends on 118.8)

Phase 5: Documentation & Testing (Day 3)
├── Task 118.10: Document Verification Process
├── Task 118.11: Write Unit Tests (depends on 118.5)
└── Task 118.12: Write Integration Tests (depends on all)
```

**Parallel Opportunities:**
- Tasks 118.1, 118.2 can be done in parallel
- Tasks 118.7, 118.8 can start once 118.5 is complete
- Task 118.10 can start once design is finalized

**Estimated Timeline:** 3 days

---

## 5. Acceptance Criteria Summary

### Functional Requirements

- [ ] User can initiate verification from web UI
- [ ] Discord bot generates codes via `/verify-account` command
- [ ] Codes are formatted as "ABC-123" for easy reading
- [ ] Codes expire after 15 minutes
- [ ] Rate limiting enforced (3 codes per hour per Discord user)
- [ ] Successful verification links Discord ID to ApplicationUser
- [ ] User receives clear feedback at each step
- [ ] Expired codes are cleaned up automatically

### Technical Requirements

- [ ] VerificationCode entity with proper EF Core configuration
- [ ] Database migration created and applied
- [ ] IVerificationService interface with clear contracts
- [ ] VerificationService implements all required logic
- [ ] Background cleanup service runs every 5 minutes
- [ ] Slash command registered and working
- [ ] UI updated with bot verification option
- [ ] All services registered in DI container

### Security Requirements

- [ ] Codes use cryptographically secure random generation
- [ ] Bot responses are ephemeral (only visible to user)
- [ ] Codes are single-use
- [ ] Rate limiting prevents abuse
- [ ] Code charset excludes ambiguous characters

### Documentation Requirements

- [ ] User documentation created
- [ ] Troubleshooting guide included
- [ ] Technical details documented

### Testing Requirements

- [ ] Unit tests for VerificationService
- [ ] Integration tests for full flow
- [ ] All tests pass

---

## 6. Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Race conditions in code generation | Low | Medium | Use database uniqueness constraints, handle conflicts |
| Bot command not discovered | Medium | Low | Clear instructions in UI, documentation |
| Code brute force attempts | Low | Low | Rate limiting on validation endpoint |
| User confusion with flow | Medium | Medium | Clear step-by-step instructions, progress indicators |
| Timezone issues with expiry | Low | Low | Use UTC consistently, show relative time |

### Security Considerations

1. **Code entropy is sufficient:** 32^6 = ~1 billion combinations
2. **Rate limiting on both generation and validation** prevents brute force
3. **Ephemeral messages** ensure codes aren't visible to others
4. **15-minute expiry** limits attack window
5. **User binding** ensures codes can't be used by others

---

## 7. File Summary

### Files to Create

| File | Project | Description |
|------|---------|-------------|
| `Entities/VerificationCode.cs` | Core | Verification code entity and enum |
| `DTOs/VerificationDtos.cs` | Core | Result DTOs for verification operations |
| `Interfaces/IVerificationService.cs` | Core | Service interface |
| `Data/Configurations/VerificationCodeConfiguration.cs` | Infrastructure | EF Core configuration |
| `Services/VerificationService.cs` | Bot | Service implementation |
| `Services/VerificationCleanupService.cs` | Bot | Background cleanup service |
| `Commands/VerifyAccountModule.cs` | Bot | Discord slash command |
| `articles/bot-verification.md` | docs | User documentation |
| `Services/VerificationServiceTests.cs` | Tests | Unit tests |
| `Integration/VerificationFlowTests.cs` | Tests | Integration tests |

### Files to Modify

| File | Changes |
|------|---------|
| `BotDbContext.cs` | Add VerificationCodes DbSet |
| `Program.cs` | Register services and hosted service |
| `LinkDiscord.cshtml.cs` | Add verification handlers |
| `LinkDiscord.cshtml` | Add bot verification UI |

### Migration to Create

```bash
dotnet ef migrations add AddVerificationCodes --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

---

*Document prepared by: Systems Architect*
*Ready for implementation by: dotnet-specialist, docs-writer*
