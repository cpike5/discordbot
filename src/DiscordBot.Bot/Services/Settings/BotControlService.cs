using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using System.Text.Json;

namespace DiscordBot.Bot.Services.Settings;

/// <summary>
/// Default implementation of <see cref="IBotControlService"/>. Wraps <see cref="IBotService"/>
/// and performs the audit logging for bot restart/shutdown operations.
/// </summary>
public class BotControlService : IBotControlService
{
    private readonly IBotService _botService;
    private readonly IAuditLogQueue _auditLogQueue;
    private readonly ILogger<BotControlService> _logger;

    public BotControlService(
        IBotService botService,
        IAuditLogQueue auditLogQueue,
        ILogger<BotControlService> logger)
    {
        _botService = botService;
        _auditLogQueue = auditLogQueue;
        _logger = logger;
    }

    public BotControlViewModel LoadViewModel()
    {
        var status = _botService.GetStatus();
        var config = _botService.GetConfiguration();

        var viewModel = new BotControlViewModel
        {
            Status = BotStatusViewModel.FromDto(status),
            Configuration = config,
            CanRestart = true,
            CanShutdown = true
        };

        _logger.LogDebug("Bot Control ViewModel loaded: ConnectionState={ConnectionState}, GuildCount={GuildCount}",
            viewModel.Status.ConnectionState, viewModel.Status.GuildCount);

        return viewModel;
    }

    public async Task<SettingsSectionResult> RestartAsync(string userId)
    {
        try
        {
            await _botService.RestartAsync();
            _logger.LogInformation("Bot restart completed successfully, initiated by {UserId}", userId);

            _auditLogQueue.Enqueue(new AuditLogCreateDto
            {
                Category = AuditLogCategory.System,
                Action = AuditLogAction.Updated,
                ActorType = AuditLogActorType.User,
                ActorId = userId,
                Details = JsonSerializer.Serialize(new
                {
                    Operation = "BotRestart",
                    Description = "Bot restart initiated by administrator",
                    Timestamp = DateTime.UtcNow
                })
            });

            return new SettingsSectionResult { Success = true, Message = "Bot is restarting..." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart bot, requested by {UserId}", userId);
            return new SettingsSectionResult
            {
                Success = false,
                Message = "Failed to restart bot. Please check logs for details.",
                StatusCode = 500
            };
        }
    }

    public async Task<SettingsSectionResult> ShutdownAsync(string userId)
    {
        try
        {
            await _botService.ShutdownAsync();
            _logger.LogCritical("Bot shutdown initiated by {UserId}", userId);

            _auditLogQueue.Enqueue(new AuditLogCreateDto
            {
                Category = AuditLogCategory.System,
                Action = AuditLogAction.BotStopped,
                ActorType = AuditLogActorType.User,
                ActorId = userId,
                Details = JsonSerializer.Serialize(new
                {
                    Operation = "ManualShutdown",
                    Reason = "Administrator initiated shutdown",
                    Timestamp = DateTime.UtcNow
                })
            });

            return new SettingsSectionResult { Success = true, Message = "Bot is shutting down..." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to shutdown bot, requested by {UserId}", userId);
            return new SettingsSectionResult
            {
                Success = false,
                Message = "Failed to shutdown bot. Please check logs for details.",
                StatusCode = 500
            };
        }
    }
}
