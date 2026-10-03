using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;

namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// Shared styling and form-address rules for <c>_ConfirmationModal</c> and
/// <c>_TypedConfirmationModal</c>, so the two partials cannot drift apart.
/// </summary>
public static class ConfirmationVariantExtensions
{
    private const string WarningIconPath =
        "M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z";

    /// <summary>The colour token the icon badge is tinted with (<c>bg-{token}/20 text-{token}</c>).</summary>
    public static string ColorToken(this ConfirmationVariant variant) => variant switch
    {
        ConfirmationVariant.Info => "accent-blue",
        ConfirmationVariant.Danger => "error",
        _ => "warning"
    };

    /// <summary>
    /// The confirm button classes: a component class, or the warning fill with its dark ink
    /// (white on warning fails contrast, see the design system).
    /// </summary>
    public static string ConfirmButtonClass(this ConfirmationVariant variant) => variant switch
    {
        ConfirmationVariant.Info => "btn btn-accent",
        ConfirmationVariant.Danger => "btn btn-danger",
        _ => "btn bg-warning hover:bg-warning-hover active:bg-warning-active text-on-warning border-transparent"
    };

    /// <summary>The default icon path for the variant.</summary>
    public static string DefaultIconPath(this ConfirmationVariant variant) => variant switch
    {
        ConfirmationVariant.Info => "M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z",
        _ => WarningIconPath
    };

    /// <summary>
    /// The address a confirmation form posts to: <paramref name="formAction"/> (or the current
    /// page when it is empty) with <c>handler</c> set in the query string. Razor Pages reads the
    /// handler from the query, never from the form body, so a native submit and the AJAX submit
    /// both need it in the URL. An existing <c>handler</c> parameter is replaced, not duplicated.
    /// </summary>
    /// <param name="formAction">The configured action, which may carry its own query (<c>?userId=…</c>).</param>
    /// <param name="handler">The page handler name, or null for the default handler.</param>
    /// <param name="currentPathAndQuery">The current request's path and query string.</param>
    public static string ResolveFormAction(string? formAction, string? handler, string currentPathAndQuery)
    {
        var target = string.IsNullOrEmpty(formAction) ? currentPathAndQuery : formAction;
        if (string.IsNullOrEmpty(handler))
        {
            return target;
        }

        var fragmentIndex = target.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            target = target[..fragmentIndex];
        }

        var queryIndex = target.IndexOf('?');
        var path = queryIndex < 0 ? target : target[..queryIndex];
        var query = queryIndex < 0 ? string.Empty : target[(queryIndex + 1)..];

        var builder = new QueryBuilder();
        foreach (var pair in QueryHelpers.ParseQuery(query))
        {
            if (string.Equals(pair.Key, "handler", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var value in pair.Value)
            {
                builder.Add(pair.Key, value ?? string.Empty);
            }
        }

        builder.Add("handler", handler);
        return path + builder.ToQueryString().Value;
    }
}
