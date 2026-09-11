using DiscordBot.Core.Enums;
using DiscordBot.Core.DTOs.Llm.Reporting;

namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Resolves the effective OpenRouter model slug for one <see cref="LlmMode"/>: a DB setting row
/// (an admin override saved through the AI Models tab) wins over the bound options value, which
/// in turn wins over <c>OpenRouterOptions.DefaultModel</c>. Results are cached per mode and the
/// cache is invalidated when <see cref="ISettingsService.SettingsChanged"/> reports one of the
/// three mode setting keys changed, so a save takes effect on the next message with no restart.
/// </summary>
/// <remarks>
/// This is the single resolution path for a mode's model: consumers (the guild/DM assistant
/// context factories, the feature-request conversation service, and
/// <c>LlmModelsController.GetDefaults</c>) all call <see cref="ResolveAsync"/> instead of reading
/// <c>IOptions&lt;T&gt;</c> or <c>ISettingsService</c> directly. Change resolution order once,
/// here.
/// </remarks>
public interface ILlmModelResolver
{
    /// <summary>Resolves <paramref name="mode"/>'s effective slug, source, and catalog state/pricing.</summary>
    Task<LlmResolvedModel> ResolveAsync(LlmMode mode, CancellationToken cancellationToken = default);
}
