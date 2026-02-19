using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddIsEnabledToGuildModerationConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    DataType = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiresRestart = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    ActorType = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TargetId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: true),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommandModuleConfigurations",
                columns: table => new
                {
                    ModuleName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RequiresRestart = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandModuleConfigurations", x => x.ModuleName);
                });

            migrationBuilder.CreateTable(
                name: "ConnectionEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Guilds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    LeftAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Prefix = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Settings = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guilds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetricSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DatabaseAvgQueryTimeMs = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    DatabaseTotalQueries = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    DatabaseSlowQueryCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    WorkingSetMB = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    PrivateMemoryMB = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    HeapSizeMB = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    Gen0Collections = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Gen1Collections = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Gen2Collections = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CacheHitRatePercent = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    CacheTotalEntries = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CacheTotalHits = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    CacheTotalMisses = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    ServicesRunningCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    ServicesErrorCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    ServicesTotalCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CpuUsagePercent = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceAlertConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MetricName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    WarningThreshold = table.Column<double>(type: "REAL", nullable: true),
                    CriticalThreshold = table.Column<double>(type: "REAL", nullable: true),
                    ThresholdUnit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceAlertConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceIncidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MetricName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TriggeredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ThresholdValue = table.Column<double>(type: "REAL", nullable: false),
                    ActualValue = table.Column<double>(type: "REAL", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "INTEGER", nullable: false),
                    AcknowledgedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceIncidents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Themes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ThemeKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ColorDefinition = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Themes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserActivityEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<long>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LoggedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserActivityEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Discriminator = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false, defaultValue: "0"),
                    FirstSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AccountCreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AvatarHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    GlobalDisplayName = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssistantGuildSettings",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    AllowedChannelIds = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    RateLimitOverride = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantGuildSettings", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_AssistantGuildSettings_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssistantUsageMetrics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalQuestions = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalInputTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalOutputTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalCachedTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalCacheWriteTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalCacheHits = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalCacheMisses = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalToolCalls = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    EstimatedCostUsd = table.Column<decimal>(type: "decimal(18,8)", nullable: false, defaultValue: 0m),
                    FailedRequests = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    AverageLatencyMs = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantUsageMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantUsageMetrics_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChannelActivitySnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Granularity = table.Column<int>(type: "INTEGER", nullable: false),
                    MessageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UniqueUsers = table.Column<int>(type: "INTEGER", nullable: false),
                    PeakHour = table.Column<int>(type: "INTEGER", nullable: true),
                    PeakHourMessageCount = table.Column<int>(type: "INTEGER", nullable: true),
                    AverageMessageLength = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelActivitySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChannelActivitySnapshots_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlaggedEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<long>(type: "INTEGER", nullable: true),
                    RuleType = table.Column<int>(type: "INTEGER", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Evidence = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTaken = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ReviewedByUserId = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlaggedEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlaggedEvents_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildAudioSettings",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    AudioEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    AutoLeaveTimeoutMinutes = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 5),
                    QueueEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    MaxDurationSeconds = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 30),
                    MaxFileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 5242880L),
                    MaxSoundsPerGuild = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 50),
                    MaxStorageBytes = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 104857600L),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EnableMemberPortal = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    SilentPlayback = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildAudioSettings", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_GuildAudioSettings_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildMetricsSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TotalMembers = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveMembers = table.Column<int>(type: "INTEGER", nullable: false),
                    MembersJoined = table.Column<int>(type: "INTEGER", nullable: false),
                    MembersLeft = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalMessages = table.Column<int>(type: "INTEGER", nullable: false),
                    CommandsExecuted = table.Column<int>(type: "INTEGER", nullable: false),
                    ModerationActions = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveChannels = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalVoiceMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildMetricsSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildMetricsSnapshots_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildModerationConfigs",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    SimplePreset = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    SpamConfig = table.Column<string>(type: "TEXT", nullable: false),
                    ContentFilterConfig = table.Column<string>(type: "TEXT", nullable: false),
                    RaidProtectionConfig = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildModerationConfigs", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_GuildModerationConfigs_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildRatWatchSettings",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Timezone = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: "UTC"),
                    MaxAdvanceHours = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 24),
                    VotingDurationMinutes = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 5),
                    PublicLeaderboardEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildRatWatchSettings", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_GuildRatWatchSettings_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildTtsSettings",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    TtsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    DefaultVoice = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: "en-US-JennyNeural"),
                    DefaultSpeed = table.Column<double>(type: "REAL", nullable: false, defaultValue: 1.0),
                    DefaultPitch = table.Column<double>(type: "REAL", nullable: false, defaultValue: 1.0),
                    DefaultVolume = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.80000000000000004),
                    MaxMessageLength = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 500),
                    RateLimitPerMinute = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 5),
                    AutoPlayOnSend = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    AnnounceJoinsLeaves = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    SsmlEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    StrictSsmlValidation = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    MaxSsmlComplexity = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 50),
                    DefaultStyle = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    DefaultStyleDegree = table.Column<double>(type: "REAL", nullable: false, defaultValue: 1.0),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildTtsSettings", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_GuildTtsSettings_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    TargetUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    AuthorUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModNotes_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    IsFromTemplate = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModTags_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RatWatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<long>(type: "INTEGER", nullable: false),
                    AccusedUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    InitiatorUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    OriginalMessageId = table.Column<long>(type: "INTEGER", nullable: false),
                    CustomMessage = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    NotificationMessageId = table.Column<long>(type: "INTEGER", nullable: true),
                    VotingMessageId = table.Column<long>(type: "INTEGER", nullable: true),
                    ClearedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VotingStartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VotingEndedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatWatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatWatches_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TriggerAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    DeliveryAttempts = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reminders_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<long>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CronExpression = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Frequency = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    LastExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextExecutionAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledMessages_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    DurationSeconds = table.Column<double>(type: "REAL", nullable: false),
                    UploadedById = table.Column<long>(type: "INTEGER", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PlayCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sounds_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TtsMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Voice = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DurationSeconds = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TtsMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TtsMessages_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Watchlists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    AddedByUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Watchlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Watchlists_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WelcomeConfigurations",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    WelcomeChannelId = table.Column<long>(type: "INTEGER", nullable: true),
                    WelcomeMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false, defaultValue: ""),
                    IncludeAvatar = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    UseEmbed = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    EmbedColor = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WelcomeConfigurations", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_WelcomeConfigurations_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DiscordUserId = table.Column<long>(type: "INTEGER", nullable: true),
                    DiscordUsername = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DiscordAvatarUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PreferredThemeId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_Themes_PreferredThemeId",
                        column: x => x.PreferredThemeId,
                        principalTable: "Themes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AssistantInteractionLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<long>(type: "INTEGER", nullable: false),
                    MessageId = table.Column<long>(type: "INTEGER", nullable: false),
                    Question = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Response = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    InputTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    OutputTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CachedTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CacheCreationTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CacheHit = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    ToolCalls = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    LatencyMs = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    EstimatedCostUsd = table.Column<decimal>(type: "decimal(18,8)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantInteractionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantInteractionLogs_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssistantInteractionLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommandLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: true),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    CommandName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Parameters = table.Column<string>(type: "TEXT", nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResponseTimeMs = table.Column<int>(type: "INTEGER", nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommandLogs_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CommandLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildMembers",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Nickname = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    CachedRolesJson = table.Column<string>(type: "TEXT", nullable: true),
                    LastActiveAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastCachedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildMembers", x => new { x.GuildId, x.UserId });
                    table.ForeignKey(
                        name: "FK_GuildMembers_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuildMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberActivitySnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Granularity = table.Column<int>(type: "INTEGER", nullable: false),
                    MessageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ReactionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    VoiceMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    UniqueChannelsActive = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberActivitySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberActivitySnapshots_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberActivitySnapshots_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DiscordMessageId = table.Column<long>(type: "INTEGER", nullable: false),
                    AuthorId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelName = table.Column<string>(type: "TEXT", nullable: true),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: true),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LoggedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    HasAttachments = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    HasEmbeds = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    ReplyToMessageId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageLogs_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MessageLogs_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserConsents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DiscordUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    ConsentType = table.Column<int>(type: "INTEGER", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    GrantedVia = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RevokedVia = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserConsents_Users_DiscordUserId",
                        column: x => x.DiscordUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModerationCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CaseNumber = table.Column<long>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    TargetUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    ModeratorUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RelatedFlaggedEventId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContextMessageId = table.Column<ulong>(type: "INTEGER", nullable: true),
                    ContextChannelId = table.Column<ulong>(type: "INTEGER", nullable: true),
                    ContextMessageContent = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModerationCases_FlaggedEvents_RelatedFlaggedEventId",
                        column: x => x.RelatedFlaggedEventId,
                        principalTable: "FlaggedEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ModerationCases_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommandRoleRestrictions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    CommandName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AllowedRoleIds = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandRoleRestrictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommandRoleRestrictions_GuildAudioSettings_GuildId",
                        column: x => x.GuildId,
                        principalTable: "GuildAudioSettings",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserModTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    TagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AppliedByUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserModTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserModTags_ModTags_TagId",
                        column: x => x.TagId,
                        principalTable: "ModTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RatRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RatWatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    GuiltyVotes = table.Column<int>(type: "INTEGER", nullable: false),
                    NotGuiltyVotes = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OriginalMessageLink = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatRecords_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RatRecords_RatWatches_RatWatchId",
                        column: x => x.RatWatchId,
                        principalTable: "RatWatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RatVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RatWatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VoterUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsGuiltyVote = table.Column<bool>(type: "INTEGER", nullable: false),
                    VotedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatVotes_RatWatches_RatWatchId",
                        column: x => x.RatWatchId,
                        principalTable: "RatWatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SoundPlayLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SoundId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoundPlayLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoundPlayLogs_Sounds_SoundId",
                        column: x => x.SoundId,
                        principalTable: "Sounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscordOAuthTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    EncryptedAccessToken = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    EncryptedRefreshToken = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    AccessTokenExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Scopes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastRefreshedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DiscordUserId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordOAuthTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscordOAuthTokens_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserActivityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    TargetUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserActivityLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserActivityLogs_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserActivityLogs_AspNetUsers_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserDiscordGuilds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    GuildName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GuildIconHash = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsOwner = table.Column<bool>(type: "INTEGER", nullable: false),
                    Permissions = table.Column<long>(type: "INTEGER", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDiscordGuilds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDiscordGuilds_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserGuildAccess",
                columns: table => new
                {
                    ApplicationUserId = table.Column<string>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    AccessLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    GrantedByUserId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGuildAccess", x => new { x.ApplicationUserId, x.GuildId });
                    table.ForeignKey(
                        name: "FK_UserGuildAccess_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserGuildAccess_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    LinkUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: true),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DismissedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RelatedEntityId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserNotifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserNotifications_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "VerificationCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    DiscordUserId = table.Column<long>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerificationCodes_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationSettings_Category",
                table: "ApplicationSettings",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationSettings_LastModifiedAt",
                table: "ApplicationSettings",
                column: "LastModifiedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DiscordUserId",
                table: "AspNetUsers",
                column: "DiscordUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Email",
                table: "AspNetUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_IsActive",
                table: "AspNetUsers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PreferredThemeId",
                table: "AspNetUsers",
                column: "PreferredThemeId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantInteractionLogs_GuildId_Timestamp",
                table: "AssistantInteractionLogs",
                columns: new[] { "GuildId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantInteractionLogs_Timestamp",
                table: "AssistantInteractionLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantInteractionLogs_UserId_Timestamp",
                table: "AssistantInteractionLogs",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantUsageMetrics_Date",
                table: "AssistantUsageMetrics",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantUsageMetrics_GuildId_Date_Unique",
                table: "AssistantUsageMetrics",
                columns: new[] { "GuildId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "ActorId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Category",
                table: "AuditLogs",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Category_Action_Timestamp",
                table: "AuditLogs",
                columns: new[] { "Category", "Action", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CorrelationId",
                table: "AuditLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_GuildId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "GuildId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TargetType_TargetId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "TargetType", "TargetId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelActivitySnapshots_Guild_Channel_Period",
                table: "ChannelActivitySnapshots",
                columns: new[] { "GuildId", "ChannelId", "PeriodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelActivitySnapshots_Guild_Period_Granularity",
                table: "ChannelActivitySnapshots",
                columns: new[] { "GuildId", "PeriodStart", "Granularity" });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelActivitySnapshots_Unique",
                table: "ChannelActivitySnapshots",
                columns: new[] { "GuildId", "ChannelId", "PeriodStart", "Granularity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommandLogs_CommandName",
                table: "CommandLogs",
                column: "CommandName");

            migrationBuilder.CreateIndex(
                name: "IX_CommandLogs_ExecutedAt",
                table: "CommandLogs",
                column: "ExecutedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CommandLogs_GuildId",
                table: "CommandLogs",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandLogs_Success",
                table: "CommandLogs",
                column: "Success");

            migrationBuilder.CreateIndex(
                name: "IX_CommandLogs_UserId",
                table: "CommandLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandModuleConfigurations_Category",
                table: "CommandModuleConfigurations",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_CommandModuleConfigurations_IsEnabled",
                table: "CommandModuleConfigurations",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_CommandModuleConfigurations_LastModifiedAt",
                table: "CommandModuleConfigurations",
                column: "LastModifiedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CommandRoleRestrictions_GuildId_CommandName",
                table: "CommandRoleRestrictions",
                columns: new[] { "GuildId", "CommandName" });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionEvents_Timestamp",
                table: "ConnectionEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_DiscordOAuthTokens_AccessTokenExpiresAt",
                table: "DiscordOAuthTokens",
                column: "AccessTokenExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_DiscordOAuthTokens_ApplicationUserId",
                table: "DiscordOAuthTokens",
                column: "ApplicationUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscordOAuthTokens_DiscordUserId",
                table: "DiscordOAuthTokens",
                column: "DiscordUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FlaggedEvents_GuildId_RuleType_CreatedAt",
                table: "FlaggedEvents",
                columns: new[] { "GuildId", "RuleType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FlaggedEvents_GuildId_Severity_Status",
                table: "FlaggedEvents",
                columns: new[] { "GuildId", "Severity", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FlaggedEvents_GuildId_Status_CreatedAt",
                table: "FlaggedEvents",
                columns: new[] { "GuildId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FlaggedEvents_GuildId_UserId_CreatedAt",
                table: "FlaggedEvents",
                columns: new[] { "GuildId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GuildMembers_GuildId",
                table: "GuildMembers",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildMembers_GuildId_JoinedAt",
                table: "GuildMembers",
                columns: new[] { "GuildId", "JoinedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GuildMembers_GuildId_LastActiveAt",
                table: "GuildMembers",
                columns: new[] { "GuildId", "LastActiveAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GuildMembers_IsActive",
                table: "GuildMembers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_GuildMembers_LastActiveAt",
                table: "GuildMembers",
                column: "LastActiveAt");

            migrationBuilder.CreateIndex(
                name: "IX_GuildMembers_UserId",
                table: "GuildMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildMetricsSnapshots_Unique",
                table: "GuildMetricsSnapshots",
                columns: new[] { "GuildId", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guilds_IsActive",
                table: "Guilds",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Guilds_LeftAt",
                table: "Guilds",
                column: "LeftAt");

            migrationBuilder.CreateIndex(
                name: "IX_MemberActivitySnapshots_Guild_Period_Granularity",
                table: "MemberActivitySnapshots",
                columns: new[] { "GuildId", "PeriodStart", "Granularity" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberActivitySnapshots_Guild_User_Period",
                table: "MemberActivitySnapshots",
                columns: new[] { "GuildId", "UserId", "PeriodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberActivitySnapshots_Unique",
                table: "MemberActivitySnapshots",
                columns: new[] { "GuildId", "UserId", "PeriodStart", "Granularity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberActivitySnapshots_UserId",
                table: "MemberActivitySnapshots",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_AuthorId_Timestamp",
                table: "MessageLogs",
                columns: new[] { "AuthorId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_ChannelId_Timestamp",
                table: "MessageLogs",
                columns: new[] { "ChannelId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_DiscordMessageId_Unique",
                table: "MessageLogs",
                column: "DiscordMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_GuildId_Timestamp",
                table: "MessageLogs",
                columns: new[] { "GuildId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_LoggedAt",
                table: "MessageLogs",
                column: "LoggedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MetricSnapshots_Timestamp",
                table: "MetricSnapshots",
                column: "Timestamp",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_ExpiresAt_Type",
                table: "ModerationCases",
                columns: new[] { "ExpiresAt", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_GuildId_CaseNumber",
                table: "ModerationCases",
                columns: new[] { "GuildId", "CaseNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_GuildId_ModeratorUserId_CreatedAt",
                table: "ModerationCases",
                columns: new[] { "GuildId", "ModeratorUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_GuildId_TargetUserId_CreatedAt",
                table: "ModerationCases",
                columns: new[] { "GuildId", "TargetUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_GuildId_Type_CreatedAt",
                table: "ModerationCases",
                columns: new[] { "GuildId", "Type", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_RelatedFlaggedEventId",
                table: "ModerationCases",
                column: "RelatedFlaggedEventId");

            migrationBuilder.CreateIndex(
                name: "IX_ModNotes_GuildId_AuthorUserId_CreatedAt",
                table: "ModNotes",
                columns: new[] { "GuildId", "AuthorUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModNotes_GuildId_TargetUserId_CreatedAt",
                table: "ModNotes",
                columns: new[] { "GuildId", "TargetUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModTags_GuildId_Category",
                table: "ModTags",
                columns: new[] { "GuildId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_ModTags_GuildId_Name",
                table: "ModTags",
                columns: new[] { "GuildId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAlertConfigs_IsEnabled",
                table: "PerformanceAlertConfigs",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAlertConfigs_MetricName",
                table: "PerformanceAlertConfigs",
                column: "MetricName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceIncidents_MetricName_TriggeredAt",
                table: "PerformanceIncidents",
                columns: new[] { "MetricName", "TriggeredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceIncidents_Severity_Status",
                table: "PerformanceIncidents",
                columns: new[] { "Severity", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceIncidents_Status",
                table: "PerformanceIncidents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceIncidents_TriggeredAt",
                table: "PerformanceIncidents",
                column: "TriggeredAt");

            migrationBuilder.CreateIndex(
                name: "IX_RatRecords_GuildId_UserId",
                table: "RatRecords",
                columns: new[] { "GuildId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_RatRecords_RatWatchId",
                table: "RatRecords",
                column: "RatWatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RatRecords_RecordedAt",
                table: "RatRecords",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RatVotes_RatWatchId",
                table: "RatVotes",
                column: "RatWatchId");

            migrationBuilder.CreateIndex(
                name: "IX_RatVotes_RatWatchId_VoterUserId_Unique",
                table: "RatVotes",
                columns: new[] { "RatWatchId", "VoterUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RatWatches_ChannelId",
                table: "RatWatches",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_RatWatches_GuildId_AccusedUserId",
                table: "RatWatches",
                columns: new[] { "GuildId", "AccusedUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_RatWatches_GuildId_ScheduledAt_Status",
                table: "RatWatches",
                columns: new[] { "GuildId", "ScheduledAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_GuildId",
                table: "Reminders",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_Status_TriggerAt",
                table: "Reminders",
                columns: new[] { "Status", "TriggerAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_UserId",
                table: "Reminders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledMessages_ChannelId",
                table: "ScheduledMessages",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledMessages_GuildId_IsEnabled",
                table: "ScheduledMessages",
                columns: new[] { "GuildId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledMessages_NextExecutionAt_IsEnabled",
                table: "ScheduledMessages",
                columns: new[] { "NextExecutionAt", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_SoundPlayLogs_GuildId_PlayedAt",
                table: "SoundPlayLogs",
                columns: new[] { "GuildId", "PlayedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SoundPlayLogs_PlayedAt",
                table: "SoundPlayLogs",
                column: "PlayedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SoundPlayLogs_SoundId_PlayedAt",
                table: "SoundPlayLogs",
                columns: new[] { "SoundId", "PlayedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sounds_GuildId",
                table: "Sounds",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Sounds_GuildId_Name",
                table: "Sounds",
                columns: new[] { "GuildId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Themes_IsActive",
                table: "Themes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Themes_ThemeKey",
                table: "Themes",
                column: "ThemeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TtsMessages_CreatedAt",
                table: "TtsMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TtsMessages_GuildId_CreatedAt",
                table: "TtsMessages",
                columns: new[] { "GuildId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TtsMessages_GuildId_UserId_CreatedAt",
                table: "TtsMessages",
                columns: new[] { "GuildId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityEvents_EventType_GuildId_Timestamp",
                table: "UserActivityEvents",
                columns: new[] { "EventType", "GuildId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityEvents_GuildId_Timestamp",
                table: "UserActivityEvents",
                columns: new[] { "GuildId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityEvents_LoggedAt",
                table: "UserActivityEvents",
                column: "LoggedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityEvents_UserId_Timestamp",
                table: "UserActivityEvents",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityLogs_Action",
                table: "UserActivityLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityLogs_ActorUserId",
                table: "UserActivityLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityLogs_TargetUserId",
                table: "UserActivityLogs",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityLogs_Timestamp",
                table: "UserActivityLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_UserConsents_DiscordUserId_ConsentType",
                table: "UserConsents",
                columns: new[] { "DiscordUserId", "ConsentType" });

            migrationBuilder.CreateIndex(
                name: "IX_UserConsents_GrantedAt",
                table: "UserConsents",
                column: "GrantedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserConsents_RevokedAt",
                table: "UserConsents",
                column: "RevokedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserDiscordGuilds_ApplicationUserId_GuildId",
                table: "UserDiscordGuilds",
                columns: new[] { "ApplicationUserId", "GuildId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserDiscordGuilds_GuildId",
                table: "UserDiscordGuilds",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGuildAccess_GuildId",
                table: "UserGuildAccess",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_UserModTags_GuildId_UserId",
                table: "UserModTags",
                columns: new[] { "GuildId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserModTags_GuildId_UserId_TagId",
                table: "UserModTags",
                columns: new[] { "GuildId", "UserId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserModTags_TagId_AppliedAt",
                table: "UserModTags",
                columns: new[] { "TagId", "AppliedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_GuildId",
                table: "UserNotifications",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId_DismissedAt",
                table: "UserNotifications",
                columns: new[] { "UserId", "DismissedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId_IsRead_CreatedAt",
                table: "UserNotifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_LastSeenAt",
                table: "Users",
                column: "LastSeenAt");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationCodes_ApplicationUserId",
                table: "VerificationCodes",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationCodes_Code",
                table: "VerificationCodes",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationCodes_DiscordUserId",
                table: "VerificationCodes",
                column: "DiscordUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationCodes_ExpiresAt",
                table: "VerificationCodes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationCodes_Status",
                table: "VerificationCodes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationCodes_Status_ExpiresAt",
                table: "VerificationCodes",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Watchlists_GuildId_AddedAt",
                table: "Watchlists",
                columns: new[] { "GuildId", "AddedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Watchlists_GuildId_AddedByUserId",
                table: "Watchlists",
                columns: new[] { "GuildId", "AddedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Watchlists_GuildId_UserId",
                table: "Watchlists",
                columns: new[] { "GuildId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WelcomeConfigurations_IsEnabled",
                table: "WelcomeConfigurations",
                column: "IsEnabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationSettings");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AssistantGuildSettings");

            migrationBuilder.DropTable(
                name: "AssistantInteractionLogs");

            migrationBuilder.DropTable(
                name: "AssistantUsageMetrics");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ChannelActivitySnapshots");

            migrationBuilder.DropTable(
                name: "CommandLogs");

            migrationBuilder.DropTable(
                name: "CommandModuleConfigurations");

            migrationBuilder.DropTable(
                name: "CommandRoleRestrictions");

            migrationBuilder.DropTable(
                name: "ConnectionEvents");

            migrationBuilder.DropTable(
                name: "DiscordOAuthTokens");

            migrationBuilder.DropTable(
                name: "GuildMembers");

            migrationBuilder.DropTable(
                name: "GuildMetricsSnapshots");

            migrationBuilder.DropTable(
                name: "GuildModerationConfigs");

            migrationBuilder.DropTable(
                name: "GuildRatWatchSettings");

            migrationBuilder.DropTable(
                name: "GuildTtsSettings");

            migrationBuilder.DropTable(
                name: "MemberActivitySnapshots");

            migrationBuilder.DropTable(
                name: "MessageLogs");

            migrationBuilder.DropTable(
                name: "MetricSnapshots");

            migrationBuilder.DropTable(
                name: "ModerationCases");

            migrationBuilder.DropTable(
                name: "ModNotes");

            migrationBuilder.DropTable(
                name: "PerformanceAlertConfigs");

            migrationBuilder.DropTable(
                name: "PerformanceIncidents");

            migrationBuilder.DropTable(
                name: "RatRecords");

            migrationBuilder.DropTable(
                name: "RatVotes");

            migrationBuilder.DropTable(
                name: "Reminders");

            migrationBuilder.DropTable(
                name: "ScheduledMessages");

            migrationBuilder.DropTable(
                name: "SoundPlayLogs");

            migrationBuilder.DropTable(
                name: "TtsMessages");

            migrationBuilder.DropTable(
                name: "UserActivityEvents");

            migrationBuilder.DropTable(
                name: "UserActivityLogs");

            migrationBuilder.DropTable(
                name: "UserConsents");

            migrationBuilder.DropTable(
                name: "UserDiscordGuilds");

            migrationBuilder.DropTable(
                name: "UserGuildAccess");

            migrationBuilder.DropTable(
                name: "UserModTags");

            migrationBuilder.DropTable(
                name: "UserNotifications");

            migrationBuilder.DropTable(
                name: "VerificationCodes");

            migrationBuilder.DropTable(
                name: "Watchlists");

            migrationBuilder.DropTable(
                name: "WelcomeConfigurations");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "GuildAudioSettings");

            migrationBuilder.DropTable(
                name: "FlaggedEvents");

            migrationBuilder.DropTable(
                name: "RatWatches");

            migrationBuilder.DropTable(
                name: "Sounds");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "ModTags");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Guilds");

            migrationBuilder.DropTable(
                name: "Themes");
        }
    }
}
