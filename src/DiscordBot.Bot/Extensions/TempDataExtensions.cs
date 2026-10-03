using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// The TempData→toast bridge (UX plan decision D1). A page handler reports the result of an
/// action with <c>TempData.SetSuccessToast(...)</c> (or error, warning, info) and the next page
/// render shows it as a toast, whether the handler redirected or returned <c>Page()</c>.
/// <c>_ToastContainer</c>, rendered by every layout, reads the queued toasts with
/// <see cref="TakeToasts"/>. Persistent page state (a load failure, a degraded service)
/// belongs in an <c>_Alert</c> on the page instead.
/// </summary>
public static class TempDataExtensions
{
    private static readonly (string Type, string MessageKey, string TitleKey)[] ToastKeys =
    {
        ("error", "ToastError", "ToastErrorTitle"),
        ("warning", "ToastWarning", "ToastWarningTitle"),
        ("success", "ToastSuccess", "ToastSuccessTitle"),
        ("info", "ToastInfo", "ToastInfoTitle"),
    };

    /// <summary>
    /// Reads and consumes the queued toasts, most severe first. Each key is read once, so a
    /// toast shows on exactly one page render.
    /// </summary>
    /// <param name="tempData">The TempData dictionary.</param>
    /// <returns>The queued toasts; empty when there are none.</returns>
    public static IReadOnlyList<ToastMessage> TakeToasts(this ITempDataDictionary tempData)
    {
        var toasts = new List<ToastMessage>();
        foreach (var (type, messageKey, titleKey) in ToastKeys)
        {
            var message = tempData[messageKey]?.ToString();
            var title = tempData[titleKey]?.ToString();
            if (!string.IsNullOrWhiteSpace(message))
            {
                toasts.Add(new ToastMessage(type, message, string.IsNullOrWhiteSpace(title) ? null : title));
            }
        }

        return toasts;
    }

    /// <summary>
    /// Sets a success toast message to be displayed on the next page load.
    /// </summary>
    /// <param name="tempData">The TempData dictionary.</param>
    /// <param name="message">The message to display.</param>
    /// <param name="title">Optional title for the toast.</param>
    public static void SetSuccessToast(this ITempDataDictionary tempData, string message, string? title = null)
    {
        tempData["ToastSuccess"] = message;
        if (title != null)
        {
            tempData["ToastSuccessTitle"] = title;
        }
        else
        {
            tempData.Remove("ToastSuccessTitle");
        }
    }

    /// <summary>
    /// Sets an error toast message to be displayed on the next page load.
    /// Error toasts stay until the user dismisses them.
    /// </summary>
    /// <param name="tempData">The TempData dictionary.</param>
    /// <param name="message">The message to display.</param>
    /// <param name="title">Optional title for the toast.</param>
    public static void SetErrorToast(this ITempDataDictionary tempData, string message, string? title = null)
    {
        tempData["ToastError"] = message;
        if (title != null)
        {
            tempData["ToastErrorTitle"] = title;
        }
        else
        {
            tempData.Remove("ToastErrorTitle");
        }
    }

    /// <summary>
    /// Sets a warning toast message to be displayed on the next page load.
    /// </summary>
    /// <param name="tempData">The TempData dictionary.</param>
    /// <param name="message">The message to display.</param>
    /// <param name="title">Optional title for the toast.</param>
    public static void SetWarningToast(this ITempDataDictionary tempData, string message, string? title = null)
    {
        tempData["ToastWarning"] = message;
        if (title != null)
        {
            tempData["ToastWarningTitle"] = title;
        }
        else
        {
            tempData.Remove("ToastWarningTitle");
        }
    }

    /// <summary>
    /// Sets an info toast message to be displayed on the next page load.
    /// </summary>
    /// <param name="tempData">The TempData dictionary.</param>
    /// <param name="message">The message to display.</param>
    /// <param name="title">Optional title for the toast.</param>
    public static void SetInfoToast(this ITempDataDictionary tempData, string message, string? title = null)
    {
        tempData["ToastInfo"] = message;
        if (title != null)
        {
            tempData["ToastInfoTitle"] = title;
        }
        else
        {
            tempData.Remove("ToastInfoTitle");
        }
    }
}

/// <summary>
/// A toast queued through TempData, as <c>_ToastContainer</c> hands it to <c>toast.js</c>.
/// </summary>
/// <param name="Type">"success", "error", "warning" or "info".</param>
/// <param name="Message">The message text.</param>
/// <param name="Title">An optional title.</param>
public sealed record ToastMessage(string Type, string Message, string? Title);
