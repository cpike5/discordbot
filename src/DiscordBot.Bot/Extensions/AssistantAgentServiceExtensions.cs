using Microsoft.Extensions.DependencyInjection.Extensions;
using cpike.Agent;
using cpike.Agent.OpenRouter;
using cpike.Agent.Prompting;
using DiscordBot.Core.Configuration;
using DiscordBot.Infrastructure.Abstractions.LLM;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Registers the cpike.Agent framework the assistants run on: the OpenRouter client, the agent loop,
/// the tools, the prompt and skill stores, and the shared message pipeline.
/// </summary>
public static class AssistantAgentServiceExtensions
{
    /// <summary>Where the prompt files land in the build output (see the Bot csproj).</summary>
    public const string PromptsDirectory = "Prompts";

    /// <summary>
    /// The options every host reads, registered whether or not an API key is configured: the model
    /// resolver, the model catalogue client and the metrics page read <see cref="OpenRouterOptions"/>.
    /// Also maps the keys the in-repo engine used onto the framework's, so an existing deployment's
    /// configuration keeps meaning the same thing.
    /// </summary>
    public static IServiceCollection AddAssistantAgentOptions(this IServiceCollection services, IConfiguration configuration)
    {
        var openRouter = configuration.GetSection(OpenRouterOptions.SectionName);
        services.Configure<OpenRouterOptions>(openRouter);
        services.PostConfigure<OpenRouterOptions>(options =>
        {
            // OpenRouter:DefaultModel was the engine's name for OpenRouter:Model.
            if (openRouter["Model"] is null && openRouter["DefaultModel"] is { Length: > 0 } legacyModel)
            {
                options.Model = legacyModel;
            }

            if (openRouter["Timeout"] is null && openRouter.GetValue<int?>("TimeoutSeconds") is { } seconds and > 0)
            {
                options.Timeout = TimeSpan.FromSeconds(seconds);
            }

            options.AppTitle ??= "DiscordBot";
        });

        // Defaults before AddAgentCore binds the Prompts section, so configuration still wins.
        services.Configure<PromptOptions>(options =>
        {
            options.Directory = PromptsDirectory;
            options.SkillsDirectory = $"{PromptsDirectory}/skills";
            options.DefaultAgent = "bot";
            options.ContentRoot = AppContext.BaseDirectory;
        });

        services.PostConfigure<AgentOptions>(options =>
        {
            var agent = configuration.GetSection(AgentOptions.SectionName);

            // The engine had a per-tool deadline for every assistant (Assistant:Tools:ToolExecutionTimeoutMs,
            // 10 s by default); the framework's is Agent:ToolTimeoutSeconds, off by default.
            if (agent["ToolTimeoutSeconds"] is null)
            {
                var assistant = new AssistantOptions();
                configuration.GetSection(AssistantOptions.SectionName).Bind(assistant);
                options.ToolTimeoutSeconds = (int)Math.Ceiling(assistant.Tools.ToolExecutionTimeoutMs / 1000.0);
            }

            if (agent["DuplicateToolCallLimit"] is null)
            {
                var assistant = new AssistantOptions();
                configuration.GetSection(AssistantOptions.SectionName).Bind(assistant);
                options.DuplicateToolCallLimit = assistant.Tools.DuplicateToolCallLimit;
            }

            // OpenRouter:MaxRetries was the engine's retry count for a transient failure.
            if (agent["TransientRetryLimit"] is null && openRouter.GetValue<int?>("MaxRetries") is { } retries)
            {
                options.TransientRetryLimit = retries;
            }
        });

        return services;
    }

    /// <summary>
    /// The framework itself. Call only when an OpenRouter API key is configured, like every other
    /// service that calls a model.
    /// </summary>
    public static IServiceCollection AddAssistantAgent(this IServiceCollection services, IConfiguration configuration)
    {
        // The house prompt store wraps the framework's markdown store, so it must be registered
        // before AddAgentCore, which only adds its own when none is registered.
        services.AddSingleton(sp => new MarkdownAgentPromptStore(
            sp.GetRequiredService<IOptions<PromptOptions>>(),
            sp.GetService<IHostEnvironment>(),
            sp.GetRequiredService<ILogger<MarkdownAgentPromptStore>>()));
        services.AddSingleton<IAgentPromptStore>(sp => new CatalogPromptStore(
            sp.GetRequiredService<MarkdownAgentPromptStore>(),
            sp.GetRequiredService<IAgentSkillStore>(),
            sp.GetRequiredService<IOptions<AssistantOptions>>().Value.BaseUrl));

        services.AddOpenRouter(configuration);
        services.AddAgentCore(configuration, agent => agent
            .AddBuiltInTools()
            .AddToolsFrom(AgentToolAssemblies.All.ToArray()));

        services.AddScoped<DiscordRunContext>();

        // Shared by the four documentation tools: the feature map, path containment and command search.
        services.AddScoped<DiscordBot.Infrastructure.Services.LLM.Tools.DocumentationLibrary>();
        services.TryAddScoped<IAssistantMessagePipeline, AssistantMessagePipeline>();

        return services;
    }
}
