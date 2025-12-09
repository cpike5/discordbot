using DiscordBot.Core.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Bot.Pages.Account;

/// <summary>
/// Page model for user login functionality.
/// </summary>
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Login input model bound from the form.
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    /// Return URL after successful login.
    /// </summary>
    public string? ReturnUrl { get; set; }

    /// <summary>
    /// Error message to display to the user.
    /// </summary>
    [TempData]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Input model for login form.
    /// </summary>
    public class InputModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }

    /// <summary>
    /// Handles GET request to display the login page.
    /// </summary>
    public async Task OnGetAsync(string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        returnUrl ??= Url.Content("~/");

        // Clear existing external cookie to ensure clean login
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        ReturnUrl = returnUrl;

        _logger.LogDebug("Login page accessed, return URL: {ReturnUrl}", returnUrl);
    }

    /// <summary>
    /// Handles POST request for email/password login.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
        {
            _logger.LogDebug("Login form validation failed for {Email}", Input.Email);
            return Page();
        }

        _logger.LogInformation("Login attempt for user {Email}", Input.Email);

        // Check if user exists and is active
        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user != null && !user.IsActive)
        {
            _logger.LogWarning("Login attempt for inactive user {Email}", Input.Email);
            ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact an administrator.");
            return Page();
        }

        // Attempt sign-in with lockout enabled
        var result = await _signInManager.PasswordSignInAsync(
            Input.Email,
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Email} logged in successfully", Input.Email);

            // Update last login timestamp
            if (user != null)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
            }

            return LocalRedirect(returnUrl);
        }

        if (result.RequiresTwoFactor)
        {
            _logger.LogDebug("User {Email} requires two-factor authentication", Input.Email);
            return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User account {Email} is locked out", Input.Email);
            return RedirectToPage("./Lockout");
        }

        // Login failed
        _logger.LogWarning("Invalid login attempt for {Email}", Input.Email);
        ModelState.AddModelError(string.Empty, "Invalid email or password. Please try again.");
        return Page();
    }

    /// <summary>
    /// Handles POST request for Discord OAuth login.
    /// </summary>
    public IActionResult OnPostDiscordLogin(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        _logger.LogInformation("Discord OAuth login initiated");

        var properties = _signInManager.ConfigureExternalAuthenticationProperties("Discord", Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl }));
        return new ChallengeResult("Discord", properties);
    }
}
