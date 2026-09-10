using System.IO.Compression;
using System.Text.Json;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
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
    private readonly UserDataExportService _service;
    private readonly Mock<IAuditLogService> _auditLogServiceMock;
    private readonly Mock<IAuditLogBuilder> _auditLogBuilderMock;
    private readonly Mock<IWebHostEnvironment> _environmentMock;
    private readonly string _webRootPath;

    public UserDataExportServiceTests()
    {
        // EF's InMemory provider, not SQLite: ExportApplicationUserAsync's
        // Users.OfType<ApplicationUser>() query (pre-existing, unrelated to this change) does not
        // translate against the relational SQLite provider used elsewhere in this test project -
        // InMemory evaluates it client-side instead. No ExecuteDeleteAsync is exercised here (that
        // is UserPurgeServiceTests' concern, which does use SQLite), so InMemory's other
        // limitations don't apply to these tests.
        var options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new BotDbContext(options);

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

        _environmentMock = new Mock<IWebHostEnvironment>();
        _environmentMock.Setup(x => x.WebRootPath).Returns(_webRootPath);

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

        if (Directory.Exists(_webRootPath))
        {
            Directory.Delete(_webRootPath, recursive: true);
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

        // The zip should contain the new export files.
        var zipPath = Path.Combine(_webRootPath, "exports", discordUserId.ToString(), $"{result.ExportId}.zip");
        File.Exists(zipPath).Should().BeTrue();

        using var archive = ZipFile.OpenRead(zipPath);
        archive.Entries.Select(e => e.Name).Should().Contain(
            new[] { "llm_usage_records.json", "assistant_interaction_logs.json", "dm_assistant_interaction_logs.json" });

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

        var zipPath = Path.Combine(_webRootPath, "exports", discordUserId.ToString(), $"{result.ExportId}.zip");
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
}
