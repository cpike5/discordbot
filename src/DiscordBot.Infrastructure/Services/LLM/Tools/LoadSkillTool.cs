using System.Text.Json;
using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>load_skill</c> — loads one skill's instructions, and with them the tools that skill names.
/// </summary>
/// <remarks>
/// <para>
/// The tool is the model-facing half of the skill mechanism; the loop is the other half. This only
/// records the activation on the run's session and returns the instructions — the session is what
/// <c>AgentRunner</c> reads after the round to re-compose the advertised tool array, so the tools
/// arrive on the next round rather than this one.
/// </para>
/// <para>
/// It lives here rather than in <c>DiscordBot.Agents</c> for the same reason every other tool does:
/// the catalogue is what routes a tool to a surface, and the catalogue is this bot's.
/// </para>
/// </remarks>
public sealed class LoadSkillTool : IAgentTool
{
    /// <summary>The model-facing name, also the default <c>SkillSession.LoaderToolName</c>.</summary>
    public const string ToolName = SkillSession.DefaultLoaderToolName;

    private static readonly LlmToolDefinition ToolDefinition = new()
    {
        Name = ToolName,
        Description = "Loads a skill: detailed instructions for one kind of request, plus the tools "
            + "those instructions use. Use it when the skills list in your instructions names one "
            + "that fits what you were asked; the result is the instructions themselves. Load only "
            + "what the request needs.",
        InputSchema = ToolInput.ObjectSchema(
            new
            {
                key = ToolInput.Schema("string", "The key of the skill to load, exactly as it appears in the skills list.")
            },
            "key")
    };

    /// <inheritdoc />
    public LlmToolDefinition Definition => ToolDefinition;

    /// <inheritdoc />
    public Task<ToolExecutionResult> InvokeAsync(
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var key = ToolInput.GetString(input, "key");
        if (key is null)
        {
            return Task.FromResult(ToolResults.Error("Missing required parameter: key"));
        }

        var skills = context.GetSkills();

        if (skills is null || skills.Available.Count == 0)
        {
            // Reachable only from a stale cached prefix - the loop stops advertising this tool when
            // a surface has no skills - so say so plainly rather than inviting a retry.
            return Task.FromResult(ToolResults.NotFound(
                "No skills are available in this conversation. Answer with the tools you already have."));
        }

        var alreadyLoaded = skills.IsActivated(key);
        var skill = skills.Activate(key);

        if (skill is null)
        {
            return Task.FromResult(ToolResults.NotFound(
                $"There is no skill called '{key}'. Available skills: "
                + string.Join(", ", skills.Available.Select(s => s.Key)) + "."));
        }

        return Task.FromResult(ToolResults.Json(new
        {
            skill = skill.Key,
            instructions = skill.Instructions,
            tools_now_available = skill.Tools.Count > 0 ? skill.Tools : null,
            already_loaded = alreadyLoaded ? true : (bool?)null
        }));
    }
}
