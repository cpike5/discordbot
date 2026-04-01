using System.Collections.Concurrent;
using System.Text.Json;
using Discord;
using Discord.WebSocket;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models.FeatureRequests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services.FeatureRequests;

/// <summary>
/// Manages multi-step DM conversations for the /feature-request command.
/// Maintains active session tracking so the DM handler can intercept replies
/// from users who have an open feature-request flow.
/// Registered as singleton to hold the session dictionary; uses IServiceScopeFactory
/// to resolve scoped services (IFeatureRequestService) on each message.
/// </summary>
public class FeatureRequestConversationService
{
    private readonly IInteractionStateService _stateService;
    private readonly IInputValidationService _validationService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FeatureRequestsOptions _options;
    private readonly ILogger<FeatureRequestConversationService> _logger;

    // Maps userId → correlationId for active sessions. Singleton-safe (ConcurrentDictionary).
    private readonly ConcurrentDictionary<ulong, string> _activeSessions = new();

    public FeatureRequestConversationService(
        IInteractionStateService stateService,
        IInputValidationService validationService,
        IServiceScopeFactory scopeFactory,
        IOptions<FeatureRequestsOptions> options,
        ILogger<FeatureRequestConversationService> logger)
    {
        _stateService = stateService;
        _validationService = validationService;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Starts a new DM-based requirements-gathering conversation.
    /// Called when the user clicks "Tell me more" on the slash command response.
    /// </summary>
    /// <param name="user">The Discord user to DM.</param>
    /// <param name="guildId">The guild the request originated from.</param>
    /// <param name="initialDescription">The sanitized initial description the user provided.</param>
    /// <param name="existingChannel">Optional already-open DM channel (skips CreateDMChannelAsync).</param>
    /// <returns>The correlation ID for the new session.</returns>
    public async Task<string> StartConversationAsync(
        IUser user,
        ulong guildId,
        string initialDescription,
        IMessageChannel? existingChannel = null)
    {
        var state = new FeatureRequestConversationState
        {
            Stage = ConversationStage.AwaitingProblem,
            GuildId = guildId,
            InitialDescription = initialDescription
        };

        var timeout = TimeSpan.FromMinutes(_options.ConversationTimeoutMinutes);
        var correlationId = _stateService.CreateState(user.Id, state, timeout);

        _activeSessions[user.Id] = correlationId;

        _logger.LogInformation(
            "Feature request conversation started for user {UserId} in guild {GuildId}, correlationId {CorrelationId}",
            user.Id, guildId, correlationId);

        var dmChannel = existingChannel ?? await user.CreateDMChannelAsync();

        await dmChannel.SendMessageAsync(
            "**Feature Request — Step 1 of 3**\n\n" +
            "What problem does this feature solve, or what are you trying to do that's currently hard?\n\n" +
            "_Type your answer below, or reply `cancel` to cancel._");

        return correlationId;
    }

    /// <summary>
    /// Returns true if there is an active conversation session for the given user ID.
    /// </summary>
    public bool TryGetSessionCorrelationId(ulong userId, out string? correlationId)
        => _activeSessions.TryGetValue(userId, out correlationId) && correlationId != null;

    /// <summary>
    /// Processes an incoming DM message from a user with an active feature-request session.
    /// Advances the conversation state machine stage by stage.
    /// </summary>
    public async Task HandleAnswerAsync(SocketMessage message)
    {
        var userId = message.Author.Id;

        if (!_activeSessions.TryGetValue(userId, out var correlationId) || correlationId == null)
            return;

        if (!_stateService.TryGetState<FeatureRequestConversationState>(correlationId, out var state) || state == null)
        {
            // Session expired
            _activeSessions.TryRemove(userId, out _);
            await message.Channel.SendMessageAsync(
                "Your feature request session has expired. Please run `/feature-request` again.");
            _logger.LogDebug("Feature request session expired for user {UserId}", userId);
            return;
        }

        var text = message.Content?.Trim() ?? string.Empty;

        // Handle cancel at any stage
        if (text.Equals("cancel", StringComparison.OrdinalIgnoreCase))
        {
            _stateService.TryRemoveState(correlationId);
            _activeSessions.TryRemove(userId, out _);
            await message.Channel.SendMessageAsync("Feature request cancelled.");
            _logger.LogInformation("Feature request cancelled by user {UserId}", userId);
            return;
        }

        // Validate the answer — use the configured min length for consistency, cap answers at 1000 chars
        var answerMinLength = _options.MinDescriptionLength;
        var result = _validationService.Validate(text, answerMinLength, 1000);
        if (!result.IsValid)
        {
            await message.Channel.SendMessageAsync(
                $"Please provide a valid response ({answerMinLength}–1000 characters). Reason: {result.RejectionReason}");
            return;
        }

        // Advance state machine
        switch (state.Stage)
        {
            case ConversationStage.AwaitingProblem:
                state.ProblemStatement = result.SanitizedText;
                state.Stage = ConversationStage.AwaitingSuccessCriteria;
                await message.Channel.SendMessageAsync(
                    "**Feature Request — Step 2 of 3**\n\n" +
                    "How would you know this feature is working well? What would it look like in use?");
                break;

            case ConversationStage.AwaitingSuccessCriteria:
                state.SuccessCriteria = result.SanitizedText;
                state.Stage = ConversationStage.AwaitingPriority;
                await message.Channel.SendMessageAsync(
                    "**Feature Request — Step 3 of 3**\n\n" +
                    "Is this a nice-to-have, or does it block something important for your guild?");
                break;

            case ConversationStage.AwaitingPriority:
                state.Priority = result.SanitizedText;
                state.Stage = ConversationStage.AwaitingConfirmation;

                // Re-store with fresh TTL after advancing to confirmation
                _stateService.TryRemoveState(correlationId);
                var newId = _stateService.CreateState(userId, state,
                    TimeSpan.FromMinutes(_options.ConversationTimeoutMinutes));
                _activeSessions[userId] = newId;

                var summary =
                    $"**Your Feature Request Summary**\n\n" +
                    $"**Description:** {state.InitialDescription}\n\n" +
                    $"**Problem it solves:** {state.ProblemStatement}\n\n" +
                    $"**Success looks like:** {state.SuccessCriteria}\n\n" +
                    $"**Priority:** {state.Priority}\n\n" +
                    "Reply `confirm` to submit, or `cancel` to cancel.";

                await message.Channel.SendMessageAsync(summary);
                break;

            case ConversationStage.AwaitingConfirmation:
                if (text.Equals("confirm", StringComparison.OrdinalIgnoreCase))
                {
                    await SubmitFromConversationAsync(userId, state, message.Channel);
                }
                else
                {
                    await message.Channel.SendMessageAsync(
                        "Reply `confirm` to submit or `cancel` to cancel.");
                }
                break;
        }
    }

    private async Task SubmitFromConversationAsync(
        ulong userId,
        FeatureRequestConversationState state,
        IMessageChannel channel)
    {
        // Clean up session first
        if (_activeSessions.TryGetValue(userId, out var cid))
            _stateService.TryRemoveState(cid);
        _activeSessions.TryRemove(userId, out _);

        var gathered = new GatheredRequirements
        {
            ProblemStatement = state.ProblemStatement ?? string.Empty,
            SuccessCriteria = state.SuccessCriteria ?? string.Empty,
            Priority = state.Priority ?? string.Empty
        };

        var submission = new FeatureRequestSubmission
        {
            GuildId = state.GuildId,
            SubmittedByUserId = userId,
            Description = state.InitialDescription,
            GatheredRequirementsJson = JsonSerializer.Serialize(gathered),
            ConsolidatedSummary =
                $"{state.InitialDescription}\n\n" +
                $"Problem: {gathered.ProblemStatement}\n" +
                $"Success: {gathered.SuccessCriteria}\n" +
                $"Priority: {gathered.Priority}"
        };

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var featureRequestService = scope.ServiceProvider.GetRequiredService<IFeatureRequestService>();
            var request = await featureRequestService.SubmitAsync(submission);
            var shortId = request.Id.ToString("N")[..8].ToUpperInvariant();

            await channel.SendMessageAsync(
                $"Your feature request has been submitted! Reference: **#{shortId}**\n\n" +
                "An admin will review it soon.");

            _logger.LogInformation(
                "Feature request #{ShortId} submitted via conversation by user {UserId}", shortId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit feature request from conversation for user {UserId}", userId);
            await channel.SendMessageAsync(
                "An error occurred while submitting your feature request. Please try again later.");
        }
    }
}
