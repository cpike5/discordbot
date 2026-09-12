using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Abstractions.LLM;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Services.LLM;

/// <summary>
/// Logs, once at startup, what each assistant surface puts in front of the model and what it costs.
/// </summary>
/// <remarks>
/// <para>
/// One line per surface, and the reason it exists is that nothing else states the number. The tool
/// array is paid for on every request whether or not the request needs any of it, so its growth is
/// otherwise discovered on the bill — and by then it is a month of questions, not a review comment.
/// A line in the log at every deploy makes it something a person sees while the change is still
/// fresh.
/// </para>
/// <para>
/// It reports the <em>house</em> set: no guild's allow-list applied, skills applied. Per-guild
/// numbers are on the guild's Assistant Metrics page, where a guild's admin can act on them.
/// </para>
/// </remarks>
public sealed class PromptSurfaceReportService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PromptSurfaceReportService> _logger;

    /// <summary>Creates the service.</summary>
    /// <param name="scopeFactory">Builds the scope the tool providers need.</param>
    /// <param name="logger">Where the report goes.</param>
    public PromptSurfaceReportService(
        IServiceScopeFactory scopeFactory,
        ILogger<PromptSurfaceReportService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var reporter = scope.ServiceProvider.GetRequiredService<IPromptSurfaceReporter>();

            await ReportAsync(reporter, ToolScopes.Guild, cancellationToken);
            await ReportAsync(reporter, ToolScopes.Dm, cancellationToken);
        }
        catch (Exception ex)
        {
            // A report is worth a line in the log and nothing more. Constructing every tool provider
            // is the one thing here that can fail, and failing to measure the assistant is not a
            // reason to refuse to start it.
            _logger.LogWarning(ex, "Could not report the assistant prompt surface at startup");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task ReportAsync(
        IPromptSurfaceReporter reporter,
        ToolScopes scope,
        CancellationToken cancellationToken)
    {
        var report = await reporter.ReportAsync(scope, guildId: null, cancellationToken);

        if (report is null)
        {
            return;
        }

        _logger.LogInformation(
            "Prompt surface: {Surface} advertises {ToolCount} of {RegisteredCount} tools, "
            + "{SchemaChars:N0} schema chars (~{EstimatedTokens:N0} tokens per request); "
            + "{WithheldChars:N0} chars held back by skills. Largest: {LargestTools}",
            report.SurfaceName,
            report.AdvertisedCount,
            report.RegisteredCount,
            report.Advertised.TotalChars,
            report.Advertised.EstimatedTokens,
            report.WithheldChars,
            Largest(report));
    }

    /// <summary>
    /// The three biggest advertised tools, named in the line itself — the first question anyone asks
    /// after reading the total is which tools it is made of, and three names answer it without
    /// anyone opening a page.
    /// </summary>
    private static string Largest(PromptSurfaceReport report) =>
        report.Advertised.Tools.Count == 0
            ? "none"
            : string.Join(", ", report.Advertised.Tools
                .OrderByDescending(t => t.SchemaChars)
                .Take(3)
                .Select(t => $"{t.Name} ({t.SchemaChars:N0})"));
}
