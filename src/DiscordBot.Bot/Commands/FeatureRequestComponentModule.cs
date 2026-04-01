using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Services.FeatureRequests;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models.FeatureRequests;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Component interaction handlers for /feature-request button interactions.
/// Handles the "Tell me more" DM flow trigger and the "Submit directly" shortcut.
/// </summary>
public class FeatureRequestComponentModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IInteractionStateService _stateService;
    private readonly FeatureRequestConversationService _conversationService;
    private readonly IFeatureRequestService _featureRequestService;
    private readonly ILogger<FeatureRequestComponentModule> _logger;

    public FeatureRequestComponentModule(
        IInteractionStateService stateService,
        FeatureRequestConversationService conversationService,
        IFeatureRequestService featureRequestService,
        ILogger<FeatureRequestComponentModule> logger)
    {
        _stateService = stateService;
        _conversationService = conversationService;
        _featureRequestService = featureRequestService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the "Tell me more" button. Opens a DM and starts the multi-step conversation.
    /// Button ID format: fr:tellmore:{correlationId}
    /// </summary>
    [ComponentInteraction("fr:tellmore:*")]
    public async Task HandleTellMoreAsync(string correlationId)
    {
        if (!_stateService.TryGetState<FeatureRequestConversationState>(correlationId, out var state) || state == null)
        {
            await RespondAsync("This interaction has expired. Please run `/feature-request` again.", ephemeral: true);
            return;
        }

        _stateService.TryRemoveState(correlationId);

        try
        {
            await _conversationService.StartConversationAsync(Context.User, state.GuildId, state.InitialDescription);

            await RespondAsync(embed: new EmbedBuilder()
                .WithTitle("Check Your DMs!")
                .WithDescription("I've sent you a DM to gather a few more details about your feature request.")
                .WithColor(Color.Green)
                .Build(), ephemeral: true);

            _logger.LogInformation(
                "Feature request conversation started via DM for user {UserId} in guild {GuildId}",
                Context.User.Id, state.GuildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to start feature request DM conversation for user {UserId}", Context.User.Id);
            await RespondAsync(
                "I couldn't send you a DM. Please check your privacy settings and try again.",
                ephemeral: true);
        }
    }

    /// <summary>
    /// Handles the "Submit directly" button. Creates the feature request without conversation.
    /// Button ID format: fr:submitdirect:{correlationId}
    /// </summary>
    [ComponentInteraction("fr:submitdirect:*")]
    public async Task HandleSubmitDirectAsync(string correlationId)
    {
        if (!_stateService.TryGetState<FeatureRequestConversationState>(correlationId, out var state) || state == null)
        {
            await RespondAsync("This interaction has expired. Please run `/feature-request` again.", ephemeral: true);
            return;
        }

        _stateService.TryRemoveState(correlationId);

        try
        {
            var submission = new FeatureRequestSubmission
            {
                GuildId = state.GuildId,
                SubmittedByUserId = Context.User.Id,
                Description = state.InitialDescription
            };

            var request = await _featureRequestService.SubmitAsync(submission);
            var shortId = request.Id.ToString("N")[..8].ToUpperInvariant();

            await RespondAsync(embed: new EmbedBuilder()
                .WithTitle("Feature Request Submitted!")
                .WithDescription(
                    $"Your request has been submitted. Reference: **#{shortId}**\n\n" +
                    "An admin will review it soon.")
                .WithColor(Color.Green)
                .Build(), ephemeral: true);

            _logger.LogInformation(
                "Feature request #{ShortId} submitted directly by user {UserId}", shortId, Context.User.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to submit feature request directly for user {UserId}", Context.User.Id);
            await RespondAsync(
                "An error occurred while submitting your feature request. Please try again later.",
                ephemeral: true);
        }
    }
}
