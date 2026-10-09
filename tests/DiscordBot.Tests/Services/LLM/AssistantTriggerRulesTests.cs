using DiscordBot.Bot.Services.LLM;
using DiscordBot.Core.Enums;
using FluentAssertions;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>Every combination the handler can see, and what each one means.</summary>
public class AssistantTriggerRulesTests
{
    private static AssistantTrigger Classify(
        bool mentionsBot = false,
        bool isThread = false,
        bool ownedByBot = false,
        bool knownThread = false,
        AssistantConversationMode mode = AssistantConversationMode.SingleReply,
        bool authorIsBot = false,
        bool isGuild = true)
        => AssistantTriggerRules.Classify(authorIsBot, isGuild, mentionsBot, isThread, ownedByBot, knownThread, mode);

    [Fact]
    public void ABot_IsIgnored_WhateverElseIsTrue()
    {
        Classify(authorIsBot: true, mentionsBot: true, isThread: true, ownedByBot: true, knownThread: true, mode: AssistantConversationMode.Thread)
            .Should().Be(AssistantTrigger.Ignore);
    }

    [Fact]
    public void ADm_IsIgnored()
    {
        Classify(isGuild: false, mentionsBot: true).Should().Be(AssistantTrigger.Ignore);
    }

    [Fact]
    public void NoMention_OutsideAThread_IsIgnored()
    {
        Classify(mode: AssistantConversationMode.Thread).Should().Be(AssistantTrigger.Ignore);
    }

    [Theory]
    [InlineData(AssistantConversationMode.SingleReply)]
    [InlineData(AssistantConversationMode.Thread)]
    public void AMessageInAThreadTheAssistantHolds_IsATurn_NoMentionNeeded(AssistantConversationMode mode)
    {
        // Even if the guild has since switched back to single replies, a conversation already open continues
        Classify(isThread: true, ownedByBot: true, knownThread: true, mode: mode).Should().Be(AssistantTrigger.ThreadTurn);
        Classify(isThread: true, ownedByBot: true, knownThread: true, mode: mode, mentionsBot: true).Should().Be(AssistantTrigger.ThreadTurn);
    }

    [Fact]
    public void AThreadTheBotOwns_WithoutARow_IsNotAConversation()
    {
        // Some other thread the bot made; a mention there is a single reply, no mention is nothing
        Classify(isThread: true, ownedByBot: true, knownThread: false).Should().Be(AssistantTrigger.Ignore);
        Classify(isThread: true, ownedByBot: true, knownThread: false, mentionsBot: true).Should().Be(AssistantTrigger.NewQuestion);
    }

    [Fact]
    public void ARowForAThreadTheBotDoesNotOwn_IsNotTrusted()
    {
        Classify(isThread: true, ownedByBot: false, knownThread: true).Should().Be(AssistantTrigger.Ignore);
    }

    [Fact]
    public void AMentionInAChannel_SingleReplyMode_IsANewQuestion()
    {
        Classify(mentionsBot: true).Should().Be(AssistantTrigger.NewQuestion);
    }

    [Fact]
    public void AMentionInAChannel_ThreadMode_OpensAThread()
    {
        Classify(mentionsBot: true, mode: AssistantConversationMode.Thread).Should().Be(AssistantTrigger.NewThreadQuestion);
    }

    [Fact]
    public void AMentionInSomeoneElsesThread_IsASingleReply_EvenInThreadMode()
    {
        // Discord cannot nest threads
        Classify(mentionsBot: true, isThread: true, mode: AssistantConversationMode.Thread).Should().Be(AssistantTrigger.NewQuestion);
    }
}
