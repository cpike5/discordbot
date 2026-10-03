using System.IO.Compression;
using System.Text.Json;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="UserDataExportService"/>. Focused on the assistant-data export added
/// alongside the LLM usage ledger (<see cref="LlmUsageRecord"/>, <see cref="AssistantInteractionLog"/>,
/// <see cref="DmAssistantInteractionLog"/>) - the other export categories are covered indirectly
/// since <c>ExportUserDataAsync</c> runs every category in one pass.
/// </summary>
public class UserDataExportServiceTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly TestDatabase _database;
    private readonly UserDataExportService _service;
    private readonly Mock<IAuditLogService> _auditLogServiceMock;
    private readonly Mock<IAuditLogBuilder> _auditLogBuilderMock;
    private readonly Mock<IWebHostEnvironment> _environmentMock;
    private readonly string _webRootPath;
    private readonly string _contentRootPath;

    public UserDataExportServiceTests()
    {
        (_context, _database) = TestDbContextFactory.CreateContext();

        _auditLogServiceMock = new Mock<IAuditLogService>();
        _auditLogBuilderMock = new Mock<IAuditLogBuilder>();
        _auditLogServiceMock.Setup(x => x.CreateBuilder()).Returns(_auditLogBuilderMock.Object);
        _auditLogBuilderMock.Setup(x => x.ForCategory(It.IsAny<AuditLogCategory>())).Returns(_auditLogBuilderMock.Object);
        _auditLogBuilderMock.Setup(x => x.WithAction(It.IsAny<AuditLogAction>())).Returns(_auditLogBuilderMock.Object);
        _auditLogBuilderMock.Setup(x => x.ByUser(It.IsAny<string>())).Returns(_auditLogBuilderMock.Object);
        _auditLogBuilderMock.Setup(x => x.OnTarget(It.IsAny<string>(), It.IsAny<string>())).Returns(_auditLogBuilderMock.Object);
        _auditLogBuilderMock.Setup(x => x.WithDetails(It.IsAny<object>())).Returns(_auditLogBuilderMock.Object);
        _auditLogBuilderMock.Setup(x => x.WithCorrelationId(It.IsAny<string>())).Returns(_auditLogBuilderMock.Object);

        _webRootPath = Path.Combine(Path.GetTempPath(), $"export_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_webRootPath);

        _contentRootPath = Path.Combine(Path.GetTempPath(), $"export_test_content_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRootPath);

        _environmentMock = new Mock<IWebHostEnvironment>();
        _environmentMock.Setup(x => x.WebRootPath).Returns(_webRootPath);
        _environmentMock.Setup(x => x.ContentRootPath).Returns(_contentRootPath);

        var applicationOptions = Options.Create(new ApplicationOptions { BaseUrl = "https://localhost:5001" });

        _service = new UserDataExportService(
            _context,
            _auditLogServiceMock.Object,
            _environmentMock.Object,
            applicationOptions,
            new Mock<ILogger<UserDataExportService>>().Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _database.Dispose();

        foreach (var path in new[] { _webRootPath, _contentRootPath })
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExportUserDataAsync_IncludesLlmUsageRecords_AndAssistantInteractionLogs()
    {
        // Arrange
        var discordUserId = 111222333UL;
        var guildId = 444555666UL;

        _context.Guilds.Add(new Guild { Id = guildId, Name = "Test Guild", JoinedAt = DateTime.UtcNow });
        _context.Users.Add(new User { Id = discordUserId });

        _context.LlmUsageRecords.Add(new LlmUsageRecord
        {
            Timestamp = DateTime.UtcNow,
            Mode = LlmMode.GuildAssistant,
            UserId = discordUserId,
            GuildId = guildId,
            Model = "anthropic/claude-sonnet-4",
            InputTokens = 100,
            OutputTokens = 50,
            CostUsd = 0.02m,
            CostSource = LlmCostSource.Billed,
            Success = true
        });

        _context.AssistantInteractionLogs.Add(new AssistantInteractionLog
        {
            Timestamp = DateTime.UtcNow,
            UserId = discordUserId,
            GuildId = guildId,
            ChannelId = 777888999UL,
            MessageId = 1000000001UL,
            Question = "What's the weather?",
            Response = "I don't have weather data.",
            Success = true
        });

        _context.DmAssistantInteractionLogs.Add(new DmAssistantInteractionLog
        {
            Timestamp = DateTime.UtcNow,
            UserId = discordUserId,
            Message = "Hello",
            Response = "Hi there",
            Success = true
        });

        _context.DmAssistantUsageMetrics.Add(new DmAssistantUsageMetrics
        {
            UserId = discordUserId,
            Date = DateTime.UtcNow.Date,
            TotalMessages = 3,
            TotalInputTokens = 300,
            TotalOutputTokens = 150,
            EstimatedCostUsd = 0.05m,
            UpdatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ExportUserDataAsync(discordUserId);

        // Assert
        result.Success.Should().BeTrue();
        result.ExportedCounts.Should().ContainKey("LlmUsageRecords");
        result.ExportedCounts["LlmUsageRecords"].Should().Be(1);
        result.ExportedCounts.Should().ContainKey("AssistantInteractionLogs");
        result.ExportedCounts["AssistantInteractionLogs"].Should().Be(1);
        result.ExportedCounts.Should().ContainKey("DmAssistantInteractionLogs");
        result.ExportedCounts["DmAssistantInteractionLogs"].Should().Be(1);
        result.ExportedCounts.Should().ContainKey("DmAssistantUsageMetrics");
        result.ExportedCounts["DmAssistantUsageMetrics"].Should().Be(1);

        // The zip should contain the new export files.
        var zipPath = Path.Combine(_contentRootPath, "data", "exports", discordUserId.ToString(), $"{result.ExportId}.zip");
        File.Exists(zipPath).Should().BeTrue();

        using var archive = ZipFile.OpenRead(zipPath);
        archive.Entries.Select(e => e.Name).Should().Contain(
            new[]
            {
                "llm_usage_records.json", "assistant_interaction_logs.json",
                "dm_assistant_interaction_logs.json", "dm_assistant_usage_metrics.json"
            });

        var llmUsageEntry = archive.GetEntry("llm_usage_records.json")!;
        using var reader = new StreamReader(llmUsageEntry.Open());
        var json = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
        doc.RootElement[0].GetProperty("model").GetString().Should().Be("anthropic/claude-sonnet-4");
    }

    [Fact]
    public async Task ExportUserDataAsync_WithNoAssistantData_OmitsFilesButKeepsZeroCounts()
    {
        // Arrange
        var discordUserId = 222333444UL;
        _context.Users.Add(new User { Id = discordUserId });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ExportUserDataAsync(discordUserId);

        // Assert
        result.Success.Should().BeTrue();
        result.ExportedCounts["LlmUsageRecords"].Should().Be(0);
        result.ExportedCounts["AssistantInteractionLogs"].Should().Be(0);
        result.ExportedCounts["DmAssistantInteractionLogs"].Should().Be(0);
        result.ExportedCounts["DmAssistantUsageMetrics"].Should().Be(0);

        var zipPath = Path.Combine(_contentRootPath, "data", "exports", discordUserId.ToString(), $"{result.ExportId}.zip");
        using var archive = ZipFile.OpenRead(zipPath);
        archive.Entries.Select(e => e.Name).Should().NotContain("llm_usage_records.json");
    }

    [Fact]
    public async Task ExportUserDataAsync_DoesNotIncludeOtherUsersAssistantData()
    {
        // Arrange
        var targetUserId = 333444555UL;
        var otherUserId = 999888777UL;

        _context.Users.Add(new User { Id = targetUserId });
        _context.Users.Add(new User { Id = otherUserId });

        _context.LlmUsageRecords.Add(new LlmUsageRecord
        {
            Timestamp = DateTime.UtcNow,
            Mode = LlmMode.DmAssistant,
            UserId = otherUserId,
            Model = "anthropic/claude-sonnet-4",
            CostUsd = 0.01m,
            CostSource = LlmCostSource.Estimated
        });

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ExportUserDataAsync(targetUserId);

        // Assert
        result.Success.Should().BeTrue();
        result.ExportedCounts["LlmUsageRecords"].Should().Be(0, "should not include other users' ledger rows");
    }

    [Fact]
    public async Task ExportUserDataAsync_DoesNotIncludeOtherUsersDmAssistantUsageMetrics()
    {
        // Arrange
        var targetUserId = 555666777UL;
        var otherUserId = 888999000UL;

        _context.Users.Add(new User { Id = targetUserId });
        _context.Users.Add(new User { Id = otherUserId });

        _context.DmAssistantUsageMetrics.Add(new DmAssistantUsageMetrics
        {
            UserId = otherUserId,
            Date = DateTime.UtcNow.Date,
            TotalMessages = 5,
            UpdatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ExportUserDataAsync(targetUserId);

        // Assert
        result.Success.Should().BeTrue();
        result.ExportedCounts["DmAssistantUsageMetrics"].Should().Be(0, "should not include other users' DM usage metrics");
    }

    // ---- Where exports live, and who can reach them

    [Fact]
    public async Task ExportUserDataAsync_WritesOutsideWebRoot_AndLinksToTheAuthenticatedHandler()
    {
        var discordUserId = 444555666UL;
        _context.Users.Add(new User { Id = discordUserId });
        await _context.SaveChangesAsync();

        var result = await _service.ExportUserDataAsync(discordUserId);

        result.Success.Should().BeTrue();
        Directory.Exists(Path.Combine(_webRootPath, "exports")).Should().BeFalse("nothing under wwwroot may hold an export");
        result.DownloadUrl.Should().Be($"https://localhost:5001/Account/Privacy?handler=DownloadExport&id={result.ExportId}");
        result.DownloadUrl.Should().NotContain("/exports/", "the link is the authenticated handler, not a static path");
    }

    [Fact]
    public async Task GetExportFilePath_ResolvesOnlyTheOwnersExport()
    {
        var ownerId = 111UL;
        var otherId = 222UL;
        _context.Users.Add(new User { Id = ownerId });
        await _context.SaveChangesAsync();
        var result = await _service.ExportUserDataAsync(ownerId);

        _service.GetExportFilePath(ownerId, result.ExportId!.Value).Should().NotBeNull().And.Subject.Should().EndWith($"{result.ExportId}.zip");
        _service.GetExportFilePath(otherId, result.ExportId.Value).Should().BeNull("another user's id never resolves it");
        _service.GetExportFilePath(ownerId, Guid.NewGuid()).Should().BeNull();
        _service.GetExportFilePath(ownerId, Guid.Empty).Should().BeNull();
    }

    [Fact]
    public async Task GetExportFilePath_ForAnExpiredFile_ReturnsNull_EvenBeforeCleanupRuns()
    {
        var discordUserId = 333UL;
        _context.Users.Add(new User { Id = discordUserId });
        await _context.SaveChangesAsync();
        var result = await _service.ExportUserDataAsync(discordUserId);
        var zipPath = _service.GetExportFilePath(discordUserId, result.ExportId!.Value)!;

        File.SetLastWriteTimeUtc(zipPath, DateTime.UtcNow.AddDays(-8));

        _service.GetExportFilePath(discordUserId, result.ExportId.Value).Should().BeNull();
    }

    [Fact]
    public async Task DeleteUserExports_RemovesTheUsersDirectory_AndLeavesOthers()
    {
        var userId = 555UL;
        var otherId = 666UL;
        _context.Users.AddRange(new User { Id = userId }, new User { Id = otherId });
        await _context.SaveChangesAsync();
        await _service.ExportUserDataAsync(userId);
        await _service.ExportUserDataAsync(userId);
        var other = await _service.ExportUserDataAsync(otherId);

        var deleted = _service.DeleteUserExports(userId);

        deleted.Should().Be(2);
        Directory.Exists(Path.Combine(_contentRootPath, "data", "exports", userId.ToString())).Should().BeFalse();
        _service.GetExportFilePath(otherId, other.ExportId!.Value).Should().NotBeNull();
        _service.DeleteUserExports(userId).Should().Be(0, "deleting again is a no-op");
    }

    [Fact]
    public async Task CleanupExpiredExportsAsync_ExpiresOnLastWriteTime_AndRemovesLegacyPublicFiles()
    {
        var discordUserId = 777UL;
        _context.Users.Add(new User { Id = discordUserId });
        await _context.SaveChangesAsync();
        var old = await _service.ExportUserDataAsync(discordUserId);
        var fresh = await _service.ExportUserDataAsync(discordUserId);
        var oldPath = Path.Combine(_contentRootPath, "data", "exports", discordUserId.ToString(), $"{old.ExportId}.zip");
        File.SetLastWriteTimeUtc(oldPath, DateTime.UtcNow.AddDays(-8));

        var legacyDir = Path.Combine(_webRootPath, "exports", "123");
        Directory.CreateDirectory(legacyDir);
        var legacyFile = Path.Combine(legacyDir, "old.zip");
        await File.WriteAllTextAsync(legacyFile, "legacy");

        var cleaned = await _service.CleanupExpiredExportsAsync();

        cleaned.Should().Be(1);
        File.Exists(oldPath).Should().BeFalse();
        _service.GetExportFilePath(discordUserId, fresh.ExportId!.Value).Should().NotBeNull();
        File.Exists(legacyFile).Should().BeFalse("files in the old public folder are deleted");
    }
}
