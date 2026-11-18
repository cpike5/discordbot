# Admin Commands (Removed)

The Discord admin slash commands `/status`, `/guilds` and `/shutdown` (and their
button handlers) were removed from the bot. Use the admin web portal instead.

| Removed command | Portal equivalent |
|-----------------|-------------------|
| `/status` | Dashboard (`/`) and the Admin Performance pages (`/Admin/Performance`: health, system, API and command metrics) |
| `/guilds` | Servers list (`/Guilds`) |
| `/shutdown` | Bot Control tab in `/Admin/Settings` (`?category=BotControl`) |

## Related Documentation

- [Permissions](permissions.md) - Command preconditions (`RequireAdmin`, `RequireOwner`, `RateLimit`) that still apply to the remaining commands
- [Command Module Configuration](command-configuration.md) - Enabling and disabling command modules
- [Commands Page](commands-page.md) - Command log and metadata pages in the portal
- [Configuration Guide](configuration-guide.md) - `Discord:DefaultRateLimitInvokes` and related settings
