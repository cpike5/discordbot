using DiscordBot.Core.Entities;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Data.Repositories;

/// <summary>
/// Unit tests for GuildMemberRepository.
/// </summary>
public class GuildMemberRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly GuildMemberRepository _repository;
    private readonly Mock<ILogger<GuildMemberRepository>> _mockLogger;
    private readonly Mock<ILogger<Repository<GuildMember>>> _mockBaseLogger;

    public GuildMemberRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<GuildMemberRepository>>();
        _mockBaseLogger = new Mock<ILogger<Repository<GuildMember>>>();
        _repository = new GuildMemberRepository(_context, _mockLogger.Object, _mockBaseLogger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    #region GetByGuildAndUserAsync Tests

    [Fact]
    public async Task GetByGuildAndUserAsync_WithExistingMember_ReturnsMember()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user = CreateTestUser(987654321);
        var member = CreateTestGuildMember(guild.Id, user.Id);

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddAsync(user);
        await _context.GuildMembers.AddAsync(member);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByGuildAndUserAsync(guild.Id, user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.GuildId.Should().Be(guild.Id);
        result.UserId.Should().Be(user.Id);
        result.Nickname.Should().Be("TestNickname");
    }

    [Fact]
    public async Task GetByGuildAndUserAsync_WithNonExistentMember_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByGuildAndUserAsync(999999999, 888888888);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByGuildAndUserAsync_WithCompositeKeyConstraint_ReturnCorrectMember()
    {
        // Arrange - Same user in different guilds
        var guild1 = CreateTestGuild(111111111);
        var guild2 = CreateTestGuild(222222222);
        var user = CreateTestUser(987654321);
        var member1 = CreateTestGuildMember(guild1.Id, user.Id, nickname: "Guild1Nick");
        var member2 = CreateTestGuildMember(guild2.Id, user.Id, nickname: "Guild2Nick");

        await _context.Guilds.AddRangeAsync(guild1, guild2);
        await _context.Users.AddAsync(user);
        await _context.GuildMembers.AddRangeAsync(member1, member2);
        await _context.SaveChangesAsync();

        // Act
        var resultGuild1 = await _repository.GetByGuildAndUserAsync(guild1.Id, user.Id);
        var resultGuild2 = await _repository.GetByGuildAndUserAsync(guild2.Id, user.Id);

        // Assert
        resultGuild1.Should().NotBeNull();
        resultGuild1!.Nickname.Should().Be("Guild1Nick");

        resultGuild2.Should().NotBeNull();
        resultGuild2!.Nickname.Should().Be("Guild2Nick");
    }

    #endregion

    #region UpsertAsync Tests

    [Fact]
    public async Task UpsertAsync_WithNewMember_CreatesMember()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user = CreateTestUser(987654321);
        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var newMember = CreateTestGuildMember(guild.Id, user.Id);

        // Act
        var result = await _repository.UpsertAsync(newMember);

        // Assert
        result.Should().NotBeNull();
        result.GuildId.Should().Be(guild.Id);
        result.UserId.Should().Be(user.Id);
        result.Nickname.Should().Be("TestNickname");

        var savedMember = await _context.GuildMembers.FindAsync(guild.Id, user.Id);
        savedMember.Should().NotBeNull();
        savedMember!.Nickname.Should().Be("TestNickname");
    }

    [Fact]
    public async Task UpsertAsync_WithExistingMember_UpdatesMember()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user = CreateTestUser(987654321);
        var existingMember = CreateTestGuildMember(guild.Id, user.Id, nickname: "OldNick", rolesJson: "[111, 222]");

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddAsync(user);
        await _context.GuildMembers.AddAsync(existingMember);
        await _context.SaveChangesAsync();

        // Detach to simulate fresh update
        _context.Entry(existingMember).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var updatedMember = CreateTestGuildMember(guild.Id, user.Id, nickname: "NewNick", rolesJson: "[333, 444]");

        // Act
        var result = await _repository.UpsertAsync(updatedMember);

        // Assert
        result.Should().NotBeNull();
        result.Nickname.Should().Be("NewNick");
        result.CachedRolesJson.Should().Be("[333, 444]");

        var savedMember = await _context.GuildMembers.FindAsync(guild.Id, user.Id);
        savedMember.Should().NotBeNull();
        savedMember!.Nickname.Should().Be("NewNick");
        savedMember.CachedRolesJson.Should().Be("[333, 444]");
    }

    [Fact]
    public async Task UpsertAsync_WithExistingMember_UpdatesAllFields()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user = CreateTestUser(987654321);
        var originalJoinedAt = DateTime.UtcNow.AddDays(-30);
        var existingMember = new GuildMember
        {
            GuildId = guild.Id,
            UserId = user.Id,
            JoinedAt = originalJoinedAt,
            Nickname = "OldNick",
            CachedRolesJson = "[111]",
            LastActiveAt = DateTime.UtcNow.AddDays(-1),
            LastCachedAt = DateTime.UtcNow.AddDays(-1),
            IsActive = false
        };

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddAsync(user);
        await _context.GuildMembers.AddAsync(existingMember);
        await _context.SaveChangesAsync();
        _context.Entry(existingMember).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var newJoinedAt = DateTime.UtcNow.AddDays(-20);
        var newLastActive = DateTime.UtcNow;
        var newLastCached = DateTime.UtcNow;
        var updatedMember = new GuildMember
        {
            GuildId = guild.Id,
            UserId = user.Id,
            JoinedAt = newJoinedAt,
            Nickname = "NewNick",
            CachedRolesJson = "[222, 333]",
            LastActiveAt = newLastActive,
            LastCachedAt = newLastCached,
            IsActive = true
        };

        // Act
        var result = await _repository.UpsertAsync(updatedMember);

        // Assert
        var savedMember = await _context.GuildMembers.FindAsync(guild.Id, user.Id);
        savedMember.Should().NotBeNull();
        savedMember!.Nickname.Should().Be("NewNick");
        savedMember.CachedRolesJson.Should().Be("[222, 333]");
        savedMember.JoinedAt.Should().BeCloseTo(newJoinedAt, TimeSpan.FromSeconds(1));
        savedMember.LastActiveAt.Should().BeCloseTo(newLastActive, TimeSpan.FromSeconds(1));
        savedMember.LastCachedAt.Should().BeCloseTo(newLastCached, TimeSpan.FromSeconds(1));
        savedMember.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertAsync_WithInactiveMemberRejoining_SetsActiveTrue()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user = CreateTestUser(987654321);
        var inactiveMember = CreateTestGuildMember(guild.Id, user.Id);
        inactiveMember.IsActive = false;

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddAsync(user);
        await _context.GuildMembers.AddAsync(inactiveMember);
        await _context.SaveChangesAsync();
        _context.Entry(inactiveMember).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var rejoinedMember = CreateTestGuildMember(guild.Id, user.Id);
        rejoinedMember.IsActive = true;

        // Act
        var result = await _repository.UpsertAsync(rejoinedMember);

        // Assert
        result.IsActive.Should().BeTrue();
        var savedMember = await _context.GuildMembers.FindAsync(guild.Id, user.Id);
        savedMember!.IsActive.Should().BeTrue();
    }

    #endregion

    #region BatchUpsertAsync Tests

    [Fact]
    public async Task BatchUpsertAsync_WithNewMembers_CreatesAllMembers()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user1 = CreateTestUser(111111111);
        var user2 = CreateTestUser(222222222);
        var user3 = CreateTestUser(333333333);

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddRangeAsync(user1, user2, user3);
        await _context.SaveChangesAsync();

        var members = new[]
        {
            CreateTestGuildMember(guild.Id, user1.Id, nickname: "User1"),
            CreateTestGuildMember(guild.Id, user2.Id, nickname: "User2"),
            CreateTestGuildMember(guild.Id, user3.Id, nickname: "User3")
        };

        // Act
        var affected = await _repository.BatchUpsertAsync(members);

        // Assert
        affected.Should().BeGreaterThan(0);

        var savedMembers = await _repository.GetActiveByGuildAsync(guild.Id);
        savedMembers.Should().HaveCount(3);
        savedMembers.Should().Contain(m => m.Nickname == "User1");
        savedMembers.Should().Contain(m => m.Nickname == "User2");
        savedMembers.Should().Contain(m => m.Nickname == "User3");
    }

    [Fact]
    public async Task BatchUpsertAsync_WithMixOfNewAndExisting_UpdatesCorrectly()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user1 = CreateTestUser(111111111);
        var user2 = CreateTestUser(222222222);
        var user3 = CreateTestUser(333333333);

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddRangeAsync(user1, user2, user3);

        // Add existing members for user1 and user2
        var existingMember1 = CreateTestGuildMember(guild.Id, user1.Id, nickname: "OldNick1");
        var existingMember2 = CreateTestGuildMember(guild.Id, user2.Id, nickname: "OldNick2");
        await _context.GuildMembers.AddRangeAsync(existingMember1, existingMember2);
        await _context.SaveChangesAsync();

        var members = new[]
        {
            CreateTestGuildMember(guild.Id, user1.Id, nickname: "NewNick1"), // Update
            CreateTestGuildMember(guild.Id, user2.Id, nickname: "NewNick2"), // Update
            CreateTestGuildMember(guild.Id, user3.Id, nickname: "NewNick3")  // Insert
        };

        // Act
        var affected = await _repository.BatchUpsertAsync(members);

        // Assert
        affected.Should().BeGreaterThan(0);

        var savedMembers = await _repository.GetActiveByGuildAsync(guild.Id);
        savedMembers.Should().HaveCount(3);
        savedMembers.Should().Contain(m => m.Nickname == "NewNick1");
        savedMembers.Should().Contain(m => m.Nickname == "NewNick2");
        savedMembers.Should().Contain(m => m.Nickname == "NewNick3");
    }

    [Fact]
    public async Task BatchUpsertAsync_WithEmptyCollection_ReturnsZero()
    {
        // Arrange
        var emptyMembers = Array.Empty<GuildMember>();

        // Act
        var affected = await _repository.BatchUpsertAsync(emptyMembers);

        // Assert
        affected.Should().Be(0);
    }

    [Fact]
    public async Task BatchUpsertAsync_WithLargeBatch_HandlesCorrectly()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        await _context.Guilds.AddAsync(guild);

        var members = new List<GuildMember>();
        var users = new List<User>();

        // Create 1000 members (batch size is 500)
        for (ulong i = 1; i <= 1000; i++)
        {
            var user = CreateTestUser(i);
            users.Add(user);
            members.Add(CreateTestGuildMember(guild.Id, i, nickname: $"User{i}"));
        }

        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();

        // Act
        var affected = await _repository.BatchUpsertAsync(members);

        // Assert
        affected.Should().BeGreaterThan(0);
        var savedMembers = await _repository.GetActiveByGuildAsync(guild.Id);
        savedMembers.Should().HaveCount(1000);
    }

    #endregion

    #region MarkInactiveAsync Tests

    [Fact]
    public async Task MarkInactiveAsync_WithExistingMember_ReturnsTrue()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user = CreateTestUser(987654321);
        var member = CreateTestGuildMember(guild.Id, user.Id);

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddAsync(user);
        await _context.GuildMembers.AddAsync(member);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.MarkInactiveAsync(guild.Id, user.Id);

        // Assert
        result.Should().BeTrue();

        // Verify member is marked inactive
        _context.Entry(member).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        var updatedMember = await _context.GuildMembers.FindAsync(guild.Id, user.Id);
        updatedMember.Should().NotBeNull();
        updatedMember!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task MarkInactiveAsync_WithNonExistentMember_ReturnsFalse()
    {
        // Act
        var result = await _repository.MarkInactiveAsync(999999999, 888888888);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MarkInactiveAsync_WithAlreadyInactiveMember_ReturnsTrue()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user = CreateTestUser(987654321);
        var member = CreateTestGuildMember(guild.Id, user.Id);
        member.IsActive = false;

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddAsync(user);
        await _context.GuildMembers.AddAsync(member);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.MarkInactiveAsync(guild.Id, user.Id);

        // Assert - Should still return true as the operation affected the row
        result.Should().BeTrue();
    }

    #endregion

    #region UpdateMemberInfoAsync Tests

    [Fact]
    public async Task UpdateMemberInfoAsync_WithExistingMember_UpdatesNicknameAndRoles()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user = CreateTestUser(987654321);
        var member = CreateTestGuildMember(guild.Id, user.Id, nickname: "OldNick", rolesJson: "[111]");
        var originalLastCached = member.LastCachedAt;

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddAsync(user);
        await _context.GuildMembers.AddAsync(member);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.UpdateMemberInfoAsync(guild.Id, user.Id, "NewNick", "[222, 333]");

        // Assert
        result.Should().BeTrue();

        _context.Entry(member).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        var updatedMember = await _context.GuildMembers.FindAsync(guild.Id, user.Id);
        updatedMember.Should().NotBeNull();
        updatedMember!.Nickname.Should().Be("NewNick");
        updatedMember.CachedRolesJson.Should().Be("[222, 333]");
        updatedMember.LastCachedAt.Should().BeAfter(originalLastCached);
    }

    [Fact]
    public async Task UpdateMemberInfoAsync_WithNonExistentMember_ReturnsFalse()
    {
        // Act
        var result = await _repository.UpdateMemberInfoAsync(999999999, 888888888, "Nick", "[123]");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateMemberInfoAsync_WithNullNickname_ClearsNickname()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user = CreateTestUser(987654321);
        var member = CreateTestGuildMember(guild.Id, user.Id, nickname: "OldNick");

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddAsync(user);
        await _context.GuildMembers.AddAsync(member);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.UpdateMemberInfoAsync(guild.Id, user.Id, null, "[123]");

        // Assert
        result.Should().BeTrue();
        _context.Entry(member).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        var updatedMember = await _context.GuildMembers.FindAsync(guild.Id, user.Id);
        updatedMember!.Nickname.Should().BeNull();
    }

    #endregion

    #region GetActiveByGuildAsync Tests

    [Fact]
    public async Task GetActiveByGuildAsync_ReturnsOnlyActiveMembers()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user1 = CreateTestUser(111111111);
        var user2 = CreateTestUser(222222222);
        var user3 = CreateTestUser(333333333);

        var activeMember1 = CreateTestGuildMember(guild.Id, user1.Id, nickname: "Active1");
        var activeMember2 = CreateTestGuildMember(guild.Id, user2.Id, nickname: "Active2");
        var inactiveMember = CreateTestGuildMember(guild.Id, user3.Id, nickname: "Inactive");
        inactiveMember.IsActive = false;

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddRangeAsync(user1, user2, user3);
        await _context.GuildMembers.AddRangeAsync(activeMember1, activeMember2, inactiveMember);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveByGuildAsync(guild.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(m => m.Nickname == "Active1");
        result.Should().Contain(m => m.Nickname == "Active2");
        result.Should().NotContain(m => m.Nickname == "Inactive");
    }

    [Fact]
    public async Task GetActiveByGuildAsync_IncludesUserNavigationProperty()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user = CreateTestUser(987654321);
        var member = CreateTestGuildMember(guild.Id, user.Id);

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddAsync(user);
        await _context.GuildMembers.AddAsync(member);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveByGuildAsync(guild.Id);

        // Assert
        result.Should().HaveCount(1);
        result[0].User.Should().NotBeNull();
        result[0].User.Username.Should().Be("TestUser");
    }

    [Fact]
    public async Task GetActiveByGuildAsync_OrdersByJoinedAt()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user1 = CreateTestUser(111111111);
        var user2 = CreateTestUser(222222222);
        var user3 = CreateTestUser(333333333);

        var member1 = CreateTestGuildMember(guild.Id, user1.Id);
        member1.JoinedAt = DateTime.UtcNow.AddDays(-10);

        var member2 = CreateTestGuildMember(guild.Id, user2.Id);
        member2.JoinedAt = DateTime.UtcNow.AddDays(-5);

        var member3 = CreateTestGuildMember(guild.Id, user3.Id);
        member3.JoinedAt = DateTime.UtcNow.AddDays(-1);

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddRangeAsync(user1, user2, user3);
        await _context.GuildMembers.AddRangeAsync(member1, member2, member3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveByGuildAsync(guild.Id);

        // Assert
        result.Should().HaveCount(3);
        result[0].UserId.Should().Be(user1.Id); // Oldest
        result[1].UserId.Should().Be(user2.Id);
        result[2].UserId.Should().Be(user3.Id); // Newest
    }

    [Fact]
    public async Task GetActiveByGuildAsync_WithNoMembers_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetActiveByGuildAsync(999999999);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region GetMemberUserIdsAsync Tests

    [Fact]
    public async Task GetMemberUserIdsAsync_ReturnsActiveUserIds()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user1 = CreateTestUser(111111111);
        var user2 = CreateTestUser(222222222);
        var user3 = CreateTestUser(333333333);

        var activeMember1 = CreateTestGuildMember(guild.Id, user1.Id);
        var activeMember2 = CreateTestGuildMember(guild.Id, user2.Id);
        var inactiveMember = CreateTestGuildMember(guild.Id, user3.Id);
        inactiveMember.IsActive = false;

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddRangeAsync(user1, user2, user3);
        await _context.GuildMembers.AddRangeAsync(activeMember1, activeMember2, inactiveMember);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetMemberUserIdsAsync(guild.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(user1.Id);
        result.Should().Contain(user2.Id);
        result.Should().NotContain(user3.Id);
    }

    [Fact]
    public async Task GetMemberUserIdsAsync_WithNoMembers_ReturnsEmptyHashSet()
    {
        // Act
        var result = await _repository.GetMemberUserIdsAsync(999999999);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMemberUserIdsAsync_ReturnsHashSet()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user1 = CreateTestUser(111111111);
        var user2 = CreateTestUser(222222222);

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddRangeAsync(user1, user2);
        await _context.GuildMembers.AddRangeAsync(
            CreateTestGuildMember(guild.Id, user1.Id),
            CreateTestGuildMember(guild.Id, user2.Id)
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetMemberUserIdsAsync(guild.Id);

        // Assert
        result.Should().BeOfType<HashSet<ulong>>();
    }

    #endregion

    #region MarkInactiveExceptAsync Tests

    [Fact]
    public async Task MarkInactiveExceptAsync_MarksCorrectMembers()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user1 = CreateTestUser(111111111);
        var user2 = CreateTestUser(222222222);
        var user3 = CreateTestUser(333333333);

        var member1 = CreateTestGuildMember(guild.Id, user1.Id);
        var member2 = CreateTestGuildMember(guild.Id, user2.Id);
        var member3 = CreateTestGuildMember(guild.Id, user3.Id);

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddRangeAsync(user1, user2, user3);
        await _context.GuildMembers.AddRangeAsync(member1, member2, member3);
        await _context.SaveChangesAsync();

        var activeUserIds = new[] { user1.Id, user2.Id }; // user3 should be marked inactive

        // Act
        var affected = await _repository.MarkInactiveExceptAsync(guild.Id, activeUserIds);

        // Assert
        affected.Should().Be(1); // Only user3 should be marked inactive

        _context.Entry(member1).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        _context.Entry(member2).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        _context.Entry(member3).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var updatedMembers = await _repository.GetActiveByGuildAsync(guild.Id);
        updatedMembers.Should().HaveCount(2);
        updatedMembers.Should().Contain(m => m.UserId == user1.Id);
        updatedMembers.Should().Contain(m => m.UserId == user2.Id);
        updatedMembers.Should().NotContain(m => m.UserId == user3.Id);
    }

    [Fact]
    public async Task MarkInactiveExceptAsync_WithEmptyActiveList_MarksAllInactive()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user1 = CreateTestUser(111111111);
        var user2 = CreateTestUser(222222222);

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddRangeAsync(user1, user2);
        await _context.GuildMembers.AddRangeAsync(
            CreateTestGuildMember(guild.Id, user1.Id),
            CreateTestGuildMember(guild.Id, user2.Id)
        );
        await _context.SaveChangesAsync();

        var activeUserIds = Array.Empty<ulong>();

        // Act
        var affected = await _repository.MarkInactiveExceptAsync(guild.Id, activeUserIds);

        // Assert
        affected.Should().Be(2);
        var activeMembers = await _repository.GetActiveByGuildAsync(guild.Id);
        activeMembers.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkInactiveExceptAsync_WithAllUsersActive_MarksNoneInactive()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user1 = CreateTestUser(111111111);
        var user2 = CreateTestUser(222222222);

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddRangeAsync(user1, user2);
        await _context.GuildMembers.AddRangeAsync(
            CreateTestGuildMember(guild.Id, user1.Id),
            CreateTestGuildMember(guild.Id, user2.Id)
        );
        await _context.SaveChangesAsync();

        var activeUserIds = new[] { user1.Id, user2.Id };

        // Act
        var affected = await _repository.MarkInactiveExceptAsync(guild.Id, activeUserIds);

        // Assert
        affected.Should().Be(0);
        var activeMembers = await _repository.GetActiveByGuildAsync(guild.Id);
        activeMembers.Should().HaveCount(2);
    }

    [Fact]
    public async Task MarkInactiveExceptAsync_IgnoresAlreadyInactiveMembers()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user1 = CreateTestUser(111111111);
        var user2 = CreateTestUser(222222222);

        var member1 = CreateTestGuildMember(guild.Id, user1.Id);
        var member2 = CreateTestGuildMember(guild.Id, user2.Id);
        member2.IsActive = false; // Already inactive

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddRangeAsync(user1, user2);
        await _context.GuildMembers.AddRangeAsync(member1, member2);
        await _context.SaveChangesAsync();

        var activeUserIds = Array.Empty<ulong>();

        // Act
        var affected = await _repository.MarkInactiveExceptAsync(guild.Id, activeUserIds);

        // Assert
        affected.Should().Be(1); // Only user1 needs to be marked inactive
    }

    #endregion

    #region GetMemberCountAsync Tests

    [Fact]
    public async Task GetMemberCountAsync_WithActiveOnly_CountsOnlyActive()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user1 = CreateTestUser(111111111);
        var user2 = CreateTestUser(222222222);
        var user3 = CreateTestUser(333333333);

        var activeMember1 = CreateTestGuildMember(guild.Id, user1.Id);
        var activeMember2 = CreateTestGuildMember(guild.Id, user2.Id);
        var inactiveMember = CreateTestGuildMember(guild.Id, user3.Id);
        inactiveMember.IsActive = false;

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddRangeAsync(user1, user2, user3);
        await _context.GuildMembers.AddRangeAsync(activeMember1, activeMember2, inactiveMember);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetMemberCountAsync(guild.Id, activeOnly: true);

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public async Task GetMemberCountAsync_WithActiveOnlyFalse_CountsAll()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user1 = CreateTestUser(111111111);
        var user2 = CreateTestUser(222222222);
        var user3 = CreateTestUser(333333333);

        var activeMember1 = CreateTestGuildMember(guild.Id, user1.Id);
        var activeMember2 = CreateTestGuildMember(guild.Id, user2.Id);
        var inactiveMember = CreateTestGuildMember(guild.Id, user3.Id);
        inactiveMember.IsActive = false;

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddRangeAsync(user1, user2, user3);
        await _context.GuildMembers.AddRangeAsync(activeMember1, activeMember2, inactiveMember);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetMemberCountAsync(guild.Id, activeOnly: false);

        // Assert
        result.Should().Be(3);
    }

    [Fact]
    public async Task GetMemberCountAsync_WithNoMembers_ReturnsZero()
    {
        // Act
        var result = await _repository.GetMemberCountAsync(999999999);

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region GetLastSyncTimeAsync Tests

    [Fact]
    public async Task GetLastSyncTimeAsync_ReturnsLatestLastCachedAt()
    {
        // Arrange
        var guild = CreateTestGuild(123456789);
        var user1 = CreateTestUser(111111111);
        var user2 = CreateTestUser(222222222);

        var member1 = CreateTestGuildMember(guild.Id, user1.Id);
        member1.LastCachedAt = DateTime.UtcNow.AddHours(-2);

        var member2 = CreateTestGuildMember(guild.Id, user2.Id);
        member2.LastCachedAt = DateTime.UtcNow.AddHours(-1); // Most recent

        await _context.Guilds.AddAsync(guild);
        await _context.Users.AddRangeAsync(user1, user2);
        await _context.GuildMembers.AddRangeAsync(member1, member2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetLastSyncTimeAsync(guild.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeCloseTo(member2.LastCachedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetLastSyncTimeAsync_WithNoMembers_ReturnsNull()
    {
        // Act
        var result = await _repository.GetLastSyncTimeAsync(999999999);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private static Guild CreateTestGuild(ulong guildId, string name = "Test Guild")
    {
        return new Guild
        {
            Id = guildId,
            Name = name,
            JoinedAt = DateTime.UtcNow.AddDays(-30),
            IsActive = true
        };
    }

    private static User CreateTestUser(ulong userId, string username = "TestUser")
    {
        return new User
        {
            Id = userId,
            Username = username,
            Discriminator = "1234",
            FirstSeenAt = DateTime.UtcNow.AddDays(-30),
            LastSeenAt = DateTime.UtcNow
        };
    }

    private static GuildMember CreateTestGuildMember(
        ulong guildId,
        ulong userId,
        string? nickname = "TestNickname",
        string? rolesJson = "[123, 456]")
    {
        return new GuildMember
        {
            GuildId = guildId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow.AddDays(-10),
            Nickname = nickname,
            CachedRolesJson = rolesJson,
            LastActiveAt = DateTime.UtcNow,
            LastCachedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    #endregion
}
