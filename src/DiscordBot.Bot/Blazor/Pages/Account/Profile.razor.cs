using System.ComponentModel.DataAnnotations;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace DiscordBot.Bot.Blazor.Pages.Account;

/// <summary>
/// Code-behind for the static SSR port of <c>Pages/Account/Profile.cshtml</c> +
/// <c>ProfileModel</c> (docs/plans/blazor-port-plan.md Phase 4 cluster 4a). Reproduces
/// <c>ProfileModel.OnGetAsync</c>'s data load and <c>OnPostAsync</c>'s theme-save logic almost
/// verbatim; the differences are structural, not behavioral:
/// </summary>
/// <remarks>
/// <para>
/// <b>No TempData.</b> The legacy page set <c>[TempData] StatusMessage</c>/<c>IsSuccess</c> and did
/// a PRG redirect to itself; here the redirect target carries the outcome directly as
/// <c>?status=saved|error</c> (<see cref="Status"/>, <c>[SupplyParameterFromQuery]</c>) - a static
/// SSR page has no circuit-scoped state to stash a richer message in, and the two-state banner is
/// what the port plan brief calls for.
/// </para>
/// <para>
/// <b>The cookie append.</b> <see cref="SaveThemeAsync"/> writes the
/// <see cref="IThemeService.ThemePreferenceCookieName"/> cookie through the cascaded
/// <see cref="HttpContext"/>'s <c>Response</c> before calling
/// <see cref="NavigationManager.NavigateTo(string)"/> - allowed here because this is a static SSR
/// form handler running before the response has started writing (see "Auth in components" in
/// docs/architecture/patterns.md: <c>HttpContext</c> is only available during prerendering, which
/// covers every static SSR page's own request). <c>NavigateTo</c> itself becomes a real HTTP
/// redirect for the same reason - the framework turns a static-SSR-handler's navigation into a
/// <c>Location</c> header instead of a client-side route change, which is what makes this a
/// genuine post-redirect-GET.
/// </para>
/// </remarks>
public partial class Profile : ComponentBase
{
    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private IThemeService ThemeService { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private ILogger<Profile> Logger { get; set; } = default!;

    /// <summary>
    /// Supplied automatically for every static SSR page hosted by <c>MapRazorComponents</c> -
    /// see the remarks above and "Auth in components" in <c>docs/architecture/patterns.md</c>.
    /// </summary>
    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromForm]
    protected ThemeInputModel Input { get; set; } = new();

    /// <summary>Post-redirect outcome of a theme save: <c>"saved"</c> or <c>"error"</c>.</summary>
    [SupplyParameterFromQuery(Name = "status")]
    protected string? Status { get; set; }

    protected ApplicationUser? User { get; private set; }
    protected bool UserNotFound { get; private set; }

    protected string DisplayName { get; private set; } = string.Empty;
    protected string? Email { get; private set; }
    protected bool HasDiscordLinked { get; private set; }
    protected string? DiscordUsername { get; private set; }
    protected ulong? DiscordUserId { get; private set; }
    protected string? DiscordAvatarUrl { get; private set; }
    protected string UserRole { get; private set; } = "Viewer";
    protected DateTime CreatedAt { get; private set; }
    protected DateTime? LastLoginAt { get; private set; }

    protected List<SelectOption> ThemeOptions { get; private set; } = new();
    protected string CurrentThemeName { get; private set; } = string.Empty;
    protected ThemeSource CurrentThemeSource { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        Logger.LogTrace("Entering {MethodName}", nameof(OnInitializedAsync));

        var user = await UserManager.GetUserAsync(HttpContext.User);
        if (user is null)
        {
            Logger.LogWarning("User not found during Profile page load");
            UserNotFound = true;
            return;
        }

        Logger.LogDebug("Loading profile for user {UserId}", user.Id);
        User = user;

        DisplayName = user.DisplayName ?? user.Email ?? "User";
        Email = user.Email;

        HasDiscordLinked = user.DiscordUserId.HasValue;
        DiscordUsername = user.DiscordUsername;
        DiscordUserId = user.DiscordUserId;
        DiscordAvatarUrl = user.DiscordAvatarUrl;

        CreatedAt = user.CreatedAt;
        LastLoginAt = user.LastLoginAt;

        var roles = await UserManager.GetRolesAsync(user);
        UserRole = GetHighestRole(roles);

        // A POST to this page arrives with Input.SelectedThemeId already populated by
        // [SupplyParameterFromForm] (bound before OnInitializedAsync runs, per the ASP.NET Core
        // static SSR form-binding order) - only seed it from the persisted current theme on a GET,
        // or this overwrites the visitor's just-submitted choice with the (still pre-save) current
        // theme before SaveThemeAsync ever reads it, silently no-op'ing every save. Confirmed by
        // Test_P_Profile_RendersAndSavesTheme, which caught this exact regression: the save
        // "succeeded" (it re-saved the unchanged current theme) but the selection never moved.
        var isFormPost = HttpMethods.IsPost(HttpContext.Request.Method);
        await LoadThemeDataAsync(user.Id, seedSelectedThemeId: !isFormPost);
    }

    private async Task LoadThemeDataAsync(string userId, bool seedSelectedThemeId)
    {
        try
        {
            var themes = await ThemeService.GetActiveThemesAsync();
            ThemeOptions = themes.Select(t => new SelectOption { Value = t.Id.ToString(), Text = t.DisplayName }).ToList();

            var currentTheme = await ThemeService.GetUserThemeAsync(userId);
            if (seedSelectedThemeId)
            {
                Input.SelectedThemeId = currentTheme.Theme.Id;
            }
            CurrentThemeName = currentTheme.Theme.DisplayName;
            CurrentThemeSource = currentTheme.Source;

            Logger.LogDebug(
                "Loaded {ThemeCount} themes. Current theme: {ThemeName} (Source: {Source})",
                themes.Count, CurrentThemeName, CurrentThemeSource);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading theme data for user {UserId}", userId);
            ThemeOptions = [];
        }
    }

    /// <summary>Reproduces <c>ProfileModel.OnPostAsync</c>'s save/validate/cookie logic; see the class remarks for the redirect/cookie mechanics.</summary>
    protected async Task SaveThemeAsync()
    {
        Logger.LogTrace("Entering {MethodName} with SelectedThemeId={ThemeId}", nameof(SaveThemeAsync), Input.SelectedThemeId);

        if (User is null || !Input.SelectedThemeId.HasValue)
        {
            NavigationManager.NavigateTo("/Account/Profile?status=error");
            return;
        }

        var theme = await ThemeService.GetThemeByIdAsync(Input.SelectedThemeId.Value);
        if (theme is null || !theme.IsActive)
        {
            Logger.LogWarning("User {UserId} attempted to select invalid theme {ThemeId}", User.Id, Input.SelectedThemeId.Value);
            NavigationManager.NavigateTo("/Account/Profile?status=error");
            return;
        }

        // Only the awaited service call is guarded - NavigationManager.NavigateTo (below and in
        // every other branch of this method) throws a NavigationException BY DESIGN, as the
        // mechanism a static SSR form handler uses to become a real HTTP redirect (see the class
        // remarks); a try/catch spanning a NavigateTo call would swallow that exception itself
        // and misreport a successful save as "Error saving theme preference" (confirmed by
        // Test_P_Profile_RendersAndSavesTheme, which failed this exact way before this method was
        // restructured to keep every NavigateTo outside the try).
        bool success;
        try
        {
            success = await ThemeService.SetUserThemeAsync(User.Id, Input.SelectedThemeId.Value);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving theme preference for user {UserId}", User.Id);
            NavigationManager.NavigateTo("/Account/Profile?status=error");
            return;
        }

        if (!success)
        {
            Logger.LogWarning("Failed to update theme preference for user {UserId}", User.Id);
            NavigationManager.NavigateTo("/Account/Profile?status=error");
            return;
        }

        HttpContext.Response.Cookies.Append(IThemeService.ThemePreferenceCookieName, theme.ThemeKey, new CookieOptions
        {
            Path = "/",
            MaxAge = TimeSpan.FromDays(365),
            SameSite = SameSiteMode.Lax,
            IsEssential = true
        });

        Logger.LogInformation("Successfully updated theme preference for user {UserId} to {ThemeName}", User.Id, theme.DisplayName);
        NavigationManager.NavigateTo("/Account/Profile?status=saved");
    }

    protected static string GetHighestRole(IList<string> roles)
    {
        if (roles.Contains("SuperAdmin")) return "SuperAdmin";
        if (roles.Contains("Admin")) return "Admin";
        if (roles.Contains("Moderator")) return "Moderator";
        return "Viewer";
    }

    protected static BadgeVariant GetRoleBadgeVariant(string role) => role switch
    {
        "SuperAdmin" or "Admin" => BadgeVariant.Orange,
        "Moderator" => BadgeVariant.Blue,
        _ => BadgeVariant.Default
    };

    protected static string GetRoleDisplayName(string role) => role switch
    {
        "SuperAdmin" => "Super Administrator",
        "Admin" => "Administrator",
        "Moderator" => "Moderator",
        _ => "Viewer"
    };

    protected static BadgeVariant GetSourceBadgeVariant(ThemeSource source) => source switch
    {
        ThemeSource.User => BadgeVariant.Success,
        ThemeSource.Admin => BadgeVariant.Info,
        _ => BadgeVariant.Default
    };

    /// <summary>Same relative-time copy as <c>ProfileModel.FormatLastLogin</c>, rendered as the <c>&lt;LocalTime&gt;</c>'s <c>Title</c> tooltip.</summary>
    protected static string FormatLastLogin(DateTime lastLogin)
    {
        var diff = DateTime.UtcNow - lastLogin;

        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} minutes ago";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours} hours ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} days ago";

        return lastLogin.ToString("MMMM d, yyyy 'at' h:mm tt");
    }

    /// <summary>Form-bound model for the theme select - see <see cref="Input"/>.</summary>
    public sealed class ThemeInputModel
    {
        [Required(ErrorMessage = "Please select a theme.")]
        public int? SelectedThemeId { get; set; }
    }
}
