using Microsoft.Extensions.Configuration;

namespace DiscordBot.Evals;

/// <summary>
/// Where an eval run gets its API key and model from, and whether it can run at all.
/// </summary>
/// <remarks>
/// The same user-secrets store the application uses, so anyone who can run the bot locally can run
/// the evals with no extra setup, plus environment variables for a machine that has no store (
/// <c>OpenRouter__ApiKey</c>, <c>Eval__Model</c>). CI sets neither, which is what keeps this project
/// free and green there.
/// </remarks>
public static class EvalSettings
{
    private static readonly IConfiguration Configuration = Build();

    /// <summary>The OpenRouter key, or null when there is none.</summary>
    public static string? ApiKey => Blank(Configuration["OpenRouter:ApiKey"]);

    /// <summary>Whether the evals can run.</summary>
    public static bool Enabled => ApiKey is not null;

    /// <summary>
    /// The model under evaluation, as an OpenRouter slug.
    /// </summary>
    /// <remarks>
    /// Overridable, because that is the whole point: the useful run is the same dozen cases against
    /// the model you are thinking of switching to. The default is a small, cheap, tool-capable model
    /// rather than whatever the deployment happens to be set to — an eval suite nobody can afford to
    /// run is one nobody runs.
    /// </remarks>
    public static string Model =>
        Blank(Configuration["Eval:Model"]) ?? "anthropic/claude-3.5-haiku";

    /// <summary>The OpenRouter base URL.</summary>
    public static string BaseUrl =>
        Blank(Configuration["OpenRouter:BaseUrl"]) ?? "https://openrouter.ai/api/v1/";

    /// <summary>Why the suite is skipped, or null when it is not.</summary>
    public static string? SkipReason =>
        Enabled ? null : "OpenRouter:ApiKey is not configured, so the evals cannot call a model.";

    private static IConfiguration Build() =>
        new ConfigurationBuilder()
            .AddUserSecrets(typeof(EvalSettings).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>
/// A fact that runs only when an API key is configured, and is skipped — not failed — when it is not.
/// </summary>
/// <remarks>
/// xUnit decides <c>Skip</c> at discovery, so this is an attribute rather than a check inside the
/// test body: a skipped test reads as "not run here" in CI, and a test that returned early would
/// read as a pass it did not earn.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class EvalFactAttribute : FactAttribute
{
    /// <summary>Creates the attribute, skipping when the suite is not configured.</summary>
    public EvalFactAttribute()
    {
        if (!EvalSettings.Enabled)
        {
            Skip = EvalSettings.SkipReason;
        }
    }
}
