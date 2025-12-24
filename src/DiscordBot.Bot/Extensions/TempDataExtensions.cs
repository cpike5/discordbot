using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for TempData to provide toast notification support.
/// Toast messages set via these methods will be automatically displayed
/// when the _ToastContainer partial is rendered in the layout.
/// </summary>
public static class TempDataExtensions
{
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
    }

    /// <summary>
    /// Sets an error toast message to be displayed on the next page load.
    /// Error toasts display for longer (10 seconds by default).
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
    }
}
