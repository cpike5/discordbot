using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services;

public class PageMetadataService : IPageMetadataService
{
    private readonly ILogger<PageMetadataService> _logger;
    private static readonly IReadOnlyList<PageMetadataDto> _pages;

    static PageMetadataService()
    {
        _pages = new List<PageMetadataDto>
        {
            // Main Pages (Viewer+)
            new()
            {
                Name = "Dashboard",
                Route = "/",
                Description = "Main dashboard with bot status and statistics",
                Section = "Main",
                IconName = "home",
                Keywords = new[] { "home", "overview", "status", "main", "dashboard" }
            },
            new()
            {
                Name = "Commands",
                Route = "/Commands",
                Description = "View all registered slash commands, execution logs, and analytics",
                Section = "Main",
                IconName = "command-line",
                Keywords = new[] { "slash", "bot commands", "commands", "slash commands", "history", "execution", "logs", "command history", "usage", "stats", "charts", "analytics", "metrics" }
            },
            new()
            {
                Name = "Guilds",
                Route = "/Guilds",
                Description = "Connected Discord servers list",
                Section = "Main",
                IconName = "server",
                Keywords = new[] { "servers", "discord servers", "guilds" }
            },
            new()
            {
                Name = "Search",
                Route = "/Search",
                Description = "Global search across all resources",
                Section = "Main",
                IconName = "magnifying-glass",
                Keywords = new[] { "find", "lookup", "search" }
            },

            // Guild-Specific Pages
            new()
            {
                Name = "Guild Details",
                Route = "/Guilds/Details",
                Description = "View detailed server information",
                Section = "Guild",
                IconName = "information-circle",
                Keywords = new[] { "server info", "details", "guild info" }
            },
            new()
            {
                Name = "Guild Settings",
                Route = "/Guilds/Edit",
                Description = "Edit guild configuration",
                Section = "Guild",
                IconName = "cog",
                Keywords = new[] { "settings", "config", "configuration", "edit guild" }
            },
            new()
            {
                Name = "Welcome Messages",
                Route = "/Guilds/Welcome",
                Description = "Configure welcome messages for new members",
                Section = "Guild",
                IconName = "hand-raised",
                Keywords = new[] { "welcome", "greeting", "new members", "join message" }
            },
            new()
            {
                Name = "Moderation Settings",
                Route = "/Guilds/ModerationSettings",
                Description = "Guild auto-moderation configuration",
                Section = "Guild",
                IconName = "shield-check",
                Keywords = new[] { "moderation", "auto-mod", "filters", "rules" }
            },
            new()
            {
                Name = "Scheduled Messages",
                Route = "/Guilds/ScheduledMessages",
                Description = "Manage scheduled and recurring messages",
                Section = "Guild",
                IconName = "clock",
                Keywords = new[] { "automated", "recurring", "scheduled", "messages", "automation" }
            },
            new()
            {
                Name = "Rat Watch",
                Route = "/Guilds/RatWatch",
                Description = "Accountability and incident tracking",
                Section = "Guild",
                IconName = "eye",
                Keywords = new[] { "accountability", "incidents", "rat watch", "tracking" }
            },
            new()
            {
                Name = "Rat Watch Analytics",
                Route = "/Guilds/RatWatch/Analytics",
                Description = "Rat Watch metrics and analytics",
                Section = "Guild",
                IconName = "chart-pie",
                Keywords = new[] { "rat watch stats", "analytics", "metrics", "reports" }
            },
            new()
            {
                Name = "Rat Watch Incidents",
                Route = "/Guilds/RatWatch/Incidents",
                Description = "Browse and filter Rat Watch incidents",
                Section = "Guild",
                IconName = "exclamation-triangle",
                Keywords = new[] { "incidents", "reports", "rat watch", "violations" }
            },
            new()
            {
                Name = "Member Directory",
                Route = "/Guilds/Members",
                Description = "Guild member list with search and filter",
                Section = "Guild",
                IconName = "users",
                Keywords = new[] { "users", "roster", "members", "directory" }
            },
            new()
            {
                Name = "Reminders",
                Route = "/Guilds/Reminders",
                Description = "Manage guild reminders",
                Section = "Guild",
                IconName = "bell",
                Keywords = new[] { "reminders", "notifications", "alerts" }
            },
            new()
            {
                Name = "Public Leaderboard",
                Route = "/Guilds/Leaderboard",
                Description = "Public Rat Watch leaderboard",
                Section = "Guild",
                IconName = "trophy",
                Keywords = new[] { "leaderboard", "rankings", "top users", "rat watch" }
            },
            new()
            {
                Name = "Soundboard",
                Route = "/Guilds/Soundboard",
                Description = "Guild soundboard management",
                Section = "Guild",
                IconName = "speaker-wave",
                Keywords = new[] { "sounds", "audio", "soundboard", "sound effects" }
            },
            new()
            {
                Name = "Audio Settings",
                Route = "/Guilds/AudioSettings",
                Description = "Guild audio configuration",
                Section = "Guild",
                IconName = "musical-note",
                Keywords = new[] { "audio", "voice", "settings", "audio config" }
            },
            new()
            {
                Name = "Text-to-Speech",
                Route = "/Guilds/TextToSpeech",
                Description = "Guild TTS message management",
                Section = "Guild",
                IconName = "microphone",
                Keywords = new[] { "tts", "text to speech", "voice", "speech" }
            },
            new()
            {
                Name = "Assistant Settings",
                Route = "/Guilds/AssistantSettings",
                Description = "AI assistant configuration",
                Section = "Guild",
                IconName = "cpu-chip",
                Keywords = new[] { "ai", "assistant", "claude", "bot settings" }
            },
            new()
            {
                Name = "Assistant Metrics",
                Route = "/Guilds/AssistantMetrics",
                Description = "AI assistant usage metrics",
                Section = "Guild",
                IconName = "chart-bar",
                Keywords = new[] { "ai metrics", "assistant stats", "usage", "analytics" }
            },
            new()
            {
                Name = "Flagged Events",
                Route = "/Guilds/FlaggedEvents",
                Description = "Auto-moderation flagged content",
                Section = "Guild",
                IconName = "flag",
                Keywords = new[] { "flagged", "auto-mod", "violations", "moderation" }
            },
            new()
            {
                Name = "Flagged Event Details",
                Route = "/Guilds/FlaggedEvents/Details",
                Description = "View flagged event details",
                Section = "Guild",
                IconName = "document-magnifying-glass",
                Keywords = new[] { "flagged details", "event details", "violation" }
            },
            new()
            {
                Name = "Guild Analytics",
                Route = "/Guilds/Analytics",
                Description = "Guild analytics overview",
                Section = "Guild",
                IconName = "chart-pie",
                Keywords = new[] { "analytics", "stats", "guild metrics", "overview" }
            },
            new()
            {
                Name = "Guild Engagement Analytics",
                Route = "/Guilds/Analytics/Engagement",
                Description = "Member engagement metrics",
                Section = "Guild",
                IconName = "users",
                Keywords = new[] { "engagement", "activity", "member analytics", "participation" }
            },
            new()
            {
                Name = "Guild Moderation Analytics",
                Route = "/Guilds/Analytics/Moderation",
                Description = "Moderation activity analytics",
                Section = "Guild",
                IconName = "shield-check",
                Keywords = new[] { "moderation stats", "mod analytics", "enforcement" }
            },
            new()
            {
                Name = "Member Moderation",
                Route = "/Guilds/Members/Moderation",
                Description = "Member moderation history",
                Section = "Guild",
                IconName = "user-minus",
                Keywords = new[] { "member history", "user moderation", "warnings", "actions" }
            },

            // Portal Pages (Member Access)
            new()
            {
                Name = "TTS Portal",
                Route = "/Portal/TTS",
                Description = "Text-to-speech message composer",
                Section = "Portal",
                IconName = "microphone",
                Keywords = new[] { "tts", "portal", "voice", "speech composer" }
            },
            new()
            {
                Name = "Portal Soundboard",
                Route = "/Portal/Soundboard",
                Description = "Member soundboard access",
                Section = "Portal",
                IconName = "speaker-wave",
                Keywords = new[] { "sounds", "portal", "soundboard", "play sounds" }
            },

            // Admin Pages (RequireAdmin policy)
            new()
            {
                Name = "Audit Logs",
                Route = "/Admin/AuditLogs",
                Description = "System audit trail and activity history",
                Section = "Admin",
                IconName = "clipboard-document-list",
                RequiredPolicy = "RequireAdmin",
                Keywords = new[] { "activity", "trail", "audit", "logs", "history" }
            },
            new()
            {
                Name = "Audit Log Details",
                Route = "/Admin/AuditLogs/Details",
                Description = "View detailed audit log entry",
                Section = "Admin",
                IconName = "document-magnifying-glass",
                RequiredPolicy = "RequireAdmin",
                Keywords = new[] { "audit details", "log entry" }
            },
            new()
            {
                Name = "Message Logs",
                Route = "/Admin/MessageLogs",
                Description = "Discord message history",
                Section = "Admin",
                IconName = "chat-bubble-left-right",
                RequiredPolicy = "RequireAdmin",
                Keywords = new[] { "messages", "history", "chat logs", "discord messages" }
            },
            new()
            {
                Name = "Message Log Details",
                Route = "/Admin/MessageLogs/Details",
                Description = "View detailed message log entry",
                Section = "Admin",
                IconName = "document-text",
                RequiredPolicy = "RequireAdmin",
                Keywords = new[] { "message details", "log entry" }
            },
            new()
            {
                Name = "Settings",
                Route = "/Admin/Settings",
                Description = "Application configuration and settings",
                Section = "Admin",
                IconName = "adjustments-horizontal",
                RequiredPolicy = "RequireAdmin",
                Keywords = new[] { "configuration", "options", "settings", "preferences" }
            },
            new()
            {
                Name = "Global Rat Watch Analytics",
                Route = "/Admin/RatWatchAnalytics",
                Description = "Cross-guild Rat Watch metrics and analytics",
                Section = "Admin",
                IconName = "chart-bar-square",
                RequiredPolicy = "RequireAdmin",
                Keywords = new[] { "rat watch", "global analytics", "cross-guild", "metrics" }
            },

            // Performance/Monitoring Pages (Admin+)
            new()
            {
                Name = "Performance Dashboard",
                Route = "/Admin/Performance",
                Description = "Performance overview dashboard",
                Section = "Performance",
                IconName = "chart-bar-square",
                RequiredPolicy = "RequireAdmin",
                Keywords = new[] { "performance", "overview", "dashboard", "monitoring" }
            },
            new()
            {
                Name = "Health Metrics",
                Route = "/Admin/Performance/HealthMetrics",
                Description = "Bot health metrics dashboard",
                Section = "Performance",
                IconName = "heart",
                RequiredPolicy = "RequireAdmin",
                Keywords = new[] { "performance", "monitoring", "health", "metrics", "dashboard" }
            },
            new()
            {
                Name = "Command Performance",
                Route = "/Admin/Performance/Commands",
                Description = "Command response times and throughput",
                Section = "Performance",
                IconName = "bolt",
                RequiredPolicy = "RequireAdmin",
                Keywords = new[] { "response times", "throughput", "command performance", "latency" }
            },
            new()
            {
                Name = "System Health",
                Route = "/Admin/Performance/System",
                Description = "Database, cache, and service monitoring",
                Section = "Performance",
                IconName = "cpu-chip",
                RequiredPolicy = "RequireAdmin",
                Keywords = new[] { "database", "cache", "system", "monitoring", "services" }
            },
            new()
            {
                Name = "Performance Alerts",
                Route = "/Admin/Performance/Alerts",
                Description = "Alert thresholds and incident management",
                Section = "Performance",
                IconName = "bell-alert",
                RequiredPolicy = "RequireAdmin",
                Keywords = new[] { "incidents", "thresholds", "alerts", "notifications" }
            },
            new()
            {
                Name = "API Metrics",
                Route = "/Admin/Performance/ApiMetrics",
                Description = "Discord API usage and rate limits",
                Section = "Performance",
                IconName = "globe-alt",
                RequiredPolicy = "RequireAdmin",
                Keywords = new[] { "api", "rate limits", "discord api", "api metrics" }
            },

            // SuperAdmin Pages (RequireSuperAdmin policy)
            new()
            {
                Name = "Users",
                Route = "/Admin/Users",
                Description = "User account management",
                Section = "Admin",
                IconName = "user-group",
                RequiredPolicy = "RequireSuperAdmin",
                Keywords = new[] { "accounts", "management", "users", "admin users" }
            },
            new()
            {
                Name = "User Details",
                Route = "/Admin/Users/Details",
                Description = "View user profile and roles",
                Section = "Admin",
                IconName = "user-circle",
                RequiredPolicy = "RequireSuperAdmin",
                Keywords = new[] { "user profile", "roles", "permissions" }
            },
            new()
            {
                Name = "Create User",
                Route = "/Admin/Users/Create",
                Description = "Create new user account",
                Section = "Admin",
                IconName = "user-plus",
                RequiredPolicy = "RequireSuperAdmin",
                Keywords = new[] { "new user", "add user", "create account" }
            },
            new()
            {
                Name = "Edit User",
                Route = "/Admin/Users/Edit",
                Description = "Edit user account and permissions",
                Section = "Admin",
                IconName = "pencil",
                RequiredPolicy = "RequireSuperAdmin",
                Keywords = new[] { "edit user", "modify user", "user settings" }
            },
            new()
            {
                Name = "User Purge",
                Route = "/Admin/UserPurge",
                Description = "Purge user data (GDPR)",
                Section = "Admin",
                IconName = "trash",
                RequiredPolicy = "RequireSuperAdmin",
                Keywords = new[] { "gdpr", "purge", "delete data", "user purge", "privacy" }
            },

            // Account Pages (Public/Authenticated)
            new()
            {
                Name = "Login",
                Route = "/Account/Login",
                Description = "User authentication and login",
                Section = "Account",
                IconName = "arrow-right-on-rectangle",
                Keywords = new[] { "sign in", "login", "authenticate" }
            },
            new()
            {
                Name = "Link Discord",
                Route = "/Account/LinkDiscord",
                Description = "Link Discord account via OAuth",
                Section = "Account",
                IconName = "link",
                Keywords = new[] { "oauth", "discord", "link account", "connect" }
            },
            new()
            {
                Name = "Profile",
                Route = "/Account/Profile",
                Description = "User profile and theme preferences",
                Section = "Account",
                IconName = "user-circle",
                Keywords = new[] { "profile", "preferences", "theme", "account settings" }
            },
            new()
            {
                Name = "Privacy",
                Route = "/Account/Privacy",
                Description = "Privacy information and settings",
                Section = "Account",
                IconName = "shield-check",
                Keywords = new[] { "privacy", "data", "gdpr", "consent" }
            },

            // Development Pages
            new()
            {
                Name = "Components",
                Route = "/Components",
                Description = "Component showcase for development",
                Section = "Dev",
                IconName = "square-3-stack-3d",
                Keywords = new[] { "components", "showcase", "ui", "development" }
            }
        }.AsReadOnly();
    }

    public PageMetadataService(ILogger<PageMetadataService> logger)
    {
        _logger = logger;
        _logger.LogDebug("PageMetadataService initialized with {PageCount} pages", _pages.Count);
    }

    public IReadOnlyList<PageMetadataDto> GetAllPages()
    {
        _logger.LogTrace("Retrieving all {PageCount} pages", _pages.Count);
        return _pages;
    }

    public IReadOnlyList<PageMetadataDto> SearchPages(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            _logger.LogDebug("Empty search term provided, returning all pages");
            return _pages;
        }

        var normalizedSearchTerm = searchTerm.Trim().ToLowerInvariant();
        _logger.LogDebug("Searching pages with term: {SearchTerm}", normalizedSearchTerm);

        var results = _pages
            .Where(p =>
                p.Name.Contains(normalizedSearchTerm, StringComparison.OrdinalIgnoreCase) ||
                (p.Description?.Contains(normalizedSearchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Section?.Contains(normalizedSearchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                p.Keywords.Any(k => k.Contains(normalizedSearchTerm, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(p => CalculateRelevanceScore(p, normalizedSearchTerm))
            .ToList()
            .AsReadOnly();

        _logger.LogDebug("Found {ResultCount} pages matching search term", results.Count);
        return results;
    }

    public PageMetadataDto? FindExactMatch(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return null;
        }

        var normalizedSearchTerm = searchTerm.Trim().ToLowerInvariant();
        _logger.LogDebug("Finding exact match for term: {SearchTerm}", normalizedSearchTerm);

        var exactMatch = _pages.FirstOrDefault(p =>
            p.Name.Equals(normalizedSearchTerm, StringComparison.OrdinalIgnoreCase) ||
            p.Keywords.Any(k => k.Equals(normalizedSearchTerm, StringComparison.OrdinalIgnoreCase)));

        if (exactMatch != null)
        {
            _logger.LogDebug("Found exact match: {PageName}", exactMatch.Name);
        }
        else
        {
            _logger.LogDebug("No exact match found for term: {SearchTerm}", normalizedSearchTerm);
        }

        return exactMatch;
    }

    private static int CalculateRelevanceScore(PageMetadataDto page, string searchTerm)
    {
        var score = 0;

        // Exact name match gets highest score
        if (page.Name.Equals(searchTerm, StringComparison.OrdinalIgnoreCase))
        {
            score += 1000;
        }
        // Name starts with search term
        else if (page.Name.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
        {
            score += 500;
        }
        // Name contains search term
        else if (page.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
        {
            score += 250;
        }

        // Exact keyword match
        if (page.Keywords.Any(k => k.Equals(searchTerm, StringComparison.OrdinalIgnoreCase)))
        {
            score += 400;
        }
        // Keyword contains search term
        else if (page.Keywords.Any(k => k.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
        {
            score += 200;
        }

        // Section match
        if (page.Section?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false)
        {
            score += 100;
        }

        // Description match
        if (page.Description?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false)
        {
            score += 50;
        }

        return score;
    }
}
