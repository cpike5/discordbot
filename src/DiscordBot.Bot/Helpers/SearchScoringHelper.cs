namespace DiscordBot.Bot.Helpers;

/// <summary>
/// Provides relevance-scoring utilities shared across all search providers.
/// All methods are pure/static so providers do not need to instantiate anything.
/// </summary>
public static class SearchScoringHelper
{
    private const int ExactMatchScore = 100;
    private const int StartsWithScore = 75;
    private const int ContainsScore = 50;

    /// <summary>
    /// Calculates a relevance score for <paramref name="fieldValue"/> against
    /// the lower-cased <paramref name="searchTermLower"/>.
    /// </summary>
    /// <param name="fieldValue">The field value to evaluate (any casing).</param>
    /// <param name="searchTermLower">The search term, already lower-cased.</param>
    /// <returns>
    /// <see cref="ExactMatchScore"/> for an exact match, <see cref="StartsWithScore"/>
    /// when the field starts with the term, <see cref="ContainsScore"/> for a substring
    /// match, or 0 when there is no match.
    /// </returns>
    public static double CalculateRelevanceScore(string fieldValue, string searchTermLower)
    {
        if (string.IsNullOrWhiteSpace(fieldValue))
            return 0;

        var fieldLower = fieldValue.ToLowerInvariant();

        if (fieldLower == searchTermLower)
            return ExactMatchScore;

        if (fieldLower.StartsWith(searchTermLower))
            return StartsWithScore;

        if (fieldLower.Contains(searchTermLower))
            return ContainsScore;

        return 0;
    }

    /// <summary>
    /// Clamps a computed relevance score to the 0–100 range.
    /// </summary>
    /// <param name="rawScore">The raw cumulative score.</param>
    /// <returns>A value in [0, 100].</returns>
    public static double Clamp(double rawScore) => Math.Min(rawScore, 100);
}
