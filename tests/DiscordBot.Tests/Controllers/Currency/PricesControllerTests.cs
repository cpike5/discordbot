using DiscordBot.Bot.Controllers;
using DiscordBot.Core.Constants;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using static DiscordBot.Tests.Controllers.Currency.CurrencyControllerTestContext;

namespace DiscordBot.Tests.Controllers.Currency;

/// <summary>
/// Tests for <see cref="PricesController"/>. The feature key is the contract with the charge seam,
/// so most of what matters here is that the key and the guild reach the service exactly as the
/// route carried them.
/// </summary>
[Trait("Category", "Unit")]
public class PricesControllerTests
{
    private static readonly Guid SoundId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    private static readonly string SoundKey = CurrencyFeatureKeys.Soundboard(SoundId);

    private readonly Mock<ICurrencyService> _currencyService = new();
    private readonly Mock<IAuditLogService> _auditLog = AuditLog();

    private PricesController Build() =>
        new PricesController(_auditLog.Object, Mock.Of<ILogger<PricesController>>(), _currencyService.Object)
            .WithUser(AdminUser());

    [Fact]
    public async Task SetPrice_SendsTheRouteKeyAndGuildThroughUnchanged()
    {
        var controller = Build();
        PriceEntrySaveDto? captured = null;

        _currencyService
            .Setup(s => s.SetPriceAsync(It.IsAny<PriceEntrySaveDto>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .Callback<PriceEntrySaveDto, ulong, CancellationToken>((dto, _, _) => captured = dto)
            .ReturnsAsync(new PriceEntryResult { Success = true, Price = new PriceEntryDto { FeatureKey = SoundKey } });

        var currencyId = Guid.NewGuid();

        await controller.SetPrice(GuildId, SoundKey, new PriceSaveRequestDto
        {
            CurrencyId = currencyId,
            Amount = 5,
            ExemptRoleIds = new List<ulong> { 777888999000111222UL }
        });

        captured.Should().NotBeNull();
        captured!.FeatureKey.Should().Be(SoundKey,
            "the charge seam looks a sound's price up by exactly this string");
        captured.GuildId.Should().Be(GuildId);
        captured.CurrencyId.Should().Be(currencyId);
        captured.Amount.Should().Be(5);
        captured.ExemptRoleIds.Should().ContainSingle().Which.Should().Be(777888999000111222UL,
            "a role snowflake has to survive the round trip through the browser intact");
    }

    [Fact]
    public async Task SetPrice_RecordsTheActorWhoSavedIt()
    {
        var controller = Build();
        ulong capturedActor = 0;

        _currencyService
            .Setup(s => s.SetPriceAsync(It.IsAny<PriceEntrySaveDto>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .Callback<PriceEntrySaveDto, ulong, CancellationToken>((_, actor, _) => capturedActor = actor)
            .ReturnsAsync(new PriceEntryResult { Success = true, Price = new PriceEntryDto { FeatureKey = SoundKey } });

        await controller.SetPrice(GuildId, SoundKey, new PriceSaveRequestDto { CurrencyId = Guid.NewGuid(), Amount = 5 });

        capturedActor.Should().Be(ActorId);
        _auditLog.Verify(a => a.CreateBuilder(), Times.Once, "a price change is a configuration change");
    }

    [Fact]
    public async Task SetPrice_ReturnsTheReason_WhenAGuildCurrencyPricesAnotherGuild()
    {
        var controller = Build();

        _currencyService
            .Setup(s => s.SetPriceAsync(It.IsAny<PriceEntrySaveDto>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PriceEntryResult.Failed(CurrencyErrors.PriceScopeMismatch));

        var result = await controller.SetPrice(GuildId, SoundKey, new PriceSaveRequestDto
        {
            CurrencyId = Guid.NewGuid(),
            Amount = 5
        });

        var error = (result.Result as ObjectResult)?.Value as ApiErrorDto;
        error!.StatusCode.Should().Be(422);
        error.ErrorCode.Should().Be(CurrencyErrors.PriceScopeMismatch);
    }

    [Fact]
    public async Task SetPrice_ReturnsUnprocessable_ForAnAmountOfZero()
    {
        var controller = Build();

        _currencyService
            .Setup(s => s.SetPriceAsync(It.IsAny<PriceEntrySaveDto>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PriceEntryResult.Failed(CurrencyErrors.InvalidAmount));

        var result = await controller.SetPrice(GuildId, SoundKey, new PriceSaveRequestDto
        {
            CurrencyId = Guid.NewGuid(),
            Amount = 0
        });

        StatusOf(result.Result).Should().Be(422);
    }

    [Fact]
    public async Task RemovePrice_ReturnsNoContent_WhenTheFeatureWasPriced()
    {
        var controller = Build();

        _currencyService
            .Setup(s => s.RemovePriceAsync(SoundKey, GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await controller.RemovePrice(GuildId, SoundKey);

        result.Should().BeOfType<NoContentResult>();
        _auditLog.Verify(a => a.CreateBuilder(), Times.Once);
    }

    [Fact]
    public async Task RemovePrice_ReturnsNotFound_WhenItWasAlreadyFree()
    {
        var controller = Build();

        _currencyService
            .Setup(s => s.RemovePriceAsync(It.IsAny<string>(), It.IsAny<ulong?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await controller.RemovePrice(GuildId, SoundKey);

        StatusOf(result).Should().Be(404);
        _auditLog.Verify(a => a.CreateBuilder(), Times.Never, "nothing changed, so nothing is audited");
    }

    [Fact]
    public async Task GetPrice_ReturnsNotFound_WhenTheFeatureIsFreeHere()
    {
        var controller = Build();

        _currencyService
            .Setup(s => s.GetActivePriceAsync(SoundKey, GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PriceEntryDto?)null);

        var result = await controller.GetPrice(GuildId, SoundKey);

        StatusOf(result.Result).Should().Be(404);
    }

    [Fact]
    public async Task GetPrices_ReturnsNotFound_WhenTheFeatureIsDisabled()
    {
        var controller = new PricesController(_auditLog.Object, Mock.Of<ILogger<PricesController>>())
            .WithUser(AdminUser());

        var result = await controller.GetPrices(GuildId);

        StatusOf(result.Result).Should().Be(404);
    }
}
