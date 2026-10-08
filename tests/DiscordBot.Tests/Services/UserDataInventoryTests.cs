using System.Text.RegularExpressions;
using DiscordBot.Core.Entities;
using DiscordBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Keeps the user-data inventory honest (review finding H10, decision D5). Every column in the EF
/// model that holds a Discord user id or a web-account id must be classified here and in
/// <c>docs/articles/user-data-inventory.md</c>: what the user purge does with it, and whether the
/// export includes it. A new entity with a user-id column fails <see cref="EveryUserIdColumn_IsClassified"/>
/// until someone decides how purge and export treat it. That failure is the point.
/// </summary>
/// <remarks>
/// <para>How a column is recognised as a user id (<see cref="LooksLikeUserId"/>): its CLR type is
/// <c>ulong</c>, <c>ulong?</c> or <c>string</c> (Discord snowflakes and Identity keys), and its name
/// either ends in <c>UserId</c> (UserId, DiscordUserId, ApplicationUserId, TargetUserId,
/// SubmittedByUserId, ...), is one of the role-named ids <c>AuthorId</c>, <c>ActorId</c>,
/// <c>VoterId</c>, <c>PrincipalId</c> or <c>TargetId</c>, or ends in <c>By</c> / <c>ById</c>
/// (CreatedBy, LastModifiedBy, UploadedById, GrantedById, ...). The primary keys of
/// <see cref="User"/> and <see cref="ApplicationUser"/> are added by hand, since they are plain <c>Id</c>.</para>
/// <para>The heuristic is deliberately broad: a false positive costs one line here and in the doc,
/// a false negative is a table the purge silently misses.</para>
/// </remarks>
public class UserDataInventoryTests
{
    public enum PurgeAction
    {
        /// <summary>Deleted by <c>UserPurgeService</c>.</summary>
        Purged,
        /// <summary>The row stays, the id is overwritten (0 or null).</summary>
        Anonymised,
        /// <summary>Kept on purpose; the doc gives the reason.</summary>
        Retained,
        /// <summary>Deleted by a database cascade from a row the purge deletes.</summary>
        Cascade
    }

    private sealed record Entry(string Entity, string Column, PurgeAction Action, bool Exported);

    private static Entry P(string e, string c, bool exported = true) => new(e, c, PurgeAction.Purged, exported);
    private static Entry A(string e, string c, bool exported = true) => new(e, c, PurgeAction.Anonymised, exported);
    private static Entry R(string e, string c, bool exported = false) => new(e, c, PurgeAction.Retained, exported);
    private static Entry C(string e, string c, bool exported = false) => new(e, c, PurgeAction.Cascade, exported);

    /// <summary>The classification. Keep in step with docs/articles/user-data-inventory.md.</summary>
    private static readonly Entry[] Inventory =
    {
        // The user and the web account
        P("User", "Id"),
        P("ApplicationUser", "Id"),
        P("ApplicationUser", "DiscordUserId"),
        P("UserConsent", "DiscordUserId"),
        P("GuildMember", "UserId"),
        P("UserGuildAccess", "ApplicationUserId"),
        P("UserDiscordGuild", "ApplicationUserId"),
        P("DiscordOAuthToken", "ApplicationUserId", exported: false),
        P("DiscordOAuthToken", "DiscordUserId", exported: false),
        P("VerificationCode", "DiscordUserId", exported: false),
        C("VerificationCode", "ApplicationUserId"),
        C("IdentityUserClaim", "UserId"),
        C("IdentityUserLogin", "UserId"),
        C("IdentityUserRole", "UserId"),
        C("IdentityUserToken", "UserId"),
        P("UserNotification", "UserId"),
        P("UserActivityLog", "ActorUserId"),
        A("UserActivityLog", "TargetUserId"),

        // Activity and logs
        P("MessageLog", "AuthorId"),
        P("CommandLog", "UserId"),
        P("UserActivityEvent", "UserId"),
        P("MemberActivitySnapshot", "UserId"),

        // Audio
        P("SoundPlayLog", "UserId"),
        P("AudioPlaybackLog", "UserId"),
        P("UserSoundFavorite", "UserId"),
        P("TtsMessage", "UserId"),
        P("TtsMessageHistory", "UserId"),
        P("VoxMessageHistory", "UserId"),
        P("UserTtsPreset", "UserId"),
        P("UserPreference", "UserId"),
        R("Sound", "UploadedById"),

        // Assistant
        P("LlmUsageRecord", "UserId"),
        P("AssistantInteractionLog", "UserId"),
        P("DmAssistantInteractionLog", "UserId"),
        P("DmAssistantUsageMetrics", "UserId"),
        P("DmConversationMessage", "UserId"),
        P("DmAssistantNote", "UserId"),

        // Community features
        P("Reminder", "UserId"),
        P("RatVote", "VoterUserId"),
        A("RatRecord", "UserId"),
        A("RatWatch", "AccusedUserId"),
        A("RatWatch", "InitiatorUserId"),
        A("FeatureRequest", "SubmittedByUserId"),
        R("FeatureRequest", "ReviewedByUserId"),
        P("FeatureRequestRejection", "UserId"),

        // Moderation
        P("ModNote", "AuthorUserId"),
        R("ModNote", "TargetUserId"),
        P("UserModTag", "UserId"),
        R("UserModTag", "AppliedByUserId"),
        P("Watchlist", "UserId"),
        R("Watchlist", "AddedByUserId"),
        R("ModerationCase", "TargetUserId"),
        R("ModerationCase", "ModeratorUserId"),
        R("FlaggedEvent", "UserId"),
        R("FlaggedEvent", "ReviewedByUserId"),

        // Currency
        R("Wallet", "UserId", exported: true),
        R("LedgerTransaction", "ActorId"),
        R("Currency", "CreatedById"),
        R("MintAuthority", "GrantedById"),
        R("MintAuthority", "PrincipalId"),
        R("PriceEntry", "UpdatedById"),

        // Admin attribution and the audit trail
        R("UserGuildAccess", "GrantedByUserId"),
        R("AuditLog", "ActorId"),
        R("AuditLog", "TargetId"),
        R("ApplicationSetting", "LastModifiedBy"),
        R("CommandModuleConfiguration", "LastModifiedBy"),
        R("ScheduledMessage", "CreatedBy"),
        R("PerformanceAlertConfig", "UpdatedBy"),
        R("PerformanceIncident", "AcknowledgedBy"),
        R("LlmModel", "EnabledBy"),
    };

    private static readonly Regex UserIdName = new(
        @"(UserId|^AuthorId|^ActorId|^VoterId|^PrincipalId|^TargetId|By|ById)$",
        RegexOptions.CultureInvariant);

    private static bool LooksLikeUserId(IProperty property)
    {
        var type = property.ClrType;
        if (type != typeof(ulong) && type != typeof(ulong?) && type != typeof(string)) return false;

        return UserIdName.IsMatch(property.Name)
            || (property.IsPrimaryKey() && property.Name == "Id"
                && (property.DeclaringType.ClrType == typeof(User) || property.DeclaringType.ClrType == typeof(ApplicationUser)));
    }

    /// <summary>``IdentityUserClaim`1`` reads as <c>IdentityUserClaim</c>, the spelling the doc uses.</summary>
    private static string EntityName(IEntityType entityType)
    {
        var name = entityType.ClrType.Name;
        var tick = name.IndexOf('`');
        return tick < 0 ? name : name[..tick];
    }

    private static HashSet<(string Entity, string Column)> UserIdColumnsInModel()
    {
        // Model only: no connection is opened, so this test needs no database.
        var options = new DbContextOptionsBuilder<BotDbContext>().UseNpgsql("Host=localhost").Options;
        using var context = new BotDbContext(options);

        return context.Model.GetEntityTypes()
            .Where(e => !e.IsOwned())
            .SelectMany(e => e.GetDeclaredProperties().Where(LooksLikeUserId).Select(p => (EntityName(e), p.Name)))
            .ToHashSet();
    }

    [Fact]
    public void EveryUserIdColumn_IsClassified()
    {
        var classified = Inventory.Select(e => (e.Entity, e.Column)).ToHashSet();

        var unclassified = UserIdColumnsInModel().Where(c => !classified.Contains(c)).OrderBy(c => c.ToString()).ToList();

        unclassified.Should().BeEmpty(
            "every column holding a user id needs a purge and export decision: add it to UserPurgeService/" +
            "UserDataExportService (or decide to retain it), then to the Inventory here and to " +
            "docs/articles/user-data-inventory.md");
    }

    [Fact]
    public void EveryClassifiedColumn_StillExistsInTheModel()
    {
        var model = UserIdColumnsInModel();

        Inventory.Select(e => (e.Entity, e.Column)).Where(c => !model.Contains(c))
            .Should().BeEmpty("a stale inventory entry describes a column that is gone or renamed");
    }

    [Fact]
    public void Inventory_HasNoDuplicates()
    {
        Inventory.GroupBy(e => (e.Entity, e.Column)).Where(g => g.Count() > 1).Select(g => g.Key)
            .Should().BeEmpty();
    }

    [Fact]
    public void TheDocTable_MatchesTheInventory()
    {
        var path = Path.Combine(RepositoryRoot(), "docs", "articles", "user-data-inventory.md");
        File.Exists(path).Should().BeTrue();

        // Rows look like: | `Entity` | `Column` | Purged | Yes | notes |
        var row = new Regex(@"^\|\s*`(?<entity>\w+)`\s*\|\s*`(?<column>\w+)`\s*\|\s*(?<action>[A-Za-z]+)[^|]*\|\s*(?<exported>[A-Za-z]+)[^|]*\|");
        var documented = File.ReadAllLines(path)
            .Select(l => row.Match(l))
            .Where(m => m.Success)
            .Select(m => new Entry(
                m.Groups["entity"].Value,
                m.Groups["column"].Value,
                Enum.Parse<PurgeAction>(m.Groups["action"].Value, ignoreCase: true),
                m.Groups["exported"].Value.Equals("Yes", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        documented.Should().BeEquivalentTo(Inventory,
            "the inventory table in docs/articles/user-data-inventory.md and this test describe the same thing");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DiscordBot.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests must be able to find the repository they are testing");

        return directory!.FullName;
    }
}
