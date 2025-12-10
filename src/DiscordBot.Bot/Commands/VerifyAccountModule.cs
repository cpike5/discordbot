using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Discord slash command module for account verification.
/// </summary>
public class VerifyAccountModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IVerificationService _verificationService;
    private readonly ILogger<VerifyAccountModule> _logger;

    public VerifyAccountModule(
        IVerificationService verificationService,
        ILogger<VerifyAccountModule> logger)
    {
        _verificationService = verificationService;
        _logger = logger;
    }

    /// <summary>
    /// Generates a verification code to link this Discord account to a web account.
    /// </summary>
    [SlashCommand("verify-account", "Generate a verification code to link your Discord account to the admin panel")]
    [RateLimit(3, 3600, Core.Enums.RateLimitTarget.User)] // 3 per hour
    public async Task VerifyAccountAsync()
    {
        _logger.LogInformation(
            "Verify-account command executed by Discord user {Username} (ID: {UserId})",
            Context.User.Username,
            Context.User.Id);

        var result = await _verificationService.GenerateCodeForDiscordUserAsync(Context.User.Id);

        if (!result.Succeeded)
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("Verification Failed")
                .WithDescription(result.ErrorMessage)
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);

            _logger.LogWarning(
                "Verification failed for Discord user {UserId}: {ErrorCode} - {ErrorMessage}",
                Context.User.Id, result.ErrorCode, result.ErrorMessage);
            return;
        }

        var successEmbed = new EmbedBuilder()
            .WithTitle("Verification Code Generated")
            .WithDescription("Use this code to link your Discord account to your web account.")
            .WithColor(Color.Green)
            .AddField("Your Code", $"```\n{result.FormattedCode}\n```", inline: false)
            .AddField("Expires", $"<t:{new DateTimeOffset(result.ExpiresAt!.Value).ToUnixTimeSeconds()}:R>", inline: true)
            .AddField("Instructions", "1. Go to your profile page in the admin panel\n2. Enter this code in the verification form\n3. Click 'Verify' to link your account", inline: false)
            .WithFooter("This code is only visible to you and expires in 15 minutes.")
            .WithCurrentTimestamp()
            .Build();

        await RespondAsync(embed: successEmbed, ephemeral: true);

        _logger.LogInformation(
            "Verification code generated for Discord user {UserId}, expires at {ExpiresAt}",
            Context.User.Id, result.ExpiresAt);
    }
}
