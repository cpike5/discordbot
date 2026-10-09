using Discord;
using DiscordBot.Bot.Helpers;
using FluentAssertions;
using Moq;

namespace DiscordBot.Tests.Bot.Helpers;

/// <summary>The split rules of <see cref="DiscordReplyChunker"/>, and where the reply reference lands.</summary>
public class DiscordReplyChunkerTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Chunks_Blank_IsNothing(string response)
    {
        DiscordReplyChunker.Chunks(response).Should().BeEmpty();
    }

    [Fact]
    public void Chunks_ShortReply_IsOneMessage()
    {
        DiscordReplyChunker.Chunks("hello").Should().Equal("hello");
    }

    [Fact]
    public void Chunks_ExactlyAtTheLimit_IsOneMessage()
    {
        var text = new string('a', DiscordReplyChunker.MaxMessageLength);

        DiscordReplyChunker.Chunks(text).Should().Equal(text);
    }

    [Fact]
    public void Chunks_LongReply_SplitsOnLineBoundaries_EachWithinTheLimit()
    {
        var line = new string('x', 900);
        var text = string.Join('\n', Enumerable.Repeat(line, 5)); // 4504 chars

        var chunks = DiscordReplyChunker.Chunks(text);

        chunks.Should().HaveCount(3);
        chunks.Should().OnlyContain(c => c!.Length <= DiscordReplyChunker.MaxMessageLength);
        chunks[0].Should().Be(line + "\n" + line);
        string.Join('\n', chunks).Should().Be(text, "nothing is lost or reordered");
    }

    [Fact]
    public void Chunks_ASingleLineOverTheLimit_IsHardSplit()
    {
        var text = new string('y', 4100);

        var chunks = DiscordReplyChunker.Chunks(text);

        chunks.Select(c => c!.Length).Should().Equal(2000, 2000, 100);
    }

    [Fact]
    public void Chunks_VeryLongReply_GoesAsAFile()
    {
        var text = new string('z', DiscordReplyChunker.FileAttachmentThreshold + 1);

        DiscordReplyChunker.Chunks(text).Should().Equal(new string?[] { null });
    }

    [Fact]
    public async Task SendAsync_PutsTheReferenceOnTheFirstMessageOnly()
    {
        var channel = new Mock<IMessageChannel>();
        var sent = new List<(string? Text, MessageReference? Reference)>();
        channel
            .Setup(c => c.SendMessageAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(),
                It.IsAny<AllowedMentions>(), It.IsAny<MessageReference>(), It.IsAny<MessageComponent>(),
                It.IsAny<ISticker[]>(), It.IsAny<Embed[]>(), It.IsAny<MessageFlags>(), It.IsAny<PollProperties>()))
            .Callback<string, bool, Embed, RequestOptions, AllowedMentions, MessageReference, MessageComponent, ISticker[], Embed[], MessageFlags, PollProperties>(
                (text, _, _, _, _, reference, _, _, _, _, _) => sent.Add((text, reference)))
            .ReturnsAsync(Mock.Of<IUserMessage>());

        var reference = new MessageReference(123);
        var text = string.Join('\n', Enumerable.Repeat(new string('x', 1500), 3));

        await DiscordReplyChunker.SendAsync(channel.Object, text, reference);

        sent.Should().HaveCount(3);
        sent[0].Reference.Should().BeSameAs(reference);
        sent.Skip(1).Should().OnlyContain(s => s.Reference == null);
    }

    [Fact]
    public async Task SendAsync_Blank_SendsNothing()
    {
        var channel = new Mock<IMessageChannel>(MockBehavior.Strict);

        await DiscordReplyChunker.SendAsync(channel.Object, "  ");

        channel.VerifyNoOtherCalls();
    }
}
