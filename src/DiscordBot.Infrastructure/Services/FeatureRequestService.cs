using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models.FeatureRequests;
using DiscordBot.Infrastructure.Services.FeatureRequests;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services;

/// <summary>
/// Business-logic implementation for feature request lifecycle management.
/// </summary>
public class FeatureRequestService : IFeatureRequestService
{
    private readonly IFeatureRequestRepository _repo;
    private readonly IFeatureRequestDocGenQueue _queue;
    private readonly ILogger<FeatureRequestService> _logger;

    public FeatureRequestService(
        IFeatureRequestRepository repo,
        IFeatureRequestDocGenQueue queue,
        ILogger<FeatureRequestService> logger)
    {
        _repo = repo;
        _queue = queue;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<FeatureRequest> SubmitAsync(FeatureRequestSubmission submission)
    {
        // Derive a short title from the consolidated summary or raw description
        var source = !string.IsNullOrWhiteSpace(submission.ConsolidatedSummary)
            ? submission.ConsolidatedSummary
            : submission.Description;

        var title = source.Length <= 100 ? source : source[..100];

        var entity = new FeatureRequest
        {
            Id = Guid.NewGuid(),
            GuildId = submission.GuildId,
            SubmittedByUserId = submission.SubmittedByUserId,
            Title = title,
            Description = submission.Description,
            GatheredRequirements = submission.GatheredRequirementsJson,
            ConsolidatedSummary = submission.ConsolidatedSummary,
            Status = FeatureRequestStatus.Submitted
        };

        await _repo.AddAsync(entity);

        _queue.Enqueue(entity.Id);

        _logger.LogInformation(
            "Feature request {FeatureRequestId} submitted by user {UserId} in guild {GuildId}. Enqueued for doc generation.",
            entity.Id, submission.SubmittedByUserId, submission.GuildId);

        return entity;
    }

    /// <inheritdoc/>
    public Task<FeatureRequest?> GetByIdAsync(Guid id) => _repo.GetByIdAsync(id);

    /// <inheritdoc/>
    public Task<(IEnumerable<FeatureRequest> Items, int Total)> GetByGuildIdAsync(
        ulong guildId, FeatureRequestStatus? statusFilter, int page, int pageSize)
        => _repo.GetByGuildIdAsync(guildId, statusFilter, page, pageSize);

    /// <inheritdoc/>
    public async Task UpdateStatusAsync(Guid id, FeatureRequestStatus status, ulong? reviewerUserId, string? notes)
    {
        var entity = await _repo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"FeatureRequest {id} not found.");

        entity.Status = status;
        entity.ReviewNotes = notes;

        if (reviewerUserId.HasValue)
        {
            entity.ReviewedByUserId = reviewerUserId.Value;
            entity.ReviewedAt = DateTime.UtcNow;
        }

        await _repo.UpdateAsync(entity);

        _logger.LogInformation(
            "FeatureRequest {FeatureRequestId} status updated to {Status} by reviewer {ReviewerUserId}",
            id, status, reviewerUserId);
    }

    /// <inheritdoc/>
    public async Task SetDocGenResultAsync(Guid id, string? docPath, string? branchName, string? error)
    {
        var entity = await _repo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"FeatureRequest {id} not found.");

        if (!string.IsNullOrWhiteSpace(docPath) && !string.IsNullOrWhiteSpace(branchName))
        {
            entity.Status = FeatureRequestStatus.DocsGenerated;
            entity.DocPath = docPath;
            entity.DocBranchName = branchName;

            _logger.LogInformation(
                "FeatureRequest {FeatureRequestId} doc generation succeeded. Branch={BranchName}, DocPath={DocPath}",
                id, branchName, docPath);
        }
        else
        {
            entity.Status = FeatureRequestStatus.DocGenFailed;
            entity.DocGenError = error;

            _logger.LogWarning(
                "FeatureRequest {FeatureRequestId} doc generation failed. Error={DocGenError}",
                id, error);
        }

        await _repo.UpdateAsync(entity);
    }
}
