using Discord;
using DiscordBot.Bot.Handlers;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.RatWatch;
using FluentAssertions;
using Moq;

namespace DiscordBot.Tests.Bot.Helpers;

/// <summary>
/// Decision D8: bot messages built from admin templates or user content carry a restricted
/// <see cref="AllowedMentions"/>, so <c>@everyone</c>, <c>@here</c> and role mentions in the text do not ping.
/// The services get their channels from Discord.Net socket types that Moq cannot build, so these tests
/// drive each service's <c>internal static</c> send method with a mocked <see cref="IMessageChannel"/>.
/// </summary>
public class SafeMentionsTests
{
    private const string HostileText = "@everyone @here <@&123456789012345678> hello <@234567890123456789>";

    [Fact]
    public void UsersOnly_AllowsOnlyUserMentions()
    {
        var mentions = SafeMentions.UsersOnly;

        mentions.AllowedTypes.Should().Be(AllowedMentionTypes.Users);
        mentions.RoleIds.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ReplyOnly_AllowsNothingButTheRepliedUser()
    {
        var mentions = SafeMentions.ReplyOnly;

        mentions.AllowedTypes.Should().Be(AllowedMentionTypes.None);
        mentions.MentionRepliedUser.Should().BeTrue();
        mentions.UserIds.Should().BeNullOrEmpty();
        mentions.RoleIds.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Properties_ReturnFreshInstances()
    {
        SafeMentions.UsersOnly.Should().NotBeSameAs(SafeMentions.UsersOnly);
        SafeMentions.ReplyOnly.Should().NotBeSameAs(SafeMentions.ReplyOnly);
    }

    [Fact]
    public async Task ScheduledMessage_SendContent_RestrictsMentionsToUsers()
    {
        var channel = CreateChannel();

        await ScheduledMessageService.SendContentAsync(channel.Object, HostileText);

        VerifySent(channel, HostileText, IsUsersOnly);
    }

    [Fact]
    public async Task Welcome_SendPlainText_RestrictsMentionsToUsers()
    {
        var channel = CreateChannel();

        await WelcomeService.SendPlainTextAsync(channel.Object, HostileText);

        VerifySent(channel, HostileText, IsUsersOnly);
    }

    [Fact]
    public async Task RatWatch_SendVotingMessage_RestrictsMentionsToUsers()
    {
        var channel = CreateChannel();
        var components = new ComponentBuilder().WithButton("Rat", "ratwatch:vote").Build();

        await RatWatchExecutionService.SendVotingMessageAsync(channel.Object, HostileText, components);

        VerifySent(channel, HostileText, IsUsersOnly);
        channel.Verify(c => c.SendMessageAsync(
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageReference>(), components,
            It.IsAny<ISticker[]>(), It.IsAny<Embed[]>(), It.IsAny<MessageFlags>(), It.IsAny<PollProperties>()),
            Times.Once);
    }

    [Fact]
    public async Task Assistant_SendReply_PingsNothingButStillRepliesToTheAsker()
    {
        const ulong askerMessageId = 345678901234567890UL;
        var channel = CreateChannel();

        await AssistantMessageHandler.SendReplyAsync(channel.Object, HostileText, askerMessageId);

        VerifySent(channel, HostileText, a =>
            a != null
            && a.AllowedTypes == AllowedMentionTypes.None
            && a.MentionRepliedUser == true);
        channel.Verify(c => c.SendMessageAsync(
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(),
            It.IsAny<AllowedMentions>(),
            It.Is<MessageReference>(r => r != null && r.MessageId.IsSpecified && r.MessageId.Value == askerMessageId),
            It.IsAny<MessageComponent>(), It.IsAny<ISticker[]>(), It.IsAny<Embed[]>(), It.IsAny<MessageFlags>(),
            It.IsAny<PollProperties>()),
            Times.Once);
    }

    private static bool IsUsersOnly(AllowedMentions? mentions)
        => mentions != null && mentions.AllowedTypes == AllowedMentionTypes.Users;

    private static Mock<IMessageChannel> CreateChannel()
    {
        var channel = new Mock<IMessageChannel>();
        channel.Setup(c => c.SendMessageAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(),
                It.IsAny<AllowedMentions>(), It.IsAny<MessageReference>(), It.IsAny<MessageComponent>(),
                It.IsAny<ISticker[]>(), It.IsAny<Embed[]>(), It.IsAny<MessageFlags>(), It.IsAny<PollProperties>()))
            .ReturnsAsync(Mock.Of<IUserMessage>());
        return channel;
    }

    private static void VerifySent(
        Mock<IMessageChannel> channel, string text, Func<AllowedMentions?, bool> mentionsMatch)
    {
        channel.Verify(c => c.SendMessageAsync(
            text, It.IsAny<bool>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(),
            It.Is<AllowedMentions>(a => mentionsMatch(a)), It.IsAny<MessageReference>(), It.IsAny<MessageComponent>(),
            It.IsAny<ISticker[]>(), It.IsAny<Embed[]>(), It.IsAny<MessageFlags>(), It.IsAny<PollProperties>()),
            Times.Once);
    }
}
