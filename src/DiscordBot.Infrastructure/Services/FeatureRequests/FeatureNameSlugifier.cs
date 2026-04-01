using System.Text.RegularExpressions;

namespace DiscordBot.Infrastructure.Services.FeatureRequests;

/// <summary>
/// Produces URL-safe slugs from feature request titles and detects naming collisions
/// against the repository's docs directory.
/// </summary>
public class FeatureNameSlugifier
{
    private static readonly Regex NonAlphanumericHyphen = new(@"[^a-z0-9-]", RegexOptions.Compiled);
    private static readonly Regex MultipleHyphens = new(@"-{2,}", RegexOptions.Compiled);

    /// <summary>
    /// Converts a title to a URL-safe slug: lowercase, spaces become hyphens,
    /// non-alphanumeric/hyphen characters are stripped, and the result is capped at 50 characters.
    /// </summary>
    public string Slugify(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "feature";

        var slug = title.Trim().ToLowerInvariant();
        slug = slug.Replace(' ', '-');
        slug = NonAlphanumericHyphen.Replace(slug, string.Empty);
        slug = MultipleHyphens.Replace(slug, "-");
        slug = slug.Trim('-');

        if (slug.Length > 50)
            slug = slug[..50].TrimEnd('-');

        return string.IsNullOrEmpty(slug) ? "feature" : slug;
    }

    /// <summary>
    /// Returns a slug that does not already exist under <paramref name="docsBasePath"/> within
    /// <paramref name="repoRoot"/>. If the base slug is taken, appends <c>-2</c>, <c>-3</c>, etc.
    /// </summary>
    public string GetUniqueSlug(string title, string repoRoot, string docsBasePath)
    {
        var baseSlug = Slugify(title);
        var candidate = baseSlug;
        var counter = 2;

        while (Directory.Exists(Path.Combine(repoRoot, docsBasePath, candidate)))
        {
            candidate = $"{baseSlug}-{counter}";
            counter++;
        }

        return candidate;
    }
}
