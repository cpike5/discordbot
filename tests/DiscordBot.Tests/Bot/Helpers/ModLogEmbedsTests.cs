using Discord;
using DiscordBot.Bot.Helpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Helpers;

/// <summary>
/// Unit tests for <see cref="ModLogEmbeds"/>: the embed the mod-log feed posts for a case.
/// </summary>
public class ModLogEmbedsTests
{
    private const ulong BotId = 999UL;

    private static ModerationCaseDto Case(CaseType type = CaseType.Warn) => new()
    {
        Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        CaseNumber = 42,
        GuildId = 100,
        TargetUserId = 300,
        ModeratorUserId = 200,
        Type = type,
        Reason = "Spamming the lobby",
        CreatedAt = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc)
    };

    private static EmbedField Field(Embed embed, string name) =>
        embed.Fields.Should().Contain(f => f.Name == name).Subject;

    [Theory]
    [InlineData(CaseType.Warn)]
    [InlineData(CaseType.Mute)]
    [InlineData(CaseType.Kick)]
    [InlineData(CaseType.Ban)]
    [InlineData(CaseType.Unban)]
    [InlineData(CaseType.Note)]
    public void ForCase_TitlesWithNumberAndType_AndColoursByType(CaseType type)
    {
        var embed = ModLogEmbeds.ForCase(Case(type), BotId);

        embed.Title.Should().StartWith("Case #42");
        embed.Color.Should().Be(ModLogEmbeds.ColorFor(type));
    }

    [Fact]
    public void ForCase_ShowsUserModeratorAndReason()
    {
        var embed = ModLogEmbeds.ForCase(Case(), BotId);

        Field(embed, "User").Value.Should().Be("<@300> (300)");
        Field(embed, "Moderator").Value.Should().Be("<@200>");
        Field(embed, "Reason").Value.Should().Be("Spamming the lobby");
        embed.Footer!.Value.Text.Should().Contain("11111111-2222-3333-4444-555555555555");
        embed.Timestamp.Should().Be(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ForCase_WithoutReason_SaysSo()
    {
        var dto = Case();
        dto.Reason = "   ";

        var embed = ModLogEmbeds.ForCase(dto, BotId);

        Field(embed, "Reason").Value.Should().Be("No reason given");
    }

    [Fact]
    public void ForCase_LongReason_IsCutToFitTheField()
    {
        var dto = Case();
        dto.Reason = new string('x', 2000);

        var embed = ModLogEmbeds.ForCase(dto, BotId);

        Field(embed, "Reason").Value.Length.Should().BeLessThanOrEqualTo(1024);
    }

    [Fact]
    public void ForCase_CreatedByTheBot_NamesAutoModeration()
    {
        var dto = Case();
        dto.ModeratorUserId = BotId;

        var embed = ModLogEmbeds.ForCase(dto, BotId);

        Field(embed, "Moderator").Value.Should().Be(ModLogEmbeds.AutoModerationLabel);
    }

    [Fact]
    public void ForCase_PermanentAction_HasNoDurationOrExpiry()
    {
        var embed = ModLogEmbeds.ForCase(Case(CaseType.Ban), BotId);

        embed.Fields.Should().NotContain(f => f.Name == "Duration");
        embed.Fields.Should().NotContain(f => f.Name == "Expires");
    }

    [Fact]
    public void ForCase_TemporaryAction_ShowsDurationAndRelativeExpiry()
    {
        var dto = Case(CaseType.Mute);
        dto.Duration = TimeSpan.FromHours(2);
        dto.ExpiresAt = dto.CreatedAt.AddHours(2);

        var embed = ModLogEmbeds.ForCase(dto, BotId);

        Field(embed, "Duration").Value.Should().Be(DisplayFormat.Duration(TimeSpan.FromHours(2)));
        var expires = new DateTimeOffset(dto.ExpiresAt.Value, TimeSpan.Zero).ToUnixTimeSeconds();
        Field(embed, "Expires").Value.Should().Be($"<t:{expires}:R>");
    }

    [Fact]
    public void ForCase_WithContextMessage_LinksToItAndQuotesIt()
    {
        var dto = Case();
        dto.ContextChannelId = 55;
        dto.ContextMessageId = 66;
        dto.ContextMessageContent = "first line\nsecond line";

        var embed = ModLogEmbeds.ForCase(dto, BotId);

        var context = Field(embed, "Context").Value;
        context.Should().Contain("https://discord.com/channels/100/55/66");
        context.Should().Contain("> first line\n> second line");
    }

    [Fact]
    public void ForCase_WithoutContextMessage_HasNoContextField()
    {
        var embed = ModLogEmbeds.ForCase(Case(), BotId);

        embed.Fields.Should().NotContain(f => f.Name == "Context");
    }

    [Fact]
    public void PortalCaseUrl_PointsAtTheMembersModerationPage_WithoutADoubleSlash()
    {
        ModLogEmbeds.PortalCaseUrl(Case(), "https://bot.example/")
            .Should().Be("https://bot.example/Guilds/100/Members/300/Moderation");
    }

    [Fact]
    public void CaseComponents_IsOneLinkButtonToThePortal()
    {
        var components = ModLogEmbeds.CaseComponents(Case(), "https://bot.example");

        var row = components.Components.Single().Should().BeOfType<ActionRowComponent>().Subject;
        var button = row.Components.Single().Should().BeOfType<ButtonComponent>().Subject;
        button.Style.Should().Be(ButtonStyle.Link);
        button.Url.Should().Be("https://bot.example/Guilds/100/Members/300/Moderation");
    }
}
