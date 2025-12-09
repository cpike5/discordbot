# Discord Bot Setup Guide

This guide walks you through creating a Discord bot application, obtaining a bot token, and configuring it for use with this project.

## Overview

A Discord bot token is a secret credential that allows your application to authenticate with Discord's API as a bot user. This token acts like a password and must be kept secure—never commit it to version control or share it publicly.

**What you'll accomplish:**
- Create a Discord application in the Developer Portal
- Generate a bot token
- Configure necessary permissions and intents
- Invite the bot to your Discord server
- Configure the token in your development environment

## Prerequisites

- A Discord account (sign up at https://discord.com if needed)
- A Discord server where you have "Manage Server" permission (for inviting the bot)
- This project cloned and ready for configuration

## Step 1: Create a Discord Application

1. Navigate to the [Discord Developer Portal](https://discord.com/developers/applications)

2. Sign in with your Discord account if prompted

3. Click the **"New Application"** button in the top-right corner

4. Enter a name for your application (e.g., "My Bot" or "Server Manager Bot")
   - This name will be visible to users
   - You can change it later if needed

5. Review and accept Discord's Developer Terms of Service and Developer Policy

6. Click **"Create"**

You now have a Discord application! The General Information page displays your Application ID and other basic settings.

## Step 2: Create a Bot User

1. In the left sidebar, click **"Bot"**

2. Click the **"Add Bot"** button

3. Confirm by clicking **"Yes, do it!"** in the popup

Your bot user is now created. You'll see the bot's username and icon on this page.

## Step 3: Configure Bot Settings

### Bot Token

1. Under the "Token" section, click **"Reset Token"**
   - If this is a new bot, you may see "Copy" instead of "Reset Token"

2. Click **"Yes, do it!"** to confirm

3. Your bot token will be displayed. Click **"Copy"** to copy it to your clipboard

**SECURITY WARNING:**
- Treat this token like a password—anyone with this token can control your bot
- Never commit the token to Git or share it publicly
- If your token is ever compromised, return to this page and reset it immediately
- The token will only be shown once after resetting—if you lose it, you'll need to reset it again

### Privileged Gateway Intents

Some bot features require special permissions called "Privileged Gateway Intents":

1. Scroll down to the **"Privileged Gateway Intents"** section

2. Enable the following intents based on your bot's needs:
   - **Presence Intent**: Required if your bot needs to see when users are online/offline
   - **Server Members Intent**: Required for member join/leave events and accessing member lists
   - **Message Content Intent**: Required to read message content (not needed for slash commands only)

3. Click **"Save Changes"**

**Note:** This project uses slash commands by default, which don't require Message Content Intent. Enable only the intents your bot actually needs.

## Step 4: Configure Bot Permissions

In the Bot settings page, configure these settings:

- **Public Bot**: Toggle OFF if you want only you to be able to invite the bot
- **Requires OAuth2 Code Grant**: Leave OFF (not needed for typical bots)
- **Bot Permissions**: These are just defaults—you'll set actual permissions during invitation

## Step 5: Generate Bot Invitation URL

1. In the left sidebar, click **"OAuth2"** → **"URL Generator"**

2. Under **"Scopes"**, select:
   - `bot` - Required for bot functionality
   - `applications.commands` - Required for slash commands

3. Under **"Bot Permissions"**, select the permissions your bot needs. For this project, recommended permissions are:

   **General Permissions:**
   - Read Messages/View Channels

   **Text Permissions:**
   - Send Messages
   - Send Messages in Threads
   - Embed Links
   - Attach Files
   - Read Message History
   - Add Reactions

   **Voice Permissions (if using voice features):**
   - Connect
   - Speak

   **Advanced Permissions (use with caution):**
   - Manage Roles (if your bot manages roles)
   - Manage Channels (if your bot manages channels)
   - Kick Members (if implementing moderation)
   - Ban Members (if implementing moderation)

4. Copy the **"Generated URL"** at the bottom of the page

**Permission Notes:**
- Grant only the permissions your bot actually needs (principle of least privilege)
- You can always add more permissions later by generating a new invitation link
- Server administrators can modify bot permissions after invitation

## Step 6: Invite Bot to Your Server

1. Paste the generated URL into your browser

2. Select the Discord server where you want to add the bot
   - You must have "Manage Server" permission on the server

3. Review the permissions the bot is requesting

4. Click **"Authorize"**

5. Complete the CAPTCHA verification if prompted

Your bot should now appear in your server's member list (offline until you run the application).

## Step 7: Configure the Token in Your Project

**Never commit your bot token to Git.** Use .NET User Secrets to store it securely in your local development environment.

### Using User Secrets (Recommended)

1. Open a terminal in your project root directory

2. Navigate to the bot project:
   ```bash
   cd src/DiscordBot.Bot
   ```

3. Set your bot token:
   ```bash
   dotnet user-secrets set "Discord:Token" "your-bot-token-here"
   ```
   Replace `your-bot-token-here` with the token you copied in Step 3.

4. Verify the secret was set:
   ```bash
   dotnet user-secrets list
   ```

User Secrets are stored outside your project directory and will never be committed to Git. The UserSecretsId for this project is: `7b84433c-c2a8-46db-a8bf-58786ea4f28e`

### Alternative: Environment Variables (Production)

For production deployments, set the token as an environment variable:

```bash
# Linux/macOS
export Discord__Token="your-bot-token-here"

# Windows PowerShell
$env:Discord__Token="your-bot-token-here"

# Windows Command Prompt
set Discord__Token=your-bot-token-here
```

Note the double underscore `__` in the environment variable name (replaces the `:` from the JSON config path).

## Step 8: Get Test Guild ID (Optional but Recommended)

During development, you can use a Test Guild ID to instantly register slash commands instead of waiting up to an hour for global command registration.

### Enable Developer Mode in Discord

1. Open Discord
2. Click the gear icon (User Settings) at the bottom left
3. Go to **"App Settings"** → **"Advanced"**
4. Enable **"Developer Mode"**
5. Click "ESC" to close settings

### Copy Your Server/Guild ID

1. Right-click on your server icon in the left sidebar
2. Click **"Copy Server ID"** (this option only appears with Developer Mode enabled)

### Configure Test Guild ID

```bash
cd src/DiscordBot.Bot
dotnet user-secrets set "Discord:TestGuildId" "your-guild-id-here"
```

With TestGuildId configured, your slash commands will register instantly in that specific server during development.

## Step 9: Run Your Bot

1. Build the project:
   ```bash
   dotnet build
   ```

2. Run the bot:
   ```bash
   dotnet run --project src/DiscordBot.Bot
   ```

3. Check the console output for successful connection:
   ```
   [INFO] Bot is connected as YourBotName#1234
   [INFO] Registered 5 slash commands
   ```

4. In your Discord server, type `/` to see your bot's slash commands

Your bot is now running and ready to respond to commands!

## OAuth2 Configuration (Admin UI Authentication)

If you're setting up the admin web UI with Discord OAuth authentication (allowing users to "Login with Discord"), you'll need to configure OAuth2 credentials. This is separate from the bot token—OAuth2 allows users to authenticate through Discord to access your admin dashboard.

### Step 1: Get Your Client ID

1. In the [Discord Developer Portal](https://discord.com/developers/applications), select your application

2. Go to **"OAuth2"** → **"General"**

3. Your **Client ID** is displayed at the top of this page under "Client Information"
   - This is a long number (e.g., `123456789012345678`)
   - This is public and safe to share—it identifies your application

4. Copy the Client ID

### Step 2: Get Your Client Secret

1. On the same OAuth2 → General page, find the **"Client Secret"** section

2. Click **"Reset Secret"** (or **"View"** if this is the first time)

3. Confirm by clicking **"Yes, do it!"** if prompted

4. Copy the Client Secret

**SECURITY WARNING:**
- Treat the Client Secret like a password
- Never commit the Client Secret to Git or share it publicly
- If compromised, reset it immediately in the Developer Portal
- The secret is only shown once—if you lose it, you'll need to reset it

### Step 3: Configure Redirect URLs

1. Still on the OAuth2 → General page, scroll to **"Redirects"**

2. Click **"Add Redirect"** and add your callback URLs:
   - Development: `https://localhost:5001/signin-discord`
   - Production: `https://yourdomain.com/signin-discord`

3. Click **"Save Changes"**

**Notes on Redirect URLs:**
- The path `/signin-discord` is the default callback path used by the ASP.NET Discord OAuth handler
- URLs must be HTTPS (except `localhost` which can use HTTP in development)
- Add all environments where you'll run the admin UI

### Step 4: Configure OAuth2 Secrets in Your Project

Store your OAuth credentials using .NET User Secrets:

```bash
cd src/DiscordBot.Bot
dotnet user-secrets set "Discord:OAuth:ClientId" "your-client-id-here"
dotnet user-secrets set "Discord:OAuth:ClientSecret" "your-client-secret-here"
```

Verify they were set correctly:
```bash
dotnet user-secrets list
```

You should see:
```
Discord:OAuth:ClientId = 123456789012345678
Discord:OAuth:ClientSecret = ********
```

### Step 5: Configure OAuth Scopes (Already Done)

The application is pre-configured to request these OAuth scopes:
- `identify` - Allows reading the user's Discord ID, username, and avatar
- `email` - Allows reading the user's email address (if verified on Discord)

These scopes are configured in `Program.cs` and don't require any additional setup.

### OAuth2 vs Bot Token Summary

| Credential | Purpose | Where to Find |
|------------|---------|---------------|
| Bot Token | Authenticates the bot to Discord's API | Bot → Token |
| Client ID | Identifies your app for OAuth | OAuth2 → General → Client ID |
| Client Secret | Authenticates OAuth requests | OAuth2 → General → Client Secret |

All three credentials are found in the same Discord application but serve different purposes.

See [Identity Configuration](identity-configuration.md) for complete authentication setup details including password policies and role configuration.

## Troubleshooting

### Bot Appears Offline

**Cause:** Bot token is incorrect or not configured

**Solution:**
1. Verify the token is set correctly: `dotnet user-secrets list`
2. Check console output for authentication errors
3. Ensure you copied the entire token without extra spaces
4. If needed, reset the token in the Developer Portal and update your configuration

### Slash Commands Not Appearing

**Cause:** Commands not registered or insufficient permissions

**Solution:**
1. Wait up to 1 hour for global commands to register (or use TestGuildId for instant registration)
2. Verify bot has `applications.commands` scope
3. Check console logs for command registration errors
4. Re-invite the bot with the correct OAuth2 URL including `applications.commands`

### "Missing Permissions" Error

**Cause:** Bot lacks necessary permissions in the server

**Solution:**
1. Check the channel permissions—the bot role must have access to the channel
2. Verify the bot's role has the required permissions (e.g., "Send Messages")
3. Move the bot's role higher in the role hierarchy if managing roles/permissions
4. Re-invite the bot with correct permissions using the URL generator

### "Privileged Intent Provided But Not Enabled" Error

**Cause:** Code requests an intent not enabled in Developer Portal

**Solution:**
1. Go to Developer Portal → Your Application → Bot
2. Enable the required Privileged Gateway Intents
3. Click "Save Changes"
4. Restart your bot application

### Token Compromised

**Action Required:**
1. Go to Developer Portal → Your Application → Bot
2. Click **"Reset Token"** immediately
3. Update your configuration with the new token
4. Review your bot's recent activity for suspicious behavior

### Discord OAuth Login Fails with "Invalid Redirect URI"

**Cause:** The redirect URL in your application doesn't match what's configured in the Developer Portal

**Solution:**
1. Go to Developer Portal → Your Application → OAuth2 → General
2. Check that your redirect URL is exactly correct:
   - Development: `https://localhost:5001/signin-discord`
   - Note the exact port number and path
3. Ensure the URL uses HTTPS (not HTTP)
4. Click "Save Changes" after adding/modifying redirect URLs
5. Wait a few minutes for changes to propagate

### Discord OAuth Login Fails with "Invalid Client"

**Cause:** Client ID or Client Secret is incorrect

**Solution:**
1. Verify Client ID matches exactly (check for extra spaces)
2. Reset and recopy the Client Secret from Developer Portal
3. Ensure secrets are set for the correct project:
   ```bash
   cd src/DiscordBot.Bot
   dotnet user-secrets list
   ```
4. Check that `Discord:OAuth:ClientId` and `Discord:OAuth:ClientSecret` are both present

### "Access Denied" After Discord OAuth Login

**Cause:** User account issue or OAuth configuration

**Solution:**
1. Verify the user's email is verified on Discord (if requiring email)
2. Check that the OAuth scopes include `identify` and `email`
3. Review the application logs for more detailed error information
4. Ensure the user has granted the requested permissions

### Client Secret Compromised

**Action Required:**
1. Go to Developer Portal → Your Application → OAuth2 → General
2. Click **"Reset Secret"** immediately
3. Copy the new secret
4. Update your configuration:
   ```bash
   dotnet user-secrets set "Discord:OAuth:ClientSecret" "new-secret-here"
   ```
5. Restart your application

## Next Steps

- Review [Admin Commands](admin-commands.md) for available slash commands
- Learn about [Interactive Components](interactive-components.md) for building button-based interactions
- Configure [Identity and Authentication](identity-configuration.md) for the admin web UI
- Explore the [REST API Endpoints](api-endpoints.md) for programmatic bot management

## Additional Resources

- [Discord Developer Portal](https://discord.com/developers/applications)
- [Discord Developer Documentation](https://discord.com/developers/docs/intro)
- [Discord.NET Documentation](https://discordnet.dev/guides/introduction/intro.html)
- [Discord Bot Best Practices](https://discord.com/developers/docs/topics/community-resources#bot-best-practices)
- [ASP.NET Core User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)

## Support

If you encounter issues not covered in this guide:
1. Check the console output for detailed error messages
2. Review the project logs (if Serilog file logging is enabled)
3. Consult Discord.NET documentation for library-specific issues
4. Check Discord API status at https://discordstatus.com
