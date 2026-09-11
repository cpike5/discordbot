using Discord.WebSocket;
using DiscordBot.Bot.Controllers;
using DiscordBot.Bot.Pages.Admin.Currency;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using GuildCurrencyIndexModel = DiscordBot.Bot.Pages.Guilds.Currency.IndexModel;
using GuildPricesModel = DiscordBot.Bot.Pages.Guilds.Currency.PricesModel;

namespace DiscordBot.Tests.Controllers.Currency;

/// <summary>
/// With <c>Currency:Enabled</c> false, <c>AddCurrency</c> is never called and none of the currency
/// services exist. Every currency controller and page model has to still be constructible in that
/// container — they take those services as optional arguments — because a controller that cannot be
/// activated is a 500, not the 404 the rollback path promises.
/// </summary>
[Trait("Category", "Unit")]
public class CurrencyFeatureDisabledTests
{
    /// <summary>A container with everything except the currency services.</summary>
    private static ServiceProvider BuildContainerWithoutCurrency()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(Mock.Of<IAuditLogService>());
        services.AddSingleton(Mock.Of<IDiscordUserResolver>());
        services.AddSingleton(Mock.Of<IModerationService>());
        services.AddSingleton(Mock.Of<IGuildService>());
        services.AddSingleton(Mock.Of<ISoundService>());
        services.AddSingleton(Mock.Of<ISettingsService>());
        services.AddSingleton(new DiscordSocketClient());

        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(typeof(CurrenciesController))]
    [InlineData(typeof(WalletsController))]
    [InlineData(typeof(PricesController))]
    [InlineData(typeof(GuildCurrencyIndexModel))]
    [InlineData(typeof(GuildPricesModel))]
    [InlineData(typeof(DiscordBot.Bot.Pages.Guilds.Currency.DetailsModel))]
    [InlineData(typeof(IndexModel))]
    public void EveryCurrencySurface_StillActivates_WhenTheFeatureIsDisabled(Type type)
    {
        using var provider = BuildContainerWithoutCurrency();

        // ActivatorUtilities is what MVC and Razor Pages use to build controllers and page models,
        // and it falls back to a parameter's default value when the container has nothing to give.
        var act = () => ActivatorUtilities.CreateInstance(provider, type);

        act.Should().NotThrow(
            "with the currency services absent the surface must construct and answer 404, not fail to activate");
    }
}
