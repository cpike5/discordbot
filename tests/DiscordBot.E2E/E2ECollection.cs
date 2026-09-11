namespace DiscordBot.E2E;

/// <summary>
/// Groups every browser test onto one shared <see cref="BotHostFixture"/> and
/// <see cref="PlaywrightFixture"/> so the bot process and Chromium are each started once (not
/// once per test) and tests never run concurrently against them.
/// </summary>
[CollectionDefinition(Name)]
public sealed class E2ECollection : ICollectionFixture<BotHostFixture>, ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "E2E host collection";
}
