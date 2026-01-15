namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Loads and renders prompt templates with variable substitution.
/// Supports caching for performance.
/// </summary>
public interface IPromptTemplate
{
    /// <summary>
    /// Loads a prompt template from file.
    /// </summary>
    /// <param name="filePath">Relative or absolute path to template file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Raw template content.</returns>
    Task<string> LoadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a template with variable substitution.
    /// </summary>
    /// <param name="template">Template content with {{variable}} placeholders.</param>
    /// <param name="variables">Dictionary of variable names and values.</param>
    /// <returns>Rendered template.</returns>
    string Render(string template, Dictionary<string, string> variables);
}
