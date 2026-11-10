using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Infrastructure.Services;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Bot.Services.LLM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using DiscordBot.Infrastructure.Abstractions.LLM;
using cpike.Agent.OpenRouter;
using DiscordBot.Infrastructure.Services.LLM.OpenRouter;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering AI assistant services.
/// </summary>
public static class AssistantServiceExtensions
{
    /// <summary>
    /// Adds AI assistant services: the cpike.Agent framework (when an OpenRouter key is configured),
    /// the guild assistant, the usage ledger and the model catalogue.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAssistant(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        //
        // CONTRACT: AssistantOptions supports both nested option groups (e.g. "Assistant:Sampling:MaxTokens")
        // and historical flat legacy keys (e.g. "Assistant:MaxTokens") via [Obsolete] forwarding properties
        // that read/write the same nested objects. Whichever the ConfigurationBinder happens to bind last
        // during the plain Configure<T> call above wins by accident of reflection/property-declaration
        // order — that is NOT a reliable contract. To make "flat key wins when both are present" an explicit,
        // robust guarantee (not an accident of binding order), a PostConfigure step re-applies any flat
        // legacy key that is actually present in the "Assistant" configuration section, overwriting whatever
        // the nested binding produced. See AssistantOptions.cs for the mirrored contract comment, and
        // AssistantOptionsBindingTests for coverage of both the raw-binder and the real DI registration path.
        services.Configure<AssistantOptions>(
            configuration.GetSection(AssistantOptions.SectionName));
        services.PostConfigure<AssistantOptions>(options =>
            ApplyFlatLegacyKeyPrecedence(options, configuration.GetSection(AssistantOptions.SectionName)));
        services.AddAssistantAgentOptions(configuration);
        services.Configure<LlmOptions>(
            configuration.GetSection(LlmOptions.SectionName));

        // Get API key from configuration
        var apiKey = configuration.GetValue<string>("OpenRouter:ApiKey");

        // Register assistant repositories (always needed for settings management)
        services.AddScoped<IAssistantUsageMetricsRepository, AssistantUsageMetricsRepository>();
        services.AddScoped<IAssistantInteractionLogRepository, AssistantInteractionLogRepository>();
        services.AddScoped<IAssistantGuildSettingsRepository, AssistantGuildSettingsRepository>();
        services.AddScoped<AssistantGuildSettingsRepository>();

        // Register the per-guild tool allow-list resolver ungated: it reads the settings row and the
        // static tool catalogue, needs no API key, and the settings service below depends on it to
        // drop its cache on save.
        services.AddScoped<IToolAccessResolver, ToolAccessResolver>();

        // Register assistant guild settings service (always needed for admin UI)
        services.AddScoped<IAssistantGuildSettingsService, AssistantGuildSettingsService>();

        // The prompt-surface report. Ungated so the metrics page can always inject it: with no API
        // key there is no prompt store to measure and it answers null, which the page renders as "not
        // configured" rather than failing to resolve.
        services.AddScoped<IPromptSurfaceReporter, PromptSurfaceReporter>();

        // Register the LLM model catalog repository, service, and its OpenRouter client ungated (no
        // API key needed to read the already-fetched catalog), so the "AI Models" admin page - and
        // migrations, which must run without a key - work before one is set. This is safe with no
        // key configured: the client degrades to a 401 OpenRouterException from OpenRouter itself
        // rather than failing at DI resolve time, so constructing it costs nothing until a refresh
        // is actually attempted. Only the *background* scheduled refresh stays gated below, since
        // running it with no key would just be a recurring failure.
        services.AddScoped<ILlmModelRepository, LlmModelRepository>();
        services.AddScoped<ILlmModelCatalogService, LlmModelCatalogService>();

        // The per-mode model resolver (DB setting -> bound options -> OpenRouter:Model) is
        // needed by the guild/DM assistant context factories and the feature-request conversation
        // service below, none of which require an API key to construct - only to actually call
        // OpenRouter. Registered as a singleton (cheap, holds only a small per-mode cache) so it can
        // subscribe once to ISettingsService.SettingsChanged for cache invalidation.
        services.AddSingleton<ILlmModelResolver, LlmModelResolver>();

        // Usage ledger: registered ungated (no API key needed to construct) so recording works
        // whenever any assistant mode runs, and so the two context factories - registered below,
        // inside the gated block - can always resolve ILlmUsageRecorder. LlmUsageRecorder is
        // registered under its concrete type too because LlmUsageRecordProcessor needs the
        // internal DequeueAsync/Count members the interface does not expose (same pattern would
        // apply if AuditLogQueue's processor needed anything beyond IAuditLogQueue).
        services.AddScoped<ILlmUsageRepository, LlmUsageRepository>();
        services.AddSingleton<LlmUsageRecorder>();
        services.AddSingleton<ILlmUsageRecorder>(sp => sp.GetRequiredService<LlmUsageRecorder>());
        services.AddHostedService<LlmUsageRecordProcessor>();

        // Retention sweep for guild/DM assistant interaction logs and the usage ledger - none of
        // these were ever cleaned up before. Registered ungated: it only needs the repositories
        // above and DmAssistantServiceExtensions' repository (resolved at runtime, not at
        // registration time, so registration order here doesn't matter), no API key.
        services.AddHostedService<AssistantInteractionLogRetentionService>();

        services.AddHttpClient<IOpenRouterModelCatalogClient, OpenRouterModelCatalogClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenRouterOptions>>().Value;

            var baseUrl = options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/";
            http.BaseAddress = new Uri(baseUrl);

            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
            }

            if (!string.IsNullOrWhiteSpace(options.AppUrl))
            {
                http.DefaultRequestHeaders.Add("HTTP-Referer", options.AppUrl);
            }

            if (!string.IsNullOrWhiteSpace(options.AppTitle))
            {
                http.DefaultRequestHeaders.Add("X-Title", options.AppTitle);
            }
        });

        // Shared assistant pipeline pieces (also used by the DM assistant)
        services.AddSingleton<IAssistantRateLimiter, AssistantRateLimiter>();
        services.AddScoped<IAssistantAccessGate, AssistantAccessGate>();
        services.AddScoped<IAssistantTelemetryReader, AssistantTelemetryReader>();

        // Only register LLM-dependent services if API key is configured
        // This prevents DI validation failures when running migrations without API key
        if (!string.IsNullOrEmpty(apiKey))
        {
            // The cpike.Agent framework: OpenRouter client, agent loop, tools, prompts and skills.
            services.AddAssistantAgent(configuration);

            // Register the scheduled catalog refresh (Llm:CatalogRefreshHours, 0 disables). The
            // client it depends on is registered above (ungated); only this recurring background
            // job needs an API key to be worth running.
            services.AddHostedService<LlmCatalogRefreshService>();

            // Register the guild assistant context factory (builds agent context + logs usage)
            services.AddScoped<IGuildAssistantContextFactory, GuildAssistantContextFactory>();

            // Register the main assistant service
            services.AddScoped<IAssistantService, AssistantService>();

            // One line per surface at startup saying what the tool array costs. Gated with the rest:
            // with no API key there is no assistant to measure.
            services.AddHostedService<PromptSurfaceReportService>();
        }

        return services;
    }

    /// <summary>
    /// Re-applies any historical flat legacy key (e.g. "Assistant:MaxTokens") that is present in the
    /// "Assistant" configuration section onto <paramref name="options"/>, overwriting whatever value its
    /// nested equivalent (e.g. "Assistant:Sampling:MaxTokens") bound to. Only keys that are actually present
    /// in configuration are touched, so nested-only deployments are unaffected. Applied via
    /// <c>PostConfigure&lt;AssistantOptions&gt;</c> so it always runs after the plain nested+flat binding,
    /// making "flat wins when both are set" an explicit guarantee rather than an accident of the order in
    /// which <see cref="AssistantOptions"/> declares its properties.
    /// </summary>
    private static void ApplyFlatLegacyKeyPrecedence(AssistantOptions options, IConfigurationSection assistantSection)
    {
        // A nested group can have legacy names of its own (e.g. "Assistant:Tools:MaxToolCallsPerQuestion"
        // for "Assistant:Tools:MaxToolRounds"). Those are applied first so the outer flat key, which is
        // the oldest spelling of all, still wins over everything when it is present too.
        ApplyLegacyKeyPrecedence(options.Tools, assistantSection.GetSection("Tools"));
        ApplyLegacyKeyPrecedence(options, assistantSection);
    }

    /// <summary>
    /// Re-applies every <c>[Obsolete]</c> forwarding property of <paramref name="options"/> whose key is
    /// actually present in <paramref name="section"/>, so the legacy name wins over the new one.
    /// </summary>
    private static void ApplyLegacyKeyPrecedence(object options, IConfigurationSection section)
    {
        foreach (var property in options.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<ObsoleteAttribute>() is null)
            {
                continue;
            }

            var legacyKeySection = section.GetSection(property.Name);
            if (!legacyKeySection.Exists())
            {
                continue;
            }

            var value = legacyKeySection.Get(property.PropertyType);
            property.SetValue(options, value);
        }
    }
}
