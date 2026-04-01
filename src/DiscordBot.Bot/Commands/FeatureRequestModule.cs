using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models.FeatureRequests;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Slash command for submitting feature requests from within a Discord guild.
/// Validates the initial description, then presents buttons to either start a
/// multi-step DM conversation or submit directly.
/// </summary>
[RequireGuildActive]
[RateLimit(3, 3600)]
public class FeatureRequestModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IInputValidationService _validationService;
    private readonly IInteractionStateService _stateService;
    private readonly IFeatureRequestRepository _featureRequestRepository;
    private readonly FeatureRequestsOptions _options;
    private readonly ILogger<FeatureRequestModule> _logger;

    public FeatureRequestModule(
        IInputValidationService validationService,
        IInteractionStateService stateService,
        IFeatureRequestRepository featureRequestRepository,
        IOptions<FeatureRequestsOptions> options,
        ILogger<FeatureRequestModule> logger)
    {
        _validationService = validationService;
        _stateService = stateService;
        _featureRequestRepository = featureRequestRepository;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Accepts an initial feature request description and presents options for submitting
    /// directly or gathering more context via DM conversation.
    /// </summary>
    [SlashCommand("feature-request", "Submit a feature request or idea for the bot")]
    public async Task FeatureRequestAsync(
        [Summary("description", "Describe the feature you'd like (20–500 characters)")]
        string description)
    {
        var result = _validationService.Validate(description, _options.MinDescriptionLength, _options.MaxDescriptionLength);

        if (!result.IsValid)
        {
            if (result.RejectionReason?.StartsWith("PromptInjection", StringComparison.Ordinal) == true)
            {
                _logger.LogWarning(
                    "Injection attempt from {UserId} in guild {GuildId}: {Pattern}",
                    Context.User.Id, Context.Guild.Id, result.RejectionReason);

                await _featureRequestRepository.AddRejectionAsync(new FeatureRequestRejection
                {
                    GuildId = Context.Guild.Id,
                    UserId = Context.User.Id,
                    RejectionReason = result.RejectionReason,
                    CreatedAt = DateTime.UtcNow
                });

                await RespondAsync(embed: EmbedHelper.Error("Invalid Request",
                    "Your request contains content that cannot be processed."), ephemeral: true);
                return;
            }

            await RespondAsync(embed: EmbedHelper.Error("Invalid Description",
                $"Description must be between {_options.MinDescriptionLength} and {_options.MaxDescriptionLength} characters."),
                ephemeral: true);
            return;
        }

        // Store validated description in state; embed correlationId in button custom IDs
        var state = new FeatureRequestConversationState
        {
            Stage = ConversationStage.AwaitingProblem,
            GuildId = Context.Guild.Id,
            InitialDescription = result.SanitizedText
        };
        var correlationId = _stateService.CreateState(Context.User.Id, state,
            TimeSpan.FromMinutes(_options.ConversationTimeoutMinutes));

        var isDetailed = result.SanitizedText.Length >= _options.DirectSubmitThreshold;

        var components = new ComponentBuilder()
            .WithButton("Tell me more", $"fr:tellmore:{correlationId}",
                ButtonStyle.Primary, row: 0)
            .WithButton("Submit directly", $"fr:submitdirect:{correlationId}",
                isDetailed ? ButtonStyle.Success : ButtonStyle.Secondary, row: 0)
            .Build();

        var embed = new EmbedBuilder()
            .WithTitle("Feature Request Received")
            .WithDescription(isDetailed
                ? "Your description is detailed enough to submit directly, or I can ask a few questions to gather more information."
                : "I have a few quick questions to help make your request more actionable.")
            .AddField("Description", result.SanitizedText)
            .WithColor(Color.Blue)
            .Build();

        await RespondAsync(embed: embed, components: components, ephemeral: true);

        _logger.LogInformation(
            "Feature request initiated by {UserId} in guild {GuildId}, correlationId {CorrelationId}",
            Context.User.Id, Context.Guild.Id, correlationId);
    }
}
