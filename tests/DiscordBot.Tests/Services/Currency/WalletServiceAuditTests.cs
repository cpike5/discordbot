using DiscordBot.Core.Enums;
using FluentAssertions;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Fines and adjustments are the two balance operations that carry an actor and a reason, so they
/// are the two the audit log records. Spends and transfers are the ledger's own record.
/// </summary>
public class WalletServiceAuditTests
{
    private const ulong Guild = 1001UL;
    private const ulong Offender = 111UL;
    private const ulong Moderator = 999UL;

    [Fact]
    public async Task FineAsync_IsAuditedAgainstTheGuild()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync(guildId: Guild);
        await context.SeedBalanceAsync(currency.Id, Offender, 50);

        var result = await context.WalletService.FineAsync(currency.Id, Offender, 20, "spam", Moderator, null);

        result.Success.Should().BeTrue();
        context.AuditLogBuilder.Verify(b => b.ForCategory(AuditLogCategory.User), Times.Once);
        context.AuditLogBuilder.Verify(b => b.WithAction(AuditLogAction.CurrencyFined), Times.Once);
        context.AuditLogBuilder.Verify(b => b.ByUser(Moderator.ToString()), Times.Once);
        context.AuditLogBuilder.Verify(b => b.InGuild(Guild), Times.Once);
        context.AuditLogBuilder.Verify(b => b.LogAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FineAsync_WhenRefused_IsNotAudited()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync(scope: CurrencyScope.Global, guildId: null);

        var result = await context.WalletService.FineAsync(currency.Id, Offender, 20, "spam", Moderator, null);

        result.Success.Should().BeFalse();
        context.AuditLogBuilder.Verify(b => b.LogAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdjustAsync_IsAudited()
    {
        using var context = new CurrencyTestContext();
        var currency = await context.SeedCurrencyAsync(guildId: Guild);
        await context.SeedBalanceAsync(currency.Id, Offender, 50);
        var fine = await context.WalletService.FineAsync(currency.Id, Offender, 20, "spam", Moderator, null);
        context.AuditLogBuilder.Invocations.Clear();

        var result = await context.WalletService.AdjustAsync(fine.Transaction!.Id, 20, "fined the wrong person", Moderator);

        result.Success.Should().BeTrue();
        context.AuditLogBuilder.Verify(b => b.WithAction(AuditLogAction.CurrencyAdjusted), Times.Once);
        context.AuditLogBuilder.Verify(
            b => b.OnTarget("LedgerTransaction", fine.Transaction!.Id.ToString()), Times.Once);
        context.AuditLogBuilder.Verify(b => b.LogAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdjustAsync_WithNoReferenceRow_IsNotAudited()
    {
        using var context = new CurrencyTestContext();

        var result = await context.WalletService.AdjustAsync(4242L, 20, "typo", Moderator);

        result.Success.Should().BeFalse();
        context.AuditLogBuilder.Verify(b => b.LogAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
