using DiscordBot.Bot.Extensions;
using FluentAssertions;
using Moq;

namespace DiscordBot.Tests.Bot.Extensions;

/// <summary>
/// Unit tests for MappingExtensions.
/// </summary>
public class MappingExtensionsTests
{
    [Fact]
    public async Task MapToDtosAsync_EmptyCollection_ReturnsEmptyList()
    {
        // Arrange
        var entities = Enumerable.Empty<string>();
        var mapper = new Mock<Func<string, CancellationToken, Task<int>>>();

        // Act
        var result = await entities.MapToDtosAsync(mapper.Object);

        // Assert
        result.Should().BeEmpty("an empty input collection should produce an empty output list");
        mapper.Verify(m => m(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never,
            "mapper should not be called when there are no entities");
    }

    [Fact]
    public async Task MapToDtosAsync_SingleEntity_MapsSingleEntityCorrectly()
    {
        // Arrange
        var entities = new[] { "hello" };

        Task<int> Mapper(string s, CancellationToken ct) => Task.FromResult(s.Length);

        // Act
        var result = await entities.MapToDtosAsync(Mapper);

        // Assert
        result.Should().HaveCount(1, "one entity should produce one DTO");
        result[0].Should().Be(5, "length of 'hello' is 5");
    }

    [Fact]
    public async Task MapToDtosAsync_MultipleEntities_MapsInOrder()
    {
        // Arrange
        var entities = new[] { "a", "bb", "ccc" };

        Task<int> Mapper(string s, CancellationToken ct) => Task.FromResult(s.Length);

        // Act
        var result = await entities.MapToDtosAsync(Mapper);

        // Assert
        result.Should().HaveCount(3, "three entities should produce three DTOs");
        result[0].Should().Be(1, "first entity 'a' has length 1");
        result[1].Should().Be(2, "second entity 'bb' has length 2");
        result[2].Should().Be(3, "third entity 'ccc' has length 3");
    }

    [Fact]
    public async Task MapToDtosAsync_PassesCancellationTokenToMapper()
    {
        // Arrange
        var entities = new[] { "entity1", "entity2" };
        using var cts = new CancellationTokenSource();
        var receivedTokens = new List<CancellationToken>();

        Task<string> Mapper(string s, CancellationToken ct)
        {
            receivedTokens.Add(ct);
            return Task.FromResult(s.ToUpperInvariant());
        }

        // Act
        await entities.MapToDtosAsync(Mapper, cts.Token);

        // Assert
        receivedTokens.Should().HaveCount(2, "mapper should be called once per entity");
        receivedTokens.Should().AllSatisfy(ct =>
            ct.Should().Be(cts.Token, "each mapper call should receive the provided cancellation token"));
    }

    [Fact]
    public async Task MapToDtosAsync_MapperReceivesEachEntity()
    {
        // Arrange
        var entities = new[] { "first", "second", "third" };
        var receivedEntities = new List<string>();

        Task<string> Mapper(string s, CancellationToken ct)
        {
            receivedEntities.Add(s);
            return Task.FromResult(s);
        }

        // Act
        await entities.MapToDtosAsync(Mapper);

        // Assert
        receivedEntities.Should().Equal(entities, "mapper should receive each entity in order");
    }
}
