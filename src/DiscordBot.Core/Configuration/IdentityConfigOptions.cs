namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for ASP.NET Core Identity settings.
/// </summary>
public class IdentityConfigOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "Identity";

    // Password settings

    /// <summary>
    /// Gets or sets whether passwords must contain at least one digit.
    /// Default is true.
    /// </summary>
    public bool RequireDigit { get; set; } = true;

    /// <summary>
    /// Gets or sets whether passwords must contain at least one lowercase letter.
    /// Default is true.
    /// </summary>
    public bool RequireLowercase { get; set; } = true;

    /// <summary>
    /// Gets or sets whether passwords must contain at least one uppercase letter.
    /// Default is true.
    /// </summary>
    public bool RequireUppercase { get; set; } = true;

    /// <summary>
    /// Gets or sets whether passwords must contain at least one non-alphanumeric character.
    /// Default is true.
    /// </summary>
    public bool RequireNonAlphanumeric { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum required password length.
    /// Default is 8 characters.
    /// </summary>
    public int RequiredLength { get; set; } = 8;

    /// <summary>
    /// Gets or sets the minimum number of unique characters required in a password.
    /// Default is 1.
    /// </summary>
    public int RequiredUniqueChars { get; set; } = 1;

    // Lockout settings

    /// <summary>
    /// Gets or sets the duration (in minutes) for account lockout after max failed login attempts.
    /// Default is 15 minutes.
    /// </summary>
    public int LockoutTimeSpanMinutes { get; set; } = 15;

    /// <summary>
    /// Gets or sets the maximum number of failed login attempts before account lockout.
    /// Default is 5 attempts.
    /// </summary>
    public int MaxFailedAccessAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether newly created user accounts can be locked out.
    /// Default is true.
    /// </summary>
    public bool LockoutAllowedForNewUsers { get; set; } = true;

    // User settings

    /// <summary>
    /// Gets or sets whether each user must have a unique email address.
    /// Default is true.
    /// </summary>
    public bool RequireUniqueEmail { get; set; } = true;

    // Sign-in settings

    /// <summary>
    /// Gets or sets whether users must have a confirmed account to sign in.
    /// Default is false.
    /// </summary>
    public bool RequireConfirmedAccount { get; set; } = false;

    /// <summary>
    /// Gets or sets whether users must have a confirmed email to sign in.
    /// Default is false.
    /// </summary>
    public bool RequireConfirmedEmail { get; set; } = false;

    // Cookie settings

    /// <summary>
    /// Gets or sets the number of days before authentication cookies expire.
    /// Default is 7 days.
    /// </summary>
    public int CookieExpireDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets whether cookie expiration uses sliding expiration.
    /// When true, cookies are renewed on each request if more than half the expiry time has passed.
    /// Default is true.
    /// </summary>
    public bool CookieSlidingExpiration { get; set; } = true;

    /// <summary>
    /// Gets or sets the URL path for the login page.
    /// Default is "/Account/Login".
    /// </summary>
    public string LoginPath { get; set; } = "/Account/Login";

    /// <summary>
    /// Gets or sets the URL path for the logout endpoint.
    /// Default is "/Account/Logout".
    /// </summary>
    public string LogoutPath { get; set; } = "/Account/Logout";

    /// <summary>
    /// Gets or sets the URL path for the access denied page.
    /// Default is "/Account/AccessDenied".
    /// </summary>
    public string AccessDeniedPath { get; set; } = "/Account/AccessDenied";

    // Default admin (optional)

    /// <summary>
    /// Gets or sets the default admin user configuration.
    /// Used to create an initial admin account on first run. Should be configured via user secrets.
    /// Default is null (no default admin created).
    /// </summary>
    public DefaultAdminOptions? DefaultAdmin { get; set; }
}

/// <summary>
/// Configuration options for the default admin user account.
/// </summary>
public class DefaultAdminOptions
{
    /// <summary>
    /// Gets or sets the email address for the default admin user.
    /// Default is null.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the password for the default admin user.
    /// Should be stored in user secrets and changed immediately after first login.
    /// Default is null.
    /// </summary>
    public string? Password { get; set; }
}
