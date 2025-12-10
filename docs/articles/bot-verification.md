# Discord Bot Account Verification

This guide explains how to link your Discord account to your user account using the Discord bot account verification feature as an alternative to OAuth authentication.

**Feature Status:** Implemented (Issue #118)

**Use Case:** Users who prefer a code-based verification flow instead of OAuth, or for troubleshooting Discord account linking issues.

---

## Table of Contents

- [Overview](#overview)
- [Why Use Bot Verification?](#why-use-bot-verification)
- [User Flow](#user-flow)
- [Security Features](#security-features)
- [Troubleshooting](#troubleshooting)
- [Technical Details](#technical-details-for-developers)

---

## Overview

The Discord bot account verification feature provides an alternative method for linking your Discord account to your user account. Instead of redirecting through Discord's OAuth2 flow, you generate a unique verification code within the application and confirm it in Discord using a bot slash command.

**What This Feature Does:**

1. Generates a unique 6-character verification code
2. Provides a 15-minute window to confirm the code in Discord
3. Validates the code through a Discord bot command
4. Links your Discord account to your user account upon successful verification

**When You Might Use This:**

- Prefer not to use OAuth2 for privacy reasons
- Testing Discord account linking functionality
- Troubleshooting failed OAuth flows
- Quick account verification without full OAuth authorization

---

## Why Use Bot Verification?

### Bot Verification vs. Discord OAuth

| Aspect | Bot Verification | Discord OAuth |
|--------|------------------|-----------------|
| **Setup Time** | Immediate (60 seconds) | Requires Discord OAuth configuration |
| **Privacy** | Minimal permissions requested | Requests email, profile, guilds access |
| **Code Entry** | Manual code entry in bot | Automatic redirect |
| **Use Cases** | Quick linking, testing, troubleshooting | Primary authentication method |
| **Code Expiry** | 15 minutes | No expiry (after authorization) |

**Recommendation:** Use bot verification for quick testing and as an alternative verification method. Use OAuth for your primary authentication system.

---

## User Flow

### Step-by-Step Verification Process

#### Step 1: Initiate Verification from Web UI

1. Log in to the admin web interface with your email and password
2. Navigate to your profile or account settings
3. Find the **"Link via Discord Bot"** button in the Discord account section
4. Click the button to generate a verification code

   You'll see a success message:
   > Verification code generated. You have 15 minutes to confirm this code in Discord.

#### Step 2: Receive Your Verification Code

The code is displayed on the screen in one of two ways:

**Option A: Code Visible in Dialog**
- A dialog or message displays your 6-character code
- Example code: `ABC2DE`
- Copy this code for use in Step 3

**Option B: Code Sent in Discord DM (Future Feature)**
- If configured, the bot may DM you the code
- Check your Discord direct messages for the bot message

**Important:** Write down or copy your code immediately. You cannot retrieve it after closing the dialog.

#### Step 3: Confirm Code in Discord

1. Open Discord
2. Go to any server where the bot is a member
3. Use the `/verify-account` command:
   ```
   /verify-account code:ABC2DE
   ```
   Replace `ABC2DE` with your actual code.

4. Execute the command

**What You'll See:**

**Success:**
> Verification successful! Your Discord account has been linked to your user account.

**Failure (see troubleshooting section for each error)**

#### Step 4: Confirm Linking in Web UI

1. Return to the web interface in your browser
2. Refresh the page if not automatic
3. Your Discord account information is now displayed:
   - Discord username (e.g., `JohnDoe#1234`)
   - Discord user ID
   - Profile avatar image
4. A badge or indicator shows "Discord account linked"

**Verification Complete!** Your Discord account is now linked to your user account.

---

## Security Features

### Code Expiry

**Expiry Time:** 15 minutes from generation

**Why 15 Minutes?**
- Long enough to complete the verification process
- Short enough to limit exposure if code is leaked
- Aligns with common two-factor authentication practices

**What Happens After Expiry:**
- Code becomes invalid
- Verification command returns: `"Code expired"`
- You must generate a new code (Step 1)

### Rate Limiting

**Rate Limit:** 3 codes per hour per Discord user

**Example Timeline:**
- 09:00 - Generate code #1
- 09:15 - Generate code #2
- 09:30 - Generate code #3
- 09:45 - Cannot generate code #4 (rate limited)
- 10:00 - Rate limit resets, can generate new code

**Why Rate Limiting?**
- Prevents brute-force attacks
- Limits spam and abuse
- Reasonable for legitimate users (3 attempts per hour is plenty)

**Error When Rate Limited:**
> Rate limit exceeded. You can generate 3 codes per hour. Please try again later.

### Single-Use Codes

**Each Code Can Only Be Used Once**

Once you successfully verify a code:
- That code is immediately invalidated
- Attempting to use the same code again fails
- You must generate a new code

**Error When Code Already Used:**
> This code has already been used. Generate a new verification code.

### Ephemeral Bot Messages

**Message Privacy:** The bot's responses in Discord are ephemeral (temporary).

**What This Means:**
- Messages only appear to you
- Other users in the channel cannot see the verification response
- Messages disappear after a short time
- Prevents exposure of sensitive verification messages

---

## Troubleshooting

### Verification Code Errors

#### Error: "No pending verification found"

**Symptoms:** Running `/verify-account` returns this error.

**Causes:**
1. Code has expired (more than 15 minutes old)
2. Code was for a different Discord user
3. Code doesn't exist or was never generated

**Solutions:**
1. **Expired Code:** Generate a new code in the web UI (Step 1)
2. **Wrong Discord Account:** Make sure you're running the command in Discord with the account you want to link
   - If using multiple Discord accounts, switch to the correct account
   - Sign out and back into Discord if needed
3. **Verification Not Initiated:** Return to the web UI and click "Link via Discord Bot" again

**Steps to Recover:**
1. Go back to the web UI
2. Ensure you're logged in with your user account
3. Click "Link via Discord Bot" to generate a new code
4. Copy the new code
5. Run `/verify-account code:NEWCODE` in Discord

---

#### Error: "Rate limit exceeded"

**Symptoms:** Cannot generate a new verification code.

**Message:**
> Rate limit exceeded. You can generate 3 codes per hour. Please try again later.

**Cause:** You've already generated 3 codes within the last 60 minutes.

**Solutions:**
1. **Wait for Rate Limit to Reset:** Wait until an hour has passed since your first code generation
2. **Use Existing Code:** If you have a valid code within 15 minutes, use that instead of generating a new one
3. **Check Timing:** The rate limit resets on a rolling hour basis
   - Example: If you generated codes at 9:00, 9:15, and 9:30, you can generate a new code at 10:00

**Example Timeline to Avoid Rate Limiting:**
- 9:00 - Generate code #1, successfully verify
- 9:30 - Need to re-link? Generate code #2
- 10:00 - Can generate code #3 (1 hour has passed since code #1)

---

#### Error: "Code expired"

**Symptoms:** Running `/verify-account` with a code returns this error.

**Message:**
> Code expired. Generate a new verification code and try again.

**Cause:** More than 15 minutes have passed since the code was generated.

**Solutions:**
1. **Generate New Code:**
   - Return to the web UI
   - Click "Link via Discord Bot"
   - Copy the new code immediately
   - Run `/verify-account code:NEWCODE` within 15 minutes

2. **Work Faster:**
   - Prepare to run the Discord command before generating the code
   - Have Discord open in another window/tab
   - Copy the code and run the command immediately

**Why the 15-Minute Limit?**
- Security measure to limit exposure time
- Reasonable for most users to complete verification
- If you need more time, generate a new code

---

#### Error: "Discord account already linked"

**Symptoms:** Running `/verify-account` with a valid code returns this error.

**Message:**
> This Discord account is already linked to a user account.

**Causes:**
1. Your Discord account is already linked to your current user account
2. Your Discord account is linked to a different user account
3. You're trying to link the same Discord account to multiple user accounts

**Solutions:**

**If Linked to Your Account (Current Linking):**
- Verification is successful—your Discord account was linked
- Check the web UI; you should see the Discord account information
- If attempting to re-verify, your account is already linked

**If Linked to a Different Account:**
1. Verify which user account owns the Discord link:
   - Log in to the user account that has Discord linked
   - Go to account settings
   - You should see the Discord account information
2. Unlink from that account if you want to relink to a different account:
   - Click "Unlink Discord Account"
   - Confirm the action
   - Return to your current account and generate a new code
3. Generate a new code in your current account and retry `/verify-account`

**If Attempting to Link Multiple Accounts:**
- Each Discord account can only link to one user account
- You cannot link your Discord account to multiple user accounts
- If you need multiple accounts, use different Discord accounts or email/password login

---

#### Error: "Invalid code format"

**Symptoms:** Running `/verify-account` returns this error.

**Message:**
> Invalid code format. Code must be 6 characters.

**Cause:** The code you entered doesn't match the required format.

**Code Requirements:**
- Exactly 6 characters long
- Valid characters: `A-Z`, `2-9` (no 0, 1, I, O, or L to avoid confusion)
- Example valid codes: `ABC2DE`, `XYZ789`, `ABCDEF`

**Solutions:**
1. **Copy Code Exactly:** When copying from the web UI, ensure you copy all 6 characters
   - Don't include spaces before/after
   - Check for accidental extra characters
2. **Check Code Format:** Verify the code contains only letters and numbers
   - No spaces
   - No dashes
   - No special characters
3. **Generate New Code:** If in doubt, generate a fresh code in the web UI
4. **Type Carefully:** If typing manually, be careful with similar characters:
   - 0 vs O (the letter O is not valid in codes)
   - 1 vs I vs L (numbers don't use these letters)

**Example Invalid vs Valid:**
```
Invalid: "ABC-2DE"  (contains dash)
Invalid: "ABC2DEF"  (7 characters)
Invalid: "AB2DE"    (5 characters)
Valid:   "ABC2DE"   (6 characters, correct format)
```

---

### Web UI Errors

#### "Failed to generate verification code"

**Symptoms:** Clicking "Link via Discord Bot" shows an error.

**Cause:** Server-side error generating the code.

**Solutions:**
1. **Refresh the Page:** Try refreshing the web UI and attempt again
2. **Clear Cache:** Clear browser cache and retry
3. **Check Connection:** Ensure you have a stable internet connection
4. **Contact Admin:** If the error persists, contact the application administrator

**What's Being Logged:**
- Application logs contain details of the generation error
- The administrator can check logs for specific failure reasons

---

#### Code Not Displaying

**Symptoms:** You clicked "Link via Discord Bot" but no code appears.

**Possible Causes:**
1. Dialog/modal failed to display
2. Browser popup blocked
3. JavaScript error

**Solutions:**
1. **Check Popups:** Verify popups are not blocked for this domain
   - Browser address bar may show a popup blocker notification
   - Whitelist the domain to allow popups
2. **Try Again:** Refresh the page and retry
3. **Use Different Browser:** Try a different browser to rule out browser-specific issues
4. **Check Console:** Open browser developer tools (F12) and check for JavaScript errors

---

### Discord Command Issues

#### "Discord bot is offline"

**Symptoms:** Running `/verify-account` shows "Discord bot is offline" or the command doesn't appear.

**Cause:** The Discord bot is not connected to Discord.

**Solutions:**
1. **Wait a Moment:** If the bot was just started, wait 5-10 seconds for it to connect
2. **Check Bot Status:** In your Discord server member list, verify the bot shows as online
   - If offline, the bot service needs to be restarted
   - Contact your system administrator
3. **Verify Bot Has Permissions:** Ensure the bot has permission to respond in the channel
   - Right-click the channel
   - Check channel permissions
   - Bot should have "Send Messages" and "Use Application Commands" permissions

---

#### Command `/verify-account` Not Appearing

**Symptoms:** You can't find the `/verify-account` command in Discord.

**Causes:**
1. Bot is not registered in your server
2. Commands haven't registered yet
3. Bot doesn't have `applications.commands` permission

**Solutions:**
1. **Wait for Registration:** Slash commands can take up to 1 hour to register globally
   - If the test guild ID is configured, commands appear instantly in that test guild
   - Otherwise, wait up to 1 hour
2. **Re-Invite Bot:** Use the OAuth2 URL generator to re-invite the bot with correct permissions:
   - Scopes: `bot`, `applications.commands`
   - Permissions: `Send Messages`, `Use Application Commands`
3. **Check Bot Permissions:** Ensure the bot has `applications.commands` scope in your server

---

### Discord Account Already Linked

#### "This Discord account is already linked"

**Symptoms:** Verification succeeds in Discord, but you can't link a second Discord account to your user account.

**Behavior:** Each user account can only have one Discord account linked at a time.

**Solutions:**

**To Link a Different Discord Account:**
1. Unlink your current Discord account:
   - Go to account settings
   - Find "Linked Discord Account" section
   - Click "Unlink Discord"
   - Confirm the action
2. Generate a new verification code with your new Discord account
3. Run `/verify-account` in Discord with the new account

**To Link an Additional Discord Account (Not Supported):**
- The system supports only one Discord account per user account
- If you have multiple Discord accounts:
  - Create separate user accounts for each Discord account, or
  - Choose your primary Discord account to link

---

## Technical Details (For Developers)

### Code Generation and Storage

**Code Characteristics:**
- Length: 6 characters
- Character Set: `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`
  - Excludes: 0 (zero), 1 (one), I, O, L (confusing characters)
- Generation: Cryptographically random
- Storage: Hashed in database (plaintext never stored)

**Code Table Schema:**

```csharp
public class DiscordAccountVerification
{
    public Guid Id { get; set; }
    public string UserId { get; set; }                  // User requesting verification
    public ulong DiscordUserId { get; set; }            // Discord ID verifying against
    public string CodeHash { get; set; }                // Hashed verification code
    public DateTime GeneratedAt { get; set; }           // Creation timestamp
    public DateTime ExpiresAt { get; set; }             // 15 minutes from generated
    public bool IsUsed { get; set; }                    // Single-use flag
    public DateTime? VerifiedAt { get; set; }           // When code was verified
}
```

### Code Lifecycle

1. **Generation (Step 1 in user flow):**
   - Code generated as 6 random characters
   - Hashed using PBKDF2 (same as passwords)
   - Stored with `GeneratedAt` = now
   - `ExpiresAt` = now + 15 minutes
   - `IsUsed` = false

2. **Validation (Step 3 in user flow):**
   - User submits code via `/verify-account` command
   - System searches for matching code hash
   - Checks: not expired, not used, Discord user ID matches
   - If valid, sets `IsUsed` = true and `VerifiedAt` = now

3. **Cleanup (Background Task):**
   - Runs every 5 minutes
   - Deletes verification records older than 15 minutes
   - Prevents database bloat from expired codes

### Rate Limiting Implementation

**Rate Limit Data:**

```csharp
public class DiscordVerificationRateLimit
{
    public Guid Id { get; set; }
    public ulong DiscordUserId { get; set; }           // Discord user being rate limited
    public int CodeGenerationCount { get; set; }       // Number of codes generated this hour
    public DateTime ResetTime { get; set; }            // When count resets
}
```

**Rate Limit Logic:**

```
1. User requests code generation
2. Look up rate limit record for Discord user ID
3. If reset time has passed:
   - Reset count to 0
   - Update reset time to +1 hour from now
4. If count >= 3:
   - Return rate limit error
   - Exit without generating code
5. If count < 3:
   - Increment count
   - Generate and store code
   - Save updated rate limit
```

**Rate Limit Reset:**
- Rolls based on individual user's generation timestamps
- Example: If user generated codes at 9:00, 9:15, 9:30, they can generate again at 10:00, 10:15, 10:30

### Bot Command Handler

**Command Path:** `Commands/VerifyAccountModule.cs`

**Command Definition:**

```csharp
[SlashCommand("verify-account", "Link your Discord account to your user account")]
public async Task VerifyAccountAsync(
    [Summary(name: "code", description: "Your 6-character verification code")]
    string code)
{
    // Command implementation handles:
    // 1. Code format validation
    // 2. Code existence and validity check
    // 3. Code expiry check
    // 4. Code single-use verification
    // 5. Discord user ID matching
    // 6. Account linking in database
    // 7. Ephemeral response to user
}
```

**Response Behavior:**
- All responses are ephemeral (InteractionResponseType.DeferredChannelMessageWithSource + ephemeral)
- Only the command executor sees the response
- Response disappears after a short time
- No message history in channel

### State Management

**Storage Service:** `IInteractionStateService`

**State Key Pattern:**
```
discord-verification:{discordUserId}:{correlationId}
```

**State Contents:**
```json
{
  "code": "ABC2DE",
  "userId": "user-guid",
  "generatedAt": "2024-12-09T15:30:00Z",
  "expiresAt": "2024-12-09T15:45:00Z"
}
```

**State Expiry:** 15 minutes (matches code expiry)

### Security Considerations

**Code Security:**
- Never transmitted in plain text
- Hashed with PBKDF2 before storage
- Matched using constant-time comparison (prevents timing attacks)

**Rate Limiting Security:**
- Per Discord user (prevents cross-account attacks)
- Hourly rolling window
- 3 codes/hour is reasonable limit

**Session Security:**
- Ephemeral Discord messages hide codes from others
- Code must be matched to correct Discord user ID
- Each user account can have only one Discord account

**Database Security:**
- Foreign key constraints prevent orphaned verifications
- Cascade delete when user is deleted
- Audit logging of verification events

### Configuration

**Default Configuration (in `appsettings.json`):**

```json
{
  "Discord": {
    "AccountVerification": {
      "CodeExpiryMinutes": 15,
      "CodeLength": 6,
      "RateLimitCodesPerHour": 3,
      "BackgroundCleanupIntervalMinutes": 5
    }
  }
}
```

**Environment Override Example:**

```bash
# Rate limit to 5 codes per hour
Discord__AccountVerification__RateLimitCodesPerHour=5

# Increase code expiry to 30 minutes
Discord__AccountVerification__CodeExpiryMinutes=30
```

### Background Services

**Expired Code Cleanup Task:**

- Runs every 5 minutes (configurable)
- Deletes all verification records where `ExpiresAt < DateTime.UtcNow`
- Logs count of deleted records
- Runs at application startup and then on interval
- Service: `DiscordVerificationCleanupService`

**Logging:**

```
[INF] Discord verification cleanup: Deleted 5 expired verification codes
[INF] Next cleanup scheduled in 5 minutes
```

---

## Related Documentation

- [Identity Configuration](identity-configuration.md) - Authentication setup and Discord OAuth
- [User Management Guide](user-management.md) - Managing Discord account links for users
- [Interactive Components](interactive-components.md) - Discord component patterns

---

**Last Updated:** December 9, 2024

**Feature Version:** 1.0 (Issue #118)
