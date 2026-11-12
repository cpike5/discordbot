using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// Builds URLs from the current query string, for filter chips and the live search bar. Every
/// method resets paging: a URL that removes a filter starts again at page 1, so it never lands
/// beyond the end of a list that just got longer.
/// </summary>
public static class QueryStringHelper
{
    /// <summary>The paging key the list pages share (<c>PaginatedPageModel.CurrentPage</c>).</summary>
    public const string PageKey = "pageNumber";

    /// <summary>
    /// The current page URL without <paramref name="keys"/> (every value of each key) and without
    /// the paging key. Other keys, including repeated ones, are kept as they are.
    /// </summary>
    public static string Without(HttpRequest request, params string[] keys)
        => Without(request.PathBase.Add(request.Path).Value ?? string.Empty, request.Query, keys);

    /// <inheritdoc cref="Without(HttpRequest, string[])"/>
    public static string Without(string path, IQueryCollection query, params string[] keys)
        => Build(path, query, key => IsKey(key, PageKey) || keys.Any(k => IsKey(key, k)), null);

    /// <summary>
    /// The current page URL without one value of a repeated key (one role of several), and
    /// without the paging key. The key's other values stay.
    /// </summary>
    public static string WithoutValue(HttpRequest request, string key, string value)
        => WithoutValue(request.PathBase.Add(request.Path).Value ?? string.Empty, request.Query, key, value);

    /// <inheritdoc cref="WithoutValue(HttpRequest, string, string)"/>
    public static string WithoutValue(string path, IQueryCollection query, string key, string value)
        => Build(path, query, k => IsKey(k, PageKey), (k, v) => IsKey(k, key) && v == value);

    /// <summary>
    /// The query as name and value pairs, minus <paramref name="exclude"/> and the paging key. The
    /// live search form writes these as hidden inputs so a search keeps the other filters.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Pairs(IQueryCollection query, params string[] exclude)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        foreach (var (key, values) in query)
        {
            if (IsKey(key, PageKey) || exclude.Any(e => IsKey(key, e))) continue;
            foreach (var value in values)
            {
                pairs.Add(new KeyValuePair<string, string>(key, value ?? string.Empty));
            }
        }
        return pairs;
    }

    private static string Build(string path, IQueryCollection query, Func<string, bool> dropKey, Func<string, string, bool>? dropValue)
    {
        var url = path;
        foreach (var (key, values) in query)
        {
            if (dropKey(key)) continue;
            foreach (var value in values)
            {
                var text = value ?? string.Empty;
                if (dropValue is not null && dropValue(key, text)) continue;
                url = QueryHelpers.AddQueryString(url, key, text);
            }
        }
        return url;
    }

    private static bool IsKey(string actual, string expected)
        => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
}
